using GUA_Blazor.Helper;
using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.Text.Json;

namespace GUA_Blazor.Tools;

public abstract class AITool<T> : IAITool
{
    protected readonly string SessionId;
    protected string WorkPath => SessionSandbox.GetWorkPath(SessionId);

    protected AITool(string sessionId = "global")
    {
        SessionId = sessionId;
    }

    public Task<object?> ExecuteFunctionAsync(FunctionCall fn)
    {
        var args = JsonSerializer.Deserialize<T>(fn.Arguments!);
        return ExecuteAsync(args!);
    }

    protected virtual Task<object?> ExecuteAsync(T args)
        => Task.Run(() => Execute(args));

    protected virtual object? Execute(T args)
        => throw new NotImplementedException();

    public abstract ToolFunction GetToolFunction();
}