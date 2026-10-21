namespace Workbench.Core;

public static class OllamaCatalog
{
    public const string Address = "http://127.0.0.1:11434/v1";
    public static readonly string[] Suggestions = ["llama3.2", "llama3.1", "qwen2.5", "mistral"];

    public static bool CanDownload(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;
        var loopback = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (System.Net.IPAddress.TryParse(uri.IdnHost, out var address) && System.Net.IPAddress.IsLoopback(address));
        return loopback && uri.Port == 11434;
    }

    public static IReadOnlyList<string> Offers(string? endpoint, IEnumerable<string> installed)
    {
        if (!CanDownload(endpoint))
            return [];
        var present = installed as IReadOnlyCollection<string> ?? installed.ToArray();
        return Suggestions.Where(name => !present.Any(id =>
            id.Equals(name, StringComparison.Ordinal)
            || id.StartsWith(name + ":", StringComparison.Ordinal))).ToArray();
    }
}
