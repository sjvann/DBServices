using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Workbench.Contracts;
using Workbench.Core;

namespace Workbench.Web.Controllers;

public static class ApiResults
{
    public static IActionResult From<T>(OpResult<T> result)
        => result.Ok ? new OkObjectResult(result.Value) : Error(result.Error!);

    public static async Task<IActionResult> FromAsync<T>(Task<OpResult<T>> result)
        => From(await result);

    public static IActionResult Error(ErrorDto error)
    {
        var status = error.Code switch
        {
            "workbench_unavailable" => StatusCodes.Status503ServiceUnavailable,
            "unauthenticated" => StatusCodes.Status401Unauthorized,
            "forbidden" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(error) { StatusCode = status };
    }

    public static string OperatorId(HttpContext context) => (string)context.Items["operatorId"]!;

    public static string SessionId(HttpContext context) => (string)context.Items["sessionId"]!;
}

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly SessionService _sessions;

    public SessionsController(SessionService sessions) => _sessions = sessions;

    [HttpPost]
    public IActionResult Create([FromBody] LoginRequest request)
    {
        var result = _sessions.Login(request.DisplayName, request.Secret);
        if (!result.Ok)
            return ApiResults.Error(result.Error!);
        Response.Cookies.Append("wb_session", result.Value!.SessionId, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
        return Ok(result.Value);
    }

    [HttpDelete]
    public IActionResult Delete()
    {
        _sessions.Logout(ApiResults.SessionId(HttpContext));
        Response.Cookies.Delete("wb_session");
        return Ok();
    }
}

[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly OperatorService _operators;

    public MeController(OperatorService operators) => _operators = operators;

    [HttpGet]
    public IActionResult Get() => ApiResults.From(_operators.Me(ApiResults.OperatorId(HttpContext)));
}

[ApiController]
[Route("api/operators")]
public sealed class OperatorsController : ControllerBase
{
    private readonly OperatorService _operators;

    public OperatorsController(OperatorService operators) => _operators = operators;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_operators.List(ApiResults.OperatorId(HttpContext)));

    [HttpPost]
    public IActionResult Create([FromBody] OperatorWriteRequest request)
        => ApiResults.From(_operators.Create(ApiResults.OperatorId(HttpContext), request));

    [HttpPut("{id}")]
    public IActionResult RejectAdmin(string id, [FromBody] OperatorWriteRequest request)
        => ApiResults.From(_operators.RejectAdminChange(ApiResults.OperatorId(HttpContext), request));

    [HttpPut("{id}/entries")]
    public IActionResult Assign(string id, [FromBody] EntryAssignmentRequest request)
        => ApiResults.From(_operators.Assign(ApiResults.OperatorId(HttpContext), id, request.Entries));
}

[ApiController]
[Route("api/views")]
public sealed class ViewsController : ControllerBase
{
    private readonly ViewService _views;

    public ViewsController(ViewService views) => _views = views;

    [HttpGet]
    public Task<IActionResult> List(CancellationToken cancellationToken)
        => ApiResults.FromAsync(_views.ListAsync(ApiResults.OperatorId(HttpContext), cancellationToken));

    [HttpGet("{id}")]
    public Task<IActionResult> Read(string id, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_views.ReadAsync(ApiResults.OperatorId(HttpContext), id, cancellationToken));

    [HttpPut("{id}")]
    public Task<IActionResult> Save(string id, [FromBody] ViewDocumentDto document, CancellationToken cancellationToken)
    {
        document.Id = id;
        return ApiResults.FromAsync(_views.SaveAsync(ApiResults.OperatorId(HttpContext), document, cancellationToken));
    }

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_views.DeleteAsync(ApiResults.OperatorId(HttpContext), id, cancellationToken));

    [HttpPost("{id}/publish")]
    public Task<IActionResult> Publish(string id, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_views.PublishAsync(ApiResults.OperatorId(HttpContext), id, cancellationToken));

    [HttpPost("{id}/unpublish")]
    public Task<IActionResult> Unpublish(string id, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_views.UnpublishAsync(ApiResults.OperatorId(HttpContext), id, cancellationToken));
}

[ApiController]
[Route("api/styles")]
public sealed class StylesController : ControllerBase
{
    private readonly StyleService _styles;

