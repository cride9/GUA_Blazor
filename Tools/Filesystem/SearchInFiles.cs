using LlmTornado.Common;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class SearchInFiles : AITool<SearchInFilesArguments>
{
    public SearchInFiles(string sessionId) : base(sessionId) { }

    protected override string Execute(SearchInFilesArguments args)
    {
        string rootPath = Sandbox.Resolve(args.Path ?? string.Empty, SessionId);

        if (!Directory.Exists(rootPath) && !File.Exists(rootPath))
            return $"Path not found: {args.Path}";

        string pattern = args.Pattern!;
        bool ignoreCase = args.IgnoreCase ?? true;
        bool isRegex = args.Regex ?? false;
        string glob = args.FilePattern ?? "*";
        int maxResults = args.MaxResults ?? 50;

        var comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        System.Text.RegularExpressions.Regex? regex = null;
        if (isRegex)
        {
            var flags = ignoreCase
                ? System.Text.RegularExpressions.RegexOptions.IgnoreCase
                : System.Text.RegularExpressions.RegexOptions.None;
            regex = new System.Text.RegularExpressions.Regex(pattern, flags);
        }

        var files = File.Exists(rootPath)
            ? new[] { rootPath }
            : Directory.GetFiles(rootPath, glob, SearchOption.AllDirectories);

        var sb = new StringBuilder();
        int hits = 0;
        int fileHits = 0;

        foreach (var file in files)
        {
            if (hits >= maxResults) break;

            string[] lines;
            try { lines = File.ReadAllLines(file, new UTF8Encoding(false)); }
            catch { continue; }

            var fileMatches = new List<string>();

            for (int i = 0; i < lines.Length && hits < maxResults; i++)
            {
                bool matched = isRegex
                    ? regex!.IsMatch(lines[i])
                    : lines[i].Contains(pattern, comparison);

                if (!matched) continue;

                string highlight = isRegex
                    ? regex!.Replace(lines[i].Trim(), m => $">>>{m.Value}<<<")
                    : lines[i].Trim().Replace(pattern,
                        $">>>{pattern}<<<",
                        ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

                fileMatches.Add($"  {i + 1,5}: {highlight}");
                hits++;
            }

            if (fileMatches.Count == 0) continue;

            fileHits++;
            string relFile = Path.GetRelativePath(WorkPath, file);

            sb.AppendLine($"{relFile} ({fileMatches.Count} match(es))");
            foreach (var m in fileMatches) sb.AppendLine(m);
            sb.AppendLine();
        }

        if (hits == 0)
            return $"No matches found for '{pattern}' in '{args.Path ?? "sandbox"}'.";

        sb.Insert(0, $"Found {hits} match(es) across {fileHits} file(s):\n\n");

        if (hits >= maxResults)
            sb.AppendLine($"\n(Limit of {maxResults} results reached. Narrow your search or increase max_results.)");

        return sb.ToString();
    }

    public override ToolFunction GetToolFunction() => new(
        "search_in_files",
        "Searches for a text pattern or regex across files in the sandbox. Returns matching lines with line numbers and the file they were found in.",
        new
        {
            type = "object",
            properties = new
            {
                pattern = new
                {
                    type = "string",
                    description = "The text or regex pattern to search for."
                },
                path = new
                {
                    type = "string",
                    description = "Directory or file to search in. Defaults to the entire sandbox."
                },
                file_pattern = new
                {
                    type = "string",
                    description = "Glob to filter files, e.g. '*.cs' or '*.tsx'. Defaults to all files."
                },
                ignore_case = new
                {
                    type = "boolean",
                    description = "Case-insensitive search. Defaults to true."
                },
                regex = new
                {
                    type = "boolean",
                    description = "Treat pattern as a regular expression."
                },
                max_results = new
                {
                    type = "integer",
                    description = "Maximum number of matching lines to return. Defaults to 50."
                }
            },
            required = new List<string> { "pattern" }
        });
}

public class SearchInFilesArguments
{
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("file_pattern")]
    public string? FilePattern { get; set; }
    [JsonPropertyName("ignore_case")]
    public bool? IgnoreCase { get; set; }
    [JsonPropertyName("regex")]
    public bool? Regex { get; set; }
    [JsonPropertyName("max_results")]
    public int? MaxResults { get; set; }
}
