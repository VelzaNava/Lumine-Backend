using System.ComponentModel.DataAnnotations;

namespace Lumine.Backend.Models
{
    // request model para sa send-otp endpoint — email lang ang kailangan
    public class OtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    // request model para sa verify-otp — email + yung 6-digit OTP code
    public class VerifyOtpRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Token { get; set; } = string.Empty;
    }

    // request model para sa email+password registration — minimum 8 chars ang password
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;
    }

    // request model para sa login — email at password lang
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    // request model para sa OTP verify + password set sa iisang step
    public class VerifyAndRegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }

    // response na ini-return pagkatapos ng successful auth — kasama na yung isAdmin flag
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
    }
}
