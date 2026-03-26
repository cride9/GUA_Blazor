using LlmTornado.Common;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class ReadFile : AITool<ReadFileArguments>
{
    public ReadFile(string sessionId) : base(sessionId) { }

    protected override string Execute(ReadFileArguments args)
    {
        string fullPath = Sandbox.Resolve(args.Path!, SessionId);

        if (!File.Exists(fullPath))
            return $"File not found: {args.Path}";

        var info = new FileInfo(fullPath);
        if (info.Length > 1_000_000)
            return $"File too large to read at once ({info.Length / 1024} KB). " +
                   $"Use from_line and to_line to read in chunks.";

        string[] lines = File.ReadAllLines(fullPath, new UTF8Encoding(false));
        int total = lines.Length;

        int from = Math.Max(0, (args.FromLine ?? 1) - 1);
        int to = Math.Min(total - 1, (args.ToLine ?? total) - 1);

        var sb = new StringBuilder();
        sb.AppendLine($"// {args.Path}  (lines {from + 1}–{to + 1} of {total})");
        sb.AppendLine();

        for (int i = from; i <= to; i++)
            sb.AppendLine($"{i + 1,5}  {lines[i]}");

        return sb.ToString();
    }

    public override ToolFunction GetToolFunction() => new(
        "read_file",
        "Reads a file inside the sandbox and returns its content with line numbers. Use from_line and to_line to read a specific range of lines.",
        new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path to the file, e.g. 'my-app/src/App.tsx'"
                },
                from_line = new
                {
                    type = "integer",
                    description = "First line to return (1-based). Defaults to 1."
                },
                to_line = new
                {
                    type = "integer",
                    description = "Last line to return (1-based, inclusive). Defaults to end of file."
                }
            },
            required = new List<string> { "path" }
        });
}

public class ReadFileArguments
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("from_line")]
    public int? FromLine { get; set; }
    [JsonPropertyName("to_line")]
    public int? ToLine { get; set; }
}
