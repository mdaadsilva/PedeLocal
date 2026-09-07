using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Finance;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Application.Finance;

public class FinanceService(
    IAppDbContext db, EstablishmentAccessGuard accessGuard, DriverAccessGuard driverAccessGuard, INotificationService notificationService)
{
    // --- Merchant ---

    public async Task<List<DriverReceivableResponse>> GetPendingReceivablesAsync(Guid establishmentId, Guid? driverId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        var query = db.DriverReceivables.Where(r =>
            r.EstablishmentId == establishmentId && r.Status == ReceivableStatus.Pending && r.PaymentBatchId == null);

        if (driverId is { } id)
        {
            query = query.Where(r => r.DriverId == id);
        }

        return await query.OrderBy(r => r.CreatedAtUtc).Select(r => ToReceivableResponse(r)).ToListAsync(ct);
    }

    public async Task<DriverPaymentBatchResponse> CreateBatchAsync(Guid establishmentId, CreatePaymentBatchRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        if (request.ReceivableIds.Count == 0)
        {
            throw new AppException("BATCH_EMPTY", "Selecione ao menos um recebível.");
        }

        var receivables = await db.DriverReceivables
            .Where(r => request.ReceivableIds.Contains(r.Id))
            .ToListAsync(ct);

        if (receivables.Count != request.ReceivableIds.Distinct().Count())
        {
            throw new NotFoundException("RECEIVABLE_NOT_FOUND", "Um ou mais recebíveis não foram encontrados.");
        }
        if (receivables.Any(r => r.EstablishmentId != establishmentId || r.DriverId != request.DriverId))
        {
            throw new AppException("RECEIVABLE_MISMATCH", "Todos os recebíveis devem ser do mesmo entregador e estabelecimento.");
        }
        if (receivables.Any(r => r.Status != ReceivableStatus.Pending || r.PaymentBatchId is not null))
        {
            throw new ConflictException("RECEIVABLE_ALREADY_BATCHED", "Um ou mais recebíveis já foram incluídos em outro lote.");
        }

        var batch = new DriverPaymentBatch
        {
            DriverId = request.DriverId,
            EstablishmentId = establishmentId,
            TotalAmount = receivables.Sum(r => r.Amount),
            Status = PaymentBatchStatus.Pending
        };
        db.DriverPaymentBatches.Add(batch);

        foreach (var receivable in receivables)
        {
            receivable.PaymentBatchId = batch.Id;
        }

        await db.SaveChangesAsync(ct);
        return await ToBatchResponseAsync(batch, ct);
    }

    public async Task<DriverPaymentBatchResponse> ReportPaymentAsync(Guid establishmentId, Guid batchId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var batch = await GetOwnedBatchAsync(establishmentId, batchId, ct);

        if (batch.Status == PaymentBatchStatus.Pending)
        {
            batch.Status = PaymentBatchStatus.PaymentReported;
            batch.PaymentReportedAtUtc = DateTime.UtcNow;

            var receivables = await db.DriverReceivables.Where(r => r.PaymentBatchId == batch.Id).ToListAsync(ct);
            foreach (var receivable in receivables)
            {
                receivable.Status = ReceivableStatus.PaymentReported;
            }

            await db.SaveChangesAsync(ct);

            var driverUserId = await db.Drivers.Where(d => d.Id == batch.DriverId).Select(d => d.UserId).SingleOrDefaultAsync(ct);
            if (driverUserId != Guid.Empty)
            {
                await notificationService.NotifyAsync(driverUserId, NotificationEventType.PaymentReported,
                    "Pagamento reportado", $"O estabelecimento reportou o pagamento de R$ {batch.TotalAmount:F2}. Confirme o recebimento.", ct: ct);
            }
        }

        return await ToBatchResponseAsync(batch, ct);
    }

    // --- Driver ---

    public async Task<List<DriverReceivableResponse>> GetMyReceivablesAsync(CancellationToken ct)
    {
        var driver = await driverAccessGuard.GetOwnDriverAsync(ct);
        return await db.DriverReceivables
            .Where(r => r.DriverId == driver.Id)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => ToReceivableResponse(r))
            .ToListAsync(ct);
    }

    public async Task<List<DriverPaymentBatchResponse>> GetMyBatchesAsync(CancellationToken ct)
    {
        var driver = await driverAccessGuard.GetOwnDriverAsync(ct);
        var batches = await db.DriverPaymentBatches
            .Where(b => b.DriverId == driver.Id)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(ct);

        var responses = new List<DriverPaymentBatchResponse>();
        foreach (var batch in batches)
        {
            responses.Add(await ToBatchResponseAsync(batch, ct));
        }
        return responses;
    }

    public async Task<DriverPaymentBatchResponse> ConfirmReceivedAsync(Guid batchId, CancellationToken ct)
    {
        var driver = await driverAccessGuard.GetOwnDriverAsync(ct);
        var batch = await db.DriverPaymentBatches.SingleOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("PAYMENT_BATCH_NOT_FOUND", "Lote de pagamento não encontrado.");

        if (batch.DriverId != driver.Id)
        {
            throw new ForbiddenException("PAYMENT_BATCH_ACCESS_DENIED", "Você não tem acesso a este lote de pagamento.");
        }

        if (batch.Status == PaymentBatchStatus.PaymentReported)
        {
            batch.Status = PaymentBatchStatus.Received;
            batch.ReceivedAtUtc = DateTime.UtcNow;

            var receivables = await db.DriverReceivables.Where(r => r.PaymentBatchId == batch.Id).ToListAsync(ct);
            foreach (var receivable in receivables)
            {
                receivable.Status = ReceivableStatus.Received;
            }

            await db.SaveChangesAsync(ct);
        }
        else if (batch.Status == PaymentBatchStatus.Pending)
        {
            throw new ConflictException("PAYMENT_NOT_REPORTED", "O estabelecimento ainda não reportou este pagamento.");
        }

        return await ToBatchResponseAsync(batch, ct);
    }

    private async Task<DriverPaymentBatch> GetOwnedBatchAsync(Guid establishmentId, Guid batchId, CancellationToken ct) =>
        await db.DriverPaymentBatches.SingleOrDefaultAsync(b => b.Id == batchId && b.EstablishmentId == establishmentId, ct)
        ?? throw new NotFoundException("PAYMENT_BATCH_NOT_FOUND", "Lote de pagamento não encontrado.");

    private async Task<DriverPaymentBatchResponse> ToBatchResponseAsync(DriverPaymentBatch b, CancellationToken ct)
    {
        var receivableIds = await db.DriverReceivables.Where(r => r.PaymentBatchId == b.Id).Select(r => r.Id).ToListAsync(ct);
        return new DriverPaymentBatchResponse(
            b.Id, b.DriverId, b.EstablishmentId, b.TotalAmount, b.Status.ToString(),
            b.CreatedAtUtc, b.PaymentReportedAtUtc, b.ReceivedAtUtc, receivableIds);
    }

    private static DriverReceivableResponse ToReceivableResponse(DriverReceivable r) => new(
        r.Id, r.DeliveryId, r.DriverId, r.EstablishmentId, r.Amount, r.Status.ToString(), r.PaymentBatchId, r.CreatedAtUtc);
}
