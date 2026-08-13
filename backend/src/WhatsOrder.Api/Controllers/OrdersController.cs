using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Orders;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = "Owner")]
public class OrdersController(OrderService orderService) : ControllerBase
{
    /// <param name="filter">all | today | active | new | confirmed | preparing | ready | outfordelivery | completed | cancelled</param>
    [HttpGet]
    public async Task<ActionResult<OrdersPage>> List(
        [FromQuery] string? filter,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await orderService.GetOrdersAsync(filter, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await orderService.GetOrderAsync(id, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id, UpdateOrderStatusRequest request, CancellationToken ct) =>
        Ok(await orderService.UpdateStatusAsync(id, request, ct));
}
