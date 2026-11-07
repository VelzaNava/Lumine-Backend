using System.Net.Http.Headers;
using Lumine.Backend.Hosting;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lumine.Backend.Tests;

/// <summary>
/// Hosts the real API pipeline - routing, authentication, authorization,
/// validation, error handling - with every Supabase-backed service replaced by
/// an in-memory fake, so no test ever touches a real project.
/// </summary>
public sealed class LumineApiFactory : WebApplicationFactory<Program>
{
    public FakeEvaluationService Evaluations { get; } = new();

    public FakeAccountService Accounts { get; } = new();

    public HttpClient CreateClientFor(TestAccount? account)
    {
        var client = CreateClient();
        if (account is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://example.supabase.co",
                ["Supabase:Key"] = "test-service-key",
                ["Supabase:AnonKey"] = "test-anon-key",
                ["AdminEmails:0"] = TestAccounts.Admin.Email,
            }));

        builder.ConfigureTestServices(services =>
        {
            // No real database to check against.
            var schemaCheck = services.SingleOrDefault(descriptor => descriptor.ImplementationType == typeof(DatabaseSchemaCheck));
            if (schemaCheck is not null)
            {
                services.Remove(schemaCheck);
            }

            Replace<IAccessTokenValidator>(services, new FakeTokenValidator());
            Replace<IAuthService>(services, new FakeAuthService());
            Replace<IJewelryService>(services, new FakeJewelryService());
            Replace<IProfileService>(services, new FakeProfileService());
            Replace<IAdminUserService>(services, new FakeAdminUserService());
            Replace<IEvaluationService>(services, Evaluations);
            Replace<IAccountService>(services, Accounts);
        });
    }

    private static void Replace<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(implementation);
    }
}
