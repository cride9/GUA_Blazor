using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools;

public class StopTool : AITool<StopToolArgs>
{
    public override ToolFunction GetToolFunction()
    {
        return new ToolFunction("stop_loop", "stops the agentic loop, true/false value", new
        {
            type = "object",
            properties = new
            {
                stoploop = new { type = "string" },
            },
            required = new List<string> { "stoploop" }
        });
    }

    protected override string Execute(StopToolArgs args)
    {
        bool stop = bool.TryParse(args.StopLoop, out var b) && b;
        return stop ? "stopping_loop" : "continuing_loop";
    }
}

public class StopToolArgs
{
    [JsonPropertyName("stoploop")]
    public string StopLoop { get; set; } = "false";
}