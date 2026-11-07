using Lumine.Backend.Errors;
using Lumine.Backend.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Port 5111 on every interface by default - what the app and ngrok expect in
// development. A host that sets ASPNETCORE_URLS or "Urls" wins; the production
// service binds to localhost only, behind the HTTPS reverse proxy.
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5111");
}

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

// One line per outgoing Supabase call is noise; failures are still logged by the clients.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services
    .AddSupabase(builder.Configuration)
    .AddLumineSecurity()
    .AddLumineServices();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ErrorResponse.FromInvalidModelState);

// Only explicit [Required] fields are mandatory; optional text may legitimately be empty.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

builder.Services.AddHostedService<DatabaseSchemaCheck>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Caddy (production) and ngrok (development) proxy from localhost, which is
// trusted by default, so logs and rate limits see the real client address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var apiDocsEnabled = builder.Configuration.GetValue("ApiDocs:Enabled", builder.Environment.IsDevelopment());
if (apiDocsEnabled)
{
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Lumine API";
            document.Info.Version = "v1";
            document.Info.Description = "Backend for the Lumine AR jewelry try-on app.";
            return Task.CompletedTask;
        });
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });
}

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseExceptionHandler();

if (apiDocsEnabled)
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Lumine API";
        options.Theme = ScalarTheme.DeepSpace;
    }).AllowAnonymous();
}

if (corsOrigins.Length > 0)
{
    app.UseCors();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Public pages that the Google Play listing links to.
foreach (var page in new[] { "privacy", "delete-account" })
{
    var file = Path.Combine(app.Environment.WebRootPath, page + ".html");
    app.MapGet("/" + page, () => Results.File(file, "text/html; charset=utf-8"))
        .AllowAnonymous()
        .ExcludeFromDescription();
}

app.Run();

/// <summary>Application entry point; public so the integration tests can host it.</summary>
public partial class Program;
