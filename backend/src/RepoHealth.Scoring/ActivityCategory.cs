using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает, живой ли проект: давно ли в нём работали,
/// сколько работали и сколько человек.
public static class ActivityCategory
{
    public const string Name = "Активность";
    public const double BaseWeight = 15;

    // Коммит свежее двух недель — проект точно живой.
    // После полугода тишины считаем проект остановившимся.
    private const int FreshDays = 14;
    private const int StaleDays = 180;

    private const int CommitsForFullScore = 20;
    private const int AuthorsForFullScore = 4;

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        var failure = CategoryTools.FindCollectionFailure(facts);
        if (failure is not null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, $"не удалось скачать репозиторий: {failure.Reason}");
        }

        var git = facts.Git;
        var daysSinceCommit = git?.DaysSinceLastCommit;

        // Репозиторий скачался, но истории в нём нет — оценивать нечего.
        if (daysSinceCommit is null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, "в репозитории не нашлось ни одного коммита");
        }

        var commits = git?.Commits90 ?? 0;
        var authors = git?.Authors90 ?? 0;

        var freshness = Scales.Fades(daysSinceCommit, FreshDays, StaleDays) ?? 0;

        var parts = new List<PartResult>
        {
            CategoryTools.Part("свежесть", 35, freshness,
                $"последний коммит {daysSinceCommit} дн. назад " +
                $"(свежим считается до {FreshDays} дн., остановившимся — от {StaleDays})"),

            CategoryTools.Part("темп работы", 35,
                Scales.Grows(commits, CommitsForFullScore),
                $"{commits} коммитов за 90 дней, для полного балла нужно {CommitsForFullScore}"),

            CategoryTools.Part("люди", 30,
                Scales.Grows(authors, AuthorsForFullScore),
                $"{authors} авторов за 90 дней, для полного балла нужно {AuthorsForFullScore}")
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
}
