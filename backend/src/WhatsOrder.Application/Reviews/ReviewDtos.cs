using FluentValidation;

namespace WhatsOrder.Application.Reviews;

public sealed record CreateReviewRequest(int Rating, string? Comment);

public sealed record ReviewDto(
    Guid Id, Guid OrderId, int Rating, string? Comment, string ReviewerName, DateTime CreatedAt);

public sealed record StoreReviewsPage(
    IReadOnlyList<ReviewDto> Items, int Total, int Page, int PageSize,
    double? AverageRating, int ReviewsCount);

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}
