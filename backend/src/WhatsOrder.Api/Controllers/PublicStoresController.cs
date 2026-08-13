using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsOrder.Application.Chat;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Public;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Reviews;

namespace WhatsOrder.Api.Controllers;

/// <summary>
/// Customer-facing endpoints. Anonymous by design — signed-in buyers get their orders
/// linked to their account automatically, but no endpoint here requires a login.
/// </summary>
[ApiController]
[Route("api/public/stores/{slug}")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class PublicStoresController(
    PublicCatalogService catalog, OrderService orderService, ChatService chat) : ControllerBase
{
    [HttpGet("reviews")]
    public async Task<ActionResult<StoreReviewsPage>> GetReviews(
        string slug, [FromServices] ReviewService reviews,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        Ok(await reviews.GetForStoreAsync(slug, page, pageSize, ct));

    [HttpGet]
    public async Task<ActionResult<PublicStoreDto>> GetStore(string slug, CancellationToken ct) =>
        Ok(await catalog.GetStoreAsync(slug, ct));

    [HttpGet("products")]
    public async Task<ActionResult<List<PublicProductDto>>> GetProducts(
        string slug,
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? featured,
        CancellationToken ct = default) =>
        Ok(await catalog.GetProductsAsync(slug, search, categoryId, featured, ct));

    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<PublicProductDto>> GetProduct(string slug, Guid id, CancellationToken ct) =>
        Ok(await catalog.GetProductAsync(slug, id, ct));

    [HttpPost("orders")]
    [EnableRateLimiting("public-orders")]
    public async Task<ActionResult<PublicOrderCreatedDto>> CreateOrder(
        string slug, CreatePublicOrderRequest request, CancellationToken ct) =>
        Ok(await orderService.CreatePublicOrderAsync(slug, request, ct));

    /// <summary>Order status lookup for the tracking page; requires the customer's phone.</summary>
    [HttpGet("orders/{orderNumber}")]
    public async Task<ActionResult<PublicOrderStatusDto>> GetOrder(
        string slug, string orderNumber, [FromQuery] string phone, CancellationToken ct) =>
        Ok(await orderService.GetPublicOrderAsync(slug, orderNumber, phone, ct));

    /// <summary>The customer's side of the order chat; marks the store's messages read.</summary>
    [HttpGet("orders/{orderNumber}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
        string slug, string orderNumber, [FromQuery] string phone, CancellationToken ct) =>
        Ok(await chat.GetPublicMessagesAsync(slug, orderNumber, phone, ct));

    [HttpPost("orders/{orderNumber}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        string slug, string orderNumber, [FromQuery] string phone,
        SendMessageRequest request, CancellationToken ct) =>
        Ok(await chat.SendAsCustomerAsync(slug, orderNumber, phone, request.Body, ct));
}
