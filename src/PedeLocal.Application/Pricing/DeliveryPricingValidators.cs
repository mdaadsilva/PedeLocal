using FluentValidation;
using PedeLocal.Contracts.Pricing;

namespace PedeLocal.Application.Pricing;

public class UpdateDeliveryPricingRequestValidator : AbstractValidator<UpdateDeliveryPricingRequest>
{
    public UpdateDeliveryPricingRequestValidator()
    {
        RuleFor(x => x.UrbanMode).NotEmpty();
        RuleFor(x => x.RuralMode).NotEmpty();
        RuleFor(x => x.UrbanFixedFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UrbanPricePerKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UrbanMinFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RuralFixedFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RuralPricePerKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RuralMinFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxDistanceKm).GreaterThan(0);
    }
}
