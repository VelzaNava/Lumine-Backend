using Postgrest.Attributes;
using Postgrest.Models;

namespace Lumine.Backend.Models
{
    // DB model para sa evaluations table — dito naka-store lahat ng ratings ng users
    [Table("evaluations")]
    public class EvaluationRecord : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("jewelry_id")]
        public string JewelryId { get; set; } = string.Empty;

        // i-store din yung jewelry name para hindi na mag-join pag nagdi-display
        [Column("jewelry_name")]
        public string JewelryName { get; set; } = string.Empty;

        [Column("rating")]
        public int Rating { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    // DTO na ginagamit ng client para mag-submit ng evaluation pagkatapos ng AR try-on
    public class SubmitEvaluationRequest
    {
        public string UserId     { get; set; } = string.Empty;
        public string JewelryId  { get; set; } = string.Empty;
        public string JewelryName { get; set; } = string.Empty;
        public int    Rating     { get; set; }
        public string? Comment   { get; set; }
    }

    // summary DTO — pinagsama-sama yung ratings per jewelry para sa admin dashboard
    public class EvaluationSummary
    {
        public string JewelryId    { get; set; } = string.Empty;
        public string JewelryName  { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int    TotalRatings  { get; set; }
    }
}
