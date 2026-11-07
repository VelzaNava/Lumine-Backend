using Lumine.Backend.Contracts;
using Lumine.Backend.Data;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Services;

/// <summary>Try-on ratings.</summary>
public interface IEvaluationService
{
    Task SubmitAsync(Guid userId, SubmitEvaluationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvaluationSummary>> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EvaluationDto>> GetAllAsync(CancellationToken cancellationToken);
}

internal sealed class EvaluationService(SupabaseRestApi database, ILogger<EvaluationService> logger) : IEvaluationService
{
    public async Task SubmitAsync(Guid userId, SubmitEvaluationRequest request, CancellationToken cancellationToken)
    {
        await database.InsertAsync<EvaluationRow>(Tables.Evaluations, new Dictionary<string, object?>
        {
            ["user_id"] = userId.ToString(),
            ["jewelry_id"] = request.JewelryId.Trim(),
            ["jewelry_name"] = request.JewelryName?.Trim() ?? string.Empty,
            ["rating"] = request.Rating,
            ["comment"] = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            // Set explicitly: older versions of this API stored 0001-01-01 here.
            ["created_at"] = DateTimeOffset.UtcNow,
        }, cancellationToken);

        logger.LogInformation("Rating submitted: {UserId} rated {JewelryId} {Rating}/5",
            userId, request.JewelryId, request.Rating);
    }

    public async Task<IReadOnlyList<EvaluationSummary>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var rows = await database.SelectAsync<EvaluationRow>(
            Tables.Evaluations, "select=jewelry_id,jewelry_name,rating", cancellationToken);

        return rows
            .GroupBy(row => row.JewelryId)
            .Select(group => new EvaluationSummary
            {
                JewelryId = group.Key,
                JewelryName = group.Select(row => row.JewelryName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                    ?? string.Empty,
                AverageRating = Math.Round(group.Average(row => row.Rating), 1),
                TotalRatings = group.Count(),
            })
            .OrderByDescending(summary => summary.AverageRating)
            .ThenByDescending(summary => summary.TotalRatings)
            .ToList();
    }

    public async Task<IReadOnlyList<EvaluationDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await database.SelectAsync<EvaluationRow>(
            Tables.Evaluations, "select=*&order=created_at.desc", cancellationToken);

        return rows.Select(EvaluationDto.From).ToList();
    }
}
