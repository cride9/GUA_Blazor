using GUA_Blazor.Helper;
using LlmTornado.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Markdown;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class CreatePdf : AITool<CreatePdfArguments>
{
    public CreatePdf(string sessionId) : base(sessionId) { }

    protected override string Execute(CreatePdfArguments args)
    {
        if (string.IsNullOrEmpty(args.Path))
            throw new Exception("path is required.");

        var fullPath = Sandbox.Resolve(args.Path, SessionId);

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
        var relativePath = Path.GetRelativePath(SessionSandbox.GetSessionPath(SessionId), fullPath).Replace('\\', '/');
        var downloadUrl = $"/sessions/{SessionId}/{relativePath}";
        return $"PDF created: {fullPath}. Download link: [Download PDF]({downloadUrl})";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "create_pdf",
        "Creates a PDF file from markdown content. Saves into the sandbox.",
        new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Relative path for the PDF inside the sandbox, e.g. 'reports/summary.pdf'" },
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