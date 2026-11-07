using System.Text.Json;
using Lumine.Backend.Data;
using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;

namespace Lumine.Backend.Hosting;

/// <summary>
/// Confirms once at startup that the database has the columns this version
/// needs. If not, it says exactly what to run - instead of every save failing
/// later with an obscure "column not found" error from Supabase.
/// </summary>
internal sealed class DatabaseSchemaCheck(IServiceScopeFactory scopeFactory, ILogger<DatabaseSchemaCheck> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SupabaseRestApi>();
            await database.SelectAsync<JsonElement>(
                Tables.Jewelry, "select=id,image_urls,stock_quantity&limit=1", stoppingToken);

            logger.LogInformation("Database schema check passed");
        }
        catch (ApiException exception) when (exception.StatusCode == StatusCodes.Status502BadGateway)
        {
            logger.LogError("The database is missing columns this version of the API needs. " +
                            "Run database/update.sql in the Supabase SQL Editor, then restart the API.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Couldn't reach the database to verify its schema at startup");
        }
    }
}
