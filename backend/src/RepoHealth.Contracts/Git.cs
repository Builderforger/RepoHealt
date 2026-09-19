using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class Git
{
    [JsonPropertyName("readme")]
    public string Readme { get; set; } = "";

    [JsonPropertyName("commits_90")]
    public int Commits90 { get; set; }

    [JsonPropertyName("authors_total")]
    public int TotalAuthors { get; set; }

    [JsonPropertyName("authors_90")]
    public int Authors90 { get; set; }

    [JsonPropertyName("days_since_last_commit")]
    public int? DaysSinceLastCommit { get; set; }

    [JsonPropertyName("main_author_share")]
    public double MainAuthorShare { get; set; }
}
