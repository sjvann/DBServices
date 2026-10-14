using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Web;

public sealed class CircuitSession
{
    public string? SessionId { get; private set; }

    public void Adopt(string? incoming)
    {
        if (string.IsNullOrEmpty(incoming))
            return;
        if (string.IsNullOrEmpty(SessionId))
            SessionId = incoming;
    }
}

public sealed class OperatorGate
{
    private readonly CircuitSession _session;
    private readonly SessionService _sessions;
    private readonly Profiles _profiles;

    public OperatorGate(CircuitSession session, SessionService sessions, Profiles profiles)
    {
        _session = session;
        _sessions = sessions;
        _profiles = profiles;
    }

    public string? SessionId => _session.SessionId;

    public Profile? Current => _profiles.Find(_sessions.OperatorId(_session.SessionId));
}

public static class DialogHost
{
    public static DialogStack Stack { get; } = new();
    public static bool Escape() => Stack.HandleEscape();
}

public sealed class DialogStack
{
    private readonly List<int> _open = [];

    public int Count => _open.Count;

    public int Push()
    {
        var id = _open.Count == 0 ? 1 : _open[^1] + 1;
        _open.Add(id);
        return id;
    }

    public bool HandleEscape()
    {
        if (_open.Count == 0)
            return false;
        _open.RemoveAt(_open.Count - 1);
        return true;
    }
}
