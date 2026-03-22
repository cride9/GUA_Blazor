using LlmTornado.Common;

namespace GUA_Blazor.Tools.Filesystem;

public class DeleteFile : AITool<DeleteFileArguments>
{
    protected override string Execute(DeleteFileArguments args)
    {
        string fullPath = Sandbox.Resolve(args.Path!);

        if (File.Exists(fullPath))
        {
            if (args.DryRun == true)
                return $"[DRY RUN] Would delete file: {args.Path}";

            File.Delete(fullPath);
            return $"Deleted file: {args.Path}";
        }

        if (Directory.Exists(fullPath))
        {
            if (args.DryRun == true)
                return $"[DRY RUN] Would delete directory and all contents: {args.Path}";

            if (args.Recursive != true)
                return $"'{args.Path}' is a directory. Pass recursive=true to delete it and all its contents.";

            Directory.Delete(fullPath, recursive: true);
            return $"Deleted directory: {args.Path}";
        }

        return $"Nothing found at: {args.Path}";
    }

    public override ToolFunction GetToolFunction() => new(
        "delete_file",
        "Deletes a file or directory inside the sandbox. " +
        "Directories require recursive=true. Use dry_run to preview.",
        new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path to the file or directory to delete."
                },
                recursive = new
                {
                    type = "boolean",
                    description = "Required to delete a non-empty directory."
                },
                dry_run = new
                {
                    type = "boolean",
                    description = "Preview what would be deleted without actually deleting."
                }
            },
            required = new List<string> { "path" }
        });
}

public class DeleteFileArguments
{
    public string? Path { get; set; }
    public bool? Recursive { get; set; }
    public bool? DryRun { get; set; }
}
