using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Оценивает безопасность по результатам проверок SourceCraft.
///
/// Считаем не «есть ли уязвимости вообще», а насколько они
/// опасны: одна критическая находка весит больше, чем десяток
/// мелких замечаний. Своего сканирования не делаем — ТЗ требует
/// опираться только на AppSec SourceCraft.
public static class SecurityCategory
{
    public const string Name = "Security";
    public const double BaseWeight = 20;

    /// Сколько находок уровня считается «хуже некуда».
    ///
    /// Шкала растёт примерно впятеро на каждый уровень: две
    /// критические — предел, десять опасных — предел, полсотни
    /// средних — предел. Так одна критическая находка весит
    /// примерно как пять опасных и как двадцать пять средних,
    /// что отвечает их настоящей разнице в опасности.
    ///
    /// Пороги нарочно не сделаны жёсткими: у любого живого
    /// проекта анализатор находит замечания, и важно различать
    /// «шесть находок» и «шестьдесят», а не ставить обоим ноль.
    private const int CriticalIsBad = 2;
    private const int HighIsBad = 10;
    private const int MediumIsBad = 50;

    public static CategoryResult Evaluate(RepoFacts facts)
    {
        var security = facts.Security;

        if (security is null)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight,
                "результаты AppSec не отданы: они доступны участникам репозитория " +
                "и только если проверка безопасности в нём включена");
        }

        if (!security.ScanFound)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight,
                "проверка безопасности в репозитории ещё ни разу не отработала");
        }

        var parts = new List<PartResult>();

        Add(parts, "критические находки", 45, security.Critical, CriticalIsBad);
        Add(parts, "опасные находки", 30, security.High, HighIsBad);
        Add(parts, "средние находки", 15, security.Medium, MediumIsBad);

        parts.Add(Freshness(security));

        // Уровни, которые API не отдал, в оценку не вошли.
        // Если не вышло ни одного, оценивать нечего.
        if (parts.Count == 1)
        {
            return CategoryResult.NotEnoughData(
                Name, BaseWeight,
                "проверка есть, но разбивку находок по опасности получить не удалось");
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

    /// Часть за один уровень опасности. Если API не отдал число,
    /// часть не добавляется вовсе: отсутствие ответа — это не ноль
    /// находок, и обнулять за него нельзя.
    private static void Add(
        List<PartResult> parts, string name, double maxPoints, int? found, int bad)
    {
        if (found is not { } count)
            return;

        var share = Scales.Fewer(count, 0, bad) ?? 0;

        parts.Add(CategoryTools.Part(name, maxPoints, share,
            count == 0
                ? "не найдено"
                : $"найдено: {count} (полная потеря баллов начинается с {bad})"));
    }

    /// Свежесть проверки. Результат годичной давности говорит
    /// о состоянии кода, которого уже нет.
    private static PartResult Freshness(SecurityFacts security)
    {
        if (!DateTimeOffset.TryParse(security.ScanFinishedAt, out var finished))
        {
            // Даты нет — не наказываем, но и не хвалим:
            // считаем часть выполненной, раз проверка была.
            return CategoryTools.Part("свежесть проверки", 10, 1,
                "проверка была, дату завершения API не вернул");
        }

        var days = (int)(DateTimeOffset.UtcNow - finished).TotalDays;
        var share = Scales.Fades(days, 30, 180) ?? 0;

        return CategoryTools.Part("свежесть проверки", 10, share,
            $"последняя проверка: {days} дн. назад");
    }
}
