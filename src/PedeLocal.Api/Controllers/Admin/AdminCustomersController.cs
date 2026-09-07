using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Admin;
using PedeLocal.Contracts.Admin;
using PedeLocal.Contracts.Common;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminCustomersController(AdminService adminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await adminService.GetCustomersAsync(request, ct));
}
