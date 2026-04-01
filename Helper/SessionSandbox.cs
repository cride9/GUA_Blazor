using System.Security;

namespace GUA_Blazor.Helper;

public static class SessionSandbox
{
    private static readonly string BaseDir =
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "sessions"));

    public static string GetSessionPath(string sessionId)
    {
        var path = Path.GetFullPath(Path.Combine(BaseDir, sessionId));
        if (!path.StartsWith(BaseDir))
            throw new SecurityException("Invalid session ID.");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetUploadsPath(string sessionId)
    {
        var path = Path.Combine(GetSessionPath(sessionId), "work");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetWorkPath(string sessionId)
    {
        var path = Path.Combine(GetSessionPath(sessionId), "work");
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool IsPathAllowed(string fullPath, string sessionId)
    {
        var sessionPath = GetSessionPath(sessionId);
        return Path.GetFullPath(fullPath).StartsWith(sessionPath);
    }
}
