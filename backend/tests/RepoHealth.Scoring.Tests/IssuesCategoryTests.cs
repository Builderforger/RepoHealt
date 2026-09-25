using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет категорию «Issues».
/// </summary>
public class IssuesCategoryTests
{
    private static RepoFacts Facts(IssuesFacts? issues) => new()
    {
        Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
        Git = new Git { DaysSinceLastCommit = 1 },
        Issues = issues,
        Notes = []
    };

    private static PartResult PartNamed(CategoryResult result, string name) =>
        result.Parts.Single(part => part.Name == name);

    // ---------- когда оценивать нечего ----------

    [Fact]
    public void UnavailableIssuesMeanNoData()
    {
        var result = IssuesCategory.Evaluate(Facts(null));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("прав", result.NoDataReason!);
    }

    [Fact]
    public void EmptyIssueTrackerIsNotABadResult()
    {
        // Задач нет вовсе. Проект может вести их в другом месте
        // или не вести совсем — это не повод снижать оценку.
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts { Total = 0 }));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("нет задач", result.NoDataReason!);
    }

    // ---------- сами показатели ----------

    [Fact]
    public void AllIssuesClosedGivesFullPoints()
    {
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 10,
            Closed = 10,
            MedianDaysToClose = 3
        }));

        Assert.Equal(40, PartNamed(result, "закрытые задачи").Points, 1);
        Assert.Equal(30, PartNamed(result, "скорость закрытия").Points, 1);
        Assert.Equal(100, result.Score, 1);
    }

    [Fact]
    public void CancelledIssuesCountAsFinished()
    {
        // Отменённая задача — тоже разобранная: решение принято,
        // она больше не висит над проектом.
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 10,
            Closed = 6,
            Cancelled = 4
        }));

        Assert.Equal(40, PartNamed(result, "закрытые задачи").Points, 1);
    }

    [Theory]
    [InlineData(0, 30)]     // ничего не зависло
    [InlineData(5, 15)]     // половина открытых
    [InlineData(10, 0)]     // зависло всё
    public void StaleIssuesLowerTheScore(int stale, double expected)
    {
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 10,
            Open = 10,
            StaleOpen = stale
        }));

        Assert.Equal(expected, PartNamed(result, "зависшие задачи").Points, 1);
    }

    [Theory]
    [InlineData(3, 30)]        // быстро
    [InlineData(48.5, 15.2)]   // около середины между 7 и 90
    [InlineData(200, 0)]       // очень долго
    public void SpeedFollowsTimeToClose(double days, double expected)
    {
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 5,
            Closed = 5,
            MedianDaysToClose = days
        }));

        Assert.Equal(expected, PartNamed(result, "скорость закрытия").Points, 1);
    }

    [Fact]
    public void SpeedIsSkippedWhenItCannotBeMeasured()
    {
        // У закрытых задач может не быть даты закрытия.
        // Тогда часть выпадает из категории, а не обнуляется:
        // за отсутствие данных наказывать нельзя.
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 10,
            Open = 0,
            Closed = 10,
            MedianDaysToClose = null
        }));

        Assert.DoesNotContain(result.Parts, part => part.Name == "скорость закрытия");

        // Всё остальное взято полностью, значит и категория полная.
        Assert.Equal(100, result.Score, 1);
    }

    [Fact]
    public void EveryPartExplainsItself()
    {
        var result = IssuesCategory.Evaluate(Facts(new IssuesFacts
        {
            Total = 8,
            Open = 3,
            Closed = 5,
            StaleOpen = 1,
            MedianDaysToClose = 12
        }));

        Assert.All(result.Parts, part =>
            Assert.False(string.IsNullOrWhiteSpace(part.Explanation)));
    }
}
