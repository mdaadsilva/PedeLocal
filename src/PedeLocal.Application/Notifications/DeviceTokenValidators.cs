using FluentValidation;
using PedeLocal.Contracts.Notifications;

namespace PedeLocal.Application.Notifications;

public class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.Platform).NotEmpty();
    }
}

public class RemoveDeviceTokenRequestValidator : AbstractValidator<RemoveDeviceTokenRequest>
{
    public RemoveDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4096);
    }
}
