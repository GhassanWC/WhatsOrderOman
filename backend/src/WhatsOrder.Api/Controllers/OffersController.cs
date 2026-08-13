using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Offers;

namespace WhatsOrder.Api.Controllers;

/// <summary>The owner's offer management; the tenant always comes from the JWT.</summary>
[ApiController]
[Route("api/offers")]
[Authorize(Roles = "Owner")]
public class OffersController(OfferService offers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OfferDto>>> List(CancellationToken ct) =>
        Ok(await offers.ListMineAsync(ct));

    [HttpPost]
    public async Task<ActionResult<OfferDto>> Create(SaveOfferRequest request, CancellationToken ct) =>
        Ok(await offers.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OfferDto>> Update(Guid id, SaveOfferRequest request, CancellationToken ct) =>
        Ok(await offers.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await offers.DeleteAsync(id, ct);
        return NoContent();
    }
}
