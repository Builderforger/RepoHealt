using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class Catalog
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    [JsonPropertyName("language")]
    public Language? Language { get; set; }
    [JsonPropertyName("likes")]
    public double? Likes { get; set; }

    [JsonPropertyName("place_likes")]
    public int? LikesRank { get; set; }

    [JsonPropertyName("date_of_update")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("counters")]
    public Counters? Counters { get; set; } = new();

    [JsonPropertyName("migrated_from")]
    public string? MigratedFrom { get; set; }

    [JsonPropertyName("fork")]
    public bool IsFork { get; set; }
}
