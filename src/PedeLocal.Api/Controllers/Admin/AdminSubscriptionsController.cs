using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Subscriptions;
using PedeLocal.Contracts.Subscriptions;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/establishments/{establishmentId:guid}/subscription")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminSubscriptionsController(SubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(EstablishmentSubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid establishmentId, CancellationToken ct) =>
        Ok(await subscriptionService.GetAsync(establishmentId, ct));

    [HttpPut]
    [ProducesResponseType(typeof(EstablishmentSubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(Guid establishmentId, UpsertSubscriptionRequest request, CancellationToken ct) =>
        Ok(await subscriptionService.UpsertAsync(establishmentId, request, ct));

    [HttpGet("payments")]
    [ProducesResponseType(typeof(List<EstablishmentPaymentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(Guid establishmentId, CancellationToken ct) =>
        Ok(await subscriptionService.GetPaymentsAsync(establishmentId, ct));

    [HttpPost("payments")]
    [ProducesResponseType(typeof(EstablishmentPaymentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterPayment(Guid establishmentId, RegisterPaymentRequest request, CancellationToken ct)
    {
        var response = await subscriptionService.RegisterPaymentAsync(establishmentId, request, ct);
        return CreatedAtAction(nameof(GetPayments), new { establishmentId }, response);
    }
}
