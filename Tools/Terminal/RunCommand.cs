using LlmTornado.Common;
using System.Diagnostics;
using System.Security;

namespace GUA_Blazor.Tools.Terminal;

public class RunCommand : AITool<RunCommandArguments>
{
    private readonly TerminalSessionStore _store;

    public RunCommand(TerminalSessionStore store) { _store = store; }

    protected override string Execute(RunCommandArguments args)
    {
        var cmd = args.Command ?? throw new ArgumentNullException("command");
        var blocked = new[] { "rm -rf /", "mkfs", ":(){:|:&};:" };
        if (blocked.Any(b => cmd.Contains(b, StringComparison.OrdinalIgnoreCase)))
            throw new SecurityException("Blocked command.");

        // ── Auto-confirm interactive package managers ────────────────
        cmd = AutoConfirm(cmd);

        var session = _store.GetOrCreate(args.SessionId ?? "default");

        string cwd = session.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(args.WorkingDirectory))
            cwd = Path.GetFullPath(Path.Combine(cwd, args.WorkingDirectory));

        bool isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/bash",
            Arguments = isWindows ? $"/C \"{cmd}\"" : $"-c \"{cmd.Replace("\"", "\\\"")}\"",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,   // ← allows sending 'y' if needed
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Pass YES through environment so tools like npm/npx respect it
        psi.Environment["CI"] = "true";   // disables interactive prompts in many CLIs
        psi.Environment["npm_config_yes"] = "true";

        session.AppendLog($"\n$ {cmd}");
        session.MarkStarted();

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            session.AppendLog(e.Data);

            // Auto-answer any remaining y/n prompts just in case
            if (e.Data.TrimEnd().EndsWith("(y)") || e.Data.Contains("Ok to proceed"))
            {
                try { process.StandardInput.WriteLine("y"); } catch { }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) session.AppendLog("[err] " + e.Data);
        };

        process.Exited += (_, _) =>
        {
            session.AppendLog($"[exit {process.ExitCode}]");
            session.MarkFinished();

            var cdMatch = System.Text.RegularExpressions.Regex
                .Match(cmd.Trim(), @"^cd\s+(.+)$");
            if (cdMatch.Success)
            {
                string newDir = Path.GetFullPath(
                    Path.Combine(cwd, cdMatch.Groups[1].Value.Trim()));
                if (Directory.Exists(newDir))
                    session.WorkingDirectory = newDir;
            }

            process.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Immediately send 'y' to stdin in case prompt appears before stdout event fires
        Task.Delay(500).ContinueWith(_ =>
        {
            try { process.StandardInput.WriteLine("y"); } catch { }
        });

        Task.Delay(10000);

        return $"Command started in session '{session.Id}'. CWD: {cwd}. " +
               $"Use read_terminal_output to check results.";
    }

    private static string AutoConfirm(string cmd)
    {
        // npm create / npm init → add --yes
        if (System.Text.RegularExpressions.Regex.IsMatch(cmd, @"npm (create|init)\b")
            && !cmd.Contains("--yes") && !cmd.Contains("-y"))
        {
            // Insert --yes after the package name section
            cmd = cmd + " --yes";
        }

        // npx without -y
        if (cmd.TrimStart().StartsWith("npx ") && !cmd.Contains("--yes") && !cmd.Contains("-y"))
            cmd = cmd.Replace("npx ", "npx --yes ");

        return cmd;
    }

    public override ToolFunction GetToolFunction() => new(
        "run_command",
        "Runs a shell command asynchronously in a named session. Returns immediately. Use read_terminal_output to get results.",
        new
        {
            type = "object",
            properties = new
            {
                command = new
                {
                    type = "string",
                    description = "The shell command to run, e.g. 'npm create vite@latest my-app -- --template react'"
                },
                session_id = new
                {
                    type = "string",
                    description = "Named session to run in. Defaults to 'default'. Use separate sessions for parallel work."
                },
                working_directory = new
                {
                    type = "string",
                    description = "Optional path to run the command in, relative to the session's current directory."
                }
            },
            required = new List<string> { "command" }
        });
}

public class RunCommandArguments
{
    public string? Command { get; set; }
    public string? SessionId { get; set; }
    public string? WorkingDirectory { get; set; }
}
