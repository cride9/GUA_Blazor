using LlmTornado.Common;
using System.Security;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class CreatePdf : AITool<CreatePdfArguments>
{
    private static readonly string SandboxPath =
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "ai_files_temp"));

    protected override string Execute(CreatePdfArguments args)
    {
        if (string.IsNullOrEmpty(args.Path))
            throw new Exception("path is required.");

        var relativePath = args.Path.TrimStart('/', '\\');
        if (relativePath.Contains(".."))
            throw new SecurityException("Path traversal attempt detected!");

        var fullPath = Path.GetFullPath(Path.Combine(SandboxPath, relativePath));
        if (!fullPath.StartsWith(SandboxPath))
            throw new SecurityException("Access denied: Path outside sandbox!");

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        var markdownText = (args.Content ?? "").Replace("\\n", "\n").Replace("\\t", "\t");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.PageColor(Colors.White);
                page.Margin(40);
                page.Content().Markdown(markdownText);
            });
        }).GeneratePdf(fullPath);

        return $"PDF created: {fullPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "create_pdf",
        "Creates a PDF file from markdown content. Saves into ai_files_temp.",
        new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Relative path for the PDF inside ai_files_temp, e.g. 'reports/summary.pdf'" },
                content = new { type = "string", description = "Markdown formatted content for the PDF." }
            },
            required = new List<string> { "path", "content" }
        });
}

public class CreatePdfArguments
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}