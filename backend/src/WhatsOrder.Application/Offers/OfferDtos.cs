using FluentValidation;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Offers;

/// <summary>Owner-side view of an offer.</summary>
public sealed record OfferDto(
    Guid Id,
    string Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    OfferType Type,
    decimal DiscountValue,
    decimal MinimumOrderAmount,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    bool IsRunning,
    DateTime CreatedAt);

public sealed record SaveOfferRequest(
    string Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    OfferType Type,
    decimal DiscountValue,
    decimal MinimumOrderAmount,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive = true);

/// <summary>Customer-facing offer with just enough store context to render a card.</summary>
public sealed record PublicOfferDto(
    Guid Id,
    string Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    OfferType Type,
    decimal DiscountValue,
    decimal MinimumOrderAmount,
    DateTime? EndsAt,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr,
    string? StoreLogoUrl);

public class SaveOfferRequestValidator : AbstractValidator<SaveOfferRequest>
{
    public SaveOfferRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TitleAr).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.DescriptionAr).MaximumLength(300);
        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountValue)
            .InclusiveBetween(1, 100).When(x => x.Type == OfferType.Percentage)
            .WithMessage("Percentage discounts must be between 1 and 100.");
        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).When(x => x.Type == OfferType.FixedAmount)
            .WithMessage("Fixed discounts must be greater than zero.");
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt).When(x => x.EndsAt.HasValue)
            .WithMessage("The end date must be after the start date.");
    }
}

public static class OfferMapping
{
    public static OfferDto ToDto(this Offer offer) => new(
        offer.Id, offer.Title, offer.TitleAr, offer.Description, offer.DescriptionAr,
        offer.Type, offer.DiscountValue, offer.MinimumOrderAmount,
        offer.StartsAt, offer.EndsAt, offer.IsActive,
        offer.IsRunningAt(DateTime.UtcNow), offer.CreatedAt);

    public static PublicOfferDto ToPublicDto(this Offer offer, Store store) => new(
        offer.Id, offer.Title, offer.TitleAr, offer.Description, offer.DescriptionAr,
        offer.Type, offer.DiscountValue, offer.MinimumOrderAmount, offer.EndsAt,
        store.Slug, store.Name, store.NameAr, store.LogoPath);
}
