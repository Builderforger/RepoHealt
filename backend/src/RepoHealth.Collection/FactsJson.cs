using System.Text.Encodings.Web;
using System.Text.Json;
using RepoHealth.Contracts;

namespace RepoHealth.Collection;

/// Запись и чтение фактов в том же виде, в каком их
/// складывал питоновский сборщик: один файл на репозиторий.
public static class FactsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Без этого русские буквы уезжают в абв
        // и файл невозможно читать глазами.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Write(RepoFacts facts) => JsonSerializer.Serialize(facts, Options);

    public static RepoFacts Read(string json) =>
        JsonSerializer.Deserialize<RepoFacts>(json)
        ?? throw new InvalidOperationException("не удалось разобрать факты");
}
