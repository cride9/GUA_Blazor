using GUA_Blazor.Helper;
using GUA_Blazor.Models;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;

namespace GUA_Blazor.Components.Pages;

public partial class Home
{
    private Dictionary<string, ChatSession> chatHistory = new();
    private HashSet<int> expandedToolcalls = new();

    private string activeSessionId = string.Empty;
    private bool isLoading = false;
    private bool isStreaming = false;
    private ChatSession activeSession => chatHistory[activeSessionId];
    private SessionFactory chatSessionFactory = null!;

    private List<IBrowserFile> uploadedFiles = [];
    private List<string> uploadedPaths = [];
    private long maxFileSize = 1024 * 1024 * 500;
    private int maxAllowedFiles = 10;
    private string? previewImageUrl;

    private ElementReference textareaRef;
    private string _inputText = string.Empty;

    private bool isAgentMode = false;

    public Home(SessionFactory _factory)
    {
        chatSessionFactory = _factory;
    }

    // Ensure the first session exists as soon as the component loads
    protected override void OnInitialized()
    {
        StartNewChat();
    }

    private string inputText
    {
        get => _inputText;
        set
        {
            _inputText = value;
            _ = AutoResize();
        }
    }

    private void OpenImagePreview(string path)
    {
        previewImageUrl = GetImageDataUrl(path);
    }

    private void ClosePreview() => previewImageUrl = null;

    private bool CanSend =>
        !isLoading && !string.IsNullOrWhiteSpace(inputText) && !isStreaming;

    private async Task SendMessage()
    {
        if (!CanSend) return;

        var text = inputText.Trim();
        var paths = uploadedPaths.ToList();

        inputText = string.Empty;
        uploadedFiles.Clear();
        uploadedPaths.Clear();

        await activeSession.AddMessageToSession(text, true, paths);

        isLoading = true;
        isStreaming = true;
        StateHasChanged();

        var message = await activeSession.AddMessageToSession("", false);

        _ = AutoResize();

        if (isAgentMode)
        {
            ChatMessage? currentAgentMessage = null;
            await activeSession.SendMessageAgent(text, paths, async chunk =>
            {
                await InvokeAsync(() =>
                {
                    isLoading = false;

                    if (chunk == "//NEW_TURN//")
                    {
                        currentAgentMessage = null;
                        return;
                    }

                    if (currentAgentMessage == null)
                    {
                        currentAgentMessage = new ChatMessage("", false);
                        activeSession.Messages.Add(currentAgentMessage);
                    }

                    currentAgentMessage.Content += chunk;

                    // Check for voice message marker
                    if (currentAgentMessage.Content.Contains("[VOICE_MESSAGE_SENT]"))
                    {
                        var markerIndex = currentAgentMessage.Content.IndexOf("[VOICE_MESSAGE_SENT]");
                        var pathStart = markerIndex + "[VOICE_MESSAGE_SENT]".Length;
                        var pathEnd = currentAgentMessage.Content.IndexOf('\n', pathStart);
                        if (pathEnd < 0) pathEnd = currentAgentMessage.Content.Length;
                        
                        var path = currentAgentMessage.Content.Substring(pathStart, pathEnd - pathStart).Trim();
                        if (!string.IsNullOrEmpty(path) && string.IsNullOrEmpty(currentAgentMessage.VoiceMessagePath))
                        {
                            currentAgentMessage.VoiceMessagePath = path;
                        }
                    }

                    StateHasChanged();
                });
            });
        }
        else
        {
            await activeSession.SendMessageWithImage(text, paths, async chunk =>
            {
                await InvokeAsync(() =>
                {
                    isLoading = false;
                    message.Content += chunk;

                    // Check for voice message marker
                    if (message.Content.Contains("[VOICE_MESSAGE_SENT]"))
                    {
                        var markerIndex = message.Content.IndexOf("[VOICE_MESSAGE_SENT]");
                        var pathStart = markerIndex + "[VOICE_MESSAGE_SENT]".Length;
                        var pathEnd = message.Content.IndexOf('\n', pathStart);
                        if (pathEnd < 0) pathEnd = message.Content.Length;
                        
                        var path = message.Content.Substring(pathStart, pathEnd - pathStart).Trim();
                        if (!string.IsNullOrEmpty(path) && string.IsNullOrEmpty(message.VoiceMessagePath))
                        {
                            message.VoiceMessagePath = path;
                        }
                    }

                    StateHasChanged();
                });
            });
        }

        isLoading = false;
        isStreaming = false;
        StateHasChanged();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessage();
    }

    private void StartNewChat()
    {
        var newSession = chatSessionFactory.Create("New Chat");
        activeSessionId = newSession.Id;
        chatHistory[activeSessionId] = newSession;

        // Clear any pending uploads when creating a new chat
        uploadedFiles.Clear();
        uploadedPaths.Clear();
        inputText = string.Empty;
    }

