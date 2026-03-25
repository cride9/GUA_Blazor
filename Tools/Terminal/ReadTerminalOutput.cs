using LlmTornado.Common;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Terminal;

public class ReadTerminalOutput : AITool<ReadTerminalOutputArguments>
{
    private readonly TerminalSessionStore _store;
    public ReadTerminalOutput(TerminalSessionStore store) { _store = store; }

    protected override string Execute(ReadTerminalOutputArguments args)
    {
        var session = _store.Get(args.SessionId ?? "default");
        if (session is null)
            return $"Session '{args.SessionId}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Session : {session.Id}");
        sb.AppendLine($"CWD     : {session.WorkingDirectory}");

        // ← AI reads this to know whether to keep polling or move on
        sb.AppendLine($"Status  : {(session.IsRunning ? "⏳ RUNNING" : "✅ FINISHED")}");

        if (session.LastCommandStarted.HasValue)
        {
            var elapsed = (session.LastCommandFinished ?? DateTime.UtcNow) - session.LastCommandStarted.Value;
            sb.AppendLine($"Elapsed : {elapsed.TotalSeconds:F1}s");
        }

        sb.AppendLine(new string('─', 60));
        sb.AppendLine(session.ReadLog(args.Lines ?? 80));

        if (args.Clear == true) session.ClearLog();

        return sb.ToString();
    }

    public override ToolFunction GetToolFunction() => new(
        "read_terminal_output",
        "Returns the last N lines of output from a terminal session.",
        new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "Which session to read." },
                lines = new { type = "integer", description = "How many trailing lines to return (default 80)." },
                clear = new { type = "boolean", description = "Clear the log after reading." }
            },
            required = new List<string>()
        });
}

public class ReadTerminalOutputArguments
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
    [JsonPropertyName("lines")]
    public int? Lines { get; set; }
    [JsonPropertyName("clear")]
    public bool? Clear { get; set; }
}
