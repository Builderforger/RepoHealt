using System.Text.Json.Serialization;
using RepoHealth.Contracts;

namespace RepoHealth.Collection;

/// Ответ метода «все публичные репозитории».
internal sealed class DiscoverResponse
{
    [JsonPropertyName("repositories")]
    public List<ApiRepository> Repositories { get; set; } = [];

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

/// Карточка репозитория так, как её отдаёт API SourceCraft.
/// Это чужой формат — наш собственный лежит в Contracts.
internal sealed class ApiRepository
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("organization")]
    public ApiOrganization? Organization { get; set; }

    [JsonPropertyName("is_empty")]
    public bool IsEmpty { get; set; }

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = "";

    [JsonPropertyName("clone_url")]
    public ApiCloneUrl? CloneUrl { get; set; }

    [JsonPropertyName("links")]
    public List<ApiLink>? Links { get; set; }

    /// Счётчики приходят строками («45»), поэтому при чтении
    /// включено преобразование строк в числа.
    [JsonPropertyName("counters")]
    public Dictionary<string, int>? Counters { get; set; }

    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; set; }

    [JsonPropertyName("language")]
    public Language? Language { get; set; }

    [JsonPropertyName("rating")]
    public ApiRating? Rating { get; set; }

    [JsonPropertyName("migration_source")]
    public ApiMigrationSource? MigrationSource { get; set; }

    [JsonPropertyName("parent")]
    public ApiRepositoryRef? Parent { get; set; }

    public string FullName => $"{Organization?.Slug ?? ""}/{Slug}";
}

/// Ответ метода «участники репозитория».
/// Сами профили нам не нужны — нужно только их количество.
internal sealed class ContributorsResponse
{
    [JsonPropertyName("contributors")]
    public List<object> Contributors { get; set; } = [];

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

internal sealed class IssuesResponse
{
    [JsonPropertyName("issues")]
    public List<ApiIssue> Issues { get; set; } = [];

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

internal sealed class ApiIssue
{
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; set; }

    [JsonPropertyName("status")]
    public ApiIssueStatus? Status { get; set; }
}

internal sealed class ApiIssueStatus
{
    /// initial, in_progress, paused, completed, cancelled
    [JsonPropertyName("status_type")]
    public string? StatusType { get; set; }
}

internal sealed class ApiOrganization
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";
}

internal sealed class ApiCloneUrl
{
    [JsonPropertyName("https")]
    public string? Https { get; set; }

    [JsonPropertyName("ssh")]
    public string? Ssh { get; set; }
}

internal sealed class ApiLink
{
    [JsonPropertyName("link")]
    public string Link { get; set; } = "";
}

internal sealed class ApiRating
{
    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("percentile")]
    public double? Percentile { get; set; }
}

internal sealed class ApiMigrationSource
{
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class ApiRepositoryRef
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";
}

/// Ответ метода «запуски сборок».
internal sealed class CiRunsResponse
{
    [JsonPropertyName("runs")]
    public List<ApiRun> Runs { get; set; } = [];

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

internal sealed class ApiRun
{
    /// created, prepared, processing, success, failed,
    /// canceled, timeout, skipped, awaiting_approval, rejected.
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// push, pr_update, manual, restart, schedule, repository_event.
    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("dates")]
    public ApiRunDates? Dates { get; set; }
}

internal sealed class ApiRunDates
{
    [JsonPropertyName("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }
}
