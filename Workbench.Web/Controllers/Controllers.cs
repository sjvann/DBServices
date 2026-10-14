using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Workbench.Contracts;
using Workbench.Core;

namespace Workbench.Web.Controllers;

public static class ApiResults
{
    public static IActionResult From<T>(OpResult<T> result)
        => result.Ok ? new OkObjectResult(result.Value) : Error(result.Error!);

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
    private readonly RoleRailService _roles;

    public MeController(RoleRailService roles) => _roles = roles;

    [HttpGet]
    public IActionResult Get() => ApiResults.From(_roles.Me(ApiResults.OperatorId(HttpContext)));
}

[ApiController]
[Route("api/operators")]
public sealed class OperatorsController : ControllerBase
{
    private readonly AccountService _accounts;

    public OperatorsController(AccountService accounts) => _accounts = accounts;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_accounts.List(ApiResults.OperatorId(HttpContext)));

    [HttpPost]
    public IActionResult Create([FromBody] OperatorWriteRequest request)
        => ApiResults.From(_accounts.Create(ApiResults.OperatorId(HttpContext), request.DisplayName, request.Secret));
}

[ApiController]
[Route("api/groups")]
public sealed class GroupsController : ControllerBase
{
    private readonly RoleRailService _roles;

    public GroupsController(RoleRailService roles) => _roles = roles;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_roles.ListGroups(ApiResults.OperatorId(HttpContext)));

    [HttpPost]
    public IActionResult Create([FromBody] GroupWriteRequest request)
        => ApiResults.From(_roles.AddGroup(ApiResults.OperatorId(HttpContext), request.Name));

    [HttpPut("{id}")]
    public IActionResult Rename(string id, [FromBody] GroupWriteRequest request)
        => ApiResults.From(_roles.RenameGroup(ApiResults.OperatorId(HttpContext), id, request.Name));

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => ApiResults.From(_roles.DeleteGroup(ApiResults.OperatorId(HttpContext), id));
}

[ApiController]
[Route("api/assignments")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly RoleRailService _roles;

    public AssignmentsController(RoleRailService roles) => _roles = roles;

    [HttpGet("{operatorId}")]
    public IActionResult Get(string operatorId) => ApiResults.From(_roles.GetAssignments(ApiResults.OperatorId(HttpContext), operatorId));

    [HttpPut("{operatorId}")]
    public IActionResult Save(string operatorId, [FromBody] AssignmentDto request)
        => ApiResults.From(_roles.SaveAssignments(ApiResults.OperatorId(HttpContext), operatorId, request));
}

[ApiController]
[Route("api/connections")]
public sealed class ConnectionsController : ControllerBase
{
    private readonly LivePreviewService _preview;

    public ConnectionsController(LivePreviewService preview) => _preview = preview;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string surface, CancellationToken cancellationToken)
    {
        var result = await _preview.ConnectionsAsync(ApiResults.OperatorId(HttpContext), surface, cancellationToken);
        if (!result.Ok)
            return ApiResults.Error(result.Error!);
        if (!result.Value!.Ok)
            return ApiResults.Error(new ErrorDto { Code = result.Value.ErrorCode ?? "connection_failed", Message = result.Value.ErrorMessage ?? "" });
        return Ok(result.Value.Connections);
    }
}

[ApiController]
[Route("api/structure")]
public sealed class StructureController : ControllerBase
{
    private readonly LivePreviewService _preview;

    public StructureController(LivePreviewService preview) => _preview = preview;

    [HttpGet("tables")]
    public async Task<IActionResult> Tables([FromQuery] string surface, [FromQuery] string connection, CancellationToken cancellationToken)
        => ApiResults.From(await _preview.TablesAsync(ApiResults.OperatorId(HttpContext), surface, connection, cancellationToken));

    [HttpGet("fields")]
    public async Task<IActionResult> Fields([FromQuery] string surface, [FromQuery] string connection, [FromQuery] string table, CancellationToken cancellationToken)
        => ApiResults.From(await _preview.FieldsAsync(ApiResults.OperatorId(HttpContext), surface, connection, table, cancellationToken));
}

[ApiController]
[Route("api/plans")]
public sealed class PlansController : ControllerBase
{
    private readonly LivePreviewService _preview;

    public PlansController(LivePreviewService preview) => _preview = preview;

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] PlanRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _preview.ConfirmQueryAsync(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), request, cancellationToken));

    [HttpPost("count")]
    public async Task<IActionResult> Count([FromBody] PlanRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _preview.ConfirmCountAsync(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), request, cancellationToken));
}

