using Lumine.Backend.Contracts;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lumine.Backend.Controllers;

/// <summary>Ratings given after an AR try-on.</summary>
[ApiController]
[Route("api/evaluation")]
[Authorize]
public sealed class EvaluationController(IEvaluationService evaluationService) : ControllerBase
{
    /// <summary>Submits a 1-5 rating as the signed-in user.</summary>
    [HttpPost]
    public async Task<MessageResponse> Submit(SubmitEvaluationRequest request, CancellationToken cancellationToken)
    {
        await evaluationService.SubmitAsync(User.GetUserId(), request, cancellationToken);
        return new MessageResponse("Thank you for your feedback!");
    }

    /// <summary>Average rating per item, best first. Admin only.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.Admin)]
    public Task<IReadOnlyList<EvaluationSummary>> GetSummary(CancellationToken cancellationToken) =>
        evaluationService.GetSummaryAsync(cancellationToken);

    /// <summary>Every rating, newest first. Admin only.</summary>
    [HttpGet("all")]
    [Authorize(Policy = Policies.Admin)]
    public Task<IReadOnlyList<EvaluationDto>> GetAll(CancellationToken cancellationToken) =>
        evaluationService.GetAllAsync(cancellationToken);
}
