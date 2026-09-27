using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepoHealth.Collection;

/// Читает каталог открытых репозиториев SourceCraft.
public sealed class SourceCraftApi : IDisposable
{
    private const string BaseAddress = "https://api.sourcecraft.tech";
    private const int PageSize = 100;

    /// Сколько страниц сборок смотрим — это последние
    /// несколько сотен запусков, дальше в прошлое не нужно.
    private const int CiPages = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Счётчики приходят строками, а не числами.
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;

    public SourceCraftApi(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Нужен токен доступа SourceCraft.", nameof(token));

        _http = new HttpClient { BaseAddress = new Uri(BaseAddress), Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// Последние запуски сборок. null — статусы недоступны
    /// с нашими правами, и это не то же самое, что «сборок нет».
    internal async Task<List<ApiRun>?> GetCiRunsAsync(
        string org, string slug, CancellationToken cancel = default)
    {
        var runs = new List<ApiRun>();
        var pageToken = "";

        // Берём несколько последних страниц: нам нужна свежая
        // картина, а не вся история сборок за годы.
        for (var page = 0; page < CiPages; page++)
        {
            var url = $"/repos/{org}/{slug}/cicd/runs"
                    + $"?page_size={PageSize}&page_token={Uri.EscapeDataString(pageToken)}";

            var answer = await _http.GetAsync(url, cancel);

            if (!answer.IsSuccessStatusCode)
                return runs.Count > 0 ? runs : null;

            var got = JsonSerializer.Deserialize<CiRunsResponse>(
                await answer.Content.ReadAsStringAsync(cancel), JsonOptions);

            if (got is null || got.Runs.Count == 0)
                break;

            runs.AddRange(got.Runs);

            if (string.IsNullOrEmpty(got.NextPageToken))
                break;

            pageToken = got.NextPageToken;
        }

        return runs;
    }

    /// Проходит каталог постранично и отдаёт репозитории по мере чтения.
    /// limit = 0 означает «весь каталог».
    internal async IAsyncEnumerable<ApiRepository> DiscoverAsync(
        int limit,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancel = default)
    {
        var pageToken = "";
        var returned = 0;

        while (true)
        {
            var url = $"/repos?page_size={PageSize}&page_token={Uri.EscapeDataString(pageToken)}";

            var answer = await _http.GetAsync(url, cancel);
            answer.EnsureSuccessStatusCode();

            var page = JsonSerializer.Deserialize<DiscoverResponse>(
                await answer.Content.ReadAsStringAsync(cancel), JsonOptions);

            if (page is null || page.Repositories.Count == 0)
                yield break;

            foreach (var repo in page.Repositories)
            {
                yield return repo;

                returned++;
                if (limit > 0 && returned >= limit)
                    yield break;
            }

            if (string.IsNullOrEmpty(page.NextPageToken))
                yield break;

            pageToken = page.NextPageToken;
        }
    }

    /// Карточка одного репозитория. Работает и для закрытых —
    /// если у токена есть к ним доступ.
    internal async Task<ApiRepository?> GetRepositoryAsync(
        string org, string slug, CancellationToken cancel = default)
    {
        var url = $"/repos/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(slug)}";

        var answer = await _http.GetAsync(url, cancel);

        if (!answer.IsSuccessStatusCode)
            return null;

        return JsonSerializer.Deserialize<ApiRepository>(
            await answer.Content.ReadAsStringAsync(cancel), JsonOptions);
    }

    /// Задачи репозитория. Возвращает null, если прочитать не удалось —
    /// это не то же самое, что «задач нет».
    internal async Task<List<ApiIssue>?> GetIssuesAsync(
        string org, string slug, CancellationToken cancel = default)
    {
        // Больше тысячи задач для оценки здоровья не нужно:
        // картину дают и первые страницы.
        const int maxPages = 10;

        var issues = new List<ApiIssue>();
        var pageToken = "";

        for (var page = 0; page < maxPages; page++)
        {
            var url = $"/repos/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(slug)}" +
                      $"/issues?page_size=100&page_token={Uri.EscapeDataString(pageToken)}";

            HttpResponseMessage answer;
            try
            {
                answer = await _http.GetAsync(url, cancel);
            }
            catch (HttpRequestException)
            {
                return null;
            }

            if (!answer.IsSuccessStatusCode)
                return null;

            var body = JsonSerializer.Deserialize<IssuesResponse>(
                await answer.Content.ReadAsStringAsync(cancel), JsonOptions);

            if (body is null)
                return null;

            issues.AddRange(body.Issues);

            if (string.IsNullOrEmpty(body.NextPageToken))
                break;

            pageToken = body.NextPageToken;
        }

        return issues;
    }

    /// Считает участников репозитория. Платформа сама склеивает
    /// разные почты одного человека, поэтому это число точнее,
    /// чем подсчёт подписей в коммитах.
    /// Возвращает null, если метод недоступен.
    public async Task<int?> CountContributorsAsync(
        string org, string slug, CancellationToken cancel = default)
    {
        const int maxPages = 20;

        var total = 0;
        var pageToken = "";

        for (var page = 0; page < maxPages; page++)
        {
            var url = $"/repos/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(slug)}" +
                      $"/contributors?page_size=100&page_token={Uri.EscapeDataString(pageToken)}";

            HttpResponseMessage answer;
            try
            {
                answer = await _http.GetAsync(url, cancel);
            }
            catch (HttpRequestException)
            {
                return null;
            }

            if (!answer.IsSuccessStatusCode)
                return null;

            var body = JsonSerializer.Deserialize<ContributorsResponse>(
                await answer.Content.ReadAsStringAsync(cancel), JsonOptions);

            if (body is null)
                return null;

            total += body.Contributors.Count;

            if (string.IsNullOrEmpty(body.NextPageToken))
                return total;

            pageToken = body.NextPageToken;
        }

        return total;
    }

    public void Dispose() => _http.Dispose();
}
