using System.ComponentModel.DataAnnotations;
using Postgrest.Attributes;
using Postgrest.Models;

namespace Lumine.Backend.Models
{
    /// <summary>
    /// Jewelry model - represents a jewelry item in the catalog
    /// Matches the 'jewelry' table in Supabase
    /// </summary>
    [Table("jewelry")]
    public class Jewelry : BaseModel
    {
        // Primary key - auto-generated UUID
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        // Timestamp when record was created
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Name of the jewelry item
        [Column("name")]
        [Required]
        public string Name { get; set; } = string.Empty;

        // Type: necklace, earring, ring, or bracelet
        [Column("type")]
        [Required]
        public string Type { get; set; } = string.Empty;

        // Material: gold or silver only
        [Column("material")]
        [Required]
        public string Material { get; set; } = string.Empty;

        // Price in decimal format
        [Column("price")]
        [Required]
        public decimal Price { get; set; }

        // URL to jewelry image in Supabase Storage
        [Column("image_url")]
        public string? ImageUrl { get; set; }

        // URL to 3D model in Supabase Storage (optional)
        [Column("model_url")]
        public string? ModelUrl { get; set; }

        // Product description
        [Column("description")]
        public string? Description { get; set; }

        // Whether item is visible in catalog
        [Column("is_available")]
        public bool IsAvailable { get; set; } = true;

        // Whether AR try-on is enabled for this item
        [Column("is_ar_enabled")]
        public bool IsArEnabled { get; set; } = false;
    }
}