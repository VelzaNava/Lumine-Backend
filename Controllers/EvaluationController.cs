using Microsoft.AspNetCore.Mvc;
using Lumine.Backend.Models;
using Lumine.Backend.Services;

namespace Lumine.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EvaluationController : ControllerBase
    {
        private readonly SupabaseService _supabaseService;
        private readonly ILogger<EvaluationController> _logger;

        // i-inject yung supabase service at logger
        public EvaluationController(SupabaseService supabaseService, ILogger<EvaluationController> logger)
        {
            _supabaseService = supabaseService;
            _logger          = logger;
        }

        // i-save yung rating ng user pagkatapos ng AR try-on — 1 to 5 stars lang
        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitEvaluationRequest request)
        {
            // i-validate muna yung rating — hindi pwede labas ng 1-5
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { error = "Rating must be between 1 and 5." });

            try
            {
                var client = _supabaseService.Client;
                // i-build yung evaluation record tapos i-insert sa DB
                var record = new EvaluationRecord
                {
                    UserId      = request.UserId,
                    JewelryId   = request.JewelryId,
                    JewelryName = request.JewelryName,
                    Rating      = request.Rating,
                    Comment     = request.Comment
                };

                await client.From<EvaluationRecord>().Insert(record);
                _logger.LogInformation("Evaluation submitted: {UserId} rated {JewelryName} {Rating}/5",
                    request.UserId, request.JewelryName, request.Rating);

                return Ok(new { message = "Thank you for your feedback!" });
            }
            catch (Exception ex)
            {
                _logger.LogError("Submit evaluation error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // i-group lahat ng evaluations per jewelry — para makita ng admin kung alin ang may mataas na rating
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var client  = _supabaseService.Client;
                var result  = await client.From<EvaluationRecord>().Get();
                var records = result.Models;

                // i-compute yung average rating per jewelry tapos i-sort descending
                var summary = records
                    .GroupBy(e => e.JewelryId)
                    .Select(g => new EvaluationSummary
                    {
                        JewelryId     = g.Key,
                        JewelryName   = g.First().JewelryName,
                        AverageRating = Math.Round(g.Average(e => e.Rating), 1),
                        TotalRatings  = g.Count()
                    })
                    .OrderByDescending(s => s.AverageRating)
                    .ToList();

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetSummary error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // kunin lahat ng individual evaluations — para sa admin detail view, pinaka-bago muna
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var client = _supabaseService.Client;
                // i-order descending by created_at — pinaka-recent na feedback muna ang makikita
                var result = await client.From<EvaluationRecord>()
                    .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Descending)
                    .Get();

                return Ok(result.Models);
            }
            catch (Exception ex)
            {
                _logger.LogError("GetAll evaluations error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
