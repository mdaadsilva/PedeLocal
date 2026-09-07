using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Establishments;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Establishments;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/establishments")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminEstablishmentsController(EstablishmentService establishmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EstablishmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await establishmentService.GetAllAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await establishmentService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateEstablishmentRequest request, CancellationToken ct)
    {
        var response = await establishmentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdateEstablishmentRequest request, CancellationToken ct) =>
        Ok(await establishmentService.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, UpdateEstablishmentStatusRequest request, CancellationToken ct) =>
        Ok(await establishmentService.SetStatusAsync(id, request, ct));
}
