using FluentValidation;

namespace WhatsOrder.Application.Products;

public class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.NameAr).MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DescriptionAr).MaximumLength(1000);
        RuleFor(x => x.Price).InclusiveBetween(0, 100000);
        RuleFor(x => x.DiscountedPrice)
            .InclusiveBetween(0, 100000)
            .LessThan(x => x.Price).WithMessage("Discounted price must be lower than the regular price.")
            .When(x => x.DiscountedPrice.HasValue);
        RuleFor(x => x.StockQuantity).InclusiveBetween(0, 1000000).When(x => x.StockQuantity.HasValue);

        RuleFor(x => x.Variants!.Count).LessThanOrEqualTo(5).WithMessage("A product can have at most 5 variant groups.")
            .When(x => x.Variants is not null);

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Name).NotEmpty().MaximumLength(60);
            variant.RuleFor(v => v.NameAr).MaximumLength(60);
            variant.RuleFor(v => v.Options)
                .NotEmpty().WithMessage("Each variant group needs at least one option.");
            variant.RuleFor(v => v.Options.Count).LessThanOrEqualTo(20)
                .WithMessage("A variant group can have at most 20 options.");
            variant.RuleForEach(v => v.Options).ChildRules(option =>
            {
                option.RuleFor(o => o.Name).NotEmpty().MaximumLength(60);
                option.RuleFor(o => o.NameAr).MaximumLength(60);
                option.RuleFor(o => o.PriceAdjustment).InclusiveBetween(-100000, 100000);
            });
        }).When(x => x.Variants is not null);
    }
}
