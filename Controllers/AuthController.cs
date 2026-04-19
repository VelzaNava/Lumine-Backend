using Microsoft.AspNetCore.Mvc;
using Lumine.Backend.Models;
using Lumine.Backend.Services;
using Supabase.Gotrue;
using System.Net.Http.Json;

namespace Lumine.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;

        // i-inject ang mga dependencies — supabase, logger, at config
        public AuthController(SupabaseService supabaseService, ILogger<AuthController> logger, IConfiguration configuration)
        {
            _supabaseService = supabaseService;
            _logger = logger;
            _configuration = configuration;
        }

        // i-check kung nasa listahan ng admin emails yung email na pinasok
        private bool CheckIsAdmin(string email)
        {
            var adminEmails = _configuration.GetSection("AdminEmails").Get<List<string>>() ?? new List<string>();
            return adminEmails.Contains(email.ToLower().Trim());
        }

        // mag-register ng bagong user — i-create sa Supabase Auth tapos agad mag-sign in para makuha yung token
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new { error = "Email and password are required" });

                var supabaseUrl = _configuration["Supabase:Url"];
                var serviceKey  = _configuration["Supabase:Key"];

                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

                // i-create yung user via admin API — email_confirm=true para hindi na kailangan ng email verification
                var payload = new { email = request.Email, password = request.Password, email_confirm = true };
                var createResp = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/admin/users", payload);
                var createBody = await createResp.Content.ReadAsStringAsync();

                if (!createResp.IsSuccessStatusCode)
                {
                    // kung may existing na account, i-return conflict error
                    if (createBody.Contains("already registered") ||
                        createBody.Contains("already been registered") ||
                        createBody.Contains("User already exists") ||
                        (int)createResp.StatusCode == 422)
                        return Conflict(new { error = "An account with this email already exists." });

                    return BadRequest(new { error = "Could not create account.", details = createBody });
                }

                // mag-sign in agad para makuha yung valid access token
                var client  = _supabaseService.Client;
                var session = await client.Auth.SignIn(request.Email, request.Password);

                if (session?.AccessToken == null)
                    return BadRequest(new { error = "Account created but sign-in failed. Please use the Login screen." });

                // i-build yung response kasama na yung isAdmin flag
                var response = new AuthResponse
                {
                    AccessToken  = session.AccessToken,
                    RefreshToken = session.RefreshToken ?? string.Empty,
                    Email        = session.User?.Email  ?? request.Email,
                    UserId       = session.User?.Id     ?? string.Empty,
                    IsAdmin      = CheckIsAdmin(request.Email)
                };

                _logger.LogInformation("User registered: {Email}", request.Email);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Registration error for {Email}: {Message}", request.Email, ex.Message);
                return BadRequest(new { error = "Registration failed", details = ex.Message });
            }
        }

        // mag-login ng existing user — i-sign in sa Supabase tapos i-return yung session tokens
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new { error = "Email and password are required" });

                var client = _supabaseService.Client;
                var session = await client.Auth.SignIn(request.Email, request.Password);

                if (session?.AccessToken == null)
                    return Unauthorized(new { error = "Invalid email or password." });

                // same response structure ng register — kasama na yung isAdmin
                var response = new AuthResponse
                {
                    AccessToken  = session.AccessToken,
                    RefreshToken = session.RefreshToken ?? string.Empty,
                    Email        = session.User?.Email  ?? request.Email,
                    UserId       = session.User?.Id     ?? string.Empty,
                    IsAdmin      = CheckIsAdmin(request.Email)
                };

                _logger.LogInformation("User logged in: {Email}", request.Email);
                return Ok(response);
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException gex) when (
                gex.Message.Contains("Invalid login credentials") ||
                gex.Message.Contains("invalid_credentials"))
            {
                // mali yung credentials — hindi natin i-expose kung alin ang mali
                return Unauthorized(new { error = "Invalid email or password." });
            }
            catch (Exception ex)
            {
                _logger.LogError("Login error for {Email}: {Message}", request.Email, ex.Message);
                return BadRequest(new { error = "Login failed", details = ex.Message });
            }
        }

        // mag-send ng OTP sa email ng user — ginagamit yung anon key hindi service_role
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] OtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { error = "Email is required" });

            try
            {
                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;
                var anonKey     = _configuration["Supabase:AnonKey"]!;

                // i-check muna kung may confirmed account na para sa email na ito
                // para hindi ma-override yung existing password ng registered user
                using var checkHttp = new System.Net.Http.HttpClient();
                checkHttp.DefaultRequestHeaders.Add("apikey", serviceKey);
                checkHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

                var checkResp = await checkHttp.GetAsync(
                    $"{supabaseUrl}/auth/v1/admin/users?email={Uri.EscapeDataString(request.Email)}&per_page=5"
                );

                if (checkResp.IsSuccessStatusCode)
                {
                    var checkJson = await checkResp.Content.ReadAsStringAsync();
                    using var checkDoc = System.Text.Json.JsonDocument.Parse(checkJson);

                    if (checkDoc.RootElement.TryGetProperty("users", out var usersEl))
                    {
                        foreach (var user in usersEl.EnumerateArray())
                        {
                            var userEmail    = user.TryGetProperty("email", out var eEl) ? eEl.GetString() : null;
                            var confirmedAt  = user.TryGetProperty("email_confirmed_at", out var cEl) ? cEl.GetString() : null;

                            if (string.Equals(userEmail, request.Email, StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrEmpty(confirmedAt))
                            {
                                _logger.LogWarning("OTP blocked — confirmed account already exists: {Email}", request.Email);
                                return Conflict(new { error = "An account with this email already exists. Please log in instead." });
                            }
                        }
                    }
                }

                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("apikey", anonKey);
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {anonKey}");

                // /auth/v1/otp mag-send ng 6-digit code; create_user=true para sa bagong email
                var payload = new { email = request.Email, create_user = true };
                var resp    = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/otp", payload);

                // kung nag-rate limit na si Supabase, sabihin sa client na mag-wait
                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return StatusCode(429, new { error = "Too many requests. Please wait 60 seconds.", retryAfter = 60 });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogError("OTP send failed for {Email}: {Body}", request.Email, body);
                    return BadRequest(new { error = "Failed to send OTP", details = body });
                }

                _logger.LogInformation("OTP sent to {Email}", request.Email);
                return Ok(new { message = "OTP sent successfully", email = request.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending OTP for {Email}: {Message}", request.Email, ex.Message);
                return BadRequest(new { error = "Failed to send OTP", details = ex.Message });
            }
        }

        // i-verify yung OTP tapos i-set yung password — para sa new user registration flow
        [HttpPost("verify-and-register")]
        public async Task<IActionResult> VerifyAndRegister([FromBody] VerifyAndRegisterRequest request)
        {
            try
            {
                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;

                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);

                // i-verify yung OTP — type "magiclink" maps to recovery_token na ginagamit ng /auth/v1/otp
                var verifyPayload = new { type = "magiclink", token = request.Token, email = request.Email };
                var verifyResp = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/verify", verifyPayload);

                if (!verifyResp.IsSuccessStatusCode)
                {
                    var verifyBody = await verifyResp.Content.ReadAsStringAsync();
                    _logger.LogError("OTP verify failed for {Email}: {Body}", request.Email, verifyBody);
                    return Unauthorized(new { error = "Invalid or expired OTP code" });
                }

                // i-parse yung session JSON para makuha yung tokens at user ID
                var sessionJson = await verifyResp.Content.ReadAsStringAsync();
                using var sessionDoc = System.Text.Json.JsonDocument.Parse(sessionJson);
                var root = sessionDoc.RootElement;

                var accessToken  = root.TryGetProperty("access_token",  out var at) ? at.GetString()  : null;
                var refreshToken = root.TryGetProperty("refresh_token",  out var rt) ? rt.GetString()  : "";
                var userId       = root.TryGetProperty("user", out var userEl)
                    ? (userEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : "")
                    : "";

                if (string.IsNullOrEmpty(accessToken))
                    return Unauthorized(new { error = "Invalid or expired OTP code" });

                // i-update yung password gamit yung user's own access token — mas tama kaysa admin PATCH
                // kasi yung accessToken galing sa verify response ay valid session ng user
                using var userPwdHttp = new System.Net.Http.HttpClient();
                userPwdHttp.DefaultRequestHeaders.Add("apikey", serviceKey);
                userPwdHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var pwdPayload = System.Text.Json.JsonSerializer.Serialize(new { password = request.Password });
                var pwdContent = new System.Net.Http.StringContent(pwdPayload, System.Text.Encoding.UTF8, "application/json");
                var pwdResp    = await userPwdHttp.PutAsync($"{supabaseUrl}/auth/v1/user", pwdContent);

                if (!pwdResp.IsSuccessStatusCode)
                {
                    var pwdBody = await pwdResp.Content.ReadAsStringAsync();
                    _logger.LogError("Password update failed for {Email}: {Status} {Body}", request.Email, (int)pwdResp.StatusCode, pwdBody);
                    return BadRequest(new { error = "Account verified but password could not be set. Please try again." });
                }

                var isAdmin = CheckIsAdmin(request.Email);
                _logger.LogInformation("User registered via OTP: {Email}", request.Email);

                return Ok(new AuthResponse
                {
                    AccessToken  = accessToken,
                    RefreshToken = refreshToken ?? string.Empty,
                    Email        = request.Email,
                    UserId       = userId       ?? string.Empty,
                    IsAdmin      = isAdmin
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("VerifyAndRegister error for {Email}: {Message}", request.Email, ex.Message);
                return BadRequest(new { error = "Invalid or expired OTP code", details = ex.Message });
            }
        }

        // i-verify lang yung OTP para sa existing user login — walang password update dito
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
                    return BadRequest(new { error = "Email and token are required" });

                var supabaseUrl = _configuration["Supabase:Url"]!;
                var serviceKey  = _configuration["Supabase:Key"]!;

                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("apikey", serviceKey);

                // i-hit yung verify endpoint — kung invalid o expired, agad 401
                var verifyPayload = new { type = "email", token = request.Token, email = request.Email };
                var verifyResp = await http.PostAsJsonAsync($"{supabaseUrl}/auth/v1/verify", verifyPayload);

                if (!verifyResp.IsSuccessStatusCode)
                    return Unauthorized(new { error = "Invalid or expired OTP code" });

                // kunin yung session details galing sa response body
                var sessionJson = await verifyResp.Content.ReadAsStringAsync();
                using var sessionDoc = System.Text.Json.JsonDocument.Parse(sessionJson);
                var root = sessionDoc.RootElement;

                var accessToken  = root.TryGetProperty("access_token",  out var at)  ? at.GetString()  : null;
                var refreshToken = root.TryGetProperty("refresh_token",  out var rt)  ? rt.GetString()  : "";
                var userId       = root.TryGetProperty("user", out var userEl)
                    ? (userEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : "")
                    : "";

                if (string.IsNullOrEmpty(accessToken))
                    return Unauthorized(new { error = "Invalid or expired OTP code" });

                _logger.LogInformation("User authenticated: {Email}", request.Email);
                return Ok(new AuthResponse
                {
                    AccessToken  = accessToken,
                    RefreshToken = refreshToken ?? string.Empty,
                    Email        = request.Email,
                    UserId       = userId       ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error verifying OTP: {Message}", ex.Message);
                return BadRequest(new { error = "Invalid or expired OTP", details = ex.Message });
            }
        }
    }
}
