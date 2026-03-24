using Microsoft.AspNetCore.Mvc;
using Lumine.Backend.Models;
using Lumine.Backend.Services;

namespace Lumine.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProfileController> _logger;

        // i-inject yung mga kailangan — supabase client, config, at logger
        public ProfileController(SupabaseService supabaseService, IConfiguration configuration, ILogger<ProfileController> logger)
        {
            _supabaseService = supabaseService;
            _configuration   = configuration;
            _logger          = logger;
        }

        // ── User Profile ───────────────────────────────────────────────────────

        // kunin yung profile ng user base sa userId — kung wala pa, i-return empty response
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetProfile(string userId)
        {
            try
            {
                var client = _supabaseService.Client;
                var result = await client.From<UserProfileRecord>()
                    .Where(x => x.Id == userId)
                    .Get();

                var record = result.Models.FirstOrDefault();
                if (record == null)
                    return Ok(new UserProfileResponse { UserId = userId });

                return Ok(new UserProfileResponse
                {
                    UserId       = record.Id,
                    Username     = record.Username,
                    FirstName    = record.FirstName,
                    LastName     = record.LastName,
                    MobileNumber = record.MobileNumber,
                    AvatarUrl    = record.AvatarUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("GetProfile error for {UserId}: {Message}", userId, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // i-update yung profile — mag-PATCH kung may existing, mag-POST kung bago pa
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateProfile(string userId, [FromBody] UpdateProfileRequest request)
        {
            try
            {
                var client = _supabaseService.Client;

                // i-fetch muna yung existing record para ma-preserve yung avatar_url niya
                var existing = await client.From<UserProfileRecord>()
                    .Where(x => x.Id == userId)
                    .Get();

                var existingRecord = existing.Models.FirstOrDefault();
                var avatarUrl     = existingRecord?.AvatarUrl;

                // raw PostgREST para maiwasan yung bug ng postgrest-csharp na nino-null yung id column
                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;
                using var http  = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");
                http.DefaultRequestHeaders.Add("apikey", serviceKey);

                // i-serialize yung payload kasama ang preserved na avatar_url
                var payload = System.Text.Json.JsonSerializer.Serialize(new {
                    id             = userId,
                    username       = request.Username,
                    first_name     = request.FirstName,
                    last_name      = request.LastName,
                    mobile_number  = request.MobileNumber,
                    avatar_url     = avatarUrl
                });
                var body = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                // kung may record na — PATCH; kung wala pa — POST para i-create
                if (existingRecord != null)
                {
                    http.DefaultRequestHeaders.Add("Prefer", "return=minimal");
                    await http.PatchAsync($"{supabaseUrl}/rest/v1/user_profiles?id=eq.{userId}", body);
                }
                else
                {
                    http.DefaultRequestHeaders.Add("Prefer", "return=minimal");
                    await http.PostAsync($"{supabaseUrl}/rest/v1/user_profiles", body);
                }

                return Ok(new UserProfileResponse
                {
                    UserId       = userId,
                    Username     = request.Username,
                    FirstName    = request.FirstName,
                    LastName     = request.LastName,
                    MobileNumber = request.MobileNumber,
                    AvatarUrl    = existingRecord?.AvatarUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("UpdateProfile error for {UserId}: {Message}", userId, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Avatar Upload ──────────────────────────────────────────────────────

        // mag-upload ng avatar sa Supabase Storage tapos i-save yung public URL sa profile
        [HttpPost("{userId}/avatar")]
        public async Task<IActionResult> UploadAvatar(string userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            // max 5MB lang — ayaw natin mag-stress yung storage
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { error = "File too large (max 5 MB)" });

            try
            {
                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;

                // i-read yung file bytes para ma-upload sa storage
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();

                // i-normalize yung content type — hindi pwede wildcard "image/*" sa storage
                var contentType = file.ContentType switch {
                    "image/jpeg" => "image/jpeg",
                    "image/png"  => "image/png",
                    "image/webp" => "image/webp",
                    _            => "image/jpeg"
                };

                // i-PUT sa Supabase Storage — upsert behavior, okay kung may existing na
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");
                httpClient.DefaultRequestHeaders.Add("apikey", serviceKey);

                var fileName   = $"{userId}.jpg";
                var uploadUrl  = $"{supabaseUrl}/storage/v1/object/avatars/{fileName}";

                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                var uploadResponse = await httpClient.PutAsync(uploadUrl, content);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var err = await uploadResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Storage upload failed: {Error}", err);
                    return BadRequest(new { error = "Upload to storage failed", details = err });
                }

                // i-construct yung public URL — public bucket kasi yung avatars
                var publicUrl = $"{supabaseUrl}/storage/v1/object/public/avatars/{fileName}";

                // i-save yung bagong avatar_url sa user_profiles table
                httpClient.DefaultRequestHeaders.Add("Prefer", "return=minimal");
                var patchUrl     = $"{supabaseUrl}/rest/v1/user_profiles?id=eq.{userId}";
                var patchPayload = System.Text.Json.JsonSerializer.Serialize(new { avatar_url = publicUrl });
                var patchContent = new StringContent(patchPayload, System.Text.Encoding.UTF8, "application/json");
                await httpClient.PatchAsync(patchUrl, patchContent);

                return Ok(new { avatarUrl = publicUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError("UploadAvatar error for {UserId}: {Message}", userId, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Favorites ──────────────────────────────────────────────────────────

        // kunin lahat ng favorite jewelry IDs ng user
        [HttpGet("{userId}/favorites")]
        public async Task<IActionResult> GetFavorites(string userId)
        {
            try
            {
                var client = _supabaseService.Client;
                var result = await client.From<FavoriteRecord>()
                    .Where(x => x.UserId == userId)
                    .Get();

                // i-return lang yung IDs, hindi yung buong record
                var jewelryIds = result.Models.Select(f => f.JewelryId).ToList();
                return Ok(jewelryIds);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetFavorites error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // i-add sa favorites — kung may duplicate na, okay lang, hindi mag-error
        [HttpPost("{userId}/favorites/{jewelryId}")]
        public async Task<IActionResult> AddFavorite(string userId, string jewelryId)
        {
            try
            {
                var client = _supabaseService.Client;
                var record = new FavoriteRecord { UserId = userId, JewelryId = jewelryId };
                await client.From<FavoriteRecord>().Insert(record);
                return Ok(new { message = "Added to favorites" });
            }
            catch (Exception ex)
            {
                // duplicate entry — hindi error ito, i-return lang success
                if (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
                    return Ok(new { message = "Already in favorites" });

                _logger.LogError("AddFavorite error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // i-remove sa favorites — tanggalin yung specific jewelry ng specific user
        [HttpDelete("{userId}/favorites/{jewelryId}")]
        public async Task<IActionResult> RemoveFavorite(string userId, string jewelryId)
        {
            try
            {
                var client = _supabaseService.Client;
                await client.From<FavoriteRecord>()
                    .Where(x => x.UserId == userId && x.JewelryId == jewelryId)
                    .Delete();

                return Ok(new { message = "Removed from favorites" });
            }
            catch (Exception ex)
            {
                _logger.LogError("RemoveFavorite error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
