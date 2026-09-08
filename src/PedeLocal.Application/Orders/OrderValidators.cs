using FluentValidation;
using PedeLocal.Contracts.Orders;

namespace PedeLocal.Application.Orders;

public class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        // Upper bound is a sanity/abuse guard, not a real business limit — OrderService is still
        // the authority on actual availability/stock; this just rejects obviously-absurd values
        // before they reach it.
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Options).Must(o => o.Count <= 50).WithMessage("Número de adicionais excede o limite permitido.");
    }
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        // AddressId's actual requirement is conditional on OrderType (required for Delivery, ignored
        // for Pickup) — that's a cross-field business rule OrderService.BuildQuoteAsync already
        // enforces (ADDRESS_REQUIRED); duplicating it here as an unconditional NotEmpty would
        // incorrectly reject every valid Pickup order.
        RuleFor(x => x.PaymentMethod).NotEmpty();
        RuleFor(x => x.OrderType).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.ChangeForAmount).GreaterThan(0).When(x => x.ChangeForAmount.HasValue);
        RuleFor(x => x.Items).NotEmpty().WithMessage("O pedido precisa ter ao menos um item.")
            .Must(items => items.Count <= 100).WithMessage("Número de itens excede o limite permitido.");
        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
