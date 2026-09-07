using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Admin;
using PedeLocal.Contracts.Admin;
using PedeLocal.Contracts.Common;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminAuditController(AdminService adminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType, [FromQuery] Guid? actorUserId, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await adminService.GetAuditLogsAsync(entityType, actorUserId, request, ct));
}
