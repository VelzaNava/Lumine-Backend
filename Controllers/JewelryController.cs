using Microsoft.AspNetCore.Mvc;
using Lumine.Backend.Models;
using Lumine.Backend.Services;

namespace Lumine.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JewelryController : ControllerBase
    {
        private readonly JewelryService _jewelryService;
        private readonly ILogger<JewelryController> _logger;
        private readonly IConfiguration _configuration;

        // i-inject yung jewelry service, logger, at config
        public JewelryController(JewelryService jewelryService, ILogger<JewelryController> logger, IConfiguration configuration)
        {
            _jewelryService = jewelryService;
            _logger = logger;
            _configuration = configuration;
        }

        // kunin lahat ng jewelry — ipapakita sa catalog ng app
        [HttpGet]
        public async Task<IActionResult> GetAllJewelry()
        {
            try
            {
                var jewelry = await _jewelryService.GetAllJewelryAsync();
                return Ok(jewelry);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching jewelry: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch jewelry", details = ex.Message });
            }
        }

        // kunin yung isang jewelry item base sa ID — para sa detail view at AR try-on
        [HttpGet("{id}")]
        public async Task<IActionResult> GetJewelryById(Guid id)
        {
            try
            {
                var jewelry = await _jewelryService.GetJewelryByIdAsync(id);

                // kung wala yung ID, i-return 404
                if (jewelry == null)
                {
                    return NotFound(new { error = "Jewelry not found" });
                }

                return Ok(jewelry);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching jewelry {id}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch jewelry", details = ex.Message });
            }
        }

        // mag-add ng bagong jewelry sa catalog — admin lang dapat ang gumagamit nito
        [HttpPost]
        public async Task<IActionResult> CreateJewelry([FromBody] Jewelry jewelry)
        {
            try
            {
                var created = await _jewelryService.CreateJewelryAsync(jewelry);
                // i-return yung 201 Created kasama yung location header
                return CreatedAtAction(nameof(GetJewelryById), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating jewelry: {ex.Message}");
                return StatusCode(500, new { error = "Failed to create jewelry", details = ex.Message });
            }
        }

        // i-update yung existing jewelry — i-set yung ID mula sa URL param bago i-save
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateJewelry(Guid id, [FromBody] Jewelry jewelry)
        {
            try
            {
                // i-override yung ID sa body gamit yung ID sa URL para consistent
                jewelry.Id = id;
                var updated = await _jewelryService.UpdateJewelryAsync(jewelry);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating jewelry {id}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to update jewelry", details = ex.Message });
            }
        }

        // i-delete yung jewelry — mag-return ng 204 No Content kung maayos
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteJewelry(Guid id)
        {
            try
            {
                await _jewelryService.DeleteJewelryAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting jewelry {id}: {ex.Message}");
                return StatusCode(500, new { error = "Failed to delete jewelry", details = ex.Message });
            }
        }

        // i-upload yung jewelry image sa Supabase storage tapos i-return yung public URL
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadJewelryImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "File too large. Max 5MB." });

            try
            {
                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                var contentType = file.ContentType switch
                {
                    "image/jpeg" => "image/jpeg",
                    "image/png"  => "image/png",
                    "image/webp" => "image/webp",
                    _            => "image/jpeg"
                };

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");
                httpClient.DefaultRequestHeaders.Add("apikey", serviceKey);

                var fileName  = $"jewelry_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.jpg";
                var uploadUrl = $"{supabaseUrl}/storage/v1/object/jewelry/{fileName}";

                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                var uploadResponse = await httpClient.PutAsync(uploadUrl, content);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var err = await uploadResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Jewelry image upload failed: {Error}", err);
                    return BadRequest(new { error = "Upload failed", details = err });
                }

                var publicUrl = $"{supabaseUrl}/storage/v1/object/public/jewelry/{fileName}";
                return Ok(new { imageUrl = publicUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError("UploadJewelryImage error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
