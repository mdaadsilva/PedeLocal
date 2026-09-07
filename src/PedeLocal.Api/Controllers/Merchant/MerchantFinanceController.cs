using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Finance;
using PedeLocal.Contracts.Finance;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}/driver-payments")]
[Authorize]
public class MerchantFinanceController(FinanceService financeService) : ControllerBase
{
    [HttpGet("receivables/pending")]
    [ProducesResponseType(typeof(List<DriverReceivableResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReceivables(Guid establishmentId, [FromQuery] Guid? driverId, CancellationToken ct) =>
        Ok(await financeService.GetPendingReceivablesAsync(establishmentId, driverId, ct));

    [HttpPost("batches")]
    [ProducesResponseType(typeof(DriverPaymentBatchResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBatch(Guid establishmentId, CreatePaymentBatchRequest request, CancellationToken ct)
    {
        var response = await financeService.CreateBatchAsync(establishmentId, request, ct);
        return CreatedAtAction(nameof(GetPendingReceivables), new { establishmentId }, response);
    }

    [HttpPost("batches/{batchId:guid}/report")]
    [ProducesResponseType(typeof(DriverPaymentBatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReportPayment(Guid establishmentId, Guid batchId, CancellationToken ct) =>
        Ok(await financeService.ReportPaymentAsync(establishmentId, batchId, ct));
}
