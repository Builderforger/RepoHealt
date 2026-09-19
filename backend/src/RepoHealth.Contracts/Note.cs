using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class Note
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
    [JsonPropertyName("code")]
    public int? Code { get; set; } 
}
