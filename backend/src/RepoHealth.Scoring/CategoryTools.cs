using RepoHealth.Contracts;

namespace RepoHealth.Scoring;

/// Общее для всех категорий: проверка, что данные вообще собрались,
/// и сборка одной составляющей оценки.
internal static class CategoryTools
{
    /// Ищет пометку о том, что скачать репозиторий не удалось.
    /// Если такая есть — мы про проект ничего не знаем,
    /// и это не то же самое, что «в проекте всё плохо».
    public static Note? FindCollectionFailure(RepoFacts facts) =>
        facts.Notes.FirstOrDefault(note =>
            note.Operation.StartsWith("clone", StringComparison.OrdinalIgnoreCase));

    /// Сводит части в балл категории от 0 до 100.
    ///
    /// Считаем долю от того, что части могли дать вместе, а не просто
    /// сумму. Тогда набор частей можно менять — например, не считать
    /// признаки по файлам, когда список файлов не собран, — и балл
    /// останется сравнимым с остальными проектами.
    public static double Combine(List<PartResult> parts)
    {
        var possible = parts.Sum(part => part.MaxPoints);

        if (possible <= 0)
            return 0;

        return Math.Round(parts.Sum(part => part.Points) / possible * 100, 1);
    }

    /// Превращает долю (0..1) в баллы и прикладывает объяснение.
    public static PartResult Part(string name, double maxPoints, double share, string explanation) =>
        new()
        {
            Name = name,
            MaxPoints = maxPoints,
            Points = Math.Round(share * maxPoints, 1),
            Explanation = explanation
        };
}
