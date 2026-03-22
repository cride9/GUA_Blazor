using LlmTornado.Common;

namespace GUA_Blazor.Tools.Filesystem;

public class MoveFile : AITool<MoveFileArguments>
{
    protected override string Execute(MoveFileArguments args)
    {
        string srcPath = Sandbox.Resolve(args.Source!);
        string destPath = Sandbox.Resolve(args.Destination!);

        bool srcIsFile = File.Exists(srcPath);
        bool srcIsDir = Directory.Exists(srcPath);

        if (!srcIsFile && !srcIsDir)
            return $"Source not found: {args.Source}";

        // If destination is an existing directory, move source inside it
        if (Directory.Exists(destPath))
            destPath = Path.Combine(destPath, Path.GetFileName(srcPath));

        if (File.Exists(destPath) && args.Overwrite != true)
            return $"Destination already exists: {args.Destination}. Pass overwrite=true to replace it.";

        if (args.DryRun == true)
            return $"[DRY RUN] Would move '{args.Source}' → '{args.Destination}'";

        string? destDir = Path.GetDirectoryName(destPath);
        if (destDir != null) Directory.CreateDirectory(destDir);

        if (srcIsFile)
        {
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(srcPath, destPath);
        }
        else
        {
            Directory.Move(srcPath, destPath);
        }

        return $"Moved '{args.Source}' → '{args.Destination}'";
    }

    public override ToolFunction GetToolFunction() => new(
        "move_file",
        "Moves or relocates a file or directory to a new path inside the sandbox. " +
        "Creates destination directories as needed.",
        new
        {
            type = "object",
            properties = new
            {
                source = new
                {
                    type = "string",
                    description = "Relative path of the file or directory to move."
                },
                destination = new
                {
                    type = "string",
                    description = "Relative destination path. If it's an existing directory, the source is moved inside it."
                },
                overwrite = new
                {
                    type = "boolean",
                    description = "Allow overwriting an existing file at the destination."
                },
                dry_run = new
                {
                    type = "boolean",
                    description = "Preview the move without executing it."
                }
            },
            required = new List<string> { "source", "destination" }
        });
}

public class MoveFileArguments
{
    public string? Source { get; set; }
    public string? Destination { get; set; }
    public bool? Overwrite { get; set; }
    public bool? DryRun { get; set; }
}
