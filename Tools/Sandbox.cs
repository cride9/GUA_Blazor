using GUA_Blazor.Helper;
using System.Security;

namespace GUA_Blazor.Tools;

internal static class Sandbox
{
    internal static string Resolve(string relativePath, string sessionId)
    {
        string basePath = SessionSandbox.GetWorkPath(sessionId);

        if (Path.IsPathRooted(relativePath))
        {
            var fullPath = Path.GetFullPath(relativePath);
            if (!SessionSandbox.IsPathAllowed(fullPath, sessionId))
                throw new SecurityException("Access denied: Path outside session sandbox!");
            return fullPath;
        }
        else
        {
            string relative = relativePath.TrimStart('/', '\\');
            if (relative.StartsWith("..") || relative.Contains(".."))
                throw new SecurityException("Path traversal attempt detected!");

            string full = Path.GetFullPath(Path.Combine(basePath, relative));
            if (!SessionSandbox.IsPathAllowed(full, sessionId))
                throw new SecurityException("Access denied: Path outside session sandbox!");

            return full;
        }
    }
}
