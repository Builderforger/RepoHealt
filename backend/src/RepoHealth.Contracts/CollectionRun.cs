using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Итог одного прохода по каталогу.
/// По ТЗ сервис должен показывать статус и время последнего анализа —
/// это он и есть.
public class CollectionRun
{
    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }

    /// running, completed, failed
    [JsonPropertyName("status")]
    public string Status { get; set; } = "running";

    /// Сколько репозиториев было в каталоге.
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// Сколько скачали и разобрали заново.
    [JsonPropertyName("collected")]
    public int Collected { get; set; }

    /// Сколько пропустили: с прошлого раза они не менялись.
    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public TimeSpan? Duration => FinishedAt - StartedAt;
}
