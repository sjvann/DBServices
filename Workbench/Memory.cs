using System.Collections.Concurrent;
using Workbench.Core.Ports;

namespace Workbench;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class MemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);

    public string Create(string operatorId)
    {
        var id = Guid.NewGuid().ToString("N");
        _sessions[id] = operatorId;
        return id;
    }

    public string? OperatorFor(string sessionId)
        => _sessions.TryGetValue(sessionId, out var operatorId) ? operatorId : null;

    public void Delete(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public void Clear() => _sessions.Clear();
}

public sealed class MemoryReportStore : IReportMemory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ReportDocument> _current = new(StringComparer.Ordinal);

    public void Save(string sessionId, ReportDocument report)
    {
        lock (_gate)
            _current[sessionId] = report;
    }

    public ReportDocument? Current(string sessionId)
    {
        lock (_gate)
            return _current.TryGetValue(sessionId, out var report) ? report : null;
    }

    public void Clear(string sessionId)
    {
        lock (_gate)
            _current.Remove(sessionId);
    }
}
