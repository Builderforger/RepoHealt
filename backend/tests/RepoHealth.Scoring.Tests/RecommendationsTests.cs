using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Рекомендации должны быть конкретными, опираться на факты
/// и стоять в порядке пользы — этого требует раздел 3.3 ТЗ.
/// </summary>
public class RecommendationsTests
{
    private static RepoFacts Facts(
        string readme = "",
        int? daysSinceLastCommit = 1,
        int commits90 = 0,
        int authors90 = 0,
        params Note[] notes) => new()
        {
            Catalog = new Catalog { Name = "test/test", Url = "https://example.com" },
            Git = new Git
            {
                Readme = readme,
                DaysSinceLastCommit = daysSinceLastCommit,
                Commits90 = commits90,
                Authors90 = authors90
            },
            Notes = [.. notes]
        };

    private static HealthReport Report(RepoFacts facts) => RepoHealthScore.Evaluate(facts);

    [Fact]
    public void EmptyProjectGetsRecommendations()
    {
        var report = Report(Facts());

        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public void EveryRecommendationIsComplete()
    {
        // По ТЗ у рекомендации должны быть: проблема, почему важно,
        // на чём основана, что делать, приоритет и ожидаемый эффект.
        var report = Report(Facts(daysSinceLastCommit: 300));

        Assert.All(report.Recommendations, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Problem));
            Assert.False(string.IsNullOrWhiteSpace(item.Why));
            Assert.False(string.IsNullOrWhiteSpace(item.Evidence));
            Assert.False(string.IsNullOrWhiteSpace(item.Action));
            Assert.False(string.IsNullOrWhiteSpace(item.Category));
            Assert.True(item.ExpectedGain > 0);
        });
    }

    [Fact]
    public void RecommendationsAreSortedByUsefulness()
    {
        var report = Report(Facts(readme: "# Проект", daysSinceLastCommit: 300));

        var gains = report.Recommendations.Select(item => item.ExpectedGain).ToList();

        Assert.Equal(gains.OrderByDescending(gain => gain), gains);
    }

    [Fact]
    public void ExpectedGainIsAHonestPromise()
    {
        // Обещание считается по одной части, а исправление обычно
        // задевает и соседние: добавив раздел «Установка», человек
        // заодно прибавляет заголовок и строки текста.
        // Поэтому обещание должно быть нижней границей —
        // пообещать больше, чем выйдет, нельзя.
        var before = Report(Facts(readme: "# Проект\n\n## Запуск\nтекст"));

        var install = before.Recommendations
            .Single(item => item.Problem.Contains("установку"));

        var after = Report(Facts(readme: "# Проект\n\n## Установка\nтекст\n\n## Запуск\nтекст"));

        var realGain = after.Score!.Value - before.Score!.Value;

        Assert.True(realGain >= install.ExpectedGain,
            $"обещали +{install.ExpectedGain}, а вышло +{realGain}");
    }

    [Fact]
    public void BiggerProblemsGetHigherPriority()
    {
        // Мёртвый проект без документации: сверху должны стоять
        // проблемы активности, они дороже стоят.
        var report = Report(Facts(daysSinceLastCommit: 400));

        Assert.Equal(Priority.High, report.Recommendations.First().Priority);
    }

    [Fact]
    public void PerfectProjectGetsNoAdvice()
    {
        var readme = string.Join("\n", Enumerable.Repeat("строка описания", 130))
                     + "\n"
                     + string.Join("\n", Enumerable.Range(1, 14).Select(n => $"## Раздел {n}"))
                     + "\n## Установка\n## Использование\n```\nкод\n```";

        var report = Report(Facts(readme, daysSinceLastCommit: 1, commits90: 40, authors90: 6));

        Assert.Equal(100, report.Score!.Value, 1);
        Assert.Empty(report.Recommendations);
    }

    [Fact]
    public void NoAdviceWhenThereIsNoData()
    {
        // Репозиторий не скачался: советовать не из чего,
        // выдумывать рекомендации на пустом месте нельзя.
        var report = Report(Facts(notes: new Note
        {
            Source = "git",
            Operation = "clone_depth_1",
            Reason = "repository not found"
        }));

        Assert.Null(report.Score);
        Assert.Empty(report.Recommendations);
    }
}
