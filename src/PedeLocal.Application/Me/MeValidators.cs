using FluentValidation;
using PedeLocal.Contracts.Me;

namespace PedeLocal.Application.Me;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
    }
}

public class UpdateFcmTokenRequestValidator : AbstractValidator<UpdateFcmTokenRequest>
{
    public UpdateFcmTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}
