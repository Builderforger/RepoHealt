using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет категорию «Активность».
/// </summary>
public class ActivityCategoryTests
{
    private static RepoFacts Facts(
        int? daysSinceLastCommit = 1,
        int commits90 = 0,
        int authors90 = 0,
        params Note[] notes) => new()
        {
            Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
            Git = new Git
            {
                DaysSinceLastCommit = daysSinceLastCommit,
                Commits90 = commits90,
                Authors90 = authors90
            },
            Notes = [.. notes]
        };

    private static Note CloneFailed() => new()
    {
        Source = "git",
        Operation = "clone_depth_1",
        Reason = "repository not found"
    };

    private static PartResult PartNamed(CategoryResult result, string name) =>
        result.Parts.Single(part => part.Name == name);

    // ---------- нет данных ----------

    [Fact]
    public void RepositoryNotClonedMeansNoData()
    {
        var result = ActivityCategory.Evaluate(Facts(notes: CloneFailed()));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.NotNull(result.NoDataReason);
    }

    [Fact]
    public void NoCommitsAtAllMeansNoData()
    {
        // Репозиторий скачался, но истории в нём нет.
        // Считать активность не из чего — это не ноль, а неизвестность.
        var result = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: null));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("коммит", result.NoDataReason!);
    }

    // ---------- свежесть ----------

    [Theory]
    [InlineData(1, 35)]      // вчера — полный балл
    [InlineData(14, 35)]     // ровно на границе «свежего»
    [InlineData(97, 17.5)]   // посередине между 14 и 180
    [InlineData(180, 0)]     // остановился
    [InlineData(500, 0)]     // давно мёртв
    public void FreshnessFollowsTheScale(int days, double expected)
    {
        var result = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: days));

        Assert.Equal(expected, PartNamed(result, "свежесть").Points, 1);
    }

    // ---------- темп и люди ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 17.5)]   // половина от нужных двадцати
    [InlineData(20, 35)]
    [InlineData(100, 35)]    // выше потолка не поднимаемся
    public void PaceFollowsCommitCount(int commits, double expected)
    {
        var result = ActivityCategory.Evaluate(Facts(commits90: commits));

        Assert.Equal(expected, PartNamed(result, "темп работы").Points, 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 7.5)]
    [InlineData(2, 15)]
    [InlineData(4, 30)]
    [InlineData(178, 30)]    // импортированная чужая история не даёт сверх потолка
    public void PeopleFollowRecentAuthorCount(int authors, double expected)
    {
        var result = ActivityCategory.Evaluate(Facts(authors90: authors));

        Assert.Equal(expected, PartNamed(result, "люди").Points, 1);
    }

    // ---------- поведение в целом ----------

    [Fact]
    public void LivingProjectScoresMuchHigherThanAbandonedOne()
    {
        var living = ActivityCategory.Evaluate(
            Facts(daysSinceLastCommit: 2, commits90: 25, authors90: 5));

        var abandoned = ActivityCategory.Evaluate(
            Facts(daysSinceLastCommit: 400, commits90: 0, authors90: 0));

        Assert.Equal(100, living.Score, 1);
        Assert.Equal(0, abandoned.Score, 1);
    }

    [Fact]
    public void MoreCommitsNeverLowerTheScore()
    {
        // Свойство, а не число: добавление работы не может ухудшить оценку.
        var before = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: 30, commits90: 5));
        var after = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: 30, commits90: 15));

        Assert.True(after.Score > before.Score,
            $"было {before.Score}, стало {after.Score}");
    }

    [Fact]
    public void EveryPartExplainsItself()
    {
        var result = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: 30, commits90: 3, authors90: 1));

        Assert.All(result.Parts, part =>
            Assert.False(string.IsNullOrWhiteSpace(part.Explanation),
                $"часть «{part.Name}» без объяснения"));
    }

    [Fact]
    public void ScoreIsTheSumOfParts()
    {
        var result = ActivityCategory.Evaluate(Facts(daysSinceLastCommit: 40, commits90: 7, authors90: 2));

        Assert.Equal(result.Parts.Sum(part => part.Points), result.Score, 1);
    }
}
