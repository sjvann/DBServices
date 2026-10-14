using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public static class SidecarPayload
{
    public static string Probe(string placement, string endpoint, string modelId)
        => JsonSerializer.Serialize(new SidecarProbeInput
        {
            Kind = "probe",
            Placement = placement,
            Endpoint = endpoint,
            ModelId = modelId,
        });

    public static string Parse(string parseJson) => parseJson;
}

public sealed class SidecarLauncher : IModelSidecar
{
    private readonly string _path;
    private readonly TimeSpan _timeout;

    public SidecarLauncher(string path, TimeSpan timeout)
    {
        _path = path;
        _timeout = timeout;
    }

    public async Task<SidecarResponse> InvokeAsync(SidecarRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
            return Result(ModelCodes.InvalidSettings, "模型設定不正確。", "", false);

        var stdin = request.Kind == "probe"
            ? SidecarPayload.Probe(request.Placement, request.Endpoint, request.ModelId)
            : request.ParseJson ?? "";
        if (request.Kind is not ("probe" or "parse"))
            return Result(ModelCodes.Unreachable, "無法連線模型。", "", false);

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = _path,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.Environment["DBSERVICES_MODEL_API_KEY"] = request.ApiKey ?? "";
            using var process = new Process { StartInfo = start };
            if (!process.Start())
                return Result(ModelCodes.Unreachable, "無法連線模型。", "", false);

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteAsync(stdin.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            var finished = Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellationToken));
            var completed = await Task.WhenAny(finished, Task.Delay(_timeout, cancellationToken));
            if (completed != finished)
            {
                TryKill(process);
                return Result(ModelCodes.Timeout, "模型逾時。", "", true);
            }

            await finished;
            var body = stdout.Result;
            if (string.IsNullOrWhiteSpace(body))
                return Result(ModelCodes.Unreachable, "無法連線模型。", body, true);
            return Interpret(body);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
        {
            return Result(ModelCodes.Unreachable, "無法連線模型。", "", true);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static SidecarResponse Interpret(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                && code.GetString() is string text
                && text is ModelCodes.Ok or ModelCodes.InvalidSettings or ModelCodes.Unreachable or ModelCodes.Timeout)
                return Result(text, "", body, true);
        }
        catch (JsonException)
        {
        }

        return Result(ModelCodes.Ok, "", body, true);
    }

    private static SidecarResponse Result(string code, string message, string stdout, bool started) => new()
    {
        Code = code,
        Message = message,
        Stdout = stdout,
        Started = started,
    };
}
