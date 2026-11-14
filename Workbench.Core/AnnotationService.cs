using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class AnnotationService
{
    private readonly Profiles _profiles;
    private readonly IAnnotationCatalog _catalog;

    public AnnotationService(Profiles profiles, IAnnotationCatalog catalog)
    {
        _profiles = profiles;
        _catalog = catalog;
    }

    public async Task<OpResult<AnnotationPackageDto?>> DraftAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto?>.Fail(error);
        try
        {
            return OpResult<AnnotationPackageDto?>.Success(await _catalog.DraftAsync(operatorId, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto?>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationPackageDto>> ImportAsync(
        string operatorId,
        string? fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto>.Fail(error);

        var parsed = DictionaryWorkbook.Read(fileName, content);
        if (!parsed.Ok)
            return OpResult<AnnotationPackageDto>.Fail(parsed.Error!);

        try
        {
            var written = await _catalog.ImportAsync(parsed.Value!, operatorId, cancellationToken);
            if (!written.Ok)
                return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text(written.Code, written.Message));
            return OpResult<AnnotationPackageDto>.Success(written.Package!);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationPackageDto>> SaveAsync(
        string operatorId,
        AnnotationPackageDto? package,
        CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto>.Fail(error);
        if (package is null)
            return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));

        try
        {
            var written = await _catalog.SaveAsync(package, operatorId, cancellationToken);
            if (!written.Ok)
            {
                var message = written.Code == "link_outside" ? WorkbenchMessages.LinkOutside : written.Message;
                return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text(written.Code, message));
            }

            return OpResult<AnnotationPackageDto>.Success(written.Package!);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationActiveDto>> ActiveAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationActiveDto>.Fail(error);
        try
        {
            return OpResult<AnnotationActiveDto>.Success(await _catalog.ActiveAsync(operatorId, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationActiveDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationActiveDto>> DesignCatalogAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Analyst(operatorId) is { } error)
            return OpResult<AnnotationActiveDto>.Fail(error);
        try
        {
            return OpResult<AnnotationActiveDto>.Success(await _catalog.ActiveAsync(operatorId, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            return OpResult<AnnotationActiveDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<string?>> DraftDbmlAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<string?>.Fail(error);
        try
        {
            return OpResult<string?>.Success(await _catalog.DraftDbmlAsync(operatorId, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<string?>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationPackageDto>> ConfirmAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto>.Fail(error);
        try
        {
            var written = await _catalog.ConfirmAsync(operatorId, cancellationToken);
            if (!written.Ok)
                return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text(written.Code, written.Message));
            return OpResult<AnnotationPackageDto>.Success(written.Package!);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationPackageDto?>> AbandonAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto?>.Fail(error);
        try
        {
            var written = await _catalog.AbandonAsync(operatorId, cancellationToken);
            if (!written.Ok)
                return OpResult<AnnotationPackageDto?>.Fail(WorkbenchMessages.Text(written.Code, written.Message));
            return OpResult<AnnotationPackageDto?>.Success(null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto?>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    public async Task<OpResult<AnnotationPackageDto>> ReadStructureAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<AnnotationPackageDto>.Fail(error);

        try
        {
            var written = await _catalog.ReadStructureAsync(operatorId, cancellationToken);
            if (!written.Ok)
                return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text(written.Code, written.Message));
            return OpResult<AnnotationPackageDto>.Success(written.Package!);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("execution_failed", "沒有保存。"));
        }
    }

    private ErrorDto? Closed(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.Has(EntryNames.Governance) ? null : WorkbenchMessages.Forbidden();
    }

    private ErrorDto? Analyst(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.Has(EntryNames.Analyst) ? null : WorkbenchMessages.Forbidden();
    }
}
