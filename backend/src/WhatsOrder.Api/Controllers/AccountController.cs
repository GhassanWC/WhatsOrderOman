using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Account;
using WhatsOrder.Application.Chat;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Reviews;

namespace WhatsOrder.Api.Controllers;

/// <summary>
/// The buyer's private account area. Everything is scoped to the authenticated user id —
/// client-supplied ids are only ever lookups inside that scope, never proof of ownership.
/// Any signed-in user (buyer or seller) can use it.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(
    AccountService account,
    IBuyerProfileService profiles,
    BuyerOrderService buyerOrders,
    ChatService chat,
    ReviewService reviews,
    ICurrentUser currentUser,
    IFileStorage fileStorage) : ControllerBase
{
    private Guid UserId => currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");

    [HttpGet]
    public async Task<ActionResult<AccountOverviewDto>> Overview(CancellationToken ct) =>
        Ok(await account.GetOverviewAsync(ct));

    // ── Profile ────────────────────────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<ActionResult<BuyerProfileDto>> GetProfile(CancellationToken ct) =>
        Ok(await profiles.GetAsync(UserId, ct));

    [HttpPut("profile")]
    public async Task<ActionResult<BuyerProfileDto>> UpdateProfile(
        UpdateBuyerProfileRequest request, CancellationToken ct) =>
        Ok(await profiles.UpdateAsync(UserId, request, ct));

    [HttpPost("profile/avatar")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<BuyerProfileDto>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        StoreController.ValidateImage(file);
        await using var stream = file.OpenReadStream();
        var path = await fileStorage.SaveAsync(stream, Path.GetExtension(file.FileName), "avatars", ct);
        return Ok(await profiles.SetAvatarAsync(UserId, path, ct));
    }

    // ── Orders ─────────────────────────────────────────────────────────────

    [HttpGet("orders")]
    public async Task<ActionResult<BuyerOrdersPage>> Orders(
        [FromQuery] string? filter, [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await buyerOrders.ListAsync(filter, page, pageSize, ct));

    [HttpGet("orders/{id:guid}")]
    public async Task<ActionResult<BuyerOrderDto>> Order(Guid id, CancellationToken ct) =>
        Ok(await buyerOrders.GetAsync(id, ct));

    [HttpPost("orders/{id:guid}/review")]
    public async Task<ActionResult<ReviewDto>> Review(
        Guid id, CreateReviewRequest request, CancellationToken ct)
    {
        var name = User.FindFirst("name")?.Value ?? "";
        return Ok(await reviews.CreateAsync(UserId, name, id, request, ct));
    }

    // ── Messages ───────────────────────────────────────────────────────────

    [HttpGet("conversations")]
    public async Task<ActionResult<List<BuyerConversationDto>>> Conversations(
        [FromQuery] string? search, CancellationToken ct) =>
        Ok(await buyerOrders.GetConversationsAsync(search, ct));

    [HttpGet("orders/{id:guid}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> Messages(Guid id, CancellationToken ct) =>
        Ok(await chat.GetBuyerMessagesAsync(UserId, id, ct));

    [HttpPost("orders/{id:guid}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        Guid id, SendMessageRequest request, CancellationToken ct) =>
        Ok(await chat.SendAsBuyerAsync(UserId, id, request.Body, ct));
}
