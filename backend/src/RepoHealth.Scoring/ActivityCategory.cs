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

        // Участников берём у платформы: она склеивает разные почты
        // одного человека. Подписи в коммитах — запасной вариант,
        // они завышают счёт и тянут за собой чужую историю.
        var contributors = facts.Catalog?.Contributors;
        var people = contributors ?? git?.Authors90 ?? 0;

        var peopleExplanation = contributors is not null
            ? $"участников по данным SourceCraft: {contributors}; " +
              $"коммитили за последние 90 дней: {git?.Authors90 ?? 0}; " +
              $"для полного балла нужно {AuthorsForFullScore}"
            : $"авторов за 90 дней по подписям в коммитах: {people} " +
              $"(список участников недоступен); " +
              $"для полного балла нужно {AuthorsForFullScore}";

        var freshness = Scales.Fades(daysSinceCommit, FreshDays, StaleDays) ?? 0;

        var parts = new List<PartResult>
        {
            CategoryTools.Part("свежесть", 35, freshness,
                $"последний коммит {daysSinceCommit} дн. назад " +
                $"(свежим считается до {FreshDays} дн., остановившимся — от {StaleDays})"),

            CategoryTools.Part("темп работы", 35,
                Scales.Grows(commits, CommitsForFullScore),
                $"коммитов за 90 дней: {commits}, для полного балла нужно {CommitsForFullScore}"),

            CategoryTools.Part("участники", 30,
                Scales.Grows(people, AuthorsForFullScore),
                peopleExplanation)
        };

        return new CategoryResult
        {
            Name = Name,
            Status = CategoryStatus.Scored,
            BaseWeight = BaseWeight,
            Score = CategoryTools.Combine(parts),
            Parts = parts
        };
    }
}
