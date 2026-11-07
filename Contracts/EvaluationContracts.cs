using System.ComponentModel.DataAnnotations;
using Lumine.Backend.Data;

namespace Lumine.Backend.Contracts;

/// <summary>
/// A rating given after an AR try-on. The rater is always the signed-in user;
/// a <c>userId</c> in the body (sent by older app versions) is ignored.
/// </summary>
public sealed class SubmitEvaluationRequest
{
    [Required]
    public string JewelryId { get; init; } = string.Empty;

    [StringLength(120)]
    public string? JewelryName { get; init; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }
}

public sealed class EvaluationDto
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string JewelryId { get; init; } = string.Empty;
    public string JewelryName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public string CreatedAt { get; init; } = string.Empty;

    public static EvaluationDto From(EvaluationRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        JewelryId = row.JewelryId,
        JewelryName = row.JewelryName ?? string.Empty,
        Rating = row.Rating,
        Comment = row.Comment,
        CreatedAt = row.CreatedAt ?? string.Empty,
    };
}

/// <summary>Average rating per catalog item, for the admin ratings tab.</summary>
public sealed class EvaluationSummary
{
    public string JewelryId { get; init; } = string.Empty;
    public string JewelryName { get; init; } = string.Empty;
    public double AverageRating { get; init; }
    public int TotalRatings { get; init; }
}
