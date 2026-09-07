using FluentValidation;
using PedeLocal.Contracts.Finance;

namespace PedeLocal.Application.Finance;

public class CreatePaymentBatchRequestValidator : AbstractValidator<CreatePaymentBatchRequest>
{
    public CreatePaymentBatchRequestValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.ReceivableIds).NotEmpty();
    }
}
