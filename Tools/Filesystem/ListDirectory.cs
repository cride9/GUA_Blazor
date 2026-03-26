using LlmTornado.Common;
using System.Security;
using System.Text;

namespace GUA_Blazor.Tools.Filesystem;

public class ListDirectory : AITool<ListDirectoryArguments>
{
    public ListDirectory(string sessionId) : base(sessionId) { }

    protected override string Execute(ListDirectoryArguments args)
    {
        string fullPath = Sandbox.Resolve(args.Path ?? string.Empty, SessionId);
        string subPath = Path.GetRelativePath(WorkPath, fullPath);
        if (subPath == ".") subPath = string.Empty;

        if (!Directory.Exists(fullPath))
        {
            return $"Directory not found: {fullPath}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Contents of: {(string.IsNullOrEmpty(subPath) ? "sandbox root" : subPath)}");
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
        return new ToolFunction("list_directory", "Lists all files and subdirectories in a given folder inside the sandbox. If no path is provided, lists the root sandbox folder.", new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "Relative path within sandbox to list. Omit or leave empty to list the root sandbox folder."
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