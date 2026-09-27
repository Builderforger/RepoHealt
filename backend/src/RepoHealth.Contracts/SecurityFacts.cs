using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Результаты проверки безопасности из AppSec SourceCraft.
///
/// Своё сканирование мы не делаем — ТЗ это прямо запрещает.
/// Здесь только то, что показал сам SourceCraft тому, у кого
/// есть права на репозиторий.
public class SecurityFacts
{
    /// Было ли вообще сканирование. false — проверка
    /// в репозитории включена, но ещё ни разу не отработала.
    [JsonPropertyName("scan_found")]
    public bool ScanFound { get; set; }

    [JsonPropertyName("scan_finished_at")]
    public string? ScanFinishedAt { get; set; }

    /// Сколько групп дефектов нашла последняя проверка,
    /// по уровням опасности. null — уровень посчитать не вышло.
    [JsonPropertyName("critical")]
    public int? Critical { get; set; }

    [JsonPropertyName("high")]
    public int? High { get; set; }

    [JsonPropertyName("medium")]
    public int? Medium { get; set; }

    [JsonPropertyName("low")]
    public int? Low { get; set; }

    /// Всего групп в последней проверке, без разбивки.
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    /// Из них остались открытыми — то есть их ещё не разобрали
    /// и не отметили ложным срабатыванием.
    [JsonPropertyName("open")]
    public int? Open { get; set; }
}
