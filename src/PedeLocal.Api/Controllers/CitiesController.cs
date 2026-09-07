using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Cities;
using PedeLocal.Contracts.Cities;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/cities")]
[AllowAnonymous]
public class CitiesController(CityService cityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken ct) => Ok(await cityService.GetActiveAsync(ct));
}
