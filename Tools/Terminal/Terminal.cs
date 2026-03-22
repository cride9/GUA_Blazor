using System.Collections.Concurrent;

namespace GUA_Blazor.Tools.Terminal;

public class TerminalSessionStore
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();

    public TerminalSession GetOrCreate(string sessionId)
        => _sessions.GetOrAdd(sessionId, id => new TerminalSession(id));

    public TerminalSession? Get(string sessionId)
        => _sessions.TryGetValue(sessionId, out var s) ? s : null;

    public IEnumerable<string> ListIds() => _sessions.Keys;
}

public class TerminalSession
{
    public string Id { get; }
    public string WorkingDirectory { get; set; }
    public bool IsRunning { get; private set; }
    public DateTime? LastCommandStarted { get; private set; }
    public DateTime? LastCommandFinished { get; private set; }

    private readonly Queue<string> _log = new();
    private const int MaxLines = 500;
    private readonly object _lock = new();

    public TerminalSession(string id)
    {
        Id = id;
        WorkingDirectory = Environment.CurrentDirectory;
    }

    public void MarkStarted()
    {
        IsRunning = true;
        LastCommandStarted = DateTime.UtcNow;
    }

    public void MarkFinished()
    {
        IsRunning = false;
        LastCommandFinished = DateTime.UtcNow;
    }

    public void AppendLog(string line)
    {
        lock (_lock)
        {
            _log.Enqueue(line);
            while (_log.Count > MaxLines) _log.Dequeue();
        }
    }

    public string ReadLog(int lastN = 80)
    {
        lock (_lock)
        {
            var lines = _log.TakeLast(lastN).ToArray();
            return lines.Length == 0 ? "(no output yet)" : string.Join('\n', lines);
        }
    }

    public void ClearLog()
    {
        lock (_lock) { _log.Clear(); }
    }
}