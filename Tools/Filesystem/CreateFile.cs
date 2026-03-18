using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.Security;
using System.Text;
using System.Text.Json;

namespace GUA_Blazor.Tools.Filesystem;

public class CreateFile : AITool<CreateFileArguments>
{
    protected override string Execute(CreateFileArguments args)
    {
         string basePath = Path.Combine(
            Environment.CurrentDirectory,
            "ai_files_temp"
        );

        string relativePath = args.Path.TrimStart('/', '\\');
        if (relativePath.StartsWith("..") || relativePath.Contains(".."))
        {
            throw new SecurityException("Path traversal attempt detected!");
        }

        string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

        if (!fullPath.StartsWith(Path.GetFullPath(basePath)))
        {
            throw new SecurityException("Access denied: Path outside sandbox!");
        }

        string filePath = Path.Combine(fullPath, args.Filename);
        if (File.Exists(filePath))
        {
            string baseName = Path.GetFileNameWithoutExtension(args.Filename);
            string ext = Path.GetExtension(args.Filename);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = Path.Combine(fullPath, $"{baseName}_{timestamp}{ext}");
        }

        string directory = Path.GetDirectoryName(filePath);
        Directory.CreateDirectory(directory);

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
    public string Path { get; set; }
    public string Filename { get; set; }
    public string Content { get; set; }
}