using System.Diagnostics;
using System.Text;
using RepoHealth.Contracts;

namespace RepoHealth.Collection;

/// Скачивает репозиторий и достаёт из него факты.
/// Рабочая копия удаляется сразу после разбора —
/// чужой код у нас не задерживается.
internal static class GitReader
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(3);

    public sealed record Result(Git Facts, CodeFacts? Code, List<Note> Notes);

    private static readonly string[] TestMarkers =
        ["test", "tests", "spec", "specs", "__tests__", "testing", "it"];

    /// Файлы, которые действительно пишут руками.
    /// Всё остальное — словари, разметка, картинки, файлы
    /// зависимостей — бывает огромным по делу.
    private static readonly string[] SourceExtensions =
        [
            ".cs", ".py", ".js", ".jsx", ".ts", ".tsx", ".java", ".kt", ".kts",
            ".go", ".rs", ".rb", ".php", ".c", ".h", ".cpp", ".hpp", ".cc", ".hh",
            ".m", ".mm", ".swift", ".scala", ".sh", ".ps1", ".sql", ".vue",
            ".svelte", ".dart", ".lua", ".pl", ".r", ".jl", ".ex", ".exs",
            ".erl", ".clj", ".fs", ".vb"
        ];

    /// Куски, по которым видно, что файл не писали руками.
    private static readonly string[] NotHandWritten =
        [
            ".min.", "/vendor/", "/node_modules/", "/third_party/", "/generated/",
            ".g.cs", ".designer.cs", "_pb2.py", ".pb.go", ".generated."
        ];

    private static readonly string[] QualityConfigs =
        [
            ".editorconfig", ".eslintrc", ".eslintrc.json", ".eslintrc.js", "eslint.config.js",
            ".prettierrc", ".prettierrc.json", "biome.json", ".stylelintrc",
            "ruff.toml", ".ruff.toml", ".flake8", "setup.cfg", "mypy.ini", ".pylintrc",
            "detekt-config.yml", "detekt.yml", "diktat-analysis.yml", "ktlint.yml",
            ".golangci.yml", ".golangci.yaml", ".rubocop.yml", "clippy.toml", "rustfmt.toml",
            "checkstyle.xml", ".clang-format", ".editorconfig.json", "sonar-project.properties"
        ];

    /// <param name="now">Момент сбора. Передаётся снаружи, чтобы
    /// «дней с последнего коммита» считалось от одной точки для всех
    /// репозиториев одного прохода.</param>
    public static Result Read(string cloneUrl, DateTimeOffset now)
    {
        var facts = new Git();
        var notes = new List<Note>();
        CodeFacts? code = null;

        var folder = Path.Combine(Path.GetTempPath(), "repohealth-" + Guid.NewGuid().ToString("N"));

        try
        {
            if (!TryClone(cloneUrl, folder, notes))
                return new Result(facts, null, notes);

            ReadReadme(folder, facts, notes);
            ReadHistory(folder, facts, notes, now);
            code = ReadCode(folder, notes);
        }
        finally
        {
            DeleteFolder(folder);
        }

        return new Result(facts, code, notes);
    }

    /// Сначала пробуем забрать историю за год. Если сервер отказал
    /// или проект не трогали дольше — берём хотя бы последний срез.
    private static bool TryClone(string url, string folder, List<Note> notes)
    {
        var shallow = RunGit(null, "clone", "--no-checkout",
            "--shallow-since=1 year ago", "--quiet", url, folder);

        if (shallow.Ok && Directory.Exists(Path.Combine(folder, ".git")))
            return true;

        var firstFailure = Failure("clone_shallow_since", shallow);

        DeleteFolder(folder);

        var lastState = RunGit(null, "clone", "--no-checkout", "--depth", "1", "--quiet", url, folder);

        if (lastState.Ok && Directory.Exists(Path.Combine(folder, ".git")))
        {
            // Истории за год нет — взяли только последний срез.
            // Значит число авторов и доля главного автора посчитаны
            // по одному коммиту и о проекте ничего не говорят.
            notes.Add(new Note
            {
                Source = "git",
                Operation = "history",
                Reason = "за последний год коммитов не было, взят только последний срез: " +
                         "числу авторов в истории доверять нельзя"
            });

            return true;
        }

        notes.Add(firstFailure);
        notes.Add(Failure("clone_depth_1", lastState));
        return false;
    }

    private static void ReadReadme(string folder, Git facts, List<Note> notes)
    {
        var listing = RunGit(folder, "ls-tree", "--name-only", "HEAD");

        if (!listing.Ok)
        {
            notes.Add(Failure("ls_tree", listing));
            return;
        }

        facts.RootFiles = listing.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(name => name.Length > 0)
            .ToList();

        var readmeName = facts.RootFiles
            .FirstOrDefault(name => name.StartsWith("readme", StringComparison.OrdinalIgnoreCase));

        if (readmeName is null)
        {
            notes.Add(new Note { Source = "git", Operation = "readme", Reason = "README не найден" });
            return;
        }

        var readme = RunGit(folder, "show", "HEAD:" + readmeName);

        if (readme.Ok)
            facts.Readme = readme.Output;
        else
            notes.Add(Failure("readme", readme));
    }

    private static void ReadHistory(string folder, Git facts, List<Note> notes, DateTimeOffset now)
    {
        var log = RunGit(folder, "log", "--pretty=%cI|%an");

        if (!log.Ok)
        {
            notes.Add(Failure("log", log));
            return;
        }

        var allAuthors = new Dictionary<string, int>();
        var recentAuthors = new HashSet<string>();

        foreach (var line in log.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('|');
            if (separator <= 0)
                continue;

            if (!DateTimeOffset.TryParse(line[..separator], out var when))
                continue;

            var author = line[(separator + 1)..].Trim();
            var days = (int)(now - when).TotalDays;

            allAuthors[author] = allAuthors.GetValueOrDefault(author) + 1;

            if (facts.DaysSinceLastCommit is null || days < facts.DaysSinceLastCommit)
                facts.DaysSinceLastCommit = days;

            if (days <= 90)
            {
                facts.Commits90++;
                recentAuthors.Add(author);
            }
        }

        facts.TotalAuthors = allAuthors.Count;
        facts.Authors90 = recentAuthors.Count;

        if (allAuthors.Count > 0)
        {
            var total = allAuthors.Values.Sum();
            facts.MainAuthorShare = Math.Round((double)allAuthors.Values.Max() / total, 3);
        }
    }

    /// Состояние кода. Читаем дерево файлов и ищем в нём недоделки —
    /// всё прямо в репозитории, без выгрузки кода на диск.
    private static CodeFacts? ReadCode(string folder, List<Note> notes)
    {
        // Длинный формат ls-tree даёт размеры файлов.
        var listing = RunGit(folder, "ls-tree", "-r", "-l", "HEAD");

        if (!listing.Ok)
        {
            notes.Add(Failure("ls_tree_full", listing));
            return null;
        }

        var code = new CodeFacts();

        foreach (var line in listing.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // строка вида: <права> blob <хеш> <размер>\t<путь>
            var tab = line.IndexOf('\t');
            if (tab < 0)
                continue;

            var path = line[(tab + 1)..].Trim();
            code.FilesTotal++;

            var lowered = path.ToLowerInvariant();

            var columns = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length >= 4
                && long.TryParse(columns[3], out var size)
                && size > code.LargestSourceFileBytes
                && IsHandWrittenCode(lowered))
            {
                code.LargestSourceFileBytes = size;
                code.LargestSourceFile = path;
            }

            if (!code.HasTests && LooksLikeTest(lowered))
                code.HasTests = true;

            code.QualityConfig ??= QualityConfigs.FirstOrDefault(
                name => lowered == name || lowered.EndsWith("/" + name));
        }

        if (code.FilesTotal == 0)
            return null;

        // -I пропускает двоичные файлы, -c даёт число совпадений на файл.
        var todos = RunGit(folder, "grep", "-I", "-c", "-E", "(TODO|FIXME|HACK)[:( ]", "HEAD");

        // Код возврата 1 у grep означает «ничего не нашлось», это не ошибка.
        if (todos.Ok || todos.ExitCode == 1)
        {
            foreach (var line in todos.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = line.LastIndexOf(':');
                if (colon > 0 && int.TryParse(line[(colon + 1)..].Trim(), out var found))
                    code.TodoCount += found;
            }
        }
        else
        {
            notes.Add(Failure("grep_todo", todos));
        }

        return code;
    }

    private static bool IsHandWrittenCode(string path) =>
        SourceExtensions.Any(path.EndsWith) &&
        !NotHandWritten.Any(path.Contains);

    private static bool LooksLikeTest(string path) =>
        path.Split('/', '\\').Any(part =>
            TestMarkers.Contains(part) ||
            part.StartsWith("test_") ||
            part.EndsWith("_test") ||
            part.Contains(".test.") ||
            part.Contains(".spec."));

    // запуск git

    private sealed record GitOutput(bool Ok, int ExitCode, string Output, string Error);

    private static GitOutput RunGit(string? workingFolder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (workingFolder is not null)
        {
            start.ArgumentList.Add("-C");
            start.ArgumentList.Add(workingFolder);
        }

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start)!;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(CommandTimeout))
            {
                process.Kill(entireProcessTree: true);
                return new GitOutput(false, -1, output, "превышено время ожидания");
            }

            return new GitOutput(process.ExitCode == 0, process.ExitCode, output, error);
        }
        catch (Exception error)
        {
            return new GitOutput(false, -1, "", error.Message);
        }
    }

    private static Note Failure(string operation, GitOutput result) => new()
    {
        Source = "git",
        Operation = operation,
        Reason = string.IsNullOrWhiteSpace(result.Error) ? result.Output.Trim() : result.Error.Trim()
    };

    private static void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
            return;

        try
        {
            // git помечает часть файлов только для чтения —
            // без снятия признака удаление падает.
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(folder, recursive: true);
        }
        catch
        {
            // Не смогли убрать временную папку — операционная система
            // разберётся сама. Ронять из-за этого сбор не стоит.
        }
    }
}
