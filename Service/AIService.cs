using GUA_Blazor.Models;
using GUA_Blazor.Tools;
using GUA_Blazor.Tools.Filesystem;
using GUA_Blazor.Tools.Terminal;
using GUA_Blazor.Tools.TTS;
using GUA_Blazor.Tools.Web;
using GUA_Blazor.Tools.WhisperTools;
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
    private string _sessionId;

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

    private static readonly HashSet<string> VideoExtensions = new()
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm"
    };

    private static readonly HashSet<string> AudioExtensions = new()
    {
        ".mp3", ".wav", ".ogg", ".flac", ".m4a"
    };

    public AIService(string sessionId)
    {
        _sessionId = sessionId;
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
            ["create_file"] = new CreateFile(_sessionId),
            ["read_file"] = new ReadFile(_sessionId),
            ["edit_file"] = new EditFile(_sessionId),
            ["delete_file"] = new DeleteFile(_sessionId),
            ["move_file"] = new MoveFile(_sessionId),
            ["rename_file"] = new RenameFile(_sessionId),
            ["search_in_files"] = new SearchInFiles(_sessionId),
            ["list_directory"] = new ListDirectory(_sessionId),
            ["run_command"] = new RunCommand(_sessionStore, _sessionId),
            ["read_terminal_output"] = new ReadTerminalOutput(_sessionStore),
            ["stop_loop"] = new StopTool(),
            ["extract_audio"] = new ExtractAudio(_sessionId),
            ["transcribe_audio"] = new TranscribeAudio(_sessionId),
            ["burn_subtitles"] = new BurnSubtitles(_sessionId),
            ["text_to_speech"] = new TextToSpeech(_sessionId),
            ["merge_audio"] = new MergeAudio(_sessionId),
            ["merge_audio_with_video"] = new MergeAudioWithVideo(_sessionId),
            ["web_search"] = new WebSearch(),
            ["scrape_url"] = new ScrapeUrl(),
            ["zip_directory"] = new ZipDirectory(_sessionId),
            ["create_pdf"] = new CreatePdf(_sessionId),
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
        const int MaxTurns = 50;

        var parts = ResolveImages(imagesPath);
        parts[0] = new ChatMessagePart(message);

        var messages = new List<LlmTornado.Chat.ChatMessage>
        {
            new(ChatMessageRoles.System, Instructions.AgentInstruction)
        };
        messages.AddRange(_conversation.Messages
            .Where(m => m.Role != ChatMessageRoles.System && m.Name != "stop_loop"));
        messages.Add(new(ChatMessageRoles.User, parts));

        _conversation = _api.Chat.CreateConversation(new ChatRequest
        {
            Tools = _tools.Values.Select(t => new Tool(t.GetToolFunction())).ToList(),
            Messages = messages,
            InvokeClrToolsAutomatically = false,
            ToolChoice = OutboundToolChoice.Auto,
            ParallelToolCalls = true,
        });

        var stopSignal = new StopSignal();
        var handler = CreateHandler(onResponse, _conversation, stopSignal);

        for (int turn = 0; turn < MaxTurns && !stopSignal.Stop; turn++)
        {
            var lastMessage = _conversation.Messages.LastOrDefault();
            bool needsPrompt = turn > 0 && lastMessage?.Role != ChatMessageRoles.Tool;

            if (needsPrompt)
                _conversation.AppendUserInputWithName("agent_helper", "continue or stop with the tool");

            await _conversation.StreamResponseRich(handler);
            onResponse("//NEW_TURN//");
        }
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
                try
                {
                    var result = await tool.ExecuteFunctionAsync(call);
                    call.Result = new FunctionResult(call, result, null);
                } catch (Exception e)
                {
                    call.Result = new FunctionResult(call, e.Message, null);
                }
            }
            else
            {
                call.Result = new FunctionResult(call, "Function not found", null);
            }
        }
    }

    private ChatMessagePart[] ResolveImages(List<string> filePaths)
    {
        var parts = new List<ChatMessagePart> { null! };

        foreach (var filePath in filePaths)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var fileName = Path.GetFileName(filePath);

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
                parts.Add(new ChatMessagePart(
                    $"[Attached file: {fileName}]\n```\n{content}\n```"));
            }
            else if (ext == ".pdf")
            {
                var text = ExtractPdfText(filePath);
                parts.Add(new ChatMessagePart($"[Attached PDF: {fileName}]\n{text}"));
            }
            else if (VideoExtensions.Contains(ext) || AudioExtensions.Contains(ext))
            {
                // Tell the agent the file exists and its path so it can call extract_audio / transcribe_audio
                parts.Add(new ChatMessagePart(
                    $"[Attached {(VideoExtensions.Contains(ext) ? "video" : "audio")} file: {fileName}]\n" +
                    $"Full path: {filePath}\n" +
                    $"You can transcribe this using extract_audio (if video) then transcribe_audio, " +
                    $"and burn subtitles with burn_subtitles."));
            }
            else
            {
                parts.Add(new ChatMessagePart(
                    $"[Attached file: {fileName} — binary format, content not available]"));
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