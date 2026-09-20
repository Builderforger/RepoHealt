using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Оценка одной категории: балл, статус и разбор по частям.
public class CategoryResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public CategoryStatus Status { get; set; } = CategoryStatus.Scored;

    /// Балл внутри категории, от 0 до 100. При статусе NoData не имеет смысла и игнорируется.
    [JsonPropertyName("score")]
    public double Score { get; set; }

    /// Базовый вес категории в итоговой оценке. Реально применённый вес считается при сведении категорий: те, что в статусе NoData, выпадают, а веса остальных пересчитываются так, чтобы в сумме давать сто процентов.
    [JsonPropertyName("base_weight")]
    public double BaseWeight { get; set; }

    /// Вес, который категория получила на самом деле, в процентах.
    /// Заполняется при сведении: категории без данных выпадают,
    /// а их вес расходится по остальным.
    [JsonPropertyName("applied_weight")]
    public double AppliedWeight { get; set; }

    /// Из чего сложился балл.
    [JsonPropertyName("parts")]
    public List<PartResult> Parts { get; set; } = [];

    /// Почему данных нет. Заполняется только при NoData.
    [JsonPropertyName("no_data_reason")]
    public string? NoDataReason { get; set; }

    public static CategoryResult NotEnoughData(string name, double baseWeight, string reason) =>
        new()
        {
            Name = name,
            Status = CategoryStatus.NoData,
            BaseWeight = baseWeight,
            NoDataReason = reason
        };
}
