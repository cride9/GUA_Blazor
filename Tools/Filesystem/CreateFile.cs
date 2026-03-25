using LlmTornado.Common;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class CreateFile : AITool<CreateFileArguments>
{
    private static readonly string[] AllowedBases =
    [
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "ai_files_temp")),
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "unsafe_uploads")),
    ];

    protected override string Execute(CreateFileArguments args)
    {
        string filePath;

        // if the model passes an absolute path, validate it directly
        if (Path.IsPathRooted(args.Path))
        {
            var fullPath = Path.GetFullPath(args.Path!);
            if (!AllowedBases.Any(b => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
                throw new SecurityException("Access denied: Path outside sandbox!");
            filePath = fullPath;
        }
        else
        {
            // original relative path logic
            var basePath = AllowedBases[0]; // ai_files_temp
            var relativePath = args.Path!.TrimStart('/', '\\');
            if (relativePath.Contains(".."))
                throw new SecurityException("Path traversal attempt detected!");

            var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
            if (!AllowedBases.Any(b => fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
                throw new SecurityException("Access denied: Path outside sandbox!");

            filePath = Path.Combine(fullPath, args.Filename!);
        }

        // handle duplicate filenames
        if (File.Exists(filePath))
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var dir = Path.GetDirectoryName(filePath)!;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = Path.Combine(dir, $"{baseName}_{timestamp}{ext}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        try
        {
            File.WriteAllText(filePath, args.Content, new UTF8Encoding(false));
            return $"File created: {filePath}";
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to write file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Permission denied: {ex.Message}", ex);
        }
    }

    public override ToolFunction GetToolFunction()
    {
        return new ToolFunction("create_file", "creates a new text based file.", new
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