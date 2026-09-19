using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class Counters
{
    [JsonPropertyName("forks")]
    public int Forks { get; set; }

    [JsonPropertyName("pull_requests")]
    public int PullRequests { get; set; }

    [JsonPropertyName("issues")]
    public int Issues { get; set; }

    [JsonPropertyName("tags")]
    public int Tags { get; set; }

    [JsonPropertyName("branches")]
    public int Branches { get; set; }
}
