using System.Text.Json;
using RepoHealth.Contracts;

namespace RepoHealth.Scoring.Tests;

/// <summary>
/// Проверяет сведение категорий в итоговую оценку
/// и главное свойство методики — перенормировку весов.
/// </summary>
public class RepoHealthScoreTests
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

    private static Note CloneFailed() => new()
    {
        Source = "git",
        Operation = "clone_shallow_since",
        Reason = "repository not found"
    };

    private static CategoryResult Category(HealthReport report, string name) =>
        report.Categories.Single(category => category.Name == name);

    // ---------- все шесть категорий на месте ----------

    [Fact]
    public void AllSixCategoriesArePresent()
    {
        var report = RepoHealthScore.Evaluate(Facts());

        Assert.Equal(6, report.Categories.Count);
        Assert.Contains(report.Categories, c => c.Name == "Документация");
        Assert.Contains(report.Categories, c => c.Name == "Активность");
        Assert.Contains(report.Categories, c => c.Name == "CI/CD");
        Assert.Contains(report.Categories, c => c.Name == "Issues");
        Assert.Contains(report.Categories, c => c.Name == "Security");
        Assert.Contains(report.Categories, c => c.Name == "Состояние кода");
    }

    [Fact]
    public void BaseWeightsAddUpToHundred()
    {
        var report = RepoHealthScore.Evaluate(Facts());

        Assert.Equal(100, report.Categories.Sum(c => c.BaseWeight), 1);
    }

    [Fact]
    public void EveryMissingCategoryExplainsWhy()
    {
        var report = RepoHealthScore.Evaluate(Facts());

        var missing = report.Categories.Where(c => c.Status == CategoryStatus.NoData);

        Assert.All(missing, category =>
            Assert.False(string.IsNullOrWhiteSpace(category.NoDataReason),
                $"категория «{category.Name}» без причины"));
    }

    // ---------- перенормировка весов ----------

    [Fact]
    public void WeightsOfScoredCategoriesAddUpToHundred()
    {
        // Считаются две категории из шести. Их веса — 15 и 15,
        // значит каждая должна получить по половине итога.
        var report = RepoHealthScore.Evaluate(Facts());

        Assert.Equal(50, Category(report, "Документация").AppliedWeight, 1);
        Assert.Equal(50, Category(report, "Активность").AppliedWeight, 1);
        Assert.Equal(100, report.Categories.Sum(c => c.AppliedWeight), 1);
    }

    [Fact]
    public void MissingCategoriesGetNoWeight()
    {
        var report = RepoHealthScore.Evaluate(Facts());

        Assert.All(
            report.Categories.Where(c => c.Status == CategoryStatus.NoData),
            category => Assert.Equal(0, category.AppliedWeight));
    }

    [Fact]
    public void MissingDataDoesNotDragTheScoreDown()
    {
        // Ключевая проверка требования ТЗ.
        // У проекта идеальная активность и четыре категории без данных.
        // Если бы отсутствие данных считалось нулём, итог был бы около 15.
        var report = RepoHealthScore.Evaluate(
            Facts(daysSinceLastCommit: 1, commits90: 30, authors90: 5));

        Assert.Equal(100, Category(report, "Активность").Score, 1);

        // документация пустая и честно даёт ноль, активность — сто,
        // вес пополам, значит итог ровно пятьдесят
        Assert.Equal(50, report.Score!.Value, 1);
    }

    // ---------- когда данных нет совсем ----------

    [Fact]
    public void NoDataAtAllMeansNoScore()
    {
        // Репозиторий не скачался: неизвестно вообще ничего.
        // Итог должен быть «нет оценки», а не ноль.
        var report = RepoHealthScore.Evaluate(Facts(notes: CloneFailed()));

        Assert.Null(report.Score);
        Assert.All(report.Categories, c => Assert.Equal(CategoryStatus.NoData, c.Status));
        Assert.All(report.Categories, c => Assert.Equal(0, c.AppliedWeight));
    }

    // ---------- поведение методики ----------

    [Fact]
    public void FixingTheReadmeRaisesTheScore()
    {
        var before = RepoHealthScore.Evaluate(Facts(readme: "# Проект"));

        var after = RepoHealthScore.Evaluate(Facts(readme: """
            # Проект

            ## Установка
            Инструкция по установке.

            ## Запуск
            ```
            run me
            ```
            """));

        Assert.True(after.Score > before.Score,
            $"было {before.Score}, стало {after.Score}");
    }

    [Fact]
    public void AbandoningTheProjectLowersTheScore()
    {
        var living = RepoHealthScore.Evaluate(Facts(daysSinceLastCommit: 3, commits90: 20));
        var abandoned = RepoHealthScore.Evaluate(Facts(daysSinceLastCommit: 400, commits90: 0));

        Assert.True(abandoned.Score < living.Score,
            $"живой {living.Score}, заброшенный {abandoned.Score}");
    }

    [Fact]
    public void ReportCarriesMethodologyVersionAndRepoName()
    {
        var report = RepoHealthScore.Evaluate(Facts());

        Assert.Equal("test/test", report.Repo);
        Assert.Equal("https://example.com", report.Url);
        Assert.False(string.IsNullOrWhiteSpace(report.MethodologyVersion));
    }

    // ---------- расчёт повторяем ----------

    [Fact]
    public void SameFactsAlwaysGiveTheSameScore()
    {
        // Расчёт не смотрит на текущее время и никуда не ходит,
        // поэтому одни и те же данные обязаны давать один и тот же балл.
        var facts = Facts(readme: "# Проект\n## Установка\nтекст", daysSinceLastCommit: 20, commits90: 4);

        var first = RepoHealthScore.Evaluate(facts).Score;
        var second = RepoHealthScore.Evaluate(facts).Score;

        Assert.Equal(first, second);
    }

    // ---------- на настоящих данных ----------

    [Fact]
    public void AllFixturesProduceSensibleReports()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "fixtures");
        var problems = new List<string>();

        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var facts = JsonSerializer.Deserialize<RepoFacts>(File.ReadAllText(file))!;
            var report = RepoHealthScore.Evaluate(facts);

            var name = Path.GetFileName(file);

            if (report.Score is < 0 or > 100)
                problems.Add($"{name}: балл вне диапазона — {report.Score}");

            if (report.Categories.Count != 6)
                problems.Add($"{name}: категорий {report.Categories.Count}, а должно быть 6");

            var appliedSum = report.Categories.Sum(c => c.AppliedWeight);
            if (report.Score is not null && Math.Abs(appliedSum - 100) > 0.5)
                problems.Add($"{name}: сумма применённых весов {appliedSum}, а должна быть 100");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
