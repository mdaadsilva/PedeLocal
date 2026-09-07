using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Cities;
using PedeLocal.Contracts.Cities;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/cities")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminCitiesController(CityService cityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await cityService.GetAllAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(CityResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateCityRequest request, CancellationToken ct)
    {
        var response = await cityService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), new { }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdateCityRequest request, CancellationToken ct) =>
        Ok(await cityService.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(CityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, UpdateCityStatusRequest request, CancellationToken ct) =>
        Ok(await cityService.SetStatusAsync(id, request, ct));
}
