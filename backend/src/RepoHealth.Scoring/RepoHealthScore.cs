using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Сводит все категории в одну оценку.
public static class RepoHealthScore
{
    public const string MethodologyVersion = "0.2";

    // Все шесть категорий из ТЗ лежат здесь сразу.
    // Те, что ещё не считаются, честно помечены «нет данных»
    // с указанием причины — так видно, что они не забыты,
    // и при появлении данных менять придётся только сбор.
    private const double CiWeight = 15;
    private const double IssuesWeight = 15;
    private const double SecurityWeight = 20;
    private const double CodeHealthWeight = 20;

    public static HealthReport Evaluate(RepoFacts facts)
    {
        var categories = new List<CategoryResult>
        {
            DocumentationCategory.Evaluate(facts),
            ActivityCategory.Evaluate(facts),

            CategoryResult.NotEnoughData("CI/CD", CiWeight,
                "статусы сборок доступны только участникам репозитория; " +
                "с текущими правами не отдаются"),

            CategoryResult.NotEnoughData("Issues", IssuesWeight,
                "данные о задачах пока не собираются"),

            CategoryResult.NotEnoughData("Security", SecurityWeight,
                "результаты AppSec доступны только участникам репозитория; " +
                "с текущими правами не отдаются"),

            CategoryResult.NotEnoughData("Состояние кода", CodeHealthWeight,
                "анализ кода пока не реализован")
        };

        var report = new HealthReport
        {
            Repo = facts.Catalog?.Name ?? "",
            Url = facts.Catalog?.Url ?? "",
            Language = facts.Catalog?.Language?.Name,
            Likes = facts.Catalog?.Likes,
            LastActivity = facts.Catalog?.UpdatedAt,
            MethodologyVersion = MethodologyVersion,
            Score = Combine(categories),
            Categories = categories
        };

        // Рекомендации строятся после сведения: им нужны веса,
        // которые категории получили после перенормировки.
        report.Recommendations = Recommendations.For(report);

        return report;
    }

    /// Сводит категории в итоговый балл.
    /// Категории без данных выпадают, а их вес расходится
    /// по остальным — так, чтобы в сумме снова получилось 100%.
    private static double? Combine(List<CategoryResult> categories)
    {
        var scored = categories
            .Where(category => category.Status == CategoryStatus.Scored)
            .ToList();

        var totalWeight = scored.Sum(category => category.BaseWeight);

        // Данных нет вообще ни по одной категории.
        // Ставить ноль было бы враньём: мы просто ничего не знаем.
        if (totalWeight <= 0)
        {
            foreach (var category in categories)
                category.AppliedWeight = 0;

            return null;
        }

        foreach (var category in categories)
        {
            category.AppliedWeight = category.Status == CategoryStatus.Scored
                ? Math.Round(category.BaseWeight / totalWeight * 100, 1)
                : 0;
        }

        var total = scored.Sum(category => category.Score * category.BaseWeight) / totalWeight;

        return Math.Round(total, 1);
    }
}
