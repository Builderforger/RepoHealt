using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

public class CiCdCategoryTests
{
    private static RepoFacts With(CiFacts? ci) =>
        new() { Catalog = new Catalog(), Git = new Git(), Ci = ci };

    private static CiFacts Good() => new()
    {
        RunsSeen = 20, Finished = 20, Successful = 20, Failed = 0,
        Automatic = 20, DaysSinceLastRun = 1
    };

    [Fact]
    public void NoAccessToBuildsIsNotABadScore()
    {
        var result = CiCdCategory.Evaluate(With(null));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("участникам репозитория", result.NoDataReason);
    }

    [Fact]
    public void MissingBuildsAreNotPenalized()
    {
        var result = CiCdCategory.Evaluate(With(new CiFacts { RunsSeen = 0 }));

        Assert.Equal(CategoryStatus.NoData, result.Status);
        Assert.Contains("в другом месте", result.NoDataReason);
    }

    [Fact]
    public void GreenAutomatedBuildsGiveHundred()
    {
        var result = CiCdCategory.Evaluate(With(Good()));

        Assert.Equal(CategoryStatus.Scored, result.Status);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void FailingBuildsLowerScore()
    {
        var half = Good();
        half.Successful = 10;
        half.Failed = 10;

        var result = CiCdCategory.Evaluate(With(half));

        var part = result.Parts.Single(p => p.Name == "успешные сборки");
        Assert.Equal(22.5, part.Points);
        Assert.True(result.Score < 100);
    }

    [Fact]
    public void ManualRunsAreValuedLower()
    {
        var byHand = Good();
        byHand.Automatic = 0;

        var result = CiCdCategory.Evaluate(With(byHand));

        var part = result.Parts.Single(p => p.Name == "запуск без участия человека");
        Assert.Equal(0, part.Points);
        Assert.Contains("вручную", part.Explanation);
    }

    [Fact]
    public void LongSilentBuildsLoseFreshness()
    {
        var old = Good();
        old.DaysSinceLastRun = 400;

        var result = CiCdCategory.Evaluate(With(old));

        Assert.Equal(0, result.Parts.Single(p => p.Name == "свежесть сборок").Points);
    }

    [Fact]
    public void WithoutDateFreshnessIsSkippedNotZeroed()
    {
        var noDates = Good();
        noDates.DaysSinceLastRun = null;

        var result = CiCdCategory.Evaluate(With(noDates));

        Assert.Equal(100, result.Score);
        Assert.DoesNotContain(result.Parts, p => p.Name == "свежесть сборок");
    }

    [Fact]
    public void RunningBuildsCountAsNeitherSuccessNorFailure()
    {
        // Двадцать запусков, из них завершились только два.
        var running = new CiFacts
        {
            RunsSeen = 20, Finished = 2, Successful = 2, Failed = 0,
            Automatic = 20, DaysSinceLastRun = 1
        };

        var result = CiCdCategory.Evaluate(With(running));

        var part = result.Parts.Single(p => p.Name == "успешные сборки");
        Assert.Equal("успешно завершились: 2 из 2", part.Explanation);
    }

    [Fact]
    public void BuildsContributeToOverallScore()
    {
        var facts = With(Good());
        facts.Catalog!.Name = "org/project";

        var report = RepoHealthScore.Evaluate(facts);
        var ci = report.Categories.Single(c => c.Name == CiCdCategory.Name);

        Assert.Equal(CategoryStatus.Scored, ci.Status);
        Assert.True(ci.AppliedWeight > 0);
    }
}
