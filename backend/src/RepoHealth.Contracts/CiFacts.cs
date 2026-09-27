using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Что известно о сборках репозитория в SourceCraft.
///
/// Считаем по последним запускам: их статусам, датам и тому,
/// запускались они сами или руками.
public class CiFacts
{
    /// Сколько запусков посмотрели.
    [JsonPropertyName("runs_seen")]
    public int RunsSeen { get; set; }

    /// Из них завершившихся — успехом или падением.
    /// Отменённые и пропущенные сюда не входят: они ничего
    /// не говорят о том, собирается проект или нет.
    [JsonPropertyName("finished")]
    public int Finished { get; set; }

    [JsonPropertyName("successful")]
    public int Successful { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// Сколько запусков пришло от событий в репозитории,
    /// а не от нажатия кнопки руками.
    [JsonPropertyName("automatic")]
    public int Automatic { get; set; }

    /// Дней с последней завершившейся сборки.
    /// null — ни одна не завершилась или дат не было.
    [JsonPropertyName("days_since_last_run")]
    public int? DaysSinceLastRun { get; set; }
}
