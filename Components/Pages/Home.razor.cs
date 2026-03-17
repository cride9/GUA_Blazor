using GUA_Blazor.Models;
using GUA_Blazor.Service;
using Markdig;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GUA_Blazor.Components.Pages;

public partial class Home
{
    private Dictionary<Guid, ChatSession> chatHistory = new();
    private HashSet<int> expandedToolcalls = new();

    private Guid activeSessionId = Guid.NewGuid();
    private string inputText = string.Empty;
    private bool isLoading = false;
    private ChatSession activeSession => GetActiveChatSession();
    private SessionFactory chatSessionFactory = null!;

    public Home(SessionFactory _factory)
    {
        chatSessionFactory = _factory;
    }

    private bool CanSend =>
        !isLoading && !string.IsNullOrWhiteSpace(inputText);

    private async Task SendMessage()
    {
        if (!CanSend) return;

        var text = inputText.Trim();
        inputText = string.Empty;

        await activeSession.AddMessageToSession(text, true);
        isLoading = true;
        StateHasChanged();

        var message = await activeSession.AddMessageToSession("", false);
        activeSession.SendMessageWithTool(text, chunk =>
        {
            message.Content += chunk;
            StateHasChanged();
        });
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
}
