using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoHealth.Contracts;

namespace RepoHealth.Collection;

/// Читает результаты проверок безопасности SourceCraft.
///
/// Своё сканирование мы не запускаем — ТЗ разрешает опираться
/// только на то, что нашёл сам SourceCraft. Сюда ходим с токеном
/// того, кто смотрит: у участника репозитория доступ есть,
/// у постороннего — нет, и это нормальный ответ, а не поломка.
public sealed class AppSecApi : IDisposable
{
    /// Адрес в выгрузке спецификации не указан — его просили
    /// уточнять у владельца API. Поэтому он вынесен в настройку.
    private const string DefaultBaseAddress = "https://appsec.sourcecraft.tech";

    /// Уровни опасности, которые умеет фильтровать API.
    private static readonly string[] Severities = ["CRITICAL", "HIGH", "MEDIUM", "LOW"];

    private readonly HttpClient _http;

    public AppSecApi(string token, string? baseAddress = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Нужен токен доступа SourceCraft.", nameof(token));

        var address = FirstFilled(
            baseAddress,
            Environment.GetEnvironmentVariable("APPSEC_BASE_URL"),
            DefaultBaseAddress);

        _http = new HttpClient
        {
            BaseAddress = new Uri(address.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// Пустая переменная окружения — это «не задано»,
    /// а не «адрес пустой».
    private static string FirstFilled(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    /// Собирает результаты последней проверки репозитория.
    ///
    /// Возвращает null, когда данных нет: нет прав, безопасность
    /// в репозитории выключена или адрес API не отвечает. Пустой
    /// ответ и отказ в доступе — разные вещи, и выдавать отказ
    /// за «уязвимостей нет» нельзя, об этом прямо сказано
    /// в описании API.
    public async Task<SecurityFacts?> ReadAsync(
        string gitRepo, List<Note> notes, CancellationToken cancel = default)
    {
        var scan = await LatestScanAsync(gitRepo, notes, cancel);

        if (scan is null)
            return null;

        // Сканирование есть, но ещё не завершилось — считать нечего.
        if (string.IsNullOrEmpty(scan.Uuid))
            return new SecurityFacts { ScanFound = false };

        var facts = new SecurityFacts
        {
            ScanFound = true,
            ScanFinishedAt = scan.FinishedAt
        };

        facts.Total = await CountAsync(gitRepo, scan.Uuid, severity: null, notes, cancel);

        // Разбивка по уровням: спрашиваем у API только количество,
        // сами группы нам не нужны. Так и быстрее, и не зависим
        // от того, как названы поля внутри группы.
        var byLevel = new Dictionary<string, int?>();

        foreach (var severity in Severities)
            byLevel[severity] = await CountAsync(gitRepo, scan.Uuid, severity, notes, cancel);

        facts.Critical = byLevel["CRITICAL"];
        facts.High = byLevel["HIGH"];
        facts.Medium = byLevel["MEDIUM"];
        facts.Low = byLevel["LOW"];

        return facts;
    }

    private async Task<ScanSummary?> LatestScanAsync(
        string gitRepo, List<Note> notes, CancellationToken cancel)
    {
        var url = $"/v1/scans/latest?gitRepo={Uri.EscapeDataString(gitRepo)}";

        try
        {
            var answer = await _http.GetAsync(url, cancel);

            // 403 — прав нет или безопасность выключена,
            // 404 — репозиторий не найден. И то и другое означает
            // «данных нет», а не «всё чисто».
            if (answer.StatusCode is HttpStatusCode.Forbidden
                or HttpStatusCode.NotFound
                or HttpStatusCode.Unauthorized)
            {
                notes.Add(new Note
                {
                    Source = "appsec",
                    Operation = "appsec/scans-latest",
                    Reason = "проверки безопасности недоступны",
                    Code = (int)answer.StatusCode
                });

                return null;
            }

            answer.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<ScanSummary>(
                await answer.Content.ReadAsStringAsync(cancel));
        }
        catch (Exception error)
        {
            notes.Add(new Note
            {
                Source = "appsec",
                Operation = "appsec/scans-latest",
                Reason = $"не удалось прочитать проверки безопасности: {error.Message}"
            });

            return null;
        }
    }

    /// Сколько групп дефектов в проверке. Берём из totalSize:
    /// сами группы выкачивать незачем, нас интересует количество.
    private async Task<int?> CountAsync(
        string gitRepo, string scanUuid, string? severity, List<Note> notes, CancellationToken cancel)
    {
        var url = $"/v1/defect-groups?gitRepo={Uri.EscapeDataString(gitRepo)}"
                + $"&scanUuid={Uri.EscapeDataString(scanUuid)}&pageSize=1&pageToken=";

        if (severity is not null)
            url += $"&severity={severity}";

        try
        {
            var answer = await _http.GetAsync(url, cancel);

            if (!answer.IsSuccessStatusCode)
            {
                notes.Add(new Note
                {
                    Source = "appsec",
                    Operation = "appsec/defect-groups",
                    Reason = $"не удалось посчитать уровень {severity ?? "все"}",
                    Code = (int)answer.StatusCode
                });

                return null;
            }

            var page = JsonSerializer.Deserialize<DefectGroupsPage>(
                await answer.Content.ReadAsStringAsync(cancel));

            return page?.TotalSize;
        }
        catch (Exception error)
        {
            notes.Add(new Note
            {
                Source = "appsec",
                Operation = "appsec/defect-groups",
                Reason = $"уровень {severity ?? "все"}: {error.Message}"
            });

            return null;
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class ScanSummary
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";

        [JsonPropertyName("finishedAt")]
        public string? FinishedAt { get; set; }
    }

    private sealed class DefectGroupsPage
    {
        [JsonPropertyName("totalSize")]
        public int? TotalSize { get; set; }
    }
}
