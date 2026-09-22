using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Удалось ли посчитать категорию.
/// В JSON пишется словом, а не числом: «Scored» понятнее,
/// чем «0», всем, кто потом читает отчёт.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CategoryStatus
{
    /// Данные собраны, оценка посчитана.
    Scored,

    /// Данные собрать не удалось — категория выпадает из расчёта. Это не то же самое, что низкая оценка.
    NoData
}
