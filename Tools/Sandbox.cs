using System.Security;

namespace GUA_Blazor.Tools;

internal static class Sandbox
{
    internal static string Resolve(string relativePath)
    {
        string basePath = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "ai_files_temp"));

        string relative = relativePath.TrimStart('/', '\\');
        if (relative.StartsWith("..") || relative.Contains(".."))
            throw new SecurityException("Path traversal attempt detected!");

        string full = Path.GetFullPath(Path.Combine(basePath, relative));
        if (!full.StartsWith(basePath))
            throw new SecurityException("Access denied: Path outside sandbox!");

        return full;
    }
}
