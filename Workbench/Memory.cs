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

public sealed class MemoryPreviewStore : IPreviewMemory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FillPlan> _plans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Snapshot> _current = new(StringComparer.Ordinal);

    public void SavePlan(FillPlan plan)
    {
        lock (_gate)
            _plans[plan.Id] = plan;
    }

    public FillPlan? FindPlan(string planId)
    {
        lock (_gate)
            return _plans.TryGetValue(planId, out var plan) ? plan : null;
    }

    public bool MarkUsed(string planId)
    {
        lock (_gate)
        {
            if (!_plans.TryGetValue(planId, out var plan) || plan.Used)
                return false;
            plan.Used = true;
            return true;
        }
    }

    public void ReplaceSnapshot(Snapshot snapshot)
    {
        lock (_gate)
            _current[snapshot.SessionId] = snapshot;
    }

    public Snapshot? Current(string sessionId)
    {
        lock (_gate)
            return _current.TryGetValue(sessionId, out var snapshot) ? snapshot : null;
    }

    public Snapshot? FindSnapshot(string sessionId, string snapshotId)
    {
        lock (_gate)
        {
            if (!_current.TryGetValue(sessionId, out var snapshot) || snapshot.Id != snapshotId)
                return null;
            return snapshot;
        }
    }
}
