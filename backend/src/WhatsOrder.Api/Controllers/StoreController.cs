using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Application.Stores;
using WhatsOrder.Infrastructure.Identity;

namespace WhatsOrder.Api.Controllers;

// Any signed-in account may open a store (buyers become sellers with the same
// account); every action below is scoped to the caller's own store anyway.
[ApiController]
[Route("api/store")]
[Authorize]
public class StoreController(StoreService storeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StoreDto>> Get(CancellationToken ct) =>
        Ok(await storeService.GetMyStoreAsync(ct));

    [HttpPost]
    public async Task<ActionResult<StoreDto>> Create(
        CreateStoreRequest request,
        [FromServices] UserManager<ApplicationUser> userManager,
        [FromServices] ICurrentUser currentUser,
        CancellationToken ct)
    {
        var store = await storeService.CreateStoreAsync(request, ct);

        // A buyer who opens a store becomes an owner too. The client must refresh
        // its tokens afterwards so the new role lands in the access token.
        if (currentUser.UserId is { } userId &&
            await userManager.FindByIdAsync(userId.ToString()) is { } user &&
            !await userManager.IsInRoleAsync(user, "Owner"))
        {
            await userManager.AddToRoleAsync(user, "Owner");
        }

        return Ok(store);
    }

    [HttpPut]
    public async Task<ActionResult<StoreDto>> Update(UpdateStoreRequest request, CancellationToken ct) =>
        Ok(await storeService.UpdateStoreAsync(request, ct));

    [HttpPut("settings")]
    public async Task<ActionResult<StoreDto>> UpdateSettings(UpdateStoreSettingsRequest request, CancellationToken ct) =>
        Ok(await storeService.UpdateSettingsAsync(request, ct));

    [HttpPost("logo")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<StoreDto>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        ValidateImage(file);
        await using var stream = file.OpenReadStream();
        return Ok(await storeService.SetLogoAsync(stream, Path.GetExtension(file.FileName), ct));
    }

    [HttpPost("banner")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<StoreDto>> UploadBanner(IFormFile file, CancellationToken ct)
    {
        ValidateImage(file);
        await using var stream = file.OpenReadStream();
        return Ok(await storeService.SetBannerAsync(stream, Path.GetExtension(file.FileName), ct));
    }

    [HttpDelete("logo")]
    public async Task<ActionResult<StoreDto>> RemoveLogo(CancellationToken ct) =>
        Ok(await storeService.RemoveLogoAsync(ct));

    [HttpDelete("banner")]
    public async Task<ActionResult<StoreDto>> RemoveBanner(CancellationToken ct) =>
        Ok(await storeService.RemoveBannerAsync(ct));

    [HttpGet("slug-available")]
    public async Task<ActionResult<SlugAvailabilityDto>> SlugAvailable([FromQuery] string slug, CancellationToken ct) =>
        Ok(await storeService.CheckSlugAsync(slug, ct));

    [HttpGet("reviews")]
    public async Task<ActionResult<StoreReviewsPage>> Reviews(
        [FromServices] ReviewService reviews,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        Ok(await reviews.GetForMyStoreAsync(page, pageSize, ct));

    internal static void ValidateImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("no_file", "No file was uploaded.");
        if (file.Length > 5 * 1024 * 1024)
            throw new BusinessRuleException("file_too_large", "Images must be smaller than 5 MB.");
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("invalid_file_type", "Only image files are allowed.");
    }
}
