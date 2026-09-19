using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class Language
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}
