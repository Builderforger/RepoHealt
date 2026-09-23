using System.Text.Json.Serialization;

namespace RepoHealth.Contracts;

/// Что известно о состоянии кода.
/// Считается по дереву файлов и поиску в нём, без выгрузки кода на диск.
public class CodeFacts
{
    [JsonPropertyName("files_total")]
    public int FilesTotal { get; set; }

    /// Сколько строк помечено как недоделка: TODO, FIXME, HACK.
    [JsonPropertyName("todo_count")]
    public int TodoCount { get; set; }

    /// Нашлись ли файлы тестов.
    [JsonPropertyName("has_tests")]
    public bool HasTests { get; set; }

    /// Имя найденной настройки проверки кода: линтер, форматтер,
    /// .editorconfig. Пусто, если ничего не нашлось.
    [JsonPropertyName("quality_config")]
    public string? QualityConfig { get; set; }

    /// Размер самого большого файла в байтах.
    /// Огромные файлы — признак накопленного долга.
    [JsonPropertyName("largest_file_bytes")]
    public long LargestFileBytes { get; set; }
}
