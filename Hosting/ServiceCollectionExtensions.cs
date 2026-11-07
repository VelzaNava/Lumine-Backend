using System.Globalization;
using System.Threading.RateLimiting;
using Lumine.Backend.Configuration;
using Lumine.Backend.Errors;
using Lumine.Backend.Infrastructure.Supabase;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Lumine.Backend.Hosting;

public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan SupabaseTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Configuration and the typed Supabase clients.</summary>
    public static IServiceCollection AddSupabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SupabaseSettings>()
            .Bind(configuration.GetSection(SupabaseSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<SupabaseAuthApi>(ForSupabasePath("auth/v1/"));
        services.AddHttpClient<SupabaseRestApi>(ForSupabasePath("rest/v1/"));
        services.AddHttpClient<SupabaseStorageApi>(ForSupabasePath("storage/v1/"));

        return services;
    }

    /// <summary>Bearer-token authentication, authorization policies and rate limits.</summary>
    public static IServiceCollection AddLumineSecurity(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginCooldown>();
        services.AddSingleton<AdminDirectory>();
        services.AddScoped<IAccessTokenValidator, SupabaseAccessTokenValidator>();

        services.AddAuthentication(SupabaseAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SupabaseAuthenticationHandler>(
                SupabaseAuthenticationHandler.SchemeName, configureOptions: null);

        services.AddAuthorizationBuilder()
            // Secure by default: anything not explicitly [AllowAnonymous] needs a signed-in user.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Too many requests. Please wait a moment and try again."), cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.Auth, httpContext => PerClientIp(httpContext, 30, TimeSpan.FromMinutes(1)));
            options.AddPolicy(RateLimitPolicies.EmailCode, httpContext => PerClientIp(httpContext, 5, TimeSpan.FromMinutes(5)));
        });

        return services;
    }

    /// <summary>The application services behind the controllers.</summary>
    public static IServiceCollection AddLumineServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJewelryService, JewelryService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<AccountDataEraser>();
        return services;
    }

    private static Action<IServiceProvider, HttpClient> ForSupabasePath(string relativePath) =>
        (provider, http) =>
        {
            var settings = provider.GetRequiredService<IOptions<SupabaseSettings>>().Value;
            http.BaseAddress = new Uri($"{settings.BaseUrl}/{relativePath}");
            http.Timeout = SupabaseTimeout;
        };

    private static RateLimitPartition<string> PerClientIp(HttpContext httpContext, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });
}
