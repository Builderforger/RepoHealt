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

    /// Размер самого большого файла **с кодом**, в байтах.
    /// Именно с кодом: словари, картинки, файлы зависимостей
    /// и сжатые сборки бывают огромными, и это нормально.
    [JsonPropertyName("largest_source_file_bytes")]
    public long LargestSourceFileBytes { get; set; }

    /// Путь к этому файлу — чтобы человек мог пойти и посмотреть.
    [JsonPropertyName("largest_source_file")]
    public string? LargestSourceFile { get; set; }
}
