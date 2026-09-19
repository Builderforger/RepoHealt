using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class RepoFacts
{
    [JsonPropertyName("catalog")]
    public Catalog? Catalog { get; set; } = new();

    [JsonPropertyName("git")]
    public Git? Git { get; set; } = new();
    [JsonPropertyName("notes")]
    public List<Note> Notes { get; set; } = [];
}