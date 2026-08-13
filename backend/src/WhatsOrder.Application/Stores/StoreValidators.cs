using FluentValidation;
using WhatsOrder.Application.Common;

namespace WhatsOrder.Application.Stores;

public class CreateStoreRequestValidator : AbstractValidator<CreateStoreRequest>
{
    public CreateStoreRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.NameAr).MaximumLength(100);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .Must(s => Slugs.IsValid(s))
            .WithMessage("Slug must be 3–40 characters of lowercase letters, numbers and hyphens, and not a reserved word.");
        // Optional — ordering works without WhatsApp; validated only when provided.
        RuleFor(x => x.WhatsAppNumber)
            .Must(PhoneNumber.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber))
            .WithMessage("Enter a valid WhatsApp number, e.g. 91234567 or +96891234567.");
        RuleFor(x => x.InstagramHandle).MaximumLength(50)
            .Matches(@"^[A-Za-z0-9._]*$").WithMessage("Instagram username can contain letters, numbers, dots and underscores.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.LocationText).MaximumLength(200);
        RuleFor(x => x.Governorate).MaximumLength(50);
        RuleFor(x => x.Wilayat).MaximumLength(50);
    }
}

public class UpdateStoreRequestValidator : AbstractValidator<UpdateStoreRequest>
{
    public UpdateStoreRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.NameAr).MaximumLength(100);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .Must(s => Slugs.IsValid(s))
            .WithMessage("Slug must be 3–40 characters of lowercase letters, numbers and hyphens, and not a reserved word.");
        // Optional — ordering works without WhatsApp; validated only when provided.
        RuleFor(x => x.WhatsAppNumber)
            .Must(PhoneNumber.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber))
            .WithMessage("Enter a valid WhatsApp number, e.g. 91234567 or +96891234567.");
        RuleFor(x => x.InstagramHandle).MaximumLength(50)
            .Matches(@"^[A-Za-z0-9._]*$").WithMessage("Instagram username can contain letters, numbers, dots and underscores.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DescriptionAr).MaximumLength(500);
        RuleFor(x => x.LocationText).MaximumLength(200);
        RuleFor(x => x.Governorate).MaximumLength(50);
        RuleFor(x => x.Wilayat).MaximumLength(50);
    }
}

public class UpdateStoreSettingsRequestValidator : AbstractValidator<UpdateStoreSettingsRequest>
{
    public UpdateStoreSettingsRequestValidator()
    {
        RuleFor(x => x.DeliveryFee).InclusiveBetween(0, 100);
        RuleFor(x => x.MinimumOrderAmount).InclusiveBetween(0, 10000);
        RuleFor(x => x.OpeningHours).NotNull();
        RuleForEach(x => x.OpeningHours).ChildRules(h =>
        {
            h.RuleFor(i => i.Day).InclusiveBetween(0, 6);
            h.RuleFor(i => i.Open).Must(v => TimeOnly.TryParse(v, out _)).WithMessage("Invalid opening time.");
            h.RuleFor(i => i.Close).Must(v => TimeOnly.TryParse(v, out _)).WithMessage("Invalid closing time.");
        });
        RuleFor(x => x.DefaultLanguage).Must(l => l is "en" or "ar").WithMessage("Language must be 'en' or 'ar'.");
        RuleFor(x => x)
            .Must(x => x.DeliveryEnabled || x.PickupEnabled)
            .WithMessage("Enable at least one fulfillment method (pickup or delivery).");
    }
}
