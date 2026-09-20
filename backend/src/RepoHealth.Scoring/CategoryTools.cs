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
