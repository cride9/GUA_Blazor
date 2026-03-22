using LlmTornado.Common;
using System.Security;
using System.Text;

namespace GUA_Blazor.Tools.Filesystem;

public class EditFile : AITool<EditFileArguments>
{
    protected override string Execute(EditFileArguments args)
    {
        string basePath = Path.Combine(
            Environment.CurrentDirectory,
            "ai_files_temp"
        );

        string relativePath = (args.Path ?? string.Empty).TrimStart('/', '\\');
        if (relativePath.StartsWith("..") || relativePath.Contains(".."))
            throw new SecurityException("Path traversal attempt detected!");

        string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath)))
            throw new SecurityException("Access denied: Path outside sandbox!");

        if (!File.Exists(fullPath))
            return $"File not found: {relativePath}";

        string original = File.ReadAllText(fullPath, new UTF8Encoding(false));

        // ── Validate old_str exists and is unambiguous ──────────────────────
        int matchCount = CountOccurrences(original, args.OldStr!);

        if (matchCount == 0)
            return $"Edit failed: old_str not found in '{relativePath}'.\n" +
                   $"Tip: Make sure whitespace and line endings match exactly.";

        if (matchCount > 1)
            return $"Edit failed: old_str found {matchCount} times in '{relativePath}'. " +
                   $"Add more surrounding context to make it unique.";

        // ── Apply the replacement ────────────────────────────────────────────
        string updated = original.Replace(args.OldStr!, args.NewStr ?? string.Empty);

        // ── Optional dry-run ────────────────────────────────────────────────
        if (args.DryRun == true)
        {
            var diff = BuildDiff(args.OldStr!, args.NewStr ?? string.Empty);
            return $"[DRY RUN] Would apply the following change to '{relativePath}':\n\n{diff}";
        }

        // ── Backup if requested ──────────────────────────────────────────────
        if (args.Backup == true)
        {
            string backupPath = fullPath + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(fullPath, backupPath);
        }

        File.WriteAllText(fullPath, updated, new UTF8Encoding(false));

        int linesChanged = args.OldStr!.Split('\n').Length;
        return $"Edit applied to '{relativePath}'. " +
               $"Replaced {linesChanged} line(s). " +
               (args.Backup == true ? "Backup saved." : string.Empty);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static int CountOccurrences(string source, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return 0;
        int count = 0, index = 0;
        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// Produces a simple - / + diff for the AI and user to read.
    /// </summary>
    private static string BuildDiff(string oldStr, string newStr)
    {
        var sb = new StringBuilder();
        foreach (var line in oldStr.Split('\n'))
            sb.AppendLine($"- {line}");
        sb.AppendLine();
        foreach (var line in newStr.Split('\n'))
            sb.AppendLine($"+ {line}");
        return sb.ToString();
    }

    public override ToolFunction GetToolFunction() => new(
        "edit_file",
        """
        Edits a file by replacing an exact block of text (old_str) with new text (new_str).
        The old_str must match the file content exactly, including whitespace and indentation.
        To delete a block, pass an empty string for new_str.
        If old_str appears more than once, include extra surrounding lines to make it unique.
        Use dry_run=true to preview the change without writing it.
        """,
        new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path to the file inside ai_files_temp, including filename. E.g. 'my-app/src/App.tsx'"
                },
                old_str = new
                {
                    type = "string",
                    description = "The exact text to find and replace. Must be unique within the file. Include surrounding lines if needed for uniqueness."
                },
                new_str = new
                {
                    type = "string",
                    description = "The text to replace old_str with. Omit or pass empty string to delete the block."
                },
                dry_run = new
                {
                    type = "boolean",
                    description = "If true, returns a diff preview without modifying the file."
                },
                backup = new
                {
                    type = "boolean",
                    description = "If true, saves a .bak copy of the file before editing."
                }
            },
            required = new List<string> { "path", "old_str" }
        });
}

public class EditFileArguments
{
    public string? Path { get; set; }
    public string? OldStr { get; set; }
    public string? NewStr { get; set; }
    public bool? DryRun { get; set; }
    public bool? Backup { get; set; }
}