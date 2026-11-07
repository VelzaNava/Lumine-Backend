using Lumine.Backend.Contracts;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumine.Backend.Controllers;

/// <summary>The jewelry catalog. Anyone can browse it; only admins can change it.</summary>
[ApiController]
[Route("api/jewelry")]
[Authorize(Policy = Policies.Admin)]
public sealed class JewelryController(IJewelryService jewelryService) : ControllerBase
{
    /// <summary>Lists every catalog item.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IReadOnlyList<JewelryDto>> GetAll(CancellationToken cancellationToken) =>
        jewelryService.GetAllAsync(cancellationToken);

    /// <summary>Gets one catalog item.</summary>
    /// <response code="404">No item with this id.</response>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public Task<JewelryDto> GetById(Guid id, CancellationToken cancellationToken) =>
        jewelryService.GetByIdAsync(id, cancellationToken);

    /// <summary>Adds an item to the catalog. Admin only.</summary>
    [HttpPost]
    public async Task<ActionResult<JewelryDto>> Create(JewelryRequest request, CancellationToken cancellationToken)
    {
        var created = await jewelryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replaces an item's details. Admin only.</summary>
    /// <response code="404">No item with this id.</response>
    [HttpPut("{id:guid}")]
    public Task<JewelryDto> Update(Guid id, JewelryRequest request, CancellationToken cancellationToken) =>
        jewelryService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Removes an item (and any favorites pointing at it). Admin only.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await jewelryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Uploads a product photo (JPG, PNG, WebP or HEIC, max 5 MB) and returns its public URL. Admin only.</summary>
    [HttpPost("upload-image")]
    [RequestSizeLimit(ImageFile.MaxBytes + 1024 * 1024)]
    public Task<ImageUploadResponse> UploadImage(IFormFile? file, CancellationToken cancellationToken) =>
        jewelryService.UploadImageAsync(file, cancellationToken);
}
