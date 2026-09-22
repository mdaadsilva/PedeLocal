using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Establishments;
using PedeLocal.Contracts.Establishments;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}")]
[Authorize]
public class MerchantEstablishmentController(EstablishmentService establishmentService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("pause")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(Guid establishmentId, PauseEstablishmentRequest request, CancellationToken ct) =>
        Ok(await establishmentService.PauseAsync(establishmentId, request, currentUser.UserId, ct));

    [HttpPost("resume")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(Guid establishmentId, CancellationToken ct) =>
        Ok(await establishmentService.ResumeAsync(establishmentId, currentUser.UserId, ct));
}
