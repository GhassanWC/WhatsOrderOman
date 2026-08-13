using FluentValidation;

namespace WhatsOrder.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, string? NameAr, int SortOrder, bool IsActive, int ProductsCount);

public sealed record SaveCategoryRequest(string Name, string? NameAr, int SortOrder, bool IsActive);

public class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.NameAr).MaximumLength(80);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
    }
}
