using System.Diagnostics;
using System.Net.Sockets;
using Workbench.Core.Ports;

namespace Workbench;

public sealed class McpProcessHost : IDataServiceHost
{
    private readonly string _baseUrl;
    private readonly string _contentRoot;
    private readonly Func<CancellationToken, Task<bool>> _responds;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private Process? _process;

    public McpProcessHost(string baseUrl, string contentRoot)
        : this(baseUrl, contentRoot, null)
    {
    }

    public McpProcessHost(string baseUrl, string contentRoot, Func<CancellationToken, Task<bool>>? responds)
    {
        _baseUrl = baseUrl ?? "";
        _contentRoot = contentRoot ?? "";
        _responds = responds ?? Responds;
    }

    public static string? FindProject(string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(contentRoot))
            return null;
        DirectoryInfo? directory;
        try
        {
            directory = new DirectoryInfo(contentRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or IOException)
        {
            return null;
        }

        for (var depth = 0; depth < 6 && directory is not null; depth++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "McpService", "McpService.csproj");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public async Task<DataServiceStart> StartAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Port <= 0)
            return DataServiceStart.Fail("資料服務位址還沒有設定，無法啟動。");

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (await _responds(cancellationToken))
                return DataServiceStart.Ready("資料服務已在運行。");

            if (_process is not { HasExited: false })
            {
                var project = FindProject(_contentRoot);
                if (project is null)
                    return DataServiceStart.Fail("找不到資料服務程式，無法啟動。");
                try
                {
                    _process = Launch(project, uri);
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
                {
                    return DataServiceStart.Fail("無法啟動資料服務。");
                }
            }

            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                if (await _responds(cancellationToken))
                    return DataServiceStart.Ready("資料服務已啟動。");
                if (_process!.HasExited)
                    return DataServiceStart.Fail("資料服務啟動後立刻結束。");
                await Task.Delay(400, cancellationToken);
            }

            return DataServiceStart.Fail("資料服務沒有起來。");
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static Process Launch(string project, Uri uri)
    {
        var projectDirectory = Path.GetDirectoryName(project)!;
        var built = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "McpService.dll");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--no-launch-profile");
        if (File.Exists(built))
            start.ArgumentList.Add("--no-build");
        start.Environment["ASPNETCORE_URLS"] = uri.GetLeftPart(UriPartial.Authority);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += WriteThrough;
        process.ErrorDataReceived += WriteThrough;
        if (!process.Start())
            throw new InvalidOperationException("無法啟動資料服務。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void WriteThrough(object sender, DataReceivedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
            Console.Error.WriteLine(args.Data);
    }

    private async Task<bool> Responds(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri) || uri.Port <= 0)
            return false;
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            return false;
        }
    }
}
