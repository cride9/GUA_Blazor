namespace GUA_Blazor.Models;

public class ChatMessage
{
    public string Content;
    public bool IsUser;
    public List<string> AttachmentPaths { get; set; } = [];
    public string? VoiceMessagePath { get; set; }

    public ChatMessage(string _content, bool _isUser)
    {
        Content = _content;
        IsUser = _isUser;
    }
}
