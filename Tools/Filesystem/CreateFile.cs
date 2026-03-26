using GUA_Blazor.Helper;
using LlmTornado.Common;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class CreateFile : AITool<CreateFileArguments>
{
    public CreateFile(string sessionId) : base(sessionId) { }

    protected override string Execute(CreateFileArguments args)
    {
        string filePath;

        if (Path.IsPathRooted(args.Path))
        {
            var fullPath = Path.GetFullPath(args.Path!);
            if (!SessionSandbox.IsPathAllowed(fullPath, SessionId))
                throw new SecurityException("Access denied: Path outside session sandbox!");
            filePath = fullPath;
        }
        else
        {
            var relativePath = args.Path!.TrimStart('/', '\\');
            if (relativePath.Contains(".."))
                throw new SecurityException("Path traversal attempt detected!");

            filePath = Path.GetFullPath(Path.Combine(WorkPath, relativePath, args.Filename!));

            if (!SessionSandbox.IsPathAllowed(filePath, SessionId))
                throw new SecurityException("Access denied: Path outside session sandbox!");
        }

        if (File.Exists(filePath))
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var dir = Path.GetDirectoryName(filePath)!;
            filePath = Path.Combine(dir, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, args.Content, new UTF8Encoding(false));
        return $"File created: {filePath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "create_file", "Creates a new text based file.", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string" },
                filename = new { type = "string" },
                content = new { type = "string" }
            },
            required = new List<string> { "path", "filename", "content" }
        });
}

public class CreateFileArguments
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}