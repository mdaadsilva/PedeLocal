using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Finance;
using PedeLocal.Contracts.Finance;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Driver;

[ApiController]
[Route("api/driver")]
[Authorize(Roles = RoleNames.Driver)]
public class DriverFinanceController(FinanceService financeService) : ControllerBase
{
    [HttpGet("receivables")]
    [ProducesResponseType(typeof(List<DriverReceivableResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReceivables(CancellationToken ct) => Ok(await financeService.GetMyReceivablesAsync(ct));

    [HttpGet("payment-batches")]
    [ProducesResponseType(typeof(List<DriverPaymentBatchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBatches(CancellationToken ct) => Ok(await financeService.GetMyBatchesAsync(ct));

    [HttpPost("payment-batches/{batchId:guid}/confirm")]
    [ProducesResponseType(typeof(DriverPaymentBatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmReceived(Guid batchId, CancellationToken ct) => Ok(await financeService.ConfirmReceivedAsync(batchId, ct));
}
