using FluentValidation;
using PedeLocal.Contracts.Inventory;

namespace PedeLocal.Application.Inventory;

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        // Upper bound guards against absurd/overflow-adjacent values reaching the stock arithmetic
        // in InventoryService — no legitimate manual adjustment needs anywhere near this.
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
