using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Итоговый отчёт по репозиторию.
public class HealthReport
{
    [JsonPropertyName("repo")]
    public string Repo { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// Итоговая оценка от 0 до 100.
    /// null означает, что оценить не удалось совсем:
    /// ни по одной категории не набралось данных.
    /// Это не ноль — это отсутствие оценки.
    [JsonPropertyName("score")]
    public double? Score { get; set; }

    /// Версия методики. Нужна, чтобы объяснить, почему
    /// вчерашняя оценка не совпадает с сегодняшней.
    [JsonPropertyName("methodology_version")]
    public string MethodologyVersion { get; set; } = "";

    /// Поля для страницы рейтинга: по ТЗ там нужны язык,
    /// лайки и дата последней активности.
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("likes")]
    public double? Likes { get; set; }

    [JsonPropertyName("last_activity")]
    public string? LastActivity { get; set; }

    [JsonPropertyName("categories")]
    public List<CategoryResult> Categories { get; set; } = [];

    /// Что стоит исправить, по убыванию пользы.
    [JsonPropertyName("recommendations")]
    public List<Recommendation> Recommendations { get; set; } = [];

    /// Когда собирали данные. Проставляет тот, кто их собирал:
    /// сам расчёт о текущем времени ничего не знает.
    [JsonPropertyName("analyzed_at")]
    public DateTimeOffset? AnalyzedAt { get; set; }
}
