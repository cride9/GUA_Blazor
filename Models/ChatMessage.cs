namespace GUA_Blazor.Models;

public class ChatMessage
{
    public string Content;
    public bool IsUser;

    public ChatMessage(string _content, bool _isUser)
    {
        Content = _content;
        IsUser = _isUser;
    }
}
