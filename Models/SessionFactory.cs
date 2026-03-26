using GUA_Blazor.Service;

namespace GUA_Blazor.Models;

public class SessionFactory
{
    public ChatSession Create(string title)
    {
        var session = new ChatSession();
        session.Service = new AIService(session.Id);
        return session;
    }
}
