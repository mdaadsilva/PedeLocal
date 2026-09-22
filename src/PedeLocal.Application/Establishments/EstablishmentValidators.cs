using FluentValidation;
using PedeLocal.Contracts.Establishments;

namespace PedeLocal.Application.Establishments;

public class PauseEstablishmentRequestValidator : AbstractValidator<PauseEstablishmentRequest>
{
    public PauseEstablishmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public class CreateEstablishmentRequestValidator : AbstractValidator<CreateEstablishmentRequest>
{
    public CreateEstablishmentRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Document).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.Number).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.AdminPhone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8);
    }
}

public class UpdateEstablishmentRequestValidator : AbstractValidator<UpdateEstablishmentRequest>
{
    public UpdateEstablishmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.Number).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}
