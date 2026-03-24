using Postgrest.Attributes;
using Postgrest.Models;

namespace Lumine.Backend.Models
{
    // DB model para sa user_profiles table — naka-map sa Supabase via Postgrest attributes
    [Table("user_profiles")]
    public class UserProfileRecord : BaseModel
    {
        // primary key — hindi auto-generate, same as auth.users id
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("mobile_number")]
        public string? MobileNumber { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }
    }

    // DB model para sa favorites table — user_id + jewelry_id lang ang important dito
    [Table("favorites")]
    public class FavoriteRecord : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("jewelry_id")]
        public string JewelryId { get; set; } = string.Empty;
    }

    // ── Request / Response DTOs ────────────────────────────────────────────────

    // DTO para sa profile update — hindi kasama yung avatar_url, separate endpoint yun
    public class UpdateProfileRequest
    {
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
    }

    // DTO na ini-return sa client — kasama na yung avatar URL
    public class UserProfileResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
        public string? AvatarUrl { get; set; }
    }

    // DTO para sa admin user list — pinagsama yung auth info at profile info
    public class AdminUserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
    }
}
