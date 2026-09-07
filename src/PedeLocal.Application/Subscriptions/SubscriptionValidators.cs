using FluentValidation;
using PedeLocal.Contracts.Subscriptions;

namespace PedeLocal.Application.Subscriptions;

public class UpsertSubscriptionRequestValidator : AbstractValidator<UpsertSubscriptionRequest>
{
    public UpsertSubscriptionRequestValidator()
    {
        RuleFor(x => x.MonthlyAmount).GreaterThan(0);
        RuleFor(x => x.BillingDay).InclusiveBetween(1, 28);
        RuleFor(x => x.GraceDays).GreaterThanOrEqualTo(0);
    }
}

public class RegisterPaymentRequestValidator : AbstractValidator<RegisterPaymentRequest>
{
    public RegisterPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(64);
    }
}
