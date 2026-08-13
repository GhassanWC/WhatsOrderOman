using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsOrder.Application.Marketplace;
using WhatsOrder.Application.Offers;
using WhatsOrder.Application.Recommendations;

namespace WhatsOrder.Api.Controllers;

/// <summary>
/// Cross-store marketplace surface: directory, unified search, homepage sections and
/// offers. Anonymous; signed-in callers automatically get personalized results because
/// ICurrentUser flows into the services.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class MarketplaceController(MarketplaceService marketplace) : ControllerBase
{
    [HttpGet("stores")]
    public async Task<ActionResult<StoresPage>> Stores(
        [FromQuery] string? search,
        [FromQuery] string? governorate,
        [FromQuery] bool? delivery,
        [FromQuery] bool? pickup,
        [FromQuery] bool? openNow,
        [FromQuery] bool? hasOffers,
        [FromQuery] double? minRating,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default) =>
        Ok(await marketplace.GetStoresAsync(new StoreDirectoryQuery(
            search, governorate, delivery, pickup, openNow, hasOffers, minRating,
            sort, page, pageSize), ct));

    [HttpGet("search")]
    public async Task<ActionResult<SearchResultsDto>> Search(
        [FromQuery] string q, CancellationToken ct = default) =>
        Ok(await marketplace.SearchAsync(q ?? "", ct));

    [HttpGet("search/suggest")]
    public async Task<ActionResult<SearchSuggestionsDto>> Suggest(
        [FromQuery] string? q, CancellationToken ct = default) =>
        Ok(await marketplace.SuggestAsync(q, ct));

    [HttpGet("home")]
    public async Task<ActionResult<MarketplaceHomeDto>> Home(CancellationToken ct = default) =>
        Ok(await marketplace.GetHomeAsync(ct));

    [HttpGet("offers")]
    public async Task<ActionResult<List<PublicOfferDto>>> Offers(
        [FromServices] OfferService offers, [FromQuery] int count = 12, CancellationToken ct = default) =>
        Ok(await offers.GetRunningAsync(count, ct));

    [HttpGet("products/{id:guid}/related")]
    public async Task<ActionResult<List<ProductCardDto>>> Related(
        Guid id, [FromServices] IRecommendationService recommendations,
        [FromQuery] int count = 8, CancellationToken ct = default) =>
        Ok(await recommendations.GetRelatedProductsAsync(id, Math.Clamp(count, 1, 20), ct));

    [HttpPost("activity")]
    public async Task<ActionResult> Track(TrackActivityRequest request, CancellationToken ct = default)
    {
        await marketplace.TrackClientActivityAsync(request, ct);
        return NoContent();
    }
}
