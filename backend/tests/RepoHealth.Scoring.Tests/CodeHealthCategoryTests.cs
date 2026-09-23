using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет категорию «Состояние кода».
/// </summary>
public class CodeHealthCategoryTests
{
    private static RepoFacts Facts(CodeFacts? code, params Note[] notes) => new()
    {
        Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
        Git = new Git { DaysSinceLastCommit = 1 },
        Code = code,
        Notes = [.. notes]
    };

    private static CodeFacts Code(
        int files = 100,
        int todo = 0,
        bool tests = false,
        string? quality = null,
        long largest = 10 * 1024) => new()
        {
            FilesTotal = files,
            TodoCount = todo,
            HasTests = tests,
            QualityConfig = quality,
            LargestFileBytes = largest
        };

    private static PartResult PartNamed(CategoryResult result, string name) =>
        result.Parts.Single(part => part.Name == name);

    // ---------- когда оценивать нечего ----------

    [Fact]
    public void RepositoryNotClonedMeansNoData()
    {
        var result = CodeHealthCategory.Evaluate(Facts(null, new Note
        {
            Source = "git",
            Operation = "clone_depth_1",
            Reason = "repository not found"
        }));

        Assert.Equal(CategoryStatus.NoData, result.Status);
    }

    [Fact]
    public void MissingFileTreeMeansNoData()
    {
        var result = CodeHealthCategory.Evaluate(Facts(null));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("дерево файлов", result.NoDataReason!);
    }

    // ---------- сами показатели ----------

    [Fact]
    public void WellKeptProjectScoresFull()
    {
        var result = CodeHealthCategory.Evaluate(Facts(
            Code(files: 200, todo: 0, tests: true, quality: ".editorconfig", largest: 20 * 1024)));

        Assert.Equal(100, result.Score, 1);
    }

    [Fact]
    public void ProjectWithoutAnythingScoresZero()
    {
        var result = CodeHealthCategory.Evaluate(Facts(
            Code(files: 10, todo: 100, tests: false, quality: null, largest: 5 * 1024 * 1024)));

        Assert.Equal(0, result.Score, 1);
    }

    [Theory]
    [InlineData(true, 30)]
    [InlineData(false, 0)]
    public void TestsAreWorthThirtyPoints(bool hasTests, double expected)
    {
        var result = CodeHealthCategory.Evaluate(Facts(Code(tests: hasTests)));

        Assert.Equal(expected, PartNamed(result, "тесты").Points, 1);
    }

    [Theory]
    [InlineData(0, 30)]      // недоделок нет
    [InlineData(2, 30)]      // две на сто файлов — в пределах нормы
    [InlineData(14, 14.7)]   // около середины между нормой и перебором
    [InlineData(40, 0)]      // проект тонет в долге
    public void TodoDensityMattersNotRawCount(int todo, double expected)
    {
        // Важно именно отношение к размеру проекта: двадцать пометок
        // на тысячу файлов и двадцать на десять — разные вещи.
        var result = CodeHealthCategory.Evaluate(Facts(Code(files: 100, todo: todo)));

        Assert.Equal(expected, PartNamed(result, "недоделки в коде").Points, 1);
    }

    [Fact]
    public void SameTodoCountScoresHigherInABiggerProject()
    {
        var small = CodeHealthCategory.Evaluate(Facts(Code(files: 20, todo: 10)));
        var big = CodeHealthCategory.Evaluate(Facts(Code(files: 2000, todo: 10)));

        Assert.True(
            PartNamed(big, "недоделки в коде").Points > PartNamed(small, "недоделки в коде").Points);
    }

    [Theory]
    [InlineData(50 * 1024, 20)]           // небольшой файл
    [InlineData(1024 * 1024, 0)]          // мегабайт в одном файле
    [InlineData(10L * 1024 * 1024, 0)]    // ниже нуля не уходим
    public void HugeFilesLowerTheScore(long largest, double expected)
    {
        var result = CodeHealthCategory.Evaluate(Facts(Code(largest: largest)));

        Assert.Equal(expected, PartNamed(result, "нет разросшихся файлов").Points, 1);
    }

    [Fact]
    public void QualityConfigIsNamedInTheExplanation()
    {
        var result = CodeHealthCategory.Evaluate(Facts(Code(quality: "diktat-analysis.yml")));

        var part = PartNamed(result, "проверка кода настроена");

        Assert.Equal(20, part.Points, 1);
        Assert.Contains("diktat-analysis.yml", part.Explanation);
    }

    [Fact]
    public void EveryPartExplainsItself()
    {
        var result = CodeHealthCategory.Evaluate(Facts(Code(files: 50, todo: 3, tests: true)));

        Assert.All(result.Parts, part =>
            Assert.False(string.IsNullOrWhiteSpace(part.Explanation)));
    }
}
