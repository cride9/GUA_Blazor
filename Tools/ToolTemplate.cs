using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.Text.Json;

namespace GUA_Blazor.Tools;

public abstract class AITool<TArgs> : IAITool
{
    protected static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ExecuteFunction(FunctionCall fn)
    {
        var args = JsonSerializer.Deserialize<TArgs>(fn?.ToolCall?.FunctionCall?.Arguments!, Options);

        if (args == null)
            return "Invalid arguments";

        return Execute(args);
    }

    protected abstract string Execute(TArgs args);

    public abstract ToolFunction GetToolFunction();
}
