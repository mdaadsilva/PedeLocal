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

public class SubmitDriverLicenseRequestValidator : AbstractValidator<SubmitDriverLicenseRequest>
{
    public SubmitDriverLicenseRequestValidator()
    {
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(8);
        // Submitting an already-expired CNH would be immediately useless — reject at submission
        // time instead of letting it silently sit there until the driver notices they can't go online.
        RuleFor(x => x.ExpiresAt).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("A CNH informada já está vencida.");
        RuleFor(x => x.DocumentImageUrl).MaximumLength(2048);
    }
}

public class SubmitDriverVehicleRequestValidator : AbstractValidator<SubmitDriverVehicleRequest>
{
    public SubmitDriverVehicleRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Model).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Plate).MaximumLength(16);
        RuleFor(x => x.DocumentImageUrl).MaximumLength(2048);
    }
}
