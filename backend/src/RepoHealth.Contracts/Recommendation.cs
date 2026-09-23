using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Насколько срочно стоит заняться.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Priority
{
    Low,
    Medium,
    High
}

/// Одна рекомендация: что не так, почему это важно,
/// на чём основан вывод, что делать и что это даст.
public class Recommendation
{
    [JsonPropertyName("priority")]
    public Priority Priority { get; set; }

    /// Категория, к которой относится проблема.
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    /// Что обнаружено.
    [JsonPropertyName("problem")]
    public string Problem { get; set; } = "";

    /// Почему это важно.
    [JsonPropertyName("why")]
    public string Why { get; set; } = "";

    /// На каких фактах основан вывод — то, что можно проверить.
    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = "";

    /// Что сделать.
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    /// Сколько баллов итоговой оценки вернёт исправление.
    [JsonPropertyName("expected_gain")]
    public double ExpectedGain { get; set; }
}
