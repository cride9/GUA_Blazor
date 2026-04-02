using LlmTornado.ChatFunctions;
using LlmTornado.Common;

namespace GUA_Blazor.Tools;

public interface IAITool
{
    Task<object?> ExecuteFunctionAsync(FunctionCall fn);
    ToolFunction GetToolFunction();
}