    public StylesController(StyleService styles) => _styles = styles;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_styles.List(ApiResults.OperatorId(HttpContext)));

    [HttpPost("suggest")]
    public Task<IActionResult> Suggest([FromBody] SuggestRequest request, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_styles.SuggestAsync(ApiResults.OperatorId(HttpContext), request.ViewId, cancellationToken));

    [HttpPost]
    public Task<IActionResult> Accept([FromBody] StyleWriteRequest request, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_styles.AcceptAsync(ApiResults.OperatorId(HttpContext), request, cancellationToken));

    [HttpPost("ignore")]
    public IActionResult Ignore([FromBody] IgnoreStyleRequest request)
        => ApiResults.From(_styles.Ignore(ApiResults.OperatorId(HttpContext), request.StyleId));

    [HttpPost("{id}/move")]
    public IActionResult Move(string id, [FromBody] MoveBlockRequest request)
        => ApiResults.From(_styles.Move(ApiResults.OperatorId(HttpContext), id, request.Index, request.Direction));
}

[ApiController]
[Route("api/utterance")]
public sealed class UtteranceController : ControllerBase
{
    private readonly UtteranceService _utterances;

    public UtteranceController(UtteranceService utterances) => _utterances = utterances;

    [HttpPost]
    public Task<IActionResult> Submit([FromBody] UtteranceRequest request, CancellationToken cancellationToken)
        => ApiResults.FromAsync(_utterances.SubmitAsync(
            ApiResults.OperatorId(HttpContext),
            ApiResults.SessionId(HttpContext),
            request.Text,
            request.StyleId,
            cancellationToken));
}

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly UtteranceService _utterances;
    private readonly StyleService _styles;

    public ReportsController(UtteranceService utterances, StyleService styles)
    {
        _utterances = utterances;
        _styles = styles;
    }

    [HttpGet("current")]
    public IActionResult Current()
        => ApiResults.From(_utterances.Current(ApiResults.OperatorId(HttpContext), ApiResults.SessionId(HttpContext)));

    [HttpPost("leave")]
    public IActionResult Leave()
    {
        _utterances.Leave(ApiResults.SessionId(HttpContext));
        return Ok();
    }

    [HttpPost("style")]
    public IActionResult SaveStyle([FromBody] LayoutWriteRequest request)
        => ApiResults.From(_styles.SaveLayout(ApiResults.OperatorId(HttpContext), ApiResults.SessionId(HttpContext), request));
}

[ApiController]
[Route("api/records")]
public sealed class RecordsController : ControllerBase
{
    private readonly RecordService _records;

    public RecordsController(RecordService records) => _records = records;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_records.List(ApiResults.OperatorId(HttpContext)));

    [HttpPost]
    public IActionResult Save([FromBody] RecordWriteRequest request)
        => ApiResults.From(_records.Save(ApiResults.OperatorId(HttpContext), ApiResults.SessionId(HttpContext), request.Shared));

    [HttpGet("{id}")]
    public IActionResult Open(string id) => ApiResults.From(_records.Open(ApiResults.OperatorId(HttpContext), id));

    [HttpPut("{id}")]
    public IActionResult Share(string id, [FromBody] RecordWriteRequest request)
        => ApiResults.From(_records.SetShared(ApiResults.OperatorId(HttpContext), id, request.Shared));

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => ApiResults.From(_records.Delete(ApiResults.OperatorId(HttpContext), id));
}

[ApiController]
[Route("api/export")]
public sealed class ExportController : ControllerBase
{
    private readonly ExportService _export;

    public ExportController(ExportService export) => _export = export;

    [HttpGet("pdf")]
    public IActionResult Pdf([FromQuery] string? recordId) => FileOf(recordId);

    [HttpGet("print")]
    public IActionResult Print([FromQuery] string? recordId) => FileOf(recordId);

    private IActionResult FileOf(string? recordId)
    {
        var result = _export.Pdf(ApiResults.OperatorId(HttpContext), ApiResults.SessionId(HttpContext), recordId);
        if (!result.Ok)
            return ApiResults.Error(result.Error!);
        return File(result.Value!, "application/pdf");
    }
}
