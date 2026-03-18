using LlmTornado.ChatFunctions;
using LlmTornado.Common;

namespace GUA_Blazor.Tools;

public interface IAITool
{
    ToolFunction GetToolFunction();
    string ExecuteFunction(FunctionCall fn);
}
