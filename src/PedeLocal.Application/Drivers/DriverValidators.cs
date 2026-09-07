using FluentValidation;
using PedeLocal.Contracts.Drivers;

namespace PedeLocal.Application.Drivers;

public class RejectDriverRequestValidator : AbstractValidator<RejectDriverRequest>
{
    public RejectDriverRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
