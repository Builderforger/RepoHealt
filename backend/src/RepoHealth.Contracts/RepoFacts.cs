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

    /// Состояние кода. null — дерево файлов прочитать не удалось.
    [JsonPropertyName("code")]
    public CodeFacts? Code { get; set; }

    /// Задачи. null — прочитать их не удалось,
    /// и это не то же самое, что «задач нет».
    [JsonPropertyName("issues")]
    public IssuesFacts? Issues { get; set; }
    /// Проверка безопасности. null — результаты недоступны
    /// с теми правами, с которыми мы смотрим репозиторий.
    [JsonPropertyName("security")]
    public SecurityFacts? Security { get; set; }

    [JsonPropertyName("notes")]
    public List<Note> Notes { get; set; } = [];
}