using FluentValidation;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Orders;

public class CreatePublicOrderRequestValidator : AbstractValidator<CreatePublicOrderRequest>
{
    public CreatePublicOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.CustomerPhone)
            .NotEmpty()
            .Must(PhoneNumber.IsValid)
            .WithMessage("Enter a valid phone number, e.g. 91234567 or +96891234567.");
        RuleFor(x => x.DeliveryAddress)
            .NotEmpty().WithMessage("Delivery address is required for delivery orders.")
            .When(x => x.FulfillmentMethod == FulfillmentMethod.Delivery);
        RuleFor(x => x.DeliveryAddress).MaximumLength(300);
        RuleFor(x => x.GoogleMapsUrl).MaximumLength(300)
            .Must(url => string.IsNullOrWhiteSpace(url)
                         || Uri.TryCreate(url, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Location link must be a valid URL.");
        RuleFor(x => x.PreferredTime).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.Items).NotEmpty().WithMessage("The cart is empty.");
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(50).When(x => x.Items is not null);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 99);
            item.RuleFor(i => i.OptionIds!.Count).LessThanOrEqualTo(10).When(i => i.OptionIds is not null);
        });
    }
}

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.EstimatedMinutes).InclusiveBetween(5, 480)
            .When(x => x.EstimatedMinutes is not null)
            .WithMessage("Estimated time must be between 5 minutes and 8 hours.");
    }
}
