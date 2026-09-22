using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Pricing;
using PedeLocal.Domain.Pricing;

namespace PedeLocal.Application.Pricing;

public class DeliveryPricingService(IAppDbContext db, EstablishmentAccessGuard accessGuard)
{
    public async Task<DeliveryPricingResponse> GetAsync(Guid establishmentId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        var settings = await db.DeliveryPricingSettings.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct);
        settings ??= new DeliveryPricingSettings { EstablishmentId = establishmentId };
        return ToResponse(settings);
    }

    public async Task<DeliveryPricingResponse> UpdateAsync(Guid establishmentId, UpdateDeliveryPricingRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        if (!await db.Establishments.AnyAsync(e => e.Id == establishmentId, ct))
        {
            throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");
        }

        var settings = await db.DeliveryPricingSettings.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct);
        if (settings is null)
        {
            settings = new DeliveryPricingSettings { EstablishmentId = establishmentId };
            db.DeliveryPricingSettings.Add(settings);
        }

        settings.DeliveryFee = request.DeliveryFee;
        settings.DeliveryRadiusKm = request.DeliveryRadiusKm;

        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    private static DeliveryPricingResponse ToResponse(DeliveryPricingSettings s) => new(
        s.EstablishmentId, s.DeliveryFee, s.DeliveryRadiusKm);
}
