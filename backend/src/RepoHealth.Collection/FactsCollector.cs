using RepoHealth.Contracts;

namespace RepoHealth.Collection;

/// Собирает факты по репозиториям: карточка из API плюс данные из git.
public sealed class FactsCollector : IDisposable
{
    private readonly SourceCraftApi _api;

    /// Сколько репозиториев обрабатываем одновременно.
    /// Клонирование упирается в сеть и диск, а не в лимит запросов,
    /// поэтому большого числа тут не нужно.
    public int Parallelism { get; init; } = 6;

    public FactsCollector(string token) => _api = new SourceCraftApi(token);

    /// <param name="limit">Сколько репозиториев взять; 0 — весь каталог.</param>
    /// <param name="now">Момент сбора: от него считается «дней назад».
    /// Один на весь проход, чтобы репозитории сравнивались между собой честно.</param>
    public async Task<int> CollectToFolderAsync(
        string outputFolder,
        int limit = 0,
        DateTimeOffset? now = null,
        IProgress<string>? progress = null,
        CancellationToken cancel = default)
    {
        Directory.CreateDirectory(outputFolder);

        var moment = now ?? DateTimeOffset.UtcNow;
        var saved = 0;

        var repositories = new List<ApiRepository>();
        await foreach (var repo in _api.DiscoverAsync(limit, cancel))
        {
            if (!repo.IsEmpty)
                repositories.Add(repo);
        }

        progress?.Report($"в каталоге взято {repositories.Count} непустых репозиториев");

        await Parallel.ForEachAsync(
            repositories,
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancel },
            async (repo, token) =>
            {
                var facts = await CollectAsync(repo, moment, token);

                var file = Path.Combine(outputFolder, FileNameFor(repo));
                await File.WriteAllTextAsync(file, FactsJson.Write(facts), token);

                var done = Interlocked.Increment(ref saved);
                progress?.Report($"{done}/{repositories.Count}  {repo.FullName}");
            });

