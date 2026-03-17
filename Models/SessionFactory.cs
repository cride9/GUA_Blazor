using GUA_Blazor.Service;

namespace GUA_Blazor.Models;

public class SessionFactory
{
    public ChatSession Create(string title)
    {
        var service = new AIService();
        return new ChatSession(title, service);
    }
}
