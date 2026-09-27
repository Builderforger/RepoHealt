using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Признаки зрелости в корне репозитория: лицензия, файл сборки,
/// отдельная документация, правила участия и подготовка к агентам.
/// </summary>
public class RepositoryFilesTests
{
    private static RepoFacts Facts(string readme, params string[] rootFiles) => new()
    {
        Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
        Git = new Git { Readme = readme, RootFiles = [.. rootFiles] },
        Notes = []
    };

    private static CategoryResult Evaluate(RepoFacts facts) =>
        DocumentationCategory.Evaluate(facts);

    private static PartResult? PartNamed(CategoryResult result, string name) =>
        result.Parts.FirstOrDefault(part => part.Name == name);

    // ---------- признаки считаются только когда есть список файлов ----------

    [Fact]
    public void WithoutFileListingOnlyReadmeIsScored()
    {
        // Старые данные не содержат списка файлов. Выдумывать
        // за них признаки нельзя: считаем только то, что знаем.
        var result = Evaluate(Facts("# Проект"));

        Assert.Equal(5, result.Parts.Count);
        Assert.Null(PartNamed(result, "лицензия"));
    }

    [Fact]
    public void FileListingAddsMaturityChecks()
    {
        var result = Evaluate(Facts("# Проект", "README.md", "LICENSE"));

        Assert.Equal(11, result.Parts.Count);
        Assert.NotNull(PartNamed(result, "лицензия"));
        Assert.NotNull(PartNamed(result, "файлы для ИИ-агентов"));
        Assert.NotNull(PartNamed(result, "политика безопасности"));
    }

    [Fact]
    public void ScoreStaysComparableWithAndWithoutFileListing()
    {
        // Балл категории — доля от возможного, поэтому разный
        // набор частей не делает проекты несравнимыми.
        var withoutFiles = Evaluate(Facts("# Проект"));
        var withFiles = Evaluate(Facts("# Проект", "README.md"));

        Assert.InRange(withoutFiles.Score, 0, 100);
        Assert.InRange(withFiles.Score, 0, 100);
    }

    // ---------- отдельные признаки ----------

    [Theory]
    [InlineData("LICENSE")]
    [InlineData("LICENSE.md")]
    [InlineData("LICENCE")]
    [InlineData("COPYING")]
    public void LicenseIsFoundUnderAnyUsualName(string fileName)
    {
        // Имя лицензии пишут по-разному — ловим все привычные варианты.
        var license = PartNamed(Evaluate(Facts("# Проект", fileName)), "лицензия");

        Assert.Equal(6, license!.Points, 1);
    }

    [Theory]
    [InlineData("Cargo.toml")]
    [InlineData("package.json")]
    [InlineData("Makefile")]
    [InlineData("pyproject.toml")]
    [InlineData("RepoHealth.csproj")]
    public void BuildFileIsFoundForDifferentEcosystems(string fileName)
    {
        var build = PartNamed(Evaluate(Facts("# Проект", fileName)), "файл сборки");

        Assert.Equal(10, build!.Points, 1);
    }

    [Theory]
    [InlineData("AGENTS.md")]
    [InlineData(".agents")]
    [InlineData(".codeassistant")]
    [InlineData("CLAUDE.md")]
    public void AgentFilesAreRecognised(string fileName)
    {
        var agents = PartNamed(Evaluate(Facts("# Проект", fileName)), "файлы для ИИ-агентов");

        Assert.Equal(4, agents!.Points, 1);
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("doc")]
    [InlineData("documentation")]
    [InlineData("info")]   // так называется папка документации у diktat
    [InlineData("wiki")]
    [InlineData("guide")]
    public void DocumentationFolderIsFoundUnderAnyUsualName(string folderName)
    {
        var docs = PartNamed(Evaluate(Facts("# Проект", folderName)), "папка документации");

        Assert.Equal(5, docs!.Points, 1);
    }

    [Fact]
    public void MissingFileIsExplainedNotJustZeroed()
    {
        var license = PartNamed(Evaluate(Facts("# Проект", "README.md")), "лицензия");

        Assert.Equal(0, license!.Points);
        Assert.Contains("не указана", license.Explanation);
    }

    [Fact]
    public void FoundFileIsNamedInTheExplanation()
    {
        // Пользователь должен видеть, что именно мы нашли.
        var license = PartNamed(Evaluate(Facts("# Проект", "LICENSE.txt")), "лицензия");

        Assert.Contains("license.txt", license!.Explanation);
    }

    // ---------- поведение на образцовом репозитории ----------

    [Fact]
    public void WellPreparedRepositoryScoresMuchHigher()
    {
        var readme = "# Проект\n\n## Установка\n```\nmake build\n```\n\n## Использование\nтекст";

        var plain = Evaluate(Facts(readme, "README.md", "src"));

        var prepared = Evaluate(Facts(readme,
            "README.md", "LICENSE", "Makefile", "docs",
            "CONTRIBUTING.md", "AGENTS.md", ".agents", ".sourcecraft", "src"));

        Assert.True(prepared.Score > plain.Score + 25,
            $"подготовленный {prepared.Score}, обычный {plain.Score}");
    }

    [Fact]
    public void ПолитикаБезопасностиПовышаетОценкуДокументации()
    {
        // Сценарий из чужой фикстуры «до и после»: в репозиторий
        // добавляют SECURITY.md, и это должно быть заметно.
        // Раньше мы такой файл не видели вовсе.
        var without = Evaluate(Facts("# Проект", "README.md", "LICENSE"));
        var with = Evaluate(Facts("# Проект", "README.md", "LICENSE", "SECURITY.md"));

        Assert.True(with.Score > without.Score,
            $"без политики: {without.Score}, с политикой: {with.Score}");

        var part = PartNamed(with, "политика безопасности");

        Assert.NotNull(part);
        Assert.Equal(part!.MaxPoints, part.Points);
    }
}
