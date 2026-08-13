using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Products;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Roles = "Owner")]
public class ProductsController(ProductService productService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await productService.ListAsync(search, categoryId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await productService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request, CancellationToken ct) =>
        Ok(await productService.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, SaveProductRequest request, CancellationToken ct) =>
        Ok(await productService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await productService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<ActionResult<ProductDto>> SetAvailability(
        Guid id, SetAvailabilityRequest request, CancellationToken ct) =>
        Ok(await productService.SetAvailabilityAsync(id, request.IsAvailable, ct));

    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<ProductDto>> AddImage(Guid id, IFormFile file, CancellationToken ct)
    {
        StoreController.ValidateImage(file);
        await using var stream = file.OpenReadStream();
        return Ok(await productService.AddImageAsync(id, stream, Path.GetExtension(file.FileName), ct));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<ActionResult<ProductDto>> DeleteImage(Guid id, Guid imageId, CancellationToken ct) =>
        Ok(await productService.DeleteImageAsync(id, imageId, ct));

    [HttpPut("{id:guid}/images/order")]
    public async Task<ActionResult<ProductDto>> ReorderImages(
        Guid id, ReorderImagesRequest request, CancellationToken ct) =>
        Ok(await productService.ReorderImagesAsync(id, request.ImageIds, ct));

    [HttpPatch("{id:guid}/images/{imageId:guid}/primary")]
    public async Task<ActionResult<ProductDto>> SetPrimaryImage(Guid id, Guid imageId, CancellationToken ct) =>
        Ok(await productService.SetPrimaryImageAsync(id, imageId, ct));
}

public sealed record ReorderImagesRequest(List<Guid> ImageIds);
