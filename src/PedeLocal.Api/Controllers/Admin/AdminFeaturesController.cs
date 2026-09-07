using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Admin;
using PedeLocal.Contracts.Admin;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/establishments/{establishmentId:guid}/features")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminFeaturesController(AdminService adminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<EstablishmentFeatureResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid establishmentId, CancellationToken ct) =>
        Ok(await adminService.GetFeaturesAsync(establishmentId, ct));

    [HttpPut("{featureKey}")]
    [ProducesResponseType(typeof(EstablishmentFeatureResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Set(Guid establishmentId, string featureKey, SetFeatureRequest request, CancellationToken ct) =>
        Ok(await adminService.SetFeatureAsync(establishmentId, featureKey, request, ct));
}
