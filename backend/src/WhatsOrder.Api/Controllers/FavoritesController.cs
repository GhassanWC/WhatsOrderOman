using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Account;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Authorize]
public class FavoritesController(FavoritesService favorites) : ControllerBase
{
    [HttpGet("/api/account/favorites")]
    public async Task<ActionResult<FavoritesDto>> All(CancellationToken ct) =>
        Ok(await favorites.GetAllAsync(ct));

    [HttpGet("/api/account/favorites/ids")]
    public async Task<ActionResult<FavoriteIdsDto>> Ids(CancellationToken ct) =>
        Ok(await favorites.GetIdsAsync(ct));

    [HttpPost("/api/favorites/stores/{id:guid}")]
    public async Task<ActionResult> AddStore(Guid id, CancellationToken ct)
    {
        await favorites.AddStoreAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("/api/favorites/stores/{id:guid}")]
    public async Task<ActionResult> RemoveStore(Guid id, CancellationToken ct)
    {
        await favorites.RemoveStoreAsync(id, ct);
        return NoContent();
    }

    [HttpPost("/api/favorites/products/{id:guid}")]
    public async Task<ActionResult> AddProduct(Guid id, CancellationToken ct)
    {
        await favorites.AddProductAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("/api/favorites/products/{id:guid}")]
    public async Task<ActionResult> RemoveProduct(Guid id, CancellationToken ct)
    {
        await favorites.RemoveProductAsync(id, ct);
        return NoContent();
    }
}
