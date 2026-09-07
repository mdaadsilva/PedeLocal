using FluentValidation;
using PedeLocal.Contracts.Addresses;

namespace PedeLocal.Application.Addresses;

public class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);

        When(x => string.Equals(x.Type, "Rural", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Reference).NotEmpty().WithMessage("Referência é obrigatória para endereço rural.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Street).NotEmpty().WithMessage("Rua é obrigatória para endereço urbano.");
            RuleFor(x => x.Number).NotEmpty().WithMessage("Número é obrigatório para endereço urbano.");
            RuleFor(x => x.District).NotEmpty().WithMessage("Bairro é obrigatório para endereço urbano.");
        });
    }
}

public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);

        When(x => string.Equals(x.Type, "Rural", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.Reference).NotEmpty().WithMessage("Referência é obrigatória para endereço rural.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Street).NotEmpty().WithMessage("Rua é obrigatória para endereço urbano.");
            RuleFor(x => x.Number).NotEmpty().WithMessage("Número é obrigatório para endereço urbano.");
            RuleFor(x => x.District).NotEmpty().WithMessage("Bairro é obrigatório para endereço urbano.");
        });
    }
}
