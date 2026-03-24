using Microsoft.AspNetCore.Mvc;
using Lumine.Backend.Models;
using Lumine.Backend.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lumine.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;

        // i-inject ang mga dependencies ng admin controller
        public AdminController(SupabaseService supabaseService, IConfiguration configuration, ILogger<AdminController> logger)
        {
            _supabaseService = supabaseService;
            _configuration  = configuration;
            _logger         = logger;
        }

        // kunin lahat ng registered users — i-join yung auth data at profile data
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var supabaseUrl = _configuration["Supabase:Url"];
                var serviceKey  = _configuration["Supabase:Key"];

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

                // kunin lahat ng users galing sa Supabase Admin API — per_page=1000 para makuha lahat
                var resp = await http.GetAsync($"{supabaseUrl}/auth/v1/admin/users?per_page=1000");
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return BadRequest(new { error = "Failed to fetch users", details = body });

                using var doc = JsonDocument.Parse(body);
                var usersArray = doc.RootElement.TryGetProperty("users", out var arr) ? arr : doc.RootElement;

                // kunin lahat ng profiles from DB tapos i-join sa users para may complete info
                var client  = _supabaseService.Client;
                var profiles = await client.From<UserProfileRecord>().Get();
                var profileMap = profiles.Models.ToDictionary(p => p.Id);

                // i-loop sa bawat user tapos i-merge yung profile data niya
                var result = new List<AdminUserInfo>();
                foreach (var user in usersArray.EnumerateArray())
                {
                    var userId = user.GetProperty("id").GetString() ?? "";
                    var email  = user.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";

                    profileMap.TryGetValue(userId, out var profile);
                    result.Add(new AdminUserInfo
                    {
                        UserId       = userId,
                        Email        = email,
                        Username     = profile?.Username     ?? "",
                        FirstName    = profile?.FirstName    ?? "",
                        LastName     = profile?.LastName     ?? "",
                        MobileNumber = profile?.MobileNumber
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetUsers error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Get single user with profile ──────────────────────────────────────

        // kunin yung isang user lang — kasama yung profile details niya
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUser(string userId)
        {
            try
            {
                var supabaseUrl = _configuration["Supabase:Url"];
                var serviceKey  = _configuration["Supabase:Key"];

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

                // i-fetch yung user galing sa auth admin endpoint
                var resp = await http.GetAsync($"{supabaseUrl}/auth/v1/admin/users/{userId}");
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return NotFound(new { error = "User not found" });

                using var doc  = JsonDocument.Parse(body);
                var email      = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";

                // kunin din yung profile niya galing sa user_profiles table
                var client  = _supabaseService.Client;
                var profiles = await client.From<UserProfileRecord>().Where(x => x.Id == userId).Get();
                var profile  = profiles.Models.FirstOrDefault();

                return Ok(new AdminUserInfo
                {
                    UserId       = userId,
                    Email        = email,
                    Username     = profile?.Username     ?? "",
                    FirstName    = profile?.FirstName    ?? "",
                    LastName     = profile?.LastName     ?? "",
                    MobileNumber = profile?.MobileNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("GetUser error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Delete user account ───────────────────────────────────────────────

        // i-delete yung user — kasama na lahat ng related data niya, profile, favorites, at avatar
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var supabaseUrl = _configuration["Supabase:Url"];
                var serviceKey  = _configuration["Supabase:Key"];

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

                // 1. tanggalin muna yung favorites at profile — para sigurado kahit walang cascade
                var client = _supabaseService.Client;
                await client.From<FavoriteRecord>().Where(x => x.UserId == userId).Delete();
                await client.From<UserProfileRecord>().Where(x => x.Id == userId).Delete();

                // 2. i-delete yung avatar niya sa storage bucket
                var deleteAvatarPayload = JsonSerializer.Serialize(new { prefixes = new[] { $"{userId}.jpg" } });
                var deleteAvatarContent = new StringContent(deleteAvatarPayload, System.Text.Encoding.UTF8, "application/json");
                await http.PostAsync($"{supabaseUrl}/storage/v1/object/avatars", deleteAvatarContent);

                // 3. i-delete na sa Supabase Auth — ito yung pang-final, mag-cascade pa rin sa DB
                var resp = await http.DeleteAsync($"{supabaseUrl}/auth/v1/admin/users/{userId}");
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    return BadRequest(new { error = "Failed to delete user", details = body });
                }

                _logger.LogInformation("Admin deleted user: {UserId}", userId);
                return Ok(new { message = "Account removed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError("DeleteUser error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // ── Get user favorites ────────────────────────────────────────────────

        // para sa admin — tingnan kung anong mga jewelry ang naka-favorite ng isang user
        [HttpGet("users/{userId}/favorites")]
        public async Task<IActionResult> GetUserFavorites(string userId)
        {
            try
            {
                var client = _supabaseService.Client;
                var result = await client.From<FavoriteRecord>()
                    .Where(x => x.UserId == userId)
                    .Get();

                var jewelryIds = result.Models.Select(f => f.JewelryId).ToList();
                return Ok(jewelryIds);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetUserFavorites error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
