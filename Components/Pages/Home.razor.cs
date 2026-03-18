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
    private Dictionary<Guid, ChatSession> chatHistory = new();
    private HashSet<int> expandedToolcalls = new();

    private Guid activeSessionId = Guid.NewGuid();
    private bool isLoading = false;
    private ChatSession activeSession => GetActiveChatSession();
    private SessionFactory chatSessionFactory = null!;

    private List<IBrowserFile> uploadedFiles = [];
    private List<string> uploadedPaths = [];
    private long maxFileSize = 1024 * 1024 * 10;
    private int maxAllowedFiles = 5;
    private string? previewImageUrl;

    private ElementReference textareaRef;
    private string _inputText = string.Empty;

    public Home(SessionFactory _factory)
    {
        chatSessionFactory = _factory;
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
        !isLoading && !string.IsNullOrWhiteSpace(inputText);

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
        StateHasChanged();

        var message = await activeSession.AddMessageToSession("", false);

        if (paths.Count > 0)
        {
            activeSession.SendMessageWithImage(text, paths, chunk =>
            {
                message.Content += chunk;
                StateHasChanged();
            });
        }
        else
        {
            activeSession.SendMessageWithTool(text, chunk =>
            {
                message.Content += chunk;
                StateHasChanged();
            });
        }

        isLoading = false;
        StateHasChanged();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessage();
    }

    private void StartNewChat()
    {
        var id = Guid.NewGuid();
        var newSession = chatSessionFactory.Create("My Chat Title");

        chatHistory.Add(id, newSession);
        activeSessionId = id;
    }

    private ChatSession GetActiveChatSession()
    {
        if (chatHistory.ContainsKey(activeSessionId))
        {
            return chatHistory[activeSessionId];
        }
        else
        {
            StartNewChat();
            return chatHistory[activeSessionId];
        }
    }

    private void LoadSession(Guid id)
    {
        activeSessionId = id;
    }

    private string RenderMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return Markdown.ToHtml(content, new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build());
    }

    private bool HasToolcall(string content) =>
        content?.Contains("//TOOLCALL") == true && content?.Contains("//TOOLCALL_END") == true;

    private string ExtractToolcallJson(string content)
    {
        if (string.IsNullOrEmpty(content)) return "{}";

        var start = content.IndexOf("//TOOLCALL") + "//TOOLCALL".Length;
        var end = content.IndexOf("//TOOLCALL_END");

        if (start < 0 || end < 0 || end <= start) return "{}";

        var json = content.Substring(start, end - start).Trim();
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

        var start = content.IndexOf("//TOOLCALL");
        var end = content.IndexOf("//TOOLCALL_END");

        if (start < 0 || end < 0) return content;

        return (content[..start] + content[(end + "//TOOLCALL_END".Length)..]).Trim();
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
                var path = Path.Combine(
                    Environment.ContentRootPath,
                    Environment.EnvironmentName,
                    "unsafe_uploads",
                    trustedFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                await using FileStream fs = new(path, FileMode.Create);
                await file.OpenReadStream(maxFileSize).CopyToAsync(fs);

                uploadedFiles.Add(file);
                uploadedPaths.Add(path);
            }
            catch (Exception ex)
            {

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

    private async Task AutoResize()
    {
        await JS.InvokeVoidAsync("autoResizeTextarea", textareaRef);
    }
}
