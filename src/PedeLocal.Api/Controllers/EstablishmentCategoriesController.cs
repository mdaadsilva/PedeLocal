using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Establishments;
using PedeLocal.Contracts.Establishments;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/establishment-categories")]
[AllowAnonymous]
public class EstablishmentCategoriesController(EstablishmentService establishmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<EstablishmentCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await establishmentService.GetCategoriesAsync(ct));
}
