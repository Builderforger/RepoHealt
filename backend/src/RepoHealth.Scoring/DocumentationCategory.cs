using System.Text.RegularExpressions;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает документацию проекта: содержимое README
/// и признаки зрелости в корне репозитория.
public static class DocumentationCategory
{
    public const string Name = "Документация";
    public const double BaseWeight = 15;

    // Пороги вынесены в константы: так их видно сразу
    // и не приходится искать число посреди формулы.
    private const int LinesForFullScore = 120;
    private const int HeadingsForFullScore = 12;

    // Словари намеренно двуязычные: на SourceCraft много русских
    // README, и английских слов для них не хватает.
    private static readonly string[] InstallWords =
        [
            "install", "setup", "build", "quick start", "quickstart", "prerequisite",
            "download", "get started",
            "установ", "сборк", "быстрый старт", "начало работы", "с чего начать",
            "требовани", "зависимост", "развёртыв", "разверт", "подготовк", "скачать"
        ];

    private static readonly string[] UsageWords =
        [
            "usage", "run", "getting started", "example", "features", "how to",
            "использов", "запуск", "пример", "возможност", "как пользоваться",
            "команд", "работа с", "сценари"
        ];

    private static readonly string[] LicenseNames =
        ["license", "licence", "copying"];

    private static readonly string[] ContributingNames =
        ["contributing", "code_of_conduct", "codeowners"];

    // Папку с документацией называют по-разному: у diktat это «info»,
    // у других «wiki» или «guide». Ищем все привычные имена.
    private static readonly string[] DocsFolders =
        ["docs", "doc", "documentation", "info", "wiki", "guide", "manual", "handbook"];

    /// Файлы, по которым видно, что проект подготовлен
    /// к работе с ИИ-агентами. На SourceCraft это осмысленный
    /// признак: Code Assistant встроен в платформу.
    private static readonly string[] AgentNames =
        ["agents.md", ".agents", ".codeassistant", "claude.md", ".cursorrules", ".cursor"];

    private static readonly string[] BuildNames =
        [
            "makefile", "cargo.toml", "package.json", "pom.xml", "build.gradle",
            "build.gradle.kts", "go.mod", "pyproject.toml", "setup.py",
            "requirements.txt", "cmakelists.txt", "dockerfile", "docker-compose.yml",
            "composer.json", "gemfile", "build.sbt"
        ];

    private static readonly string[] BuildExtensions =
        [".csproj", ".sln", ".slnx", ".fsproj", ".vcxproj"];

    // Заголовок markdown: от одной до шести решёток в начале строки,
    // пробел, дальше текст. Скобки вокруг .+ — чтобы забрать сам текст.
    private static readonly Regex HeadingPattern =
        new(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        // Сначала крайний случай: данных могло не быть вовсе.
        var failure = CategoryTools.FindCollectionFailure(facts);
        if (failure is not null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, $"не удалось скачать репозиторий: {failure.Reason}");
        }

        var parts = new List<PartResult>();
        parts.AddRange(FromReadme(facts.Git?.Readme ?? ""));

        // Признаки по файлам считаем только если список файлов собран.
        // Старые данные его не содержат — тогда категория оценивается
        // по одному README, а балл остаётся сравнимым за счёт того,
        // что считается доля от возможного.
        var files = facts.Git?.RootFiles ?? [];
        if (files.Count > 0)
            parts.AddRange(FromFiles(files));

        return new CategoryResult
        {
            Name = Name,
            Status = CategoryStatus.Scored,
            BaseWeight = BaseWeight,
            Score = CategoryTools.Combine(parts),
            Parts = parts
        };
    }

    // ---------- содержимое README ----------

    private static List<PartResult> FromReadme(string readme)
    {
        var lines = CountMeaningfulLines(readme);
        var headings = HeadingPattern.Matches(readme);
        var headingsText = string.Join(" ", headings.Select(m => m.Groups[1].Value)).ToLowerInvariant();
        var body = readme.ToLowerInvariant();
        var hasCodeBlocks = readme.Contains("```");

        return
        [
            CategoryTools.Part("объём", 20,
                Scales.Grows(lines, LinesForFullScore),
                $"{lines} непустых строк, для полного балла нужно {LinesForFullScore}"),

            CategoryTools.Part("структура", 15,
                Scales.Grows(headings.Count, HeadingsForFullScore),
                $"{headings.Count} заголовков, для полного балла нужно {HeadingsForFullScore}"),

            Section("установка", 15, InstallWords, headingsText, body),

            Section("использование", 10, UsageWords, headingsText, body),

            CategoryTools.Part("примеры кода", 5,
                hasCodeBlocks ? 1 : 0,
                hasCodeBlocks ? "есть блоки кода" : "блоков кода нет")
        ];
    }

    // ---------- признаки в корне репозитория ----------

    private static List<PartResult> FromFiles(List<string> files)
    {
        var names = files.Select(name => name.ToLowerInvariant()).ToList();

        var license = Find(names, LicenseNames);
        var contributing = Find(names, ContributingNames);
        var docs = Find(names, DocsFolders);
        var agents = Find(names, AgentNames);
        var build = names.FirstOrDefault(name =>
            BuildNames.Contains(name) || BuildExtensions.Any(name.EndsWith));

        return
        [
            Presence("лицензия", 12, license,
                "лицензия не указана — непонятно, на каких условиях можно использовать проект"),

            Presence("описание сборки", 10, build,
                "нет файла сборки — непонятно, как собрать проект"),

            Presence("отдельная документация", 5, docs,
                "отдельной папки с документацией нет"),

            Presence("участие в разработке", 4, contributing,
                "нет описания, как присоединиться к работе"),

            Presence("готовность к агентам", 4, agents,
                "нет файлов для ИИ-агентов (AGENTS.md и подобных)")
        ];
    }

    private static string? Find(List<string> names, string[] wanted) =>
        names.FirstOrDefault(name => wanted.Any(want =>
            name == want || name.StartsWith(want + ".")));

    private static PartResult Presence(
        string name, double maxPoints, string? found, string whenMissing) =>
        CategoryTools.Part(name, maxPoints, found is null ? 0 : 1,
            found is null ? whenMissing : $"найдено: {found}");

    // ---------- вспомогательное ----------

    private static int CountMeaningfulLines(string text) =>
        text.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

    /// Раздел в заголовке — полный балл, упоминание в тексте — половина.
    /// Заголовок «Установка» это правда раздел, а слово «run»
    /// в случайной фразе — ещё нет.
    private static PartResult Section(
        string name, double maxPoints, string[] words, string headings, string body)
    {
        if (words.Any(headings.Contains))
            return CategoryTools.Part(name, maxPoints, 1.0, "есть отдельный раздел");

        if (words.Any(body.Contains))
            return CategoryTools.Part(name, maxPoints, 0.5, "упоминается в тексте, отдельного раздела нет");

        return CategoryTools.Part(name, maxPoints, 0.0, "не упоминается");
    }
}
