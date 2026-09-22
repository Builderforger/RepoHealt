using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Участники считаются по данным SourceCraft, а не по подписям
/// в коммитах: платформа связывает разные почты одного человека.
/// </summary>
public class ContributorsTests
{
    private static RepoFacts Facts(int? contributors, int authors90) => new()
    {
        Catalog = new Catalog
        {
            Name = "test/test",
            Url = "https://example.com",
            Contributors = contributors
        },
        Git = new Git
        {
            DaysSinceLastCommit = 5,
            Commits90 = 10,
            Authors90 = authors90
        },
        Notes = []
    };

    private static PartResult People(RepoFacts facts) =>
        ActivityCategory.Evaluate(facts).Parts.Single(part => part.Name == "люди");

    [Fact]
    public void ContributorsFromPlatformWinOverCommitSignatures()
    {
        // В коммитах одна подпись, но в проекте четверо участников:
        // остальные просто не коммитили последние 90 дней.
        var people = People(Facts(contributors: 4, authors90: 1));

        Assert.Equal(30, people.Points, 1);
        Assert.Contains("по данным SourceCraft", people.Explanation);
    }

    [Fact]
    public void ImportedHistoryDoesNotInflateThePeopleScore()
    {
        // Чужая история принесла 178 подписей, но у платформы
        // числится один участник — верить надо платформе.
        var people = People(Facts(contributors: 1, authors90: 178));

        Assert.Equal(7.5, people.Points, 1);
    }

    [Fact]
    public void CommitSignaturesAreUsedWhenContributorsAreUnavailable()
    {
        var people = People(Facts(contributors: null, authors90: 2));

        Assert.Equal(15, people.Points, 1);
        Assert.Contains("недоступен", people.Explanation);
    }

    [Fact]
    public void ExplanationAlwaysSaysWhereTheNumberCameFrom()
    {
        // Пользователь должен понимать, что за число он видит.
        Assert.Contains("SourceCraft", People(Facts(3, 1)).Explanation);
        Assert.Contains("подписям в коммитах", People(Facts(null, 3)).Explanation);
    }
}
