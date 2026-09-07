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

        if (!Enum.TryParse<DeliveryFeeMode>(request.UrbanMode, ignoreCase: true, out var urbanMode) ||
            !Enum.TryParse<DeliveryFeeMode>(request.RuralMode, ignoreCase: true, out var ruralMode))
        {
            throw new AppException("INVALID_DELIVERY_FEE_MODE", "Modo de cobrança inválido.");
        }

        var settings = await db.DeliveryPricingSettings.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct);
        if (settings is null)
        {
            settings = new DeliveryPricingSettings { EstablishmentId = establishmentId };
            db.DeliveryPricingSettings.Add(settings);
        }

        settings.UrbanMode = urbanMode;
        settings.UrbanFixedFee = request.UrbanFixedFee;
        settings.UrbanPricePerKm = request.UrbanPricePerKm;
        settings.UrbanMinFee = request.UrbanMinFee;
        settings.RuralMode = ruralMode;
        settings.RuralFixedFee = request.RuralFixedFee;
        settings.RuralPricePerKm = request.RuralPricePerKm;
        settings.RuralMinFee = request.RuralMinFee;
        settings.MaxDistanceKm = request.MaxDistanceKm;

        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    private static DeliveryPricingResponse ToResponse(DeliveryPricingSettings s) => new(
        s.EstablishmentId, s.UrbanMode.ToString(), s.UrbanFixedFee, s.UrbanPricePerKm, s.UrbanMinFee,
        s.RuralMode.ToString(), s.RuralFixedFee, s.RuralPricePerKm, s.RuralMinFee, s.MaxDistanceKm);
}
