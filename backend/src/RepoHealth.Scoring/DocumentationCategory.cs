using System.Text.RegularExpressions;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

public static class DocumentationCategory
{
    public const string Name = "Документация";
    public const double BaseWeight = 15;

    // Пороги вынесены в константы: так их видно сразу
    // и не приходится искать число посреди формулы.
    private const int LinesForFullScore = 120;
    private const int HeadingsForFullScore = 12;

    private static readonly string[] InstallWords =
        ["install", "setup", "установ", "сборк", "build", "quick start", "quickstart"];

    private static readonly string[] UsageWords =
        ["usage", "использов", "запуск", "run", "getting started", "example", "пример"];

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

        // Разбор README отделён от начисления баллов:
        // здесь только считаем, ничего не оцениваем.
        var readme = facts.Git?.Readme ?? "";
        var lines = CountMeaningfulLines(readme);
        var headings = HeadingPattern.Matches(readme);
        var headingsText = string.Join(" ", headings.Select(m => m.Groups[1].Value)).ToLowerInvariant();
        var body = readme.ToLowerInvariant();
        var hasCodeBlocks = readme.Contains("```");

        // А здесь только начисляем баллы по тому, что насчитали выше.
        var parts = new List<PartResult>
        {
            CategoryTools.Part("объём", 30,
                Scales.Grows(lines, LinesForFullScore),
                $"{lines} непустых строк, для полного балла нужно {LinesForFullScore}"),

            CategoryTools.Part("структура", 25,
                Scales.Grows(headings.Count, HeadingsForFullScore),
                $"{headings.Count} заголовков, для полного балла нужно {HeadingsForFullScore}"),

            Section("установка", 20, InstallWords, headingsText, body),

            Section("использование", 15, UsageWords, headingsText, body),

            CategoryTools.Part("примеры кода", 10,
                hasCodeBlocks ? 1 : 0,
                hasCodeBlocks ? "есть блоки кода" : "блоков кода нет")
        };

        return new CategoryResult
        {
            Name = Name,
            Status = CategoryStatus.Scored,
            BaseWeight = BaseWeight,
            Score = Math.Round(parts.Sum(part => part.Points), 1),
            Parts = parts
        };
    }

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