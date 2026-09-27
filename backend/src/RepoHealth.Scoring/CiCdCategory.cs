using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает сборки: доходят ли они до конца, запускаются ли
/// сами и работает ли это до сих пор.
///
/// Отдельно считать «есть ли CI» не нужно: если сборок нет,
/// оценивать нечего — так и пишем, без штрафа. Проект может
/// собираться где-то ещё, и наказывать его за это неправильно.
public static class CiCdCategory
{
    public const string Name = "CI/CD";
    public const double BaseWeight = 15;

    /// Сборка недельной давности — живая, полугодовой — уже нет.
    private const int FreshDays = 7;
    private const int StaleDays = 180;

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        var ci = facts.Ci;

        if (ci is null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight,
                "статусы сборок доступны только участникам репозитория; " +
                "с текущими правами не отдаются");
        }

        if (ci.RunsSeen == 0)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight,
                "сборки в SourceCraft не запускались — возможно, проект собирается в другом месте");
        }

        var parts = new List<PartResult>();

        if (ci.Finished > 0)
        {
            var successShare = (double)ci.Successful / ci.Finished;

            parts.Add(CategoryTools.Part("успешные сборки", 45, successShare,
                $"успешно завершились: {ci.Successful} из {ci.Finished}"));
        }

        var automaticShare = (double)ci.Automatic / ci.RunsSeen;

        parts.Add(CategoryTools.Part("запуск без участия человека", 25, automaticShare,
            ci.Automatic == 0
                ? $"все {ci.RunsSeen} запусков сделаны вручную"
                : $"по событиям в репозитории: {ci.Automatic} из {ci.RunsSeen} запусков"));

        var freshness = Freshness(ci);
        if (freshness is not null)
            parts.Add(freshness);

        // Остались только запуски, ни один из которых не завершился
        // и не имеет даты — по сути, смотреть не на что.
        if (parts.Count == 0)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, "ни одна сборка пока не завершилась");
        }

        return new CategoryResult
        {
            Name = Name,
            Status = CategoryStatus.Scored,
            BaseWeight = BaseWeight,
            Score = CategoryTools.Combine(parts),
            Parts = parts
        };
    }

    /// Когда сборки шли в последний раз. Настроенный, но давно
    /// молчащий процесс — это почти то же, что его отсутствие.
    private static PartResult? Freshness(CiFacts ci)
    {
        var share = Scales.Fades(ci.DaysSinceLastRun, FreshDays, StaleDays);

        if (share is null)
            return null;

        return CategoryTools.Part("свежесть сборок", 30, share.Value,
            $"последняя сборка: {ci.DaysSinceLastRun} дн. назад " +
            $"(свежей считается до {FreshDays} дн., остановившейся — от {StaleDays})");
    }
}