    private void LoadSession(string id)
    {
        activeSessionId = id;
        
        // Clear pending uploads when switching chats
        uploadedFiles.Clear();
        uploadedPaths.Clear();
        inputText = string.Empty;
    }

    private string RenderMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return Markdown.ToHtml(content, new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build());
    }

    private bool HasToolcall(string content) =>
        content?.Contains("//TOOLCALL") == true;

    private string ExtractToolcallJson(string content)
    {
        if (string.IsNullOrEmpty(content)) return "{}";

        var start = content.IndexOf("//TOOLCALL");
        if (start < 0) return "{}";
        
        start += "//TOOLCALL".Length;
        var end = content.IndexOf("//TOOLCALL_END", start);

        string json;
        if (end < 0)
        {
            // If it's still streaming, take everything after //TOOLCALL
            json = content.Substring(start).Trim();
        }
        else
        {
            json = content.Substring(start, end - start).Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private void ToggleToolcall(int messageIndex)
    {
        if (expandedToolcalls.Contains(messageIndex))
            expandedToolcalls.Remove(messageIndex);
        else
            expandedToolcalls.Add(messageIndex);
    }

    private async Task CopyToClipboard(string text)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    private string StripToolcall(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        while (true)
        {
            var start = content.IndexOf("//TOOLCALL");
            if (start < 0) break;

            var end = content.IndexOf("//TOOLCALL_END", start);
            if (end < 0)
            {
                // If we have a start but no end, it's currently streaming a toolcall.
                // Hide the partial toolcall from the main chat bubble.
                content = content[..start];
                break;
            }

            content = content[..start] + content[(end + "//TOOLCALL_END".Length)..];
        }

        // Also strip the voice message marker from the visible text
        while (true)
        {
            var start = content.IndexOf("[VOICE_MESSAGE_SENT]");
            if (start < 0) break;

            var end = content.IndexOf('\n', start);
            if (end < 0) end = content.Length;

            content = content[..start] + content[end..];
        }

        return content.Trim();
    }

    private async Task LoadFiles(InputFileChangeEventArgs e)
    {
        uploadedFiles.Clear();
        uploadedPaths.Clear();

        foreach (var file in e.GetMultipleFiles(maxAllowedFiles))
        {
            try
            {
                var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                var trustedFileName = Path.GetRandomFileName() + ext;
                
                // activeSessionId is now guaranteed to match the active AI service
                var path = Path.Combine(
                    SessionSandbox.GetUploadsPath(activeSessionId),
                    trustedFileName);

                await using FileStream fs = new(path, FileMode.Create);
                await file.OpenReadStream(maxFileSize).CopyToAsync(fs);

                uploadedFiles.Add(file);
                uploadedPaths.Add(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    private void RemoveFile(IBrowserFile file)
    {
        var index = uploadedFiles.IndexOf(file);
        if (index >= 0)
        {
            uploadedFiles.RemoveAt(index);
            uploadedPaths.RemoveAt(index);
        }
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024} KB",
        _ => $"{bytes / (1024 * 1024)} MB"
    };

    private string GetImageDataUrl(string path)
    {
        if (!File.Exists(path)) return string.Empty;

        var mimeType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        var bytes = File.ReadAllBytes(path);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private string GetAudioDataUrl(string path)
    {
        if (!File.Exists(path)) return string.Empty;

        var mimeType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "audio/wav"
        };

        var bytes = File.ReadAllBytes(path);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private async Task AutoResize()
    {
        await JS.InvokeVoidAsync("autoResizeTextarea", textareaRef);
    }

    private List<(ChatMessage Primary, List<ChatMessage> ToolOnlyFollowers)> GroupMessages()
    {
        var groups = new List<(ChatMessage, List<ChatMessage>)>();

        foreach (var msg in activeSession.Messages)
        {
            if (string.IsNullOrWhiteSpace(msg.Content)) continue;

            bool isToolOnly = !msg.IsUser
                && HasToolcall(msg.Content)
                && string.IsNullOrWhiteSpace(StripToolcall(msg.Content));

            if (isToolOnly && groups.Count > 0)
            {
                groups[^1].Item2.Add(msg);
            }
            else
            {
                groups.Add((msg, new List<ChatMessage>()));
            }
        }

        return groups;
    }

    private static string GetFileIcon(string ext) => ext switch
    {
        ".pdf" => "📄",
        ".txt" or ".md" or ".log" => "📝",
        ".csv" => "📊",
        ".json" or ".xml" or ".yaml" or ".yml" => "🗂️",
        ".cs" or ".py" or ".js" or ".ts"
            or ".java" or ".cpp" or ".c" or ".h" => "💻",
        ".html" or ".htm" or ".css" => "🌐",
        ".sql" => "🗃️",
        ".mp4" or ".mkv" or ".avi" or ".mov"
            or ".webm" => "🎬",
        ".mp3" or ".wav" or ".ogg" or ".flac"
            or ".m4a" => "🎵",
        _ => "📎",
    };
}