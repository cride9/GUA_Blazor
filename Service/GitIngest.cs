using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GUA_Blazor.Service;

public static class GitIngest
{
    private static readonly HttpClient _http = new();

    /// <summary>
    /// Ingests a GitHub repository and returns its structure + file contents as a single string.
    /// Accepts formats like:
    ///   https://github.com/owner/repo
    ///   https://github.com/owner/repo/tree/branch
    ///   owner/repo
    /// </summary>
    public static async Task<object?> IngestAsync(string githubUrl, string? githubToken = null)
    {
        var (owner, repo, branch) = ParseUrl(githubUrl);

        if (githubToken != null)
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", githubToken);

        _http.DefaultRequestHeaders.UserAgent.TryParseAdd("GitIngest-CSharp/1.0");

        // Resolve default branch if not specified
        if (string.IsNullOrEmpty(branch))
            branch = await GetDefaultBranchAsync(owner, repo);

        var files = new List<(string Path, string Content)>();
        await WalkTreeAsync(owner, repo, branch, "", files);

        return BuildOutput(owner, repo, branch, files);
    }

    // ── Parsing ──────────────────────────────────────────────────────────────

    private static (string owner, string repo, string branch) ParseUrl(string input)
    {
        input = input.Trim().TrimEnd('/');

        // Strip https://github.com/ prefix
        if (input.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            input = input["https://github.com/".Length..];
        else if (input.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            input = input["github.com/".Length..];

        // owner/repo/tree/branch or owner/repo
        var parts = input.Split('/');
        string owner = parts[0];
        string repo = parts[1];
        string branch = "";

        // https://github.com/owner/repo/tree/main  → parts[3]
        if (parts.Length >= 4 && parts[2] == "tree")
            branch = string.Join("/", parts[3..]);

        return (owner, repo, branch);
    }

    // ── GitHub API helpers ────────────────────────────────────────────────────

    private static async Task<string> GetDefaultBranchAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}";
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("default_branch").GetString() ?? "main";
    }

    private static async Task WalkTreeAsync(
        string owner, string repo, string branch,
        string path, List<(string, string)> files)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}";
        var response = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(response);

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            string type = entry.GetProperty("type").GetString()!;
            string filePath = entry.GetProperty("path").GetString()!;

            if (type == "dir")
            {
                // Skip common noise directories
                var dirName = Path.GetFileName(filePath);
                if (IsIgnoredDirectory(dirName)) continue;

                await WalkTreeAsync(owner, repo, branch, filePath, files);
            }
            else if (type == "file")
            {
                if (!IsTextFile(filePath)) continue;

                string downloadUrl = entry.GetProperty("download_url").GetString()!;
                string content;
                try { content = await _http.GetStringAsync(downloadUrl); }
                catch { content = "[Could not fetch file content]"; }

                files.Add((filePath, content));
            }
        }
    }

    // ── Filters ───────────────────────────────────────────────────────────────

    private static readonly HashSet<string> _ignoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".vs", "bin", "obj", "__pycache__",
        ".idea", "dist", "build", ".next", "vendor", "packages"
    };

    private static bool IsIgnoredDirectory(string name) => _ignoredDirs.Contains(name);

    private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".json", ".xml", ".yaml", ".yml",
        ".md", ".txt", ".py", ".js", ".ts", ".jsx", ".tsx", ".html",
        ".css", ".scss", ".sh", ".bat", ".ps1", ".go", ".rs", ".cpp",
        ".c", ".h", ".hpp", ".java", ".kt", ".swift", ".rb", ".php",
        ".toml", ".ini", ".cfg", ".env", ".gitignore", ".editorconfig"
    };

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
        {
            // Extensionless files like Dockerfile, Makefile
            var name = Path.GetFileName(path);
            return name is "Dockerfile" or "Makefile" or "Jenkinsfile" or "Procfile";
        }
        return _textExtensions.Contains(ext);
    }

    // ── Output builder ────────────────────────────────────────────────────────

    private static string BuildOutput(
        string owner, string repo, string branch,
        List<(string Path, string Content)> files)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"Repository: {owner}/{repo}  (branch: {branch})");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        // Directory tree
        sb.AppendLine("Directory structure:");
        sb.AppendLine($"└── {repo}/");
        var sorted = files.OrderBy(f => f.Path).ToList();
        BuildTree(sb, sorted.Select(f => f.Path).ToList(), repo);
        sb.AppendLine();
        sb.AppendLine();

        // File contents
        sb.AppendLine("Files Content:");
        sb.AppendLine();

        foreach (var (path, content) in sorted)
        {
            sb.AppendLine(new string('=', 48));
            sb.AppendLine($"FILE: {path}");
            sb.AppendLine(new string('=', 48));
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void BuildTree(StringBuilder sb, List<string> paths, string repoName)
    {
        // Group by top-level segment for a simple visual tree
        var grouped = paths
            .Select(p => p.Split('/'))
            .GroupBy(p => p[0])
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var children = group.ToList();
            bool isLast = group == grouped.Last();
            string connector = isLast ? "    └── " : "    ├── ";

            if (children.All(c => c.Length == 1))
            {
                sb.AppendLine($"{connector}{group.Key}");
            }
            else
            {
                sb.AppendLine($"{connector}{group.Key}/");
                var subPaths = children
                    .Where(c => c.Length > 1)
                    .Select(c => string.Join("/", c[1..]))
                    .ToList();
                BuildSubTree(sb, subPaths, isLast ? "        " : "    │   ");
            }
        }
    }

    private static void BuildSubTree(StringBuilder sb, List<string> paths, string indent)
    {
        var grouped = paths
            .Select(p => p.Split('/'))
            .GroupBy(p => p[0])
            .OrderBy(g => g.Key)
            .ToList();

        for (int i = 0; i < grouped.Count; i++)
        {
            var group = grouped[i];
            bool isLast = i == grouped.Count - 1;
            string connector = isLast ? "└── " : "├── ";
            var children = group.ToList();

            if (children.All(c => c.Length == 1))
            {
                sb.AppendLine($"{indent}{connector}{group.Key}");
            }
            else
            {
                sb.AppendLine($"{indent}{connector}{group.Key}/");
                var subPaths = children
                    .Where(c => c.Length > 1)
                    .Select(c => string.Join("/", c[1..]))
                    .ToList();
                BuildSubTree(sb, subPaths, indent + (isLast ? "    " : "│   "));
            }
        }
    }
}

