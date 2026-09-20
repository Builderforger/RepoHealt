using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

public class PartResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("points")]
    public double Points { get; set; }

    [JsonPropertyName("max_points")]
    public double MaxPoints { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";
}
