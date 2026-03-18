using GUA_Blazor.Models;
using GUA_Blazor.Tools;
using GUA_Blazor.Tools.Filesystem;
using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Images;

namespace GUA_Blazor.Service;

public class AIService
{
    private TornadoApi _api; 
    private Conversation _conversation;

    private readonly Dictionary<string, IAITool> _tools = new()
    {
        ["create_file"] = new CreateFile(),
    };

    public AIService()
    {
        _api = new TornadoApi(new Uri("http://127.0.0.1:8080"));
        _conversation = _api.Chat.CreateConversation(new ChatRequest()
        {
            Tools = _tools
                .Select(t => new Tool(t.Value.GetToolFunction()))
                .ToList(),
            Messages = [
                new LlmTornado.Chat.ChatMessage(ChatMessageRoles.System, Instructions.BasicInstruction)
            ],
            InvokeClrToolsAutomatically = false,
            ToolChoice = OutboundToolChoice.Auto,
        });
    }

    public void SendMessageWithImage(string message, List<string> imagesPath, Action<string> onResponse)
    {
        ChatMessagePart[] parts = new ChatMessagePart[imagesPath.Count + 1];
        parts[0] = new ChatMessagePart(message);

        for (int i = 0; i < imagesPath.Count; i++)
        {
            var imagePath = imagesPath[i];
            var mimeType = Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            byte[] bytes = File.ReadAllBytes(imagePath);
            string dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            parts[i + 1] = new ChatMessagePart(dataUrl, ImageDetail.High);
        }

        var messageImage = new LlmTornado.Chat.ChatMessage(ChatMessageRoles.User, parts);
        _conversation.AppendMessage(messageImage).StreamResponseRich(CreateHandler(onResponse));
    }

    public void SendMessageWithTool(string message, Action<string> onResponse)
    {
        _conversation.AppendUserInput(message).StreamResponseRich(CreateHandler(onResponse));
    }

    public void SendMessageAgent(string message, Action<string> onResponse)
    {

    }

    private void ResolveFunctions(List<FunctionCall> calls, Action<string> onResponse)
    {
        foreach (var call in calls)
        {
            onResponse?.Invoke($"//TOOLCALL\n{call?.ToolCall?.FunctionCall?.GetJson()}\n//TOOLCALL_END");

            if (_tools.TryGetValue(call.Name, out var tool))
            {
                var result = tool.ExecuteFunction(call);
                call.Result = new FunctionResult(call, result, null);
            }
            else
            {
                call.Result = new FunctionResult(call, "Function not found", null);
            }
        }
    }

    private ChatStreamEventHandler CreateHandler(Action<string> onResponse)
    {
        return new ChatStreamEventHandler
        {
            MessageTokenHandler = async (x) =>
            {
                onResponse?.Invoke(x!);
            },
            FunctionCallHandler = async (calls) =>
            {
                ResolveFunctions(calls, onResponse);
            },
            AfterFunctionCallsResolvedHandler = async (results, currentHandler) =>
            {
                await _conversation.StreamResponseRich(currentHandler);
            }
        };
    }
}
