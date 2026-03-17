using GUA_Blazor.Service;
using LlmTornado.Chat;

namespace GUA_Blazor.Models;

public class ChatSession
{
    public string Title;
    public List<ChatMessage> Messages = new();
    private AIService _service;

    public ChatSession(string _title, AIService service = null!)
    {
        Title = _title;
        _service = service;
    }

    public Task<ChatMessage> AddMessageToSession(string message, bool isUser)
    {
        var newMessage = new ChatMessage(message, isUser);
        Messages.Add(newMessage);
        return Task.FromResult(newMessage);
    }

    public void SendMessage(string message, Action<string> onResponse)
    {
        _service.SendMessage(message, onResponse);
    }

    public void SendMessageWithTool(string message, Action<string> onResponse)
    {
        _service.SendMessageWithTool(message, onResponse);
    }
}
