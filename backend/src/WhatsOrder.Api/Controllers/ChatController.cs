using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Chat;
using WhatsOrder.Application.Realtime;

namespace WhatsOrder.Api.Controllers;

/// <summary>
/// Owner side of order conversations. The order is always checked against the
/// caller's own store (resolved from the JWT) — ids in the URL are never trusted.
/// </summary>
[ApiController]
[Route("api/orders/{id:guid}/messages")]
[Authorize(Roles = "Owner")]
public class ChatController(ChatService chat) : ControllerBase
{
    /// <summary>Returns the conversation and marks the customer's messages as read.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ChatMessageDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(await chat.GetOrderMessagesAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ChatMessageDto>> Send(Guid id, SendMessageRequest request, CancellationToken ct) =>
        Ok(await chat.SendAsOwnerAsync(id, request.Body, ct));
}
