using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Web;

public class ScrapeUrl : AITool<ScrapeUrlArguments>
{
    protected override async Task<object?> ExecuteAsync(ScrapeUrlArguments args)
    {
        if (string.IsNullOrWhiteSpace(args.Url))
            throw new Exception("URL is required.");

        return await WebScraper.ScrapeTextFromUrlAsync(args.Url);
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "scrape_url",
        "Fetches and extracts readable text content from a URL. Supports HTML pages and PDFs. For PDFs, append #page=N to get a specific page.",
        new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "The full URL to scrape, e.g. 'https://example.com' or 'https://example.com/file.pdf#page=2'" }
            },
            required = new List<string> { "url" }
        });
}

public class ScrapeUrlArguments
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}