[ApiController]
[Route("api/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly TemplateLibraryService _templates;

    public TemplatesController(TemplateLibraryService templates) => _templates = templates;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_templates.List(ApiResults.OperatorId(HttpContext)));

    [HttpGet("{id}")]
    public IActionResult Get(string id) => ApiResults.From(_templates.Get(ApiResults.OperatorId(HttpContext), id));

    [HttpPost]
    public IActionResult Create([FromBody] TemplateDto template)
        => ApiResults.From(_templates.Save(ApiResults.OperatorId(HttpContext), template));

    [HttpPut("{id}")]
    public IActionResult Save(string id, [FromBody] TemplateDto template)
    {
        template.Id = id;
        return ApiResults.From(_templates.Save(ApiResults.OperatorId(HttpContext), template));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => ApiResults.From(_templates.Delete(ApiResults.OperatorId(HttpContext), id));

    [HttpPost("{id}/preview")]
    public async Task<IActionResult> Preview(string id, [FromBody] PlanRequest request, CancellationToken cancellationToken)
    {
        var parameters = request.Filters.ToDictionary(item => item.Key, item => item.Value ?? "", StringComparer.Ordinal);
        return ApiResults.From(await _templates.PreviewAsync(
            ApiResults.SessionId(HttpContext),
            ApiResults.OperatorId(HttpContext),
            id,
            request.Surface ?? "",
            request.Connection ?? "",
            parameters,
            cancellationToken));
    }
}

[ApiController]
[Route("api/suggestions")]
public sealed class SuggestionsController : ControllerBase
{
    private readonly SuggestionService _suggestions;

    public SuggestionsController(SuggestionService suggestions) => _suggestions = suggestions;

    [HttpPost]
    public async Task<IActionResult> Propose([FromBody] SuggestionRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _suggestions.ProposeAsync(ApiResults.OperatorId(HttpContext), request, cancellationToken));

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] SuggestionRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _suggestions.AcceptAsync(ApiResults.OperatorId(HttpContext), request, cancellationToken));
}

[ApiController]
[Route("api/intent")]
public sealed class IntentController : ControllerBase
{
    private readonly IntentService _intent;

    public IntentController(IntentService intent) => _intent = intent;

    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] IntentParseRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _intent.ParseAsync(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), request.Utterance, request.Surface, cancellationToken));

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _intent.ConfirmAsync(ApiResults.SessionId(HttpContext), request.PlanId ?? "", cancellationToken));
}

[ApiController]
[Route("api/favorites")]
public sealed class FavoritesController : ControllerBase
{
    private readonly FavoriteService _favorites;

    public FavoritesController(FavoriteService favorites) => _favorites = favorites;

    [HttpGet]
    public IActionResult List() => ApiResults.From(_favorites.List(ApiResults.OperatorId(HttpContext)));

    [HttpPost]
    public IActionResult Create([FromBody] FavoriteWriteRequest request)
        => ApiResults.From(_favorites.Save(ApiResults.OperatorId(HttpContext), request));

    [HttpPost("{id}/open")]
    public IActionResult Open(string id) => ApiResults.From(_favorites.Open(ApiResults.OperatorId(HttpContext), id));
}

[ApiController]
[Route("api/preview")]
public sealed class PreviewController : ControllerBase
{
    private readonly SnapshotExportService _export;

    public PreviewController(SnapshotExportService export) => _export = export;

    [HttpGet("{id}")]
    public IActionResult Get(string id) => ApiResults.From(_export.Get(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), id));

    [HttpGet("{id}/pdf")]
    public IActionResult Pdf(string id)
    {
        var result = _export.Pdf(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), id);
        return result.Ok ? File(result.Value!, "application/pdf") : ApiResults.Error(result.Error!);
    }

    [HttpGet("{id}/csv")]
    public IActionResult Csv(string id)
    {
        var result = _export.Csv(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), id);
        return result.Ok ? Content(result.Value!, "text/csv") : ApiResults.Error(result.Error!);
    }

    [HttpGet("{id}/print")]
    public IActionResult Print(string id)
    {
        var result = _export.Print(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), id);
        return result.Ok ? Content(result.Value!, "text/html") : ApiResults.Error(result.Error!);
    }
}

[ApiController]
[Route("api/keyword-rules")]
public sealed class KeywordRulesController : ControllerBase
{
    private readonly KeywordService _keywords;

    public KeywordRulesController(KeywordService keywords) => _keywords = keywords;

    [HttpPost]
    public IActionResult Create([FromBody] KeywordWriteRequest request)
        => ApiResults.From(_keywords.AddHuman(ApiResults.OperatorId(HttpContext), request));

    [HttpPost("from-confirm")]
    public IActionResult FromConfirm([FromBody] KeywordWriteRequest request)
        => ApiResults.From(_keywords.AddFromConfirm(ApiResults.SessionId(HttpContext), ApiResults.OperatorId(HttpContext), request));
}

[ApiController]
[Route("api/model")]
public sealed class ModelController : ControllerBase
{
    private readonly ModelSettingsService _model;

    public ModelController(ModelSettingsService model) => _model = model;

    [HttpGet]
    public IActionResult Get() => ApiResults.From(_model.Read(ApiResults.OperatorId(HttpContext)));

    [HttpPut]
    public IActionResult Save([FromBody] ModelWriteRequest request)
        => ApiResults.From(_model.Save(ApiResults.OperatorId(HttpContext), request));

    [HttpPost("probe")]
    public async Task<IActionResult> Probe([FromBody] ProbeRequest request, CancellationToken cancellationToken)
        => ApiResults.From(await _model.ProbeAsync(ApiResults.OperatorId(HttpContext), request, cancellationToken));
}
