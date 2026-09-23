using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Что известно о задачах репозитория.
/// Блока нет вовсе, если прочитать задачи не удалось.
public class IssuesFacts
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// Задачи в работе или ещё не начатые.
    [JsonPropertyName("open")]
    public int Open { get; set; }

    [JsonPropertyName("closed")]
    public int Closed { get; set; }

    [JsonPropertyName("cancelled")]
    public int Cancelled { get; set; }

    /// Открытые задачи, которых не касались больше 90 дней.
    [JsonPropertyName("stale_open")]
    public int StaleOpen { get; set; }

    /// Сколько дней в среднем проходит от создания до закрытия.
    /// null — закрытых задач не было, считать не из чего.
    [JsonPropertyName("median_days_to_close")]
    public double? MedianDaysToClose { get; set; }
}
