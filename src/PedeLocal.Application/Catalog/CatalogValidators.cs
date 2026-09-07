using FluentValidation;
using PedeLocal.Contracts.Catalog;

namespace PedeLocal.Application.Catalog;

public class CreateMenuCategoryRequestValidator : AbstractValidator<CreateMenuCategoryRequest>
{
    public CreateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateMenuCategoryRequestValidator : AbstractValidator<UpdateMenuCategoryRequest>
{
    public UpdateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class ProductOptionRequestValidator : AbstractValidator<ProductOptionRequest>
{
    public ProductOptionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class ProductOptionGroupRequestValidator : AbstractValidator<ProductOptionGroupRequest>
{
    public ProductOptionGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Min).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Max).GreaterThanOrEqualTo(x => x.Min).WithMessage("'Max' deve ser maior ou igual a 'Min'.");
        RuleForEach(x => x.Options).SetValidator(new ProductOptionRequestValidator());
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.MenuCategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.PromotionalPrice).LessThan(x => x.Price)
            .When(x => x.PromotionalPrice.HasValue)
            .WithMessage("O preço promocional deve ser menor que o preço normal.");
        RuleFor(x => x.StockMode).NotEmpty();
        RuleForEach(x => x.OptionGroups).SetValidator(new ProductOptionGroupRequestValidator());
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.MenuCategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.PromotionalPrice).LessThan(x => x.Price)
            .When(x => x.PromotionalPrice.HasValue)
            .WithMessage("O preço promocional deve ser menor que o preço normal.");
        RuleForEach(x => x.OptionGroups).SetValidator(new ProductOptionGroupRequestValidator());
    }
}
