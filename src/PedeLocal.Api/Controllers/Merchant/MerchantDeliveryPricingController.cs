using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Pricing;
using PedeLocal.Contracts.Pricing;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}/delivery-pricing")]
[Authorize]
public class MerchantDeliveryPricingController(DeliveryPricingService pricingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DeliveryPricingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid establishmentId, CancellationToken ct) =>
        Ok(await pricingService.GetAsync(establishmentId, ct));

    [HttpPut]
    [ProducesResponseType(typeof(DeliveryPricingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid establishmentId, UpdateDeliveryPricingRequest request, CancellationToken ct) =>
        Ok(await pricingService.UpdateAsync(establishmentId, request, ct));
}
