using GUA_Blazor.Models;
using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;

namespace GUA_Blazor.Service;

public class AIService
{
    private TornadoApi _api; 
    private Conversation _conversation; 

    public AIService()
    {
        _api = new TornadoApi(new Uri("http://127.0.0.1:8080"));
        _conversation = _api.Chat.CreateConversation(new ChatRequest()
        {
            Tools = [
                new Tool(new ToolFunction("get_weather", "gets the current weather", new
                {
                    type = "object",
                    properties = new
                    {
                        location = new
                        {
                            type = "string",
                            description = "The location for which the weather information is required."
                        }
                    },
                    required = new List<string> { "location" }
                }))
            ],
            Messages = [
                new LlmTornado.Chat.ChatMessage(ChatMessageRoles.System, Instructions.BasicInstruction)
            ],
            InvokeClrToolsAutomatically = false,
            ToolChoice = OutboundToolChoice.None,
            ParallelToolCalls = true
        });
    }

    public void SendMessage(string message, Action<string> onResponse)
    {
        _conversation.AppendUserInput(message).StreamResponse(onResponse);
    }

    public void SendMessageWithTool(string message, Action<string> onResponse)
    {
        ChatStreamEventHandler handler = new()
        {
            MessageTokenHandler = async (x) =>
            {
                onResponse?.Invoke(x);
                return;
            },
            FunctionCallHandler = async (calls) =>
            {
                ResolveFunctions(calls, onResponse);
                return;
            },
            AfterFunctionCallsResolvedHandler = async (results, currentHandler) =>
            {
                await _conversation.StreamResponseRich(currentHandler);
            }
        };

        _conversation.AppendUserInput(message).StreamResponseRich(handler);
    }

    private void ResolveFunctions(List<FunctionCall> calls, Action<string> onResponse)
    {
        foreach (var call in calls)
        {
            onResponse?.Invoke($"//TOOLCALL\n{call?.ToolCall?.GetJson()}\n//TOOLCALL_END");
            switch (call.Name)
            {
                case "get_weather":
                    call.Result = new FunctionResult(call, "A mild rain is expected around noon.", null); break;
                default:
                    call.Result = new FunctionResult(call, "Function not found", null); break;
            }
        }
    }
}
