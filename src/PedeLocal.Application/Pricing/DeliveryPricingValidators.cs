using FluentValidation;
using PedeLocal.Contracts.Pricing;

namespace PedeLocal.Application.Pricing;

public class UpdateDeliveryPricingRequestValidator : AbstractValidator<UpdateDeliveryPricingRequest>
{
    public UpdateDeliveryPricingRequestValidator()
    {
        RuleFor(x => x.DeliveryFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DeliveryRadiusKm).GreaterThan(0);
    }
}