        return saved;
    }

    /// Разбирает один репозиторий по его адресу вида «организация/проект».
    /// Используется личным кабинетом: пользователь входит своим токеном
    /// и получает оценку доступного ему проекта, в том числе закрытого.
    public async Task<RepoFacts?> AnalyseAsync(
        string org, string slug, DateTimeOffset? now = null, CancellationToken cancel = default)
    {
        var repo = await _api.GetRepositoryAsync(org, slug, cancel);

        if (repo is null)
            return null;

        return await CollectAsync(repo, now ?? DateTimeOffset.UtcNow, cancel);
    }

    /// Собирает факты по одному репозиторию.
    internal async Task<RepoFacts> CollectAsync(
        ApiRepository repo, DateTimeOffset now, CancellationToken cancel = default)
    {
        var facts = new RepoFacts
        {
            Catalog = BuildCatalog(repo),
            Git = new Git(),
            Notes = []
        };

        // Участников берём у платформы, а не из подписей в коммитах:
        // она связывает разные почты одного человека с его аккаунтом.
        var organization = repo.Organization?.Slug ?? "";

        facts.Catalog.Contributors =
            await _api.CountContributorsAsync(organization, repo.Slug, cancel);

        if (facts.Catalog.Contributors is null)
        {
            facts.Notes.Add(new Note
            {
                Source = "api",
                Operation = "contributors",
                Reason = "список участников недоступен"
            });
        }

        if (repo.Parent is not null)
        {
            facts.Notes.Add(new Note
            {
                Source = "catalog",
                Operation = "origin",
                Reason = "репозиторий является форком"
            });
        }

        if (repo.Links is { Count: > 0 })
        {
            facts.Notes.Add(new Note
            {
                Source = "catalog",
                Operation = "origin",
                Reason = "у репозитория есть ссылки на другие площадки"
            });
        }

        var cloneUrl = repo.CloneUrl?.Https;

        if (string.IsNullOrWhiteSpace(cloneUrl))
        {
            facts.Notes.Add(new Note
            {
                Source = "git",
                Operation = "clone",
                Reason = "в карточке нет адреса для клонирования"
            });

            return facts;
        }

        var fromGit = GitReader.Read(cloneUrl, now);

        facts.Git = fromGit.Facts;
        facts.Code = fromGit.Code;
        facts.Notes.AddRange(fromGit.Notes);

        facts.Issues = await ReadIssuesAsync(organization, repo.Slug, facts.Notes, now, cancel);

        AddOriginWarnings(facts, repo, now);

        return facts;
    }

    /// Сводит задачи в несколько чисел.
    /// null означает «прочитать не удалось», а не «задач нет».
    private async Task<IssuesFacts?> ReadIssuesAsync(
        string org, string slug, List<Note> notes, DateTimeOffset now, CancellationToken cancel)
    {
        var issues = await _api.GetIssuesAsync(org, slug, cancel);

        if (issues is null)
        {
            notes.Add(new Note
            {
                Source = "api",
                Operation = "issues",
                Reason = "задачи недоступны с текущими правами"
            });

            return null;
        }

        var facts = new IssuesFacts { Total = issues.Count };
        var daysToClose = new List<double>();

        foreach (var issue in issues)
        {
            var status = issue.Status?.StatusType ?? "";

            switch (status)
            {
                case "completed":
                    facts.Closed++;
                    break;
                case "cancelled":
                    facts.Cancelled++;
                    break;
                default:
                    facts.Open++;

                    // Открытая задача, которой давно не касались, — зависшая.
                    if (DateTimeOffset.TryParse(issue.UpdatedAt, out var touched)
                        && (now - touched).TotalDays > 90)
                    {
                        facts.StaleOpen++;
                    }

                    break;
            }

            if (status == "completed"
                && DateTimeOffset.TryParse(issue.CreatedAt, out var born)
                && DateTimeOffset.TryParse(issue.CompletedAt, out var done)
                && done > born)
            {
                daysToClose.Add((done - born).TotalDays);
            }
        }

        if (daysToClose.Count > 0)
        {
            daysToClose.Sort();
            facts.MedianDaysToClose = Math.Round(daysToClose[daysToClose.Count / 2], 1);
        }

        return facts;
    }

    private static Catalog BuildCatalog(ApiRepository repo) => new()
    {
        Name = repo.FullName,
        Url = repo.WebUrl,
        Language = repo.Language,
        Likes = repo.Rating?.Value,
        LikesRank = repo.Rating?.Percentile is { } percentile ? (int)percentile : null,
        UpdatedAt = repo.LastUpdated,
        Counters = BuildCounters(repo.Counters),
        MigratedFrom = repo.MigrationSource?.Domain ?? repo.MigrationSource?.Url,
        IsFork = repo.Parent is not null
    };

    private static Counters BuildCounters(Dictionary<string, int>? source)
    {
        var counters = new Counters();

        if (source is null)
            return counters;

        counters.Forks = source.GetValueOrDefault("forks");
        counters.PullRequests = source.GetValueOrDefault("pull_requests");
        counters.Issues = source.GetValueOrDefault("issues");
        counters.Tags = source.GetValueOrDefault("tags");
        counters.Branches = source.GetValueOrDefault("branches");

        return counters;
    }

    /// Признаки того, что проект сюда просто положили,
    /// а разрабатывали где-то ещё.
    private static void AddOriginWarnings(RepoFacts facts, ApiRepository repo, DateTimeOffset now)
    {
        var codeAge = facts.Git?.DaysSinceLastCommit;

        if (codeAge is not null
            && DateTimeOffset.TryParse(repo.LastUpdated, out var updated))
        {
            var platformAge = (int)(now - updated).TotalDays;

            if (codeAge - platformAge > 120)
            {
                facts.Notes.Add(new Note
                {
                    Source = "catalog",
                    Operation = "dates",
                    Reason = $"код старше даты обновления на {codeAge - platformAge} дн."
                });
            }
        }

        if (facts.Git is { TotalAuthors: > 10, Authors90: <= 1 })
        {
            facts.Notes.Add(new Note
            {
                Source = "git",
                Operation = "authors",
                Reason = $"{facts.Git.TotalAuthors} авторов в истории, свежих {facts.Git.Authors90}"
            });
        }
    }

    private static string FileNameFor(ApiRepository repo)
    {
        var name = repo.FullName.Replace('/', '_');

        foreach (var bad in Path.GetInvalidFileNameChars())
            name = name.Replace(bad, '_');

        return name + ".json";
    }

    public void Dispose() => _api.Dispose();
}
