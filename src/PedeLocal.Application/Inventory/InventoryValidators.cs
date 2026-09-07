using FluentValidation;
using PedeLocal.Contracts.Inventory;

namespace PedeLocal.Application.Inventory;

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
