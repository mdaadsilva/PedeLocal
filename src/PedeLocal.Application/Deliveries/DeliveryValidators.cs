using FluentValidation;
using PedeLocal.Contracts.Deliveries;

namespace PedeLocal.Application.Deliveries;

public class ConfirmDeliveryRequestValidator : AbstractValidator<ConfirmDeliveryRequest>
{
    public ConfirmDeliveryRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("Código deve conter 6 dígitos.");
    }
}
