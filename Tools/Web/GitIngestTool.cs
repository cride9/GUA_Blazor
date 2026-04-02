using GUA_Blazor.Service;
using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Web;

public class GitIngestTool : AITool<GitIngestArguments>
{
    public override ToolFunction GetToolFunction() => new ToolFunction(
            "git_ingest",
            "Ingests a github repository. Use this to analyze a whole repo instantly. THIS USES A LOT OF CONTEXT ONLY USE IF SPECIFICALLY ASKED TO",
            new
            {
                type = "object",
                properties = new
                {
                    github_url = new { type = "string", description = "Repository link. e.g.: https://github.com/USER/EXAMPLE_PROJECT" },
                },
                required = new List<string> { "github_url", "repository link" }
            });

    protected override Task<object?> ExecuteAsync(GitIngestArguments args)
    {
        return GitIngest.IngestAsync(args.GithubUrl);
    }
}

public class GitIngestArguments
{
    [JsonPropertyName("github_url")]
    public string GithubUrl { get; set; } = "";
}