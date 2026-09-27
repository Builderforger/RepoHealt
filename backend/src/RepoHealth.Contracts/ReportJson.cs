using System.Text.Encodings.Web;
using System.Text.Json;

namespace RepoHealth.Contracts;

/// Как записывать отчёты. Настроек две, и путать их нельзя.
public static class ReportJson
{
    /// Для файла отчётов: один отчёт — одна строка.
    /// Сайт читает такой файл построчно, поэтому переносы
    /// внутри отчёта его ломают.
    public static readonly JsonSerializerOptions Line = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// Для показа человеку в терминале: с отступами.
    public static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
