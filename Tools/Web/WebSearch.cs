using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Web;

public class WebSearch : AITool<WebSearchArguments>
{
    protected override async Task<string> ExecuteAsync(WebSearchArguments args)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
            throw new Exception("Query is required.");

        return await WebScraper.Search(args.Query);
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "web_search",
        "Searches for a query on Google. Returns top results with titles, links and snippets.",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "The query to search for on the web." }
            },
            required = new List<string> { "query" }
        });
}


public class WebSearchArguments
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }
}