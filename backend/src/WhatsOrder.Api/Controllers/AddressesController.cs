using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Account;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/account/addresses")]
[Authorize]
public class AddressesController(AddressService addresses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BuyerAddressDto>>> List(CancellationToken ct) =>
        Ok(await addresses.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<BuyerAddressDto>> Create(SaveAddressRequest request, CancellationToken ct) =>
        Ok(await addresses.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BuyerAddressDto>> Update(
        Guid id, SaveAddressRequest request, CancellationToken ct) =>
        Ok(await addresses.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await addresses.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/default")]
    public async Task<ActionResult<BuyerAddressDto>> SetDefault(Guid id, CancellationToken ct) =>
        Ok(await addresses.SetDefaultAsync(id, ct));
}
