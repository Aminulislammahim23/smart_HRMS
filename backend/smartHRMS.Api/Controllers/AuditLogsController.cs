using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smartHRMS.Api.Auth;
using smartHRMS.Application.Common.Models;
using smartHRMS.Application.Features.Audit;

namespace smartHRMS.Api.Controllers;

/// <summary>Read-only audit log (sign-ins, user changes, leave decisions, payroll actions). Admin only.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/audit-logs")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>Newest first. Filters: entityType, entityId, action; take (1–500, default 100).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AuditLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> Search([FromQuery] AuditLogQueryDto query, CancellationToken cancellationToken)
    {
        var entries = await _auditLogService.SearchAsync(query, cancellationToken);
        return Ok(ApiResponse<List<AuditLogDto>>.Ok(entries, "Audit log retrieved successfully."));
    }
}
