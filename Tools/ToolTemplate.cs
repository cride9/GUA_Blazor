using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.Text.Json;

namespace GUA_Blazor.Tools;

public abstract class AITool<T> : IAITool
{
    public Task<string> ExecuteFunctionAsync(FunctionCall fn)
    {
        var args = JsonSerializer.Deserialize<T>(fn.Arguments!);
        return ExecuteAsync(args!);
    }

    protected virtual Task<string> ExecuteAsync(T args)
        => Task.Run(() => Execute(args));

    protected virtual string Execute(T args)
        => throw new NotImplementedException();

    public abstract ToolFunction GetToolFunction();
}
