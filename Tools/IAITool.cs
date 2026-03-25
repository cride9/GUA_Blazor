using LlmTornado.ChatFunctions;
using LlmTornado.Common;

namespace GUA_Blazor.Tools;

public interface IAITool
{
    Task<string> ExecuteFunctionAsync(FunctionCall fn);
    ToolFunction GetToolFunction();
}
