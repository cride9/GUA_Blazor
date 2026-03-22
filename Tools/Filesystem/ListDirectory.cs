using LlmTornado.Common;
using System.Security;
using System.Text;

namespace GUA_Blazor.Tools.Filesystem;

public class ListDirectory : AITool<ListDirectoryArguments>
{
    protected override string Execute(ListDirectoryArguments args)
    {
        string basePath = Path.Combine(
            Environment.CurrentDirectory,
            "ai_files_temp"
        );

        string subPath = (args.Path ?? string.Empty).TrimStart('/', '\\');

        if (subPath.StartsWith("..") || subPath.Contains(".."))
        {
            throw new SecurityException("Path traversal attempt detected!");
        }

        string fullPath = Path.GetFullPath(Path.Combine(basePath, subPath));

        if (!fullPath.StartsWith(Path.GetFullPath(basePath)))
        {
            throw new SecurityException("Access denied: Path outside sandbox!");
        }

        if (!Directory.Exists(fullPath))
        {
            return $"Directory not found: {fullPath}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Contents of: {(string.IsNullOrEmpty(subPath) ? "ai_files_temp" : subPath)}");
        sb.AppendLine();

        BuildTree(fullPath, fullPath, sb, string.Empty);

        return sb.ToString();
    }

    private static void BuildTree(string rootPath, string currentPath, StringBuilder sb, string indent)
    {
        var directories = Directory.GetDirectories(currentPath).OrderBy(d => d).ToArray();
        var files = Directory.GetFiles(currentPath).OrderBy(f => f).ToArray();

        foreach (var dir in directories)
        {
            sb.AppendLine($"{indent}📁 {Path.GetFileName(dir)}/");
            BuildTree(rootPath, dir, sb, indent + "    ");
        }

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            sb.AppendLine($"{indent}📄 {info.Name} ({FormatSize(info.Length)})");
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };

    public override ToolFunction GetToolFunction()
    {
        return new ToolFunction("list_directory", "Lists all files and subdirectories in a given folder inside the sandbox. If no path is provided, lists the root ai_files_temp folder.", new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path within ai_files_temp to list. Omit or leave empty to list the root sandbox folder."
                }
            },
            required = new List<string>()
        });
    }
}

public class ListDirectoryArguments
{
    public string? Path { get; set; }
}