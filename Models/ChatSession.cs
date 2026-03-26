using GUA_Blazor.Service;

namespace GUA_Blazor.Models;

public class ChatSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New Chat";
    public List<ChatMessage> Messages = new();
    public AIService Service = null!;

    public Task<ChatMessage> AddMessageToSession(string message, bool isUser, List<string>? attachments = null)
    {
        var newMessage = new ChatMessage(message, isUser);
        if (attachments?.Count > 0)
            newMessage.AttachmentPaths = attachments;
        Messages.Add(newMessage);
        return Task.FromResult(newMessage);
    }

    public Task SendMessageWithImage(string message, List<string> imagesPath, Action<string> onResponse)
    {
        return Service.SendMessageWithImage(message, imagesPath, onResponse);
    }

    public Task SendMessageAgent(string message, List<string> imagesPath, Action<string> onResponse)
    {
        return Service.SendMessageAgent(message, imagesPath, onResponse);
    }
}
