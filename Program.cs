using Lumine.Backend.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// i-register yung controllers — JSON serialization na may ReferenceLoopHandling para safe
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

// i-register yung services sa DI container — singleton yung Supabase, scoped yung Jewelry
builder.Services.AddSingleton<SupabaseService>();
builder.Services.AddScoped<JewelryService>();
builder.Services.AddEndpointsApiExplorer();

// i-setup yung Swagger docs — may title at version para maayos sa Scalar UI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title       = "Lumine API",
        Version     = "v1",
        Description = "Backend API for the Lumine AR Jewelry Try-On App"
    });
});

// i-configure yung CORS — allow lahat para hindi mag-block yung Android app requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAndroidApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// i-initialize yung Supabase connection bago mag-accept ng requests
var supabaseService = app.Services.GetRequiredService<SupabaseService>();
await supabaseService.InitializeAsync();

// i-setup yung Scalar API docs UI — DeepSpace theme, CSharp HttpClient ang default
app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
app.MapScalarApiReference(options =>
{
    options.Title             = "Lumine API";
    options.Theme             = ScalarTheme.DeepSpace;
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// i-enable yung CORS policy tapos i-map lahat ng controllers
app.UseCors("AllowAndroidApp");
app.MapControllers();

// i-run sa port 5111 — accessible sa lahat ng interfaces para sa Android emulator/device
app.Run("http://0.0.0.0:5111");
