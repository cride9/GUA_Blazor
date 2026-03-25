using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class RenameFile : AITool<RenameFileArguments>
{
    protected override string Execute(RenameFileArguments args)
    {
        string fullPath = Sandbox.Resolve(args.Path!);

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            return $"Not found: {args.Path}";

        string? parentDir = Path.GetDirectoryName(fullPath);
        string newName = args.NewName!.TrimStart('/', '\\');

        if (newName.Contains('/') || newName.Contains('\\'))
            return "NewName must be a name only, not a path. Use move_file to relocate.";

        string destPath = Path.Combine(parentDir!, newName);
        destPath = Sandbox.Resolve(
            Path.GetRelativePath(
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "ai_files_temp")),
                destPath));

        if ((File.Exists(destPath) || Directory.Exists(destPath)) && args.Overwrite != true)
            return $"'{args.NewName}' already exists in the same directory. Pass overwrite=true to replace it.";

        if (args.DryRun == true)
            return $"[DRY RUN] Would rename '{Path.GetFileName(fullPath)}' → '{args.NewName}'";

        if (File.Exists(fullPath))
        {
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(fullPath, destPath);
        }
        else
        {
            Directory.Move(fullPath, destPath);
        }

        return $"Renamed '{args.Path}' → '{Path.GetDirectoryName(args.Path)}/{args.NewName}'";
    }

    public override ToolFunction GetToolFunction() => new(
        "rename_file",
        "Renames a file or directory in place. " +
        "To move to a different location use move_file instead.",
        new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path to the file or directory to rename."
                },
                new_name = new
                {
                    type = "string",
                    description = "The new name (not a path, just the filename or folder name)."
                },
                overwrite = new
                {
                    type = "boolean",
                    description = "Allow overwriting if the new name already exists."
                },
                dry_run = new
                {
                    type = "boolean",
                    description = "Preview the rename without executing it."
                }
            },
            required = new List<string> { "path", "new_name" }
        });
}

public class RenameFileArguments
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("new_name")]
    public string? NewName { get; set; }
    [JsonPropertyName("overwrite")]
    public bool? Overwrite { get; set; }
    [JsonPropertyName("dry_run")]
    public bool? DryRun { get; set; }
}
