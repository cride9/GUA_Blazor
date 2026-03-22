using GUA_Blazor.Models;
using GUA_Blazor.Tools;
using GUA_Blazor.Tools.Filesystem;
using GUA_Blazor.Tools.Terminal;
using LlmTornado;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Images;
using Microsoft.AspNetCore.Session;

namespace GUA_Blazor.Service;

public class AIService
{
    private TornadoApi _api; 
    private Conversation _conversation;
    private readonly TerminalSessionStore _sessionStore;
    private readonly Dictionary<string, IAITool> _tools;

    private static readonly HashSet<string> ImageExtensions = new()
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    private static readonly HashSet<string> TextExtensions = new()
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".yaml", ".yml",
        ".html", ".htm", ".css", ".js", ".ts", ".cs", ".py",
        ".java", ".cpp", ".c", ".h", ".sql", ".log", ".ini", ".config"
    };

    public AIService()
    {
        _api = new TornadoApi(new Uri("http://26.86.240.240:8080"));
        _conversation = _api.Chat.CreateConversation(new ChatRequest()
        {
            Messages = [
                new LlmTornado.Chat.ChatMessage(ChatMessageRoles.System, Instructions.BasicInstruction)
            ],
            ToolChoice = OutboundToolChoice.None,
            ParallelToolCalls = true
        });

        _sessionStore = new();
        _tools = new Dictionary<string, IAITool>
        {
            ["create_file"] = new CreateFile(),
            ["read_file"] = new ReadFile(),
            ["edit_file"] = new EditFile(),
            ["delete_file"] = new DeleteFile(),
            ["move_file"] = new MoveFile(),
            ["rename_file"] = new RenameFile(),
            ["search_in_files"] = new SearchInFiles(),
            ["list_directory"] = new ListDirectory(),
            ["run_command"] = new RunCommand(_sessionStore),
            ["read_terminal_output"] = new ReadTerminalOutput(_sessionStore),
            ["stop_loop"] = new StopTool(),
        };
    }

    public Task SendMessageWithImage(string message, List<string> imagesPath, Action<string> onResponse)
    {
        ChatMessagePart[] parts = ResolveImages(imagesPath);
        parts[0] = new ChatMessagePart(message);

        var messages = new List<LlmTornado.Chat.ChatMessage>();
        messages.Add(new(ChatMessageRoles.System, Instructions.BasicInstruction));
        messages.AddRange(
            _conversation.Messages
                .Where(m => m.Role != ChatMessageRoles.System && m.Name != "stop_loop")
        );
        messages.Add(new(ChatMessageRoles.User, parts));

        _conversation = _api.Chat.CreateConversation(new ChatRequest()
        {
            Messages = messages,
            ToolChoice = OutboundToolChoice.None,
        });

        return _conversation.StreamResponseRich(CreateHandler(onResponse, _conversation, null));
    }

    public async Task SendMessageAgent(string message, List<string> imagesPath, Action<string> onResponse)
    {
        int maxTurns = 50;

        ChatMessagePart[] parts = ResolveImages(imagesPath);
        parts[0] = new ChatMessagePart(message);

        List<LlmTornado.Chat.ChatMessage> messages = new();
        messages.Add(new(ChatMessageRoles.System, Instructions.AgentInstruction));
        messages.AddRange(
            _conversation.Messages
                .Where(m => m.Role != ChatMessageRoles.System && m.Name != "stop_loop")
        );
        messages.Add(new(ChatMessageRoles.User, parts));

        _conversation = _api.Chat.CreateConversation(new ChatRequest()
        {
            Tools = _tools.Select(t => new Tool(t.Value.GetToolFunction())).ToList(),
            Messages = messages,
            InvokeClrToolsAutomatically = false,
            ToolChoice = OutboundToolChoice.Auto,
        });

        var currentConversation = _conversation;
        var stopSignal = new StopSignal();
        var handler = CreateHandler(onResponse, currentConversation, stopSignal);

        await currentConversation.StreamResponseRich(handler);
        onResponse("//NEW_TURN//");
        maxTurns--;

        while (maxTurns > 0 && !stopSignal.Stop)
        {
            var lastMessage = currentConversation.Messages.LastOrDefault();
            bool isToolResult = lastMessage?.Role == ChatMessageRoles.Tool;

            if (isToolResult)
            {
                await currentConversation.StreamResponseRich(handler);
            }
            else
            {
                await currentConversation
                    .AppendUserInputWithName("agent_helper", "continue or stop with the tool")
                    .StreamResponseRich(handler);
            }

            onResponse("//NEW_TURN//");
            maxTurns--;
        }

        _conversation = currentConversation;
    }

    private ChatStreamEventHandler CreateHandler(Action<string> onResponse, Conversation conversation, StopSignal stopSignal)
    {
        return new ChatStreamEventHandler
        {
            MessageTokenHandler = async (x) =>
            {
                onResponse?.Invoke(x!);
            },
            FunctionCallHandler = async (calls) =>
            {
                await ResolveFunctions(calls, onResponse);
            },
            AfterFunctionCallsResolvedHandler = async (results, currentHandler) =>
            {
                foreach (var result in results.ToolResults)
                {
                    if (result?.Result?.Name == "stop_loop" && result?.Result?.Content == "stopping_loop")
                    {
                        stopSignal.Stop = true;
                    }
                }
            }
        };
    }

    private async Task ResolveFunctions(List<FunctionCall> calls, Action<string> onResponse)
    {
        foreach (var call in calls)
        {
            if (onResponse != null)
                onResponse.Invoke($"//TOOLCALL\n{call?.ToolCall?.FunctionCall?.GetJson()}\n//TOOLCALL_END");

            if (_tools.TryGetValue(call!.Name, out var tool))
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

    private ChatMessagePart[] ResolveImages(List<string> filePaths)
    {
        var parts = new List<ChatMessagePart>
        {
            null!
        };

        foreach (var filePath in filePaths)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ImageExtensions.Contains(ext))
            {
                var mimeType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                byte[] bytes = File.ReadAllBytes(filePath);
                string dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
                parts.Add(new ChatMessagePart(dataUrl, ImageDetail.High));
            }
            else if (TextExtensions.Contains(ext))
            {
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileName(filePath);
                parts.Add(new ChatMessagePart(
                    $"[Attached file: {fileName}]\n```\n{content}\n```"));
            }
            else if (ext == ".pdf")
            {
                var fileName = Path.GetFileName(filePath);
                var text = ExtractPdfText(filePath);
                parts.Add(new ChatMessagePart(
                    $"[Attached PDF: {fileName}]\n{text}"));
            }
            else
            {
                parts.Add(new ChatMessagePart(
                    $"[Attached file: {Path.GetFileName(filePath)} — binary format, content not available]"));
            }
        }

        return parts.ToArray();
    }

    private static string ExtractPdfText(string path)
    {
        var sb = new System.Text.StringBuilder();
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}

public class StopSignal
{
    public bool Stop { get; set; } = false;
}