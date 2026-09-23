using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает, как в проекте обращаются с задачами:
/// доводят ли до конца, не зависают ли они и быстро ли закрываются.
public static class IssuesCategory
{
    public const string Name = "Issues";
    public const double BaseWeight = 15;

    /// Задача, закрытая за неделю, — очень хорошо.
    /// Дольше трёх месяцев — уже плохо.
    private const int FastDays = 7;
    private const int SlowDays = 90;

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        var issues = facts.Issues;

        if (issues is null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, "задачи недоступны с текущими правами");
        }

        // Задач нет совсем. Это не плохо: проект может вести их
        // в другом месте или не вести вовсе. Оценивать нечего.
        if (issues.Total == 0)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight, "в репозитории нет задач — работу с ними оценить не по чему");
        }

        var finished = issues.Closed + issues.Cancelled;
        var closedShare = (double)finished / issues.Total;

        var staleShare = issues.Open > 0 ? (double)issues.StaleOpen / issues.Open : 0;

        var parts = new List<PartResult>
        {
            CategoryTools.Part("доведено до конца", 40, closedShare,
                $"закрыто {finished} задач из {issues.Total}"),

            CategoryTools.Part("нет зависших", 30, 1 - staleShare,
                issues.Open == 0
                    ? "открытых задач нет"
                    : $"{issues.StaleOpen} из {issues.Open} открытых задач " +
                      "не обновлялись больше 90 дней"),

            Speed(issues)
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

    /// Скорость закрытия. Если ни одна задача ещё не закрыта,
    /// считать не из чего — часть не начисляется вовсе.
    private static PartResult Speed(IssuesFacts issues)
    {
        if (issues.MedianDaysToClose is not { } days)
        {
            return CategoryTools.Part("скорость закрытия", 30, 0,
                "закрытых задач нет, скорость считать не из чего");
        }

        var share = Scales.Fades((int)Math.Round(days), FastDays, SlowDays) ?? 0;

        return CategoryTools.Part("скорость закрытия", 30, share,
            $"задачи закрываются за {days:0.#} дн. в среднем " +
            $"(быстрым считается до {FastDays} дн., медленным — от {SlowDays})");
    }
}
