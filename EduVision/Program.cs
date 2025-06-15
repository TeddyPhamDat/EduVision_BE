using EduVision.DBContext;
using EduVision.Models.Config;
using EduVision.Models.DTO.Response;
using EduVision.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Net.payOS;
using System.Diagnostics;
using System.Text;
using Xabe.FFmpeg;

var builder = WebApplication.CreateBuilder(args);

// Set the FFmpeg executables path so that video/audio processing features can find the required binaries.
// This is essential for any functionality that relies on FFmpeg, such as video generation.
var ffmpegPath = builder.Configuration["FFmpeg:ExecutablesPath"];
if (!string.IsNullOrEmpty(ffmpegPath))
{
    // Convert a relative path to an absolute one, which is necessary for cloud deployments (e.g., Azure).
    if (!Path.IsPathRooted(ffmpegPath))
    {
        ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), ffmpegPath);
    }
    FFmpeg.SetExecutablesPath(ffmpegPath);
}

// In production, ensure the FFmpeg binary has execute permissions (especially on Linux).
// This is required because deployment processes may strip execute permissions, causing FFmpeg to fail at runtime.
if (!builder.Environment.IsDevelopment())
{
    var ffmpegBinary = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ffmpeg", "ffmpeg");
    if (File.Exists(ffmpegBinary))
    {
        try
        {
            var processInfo = new ProcessStartInfo("chmod", $"+x {ffmpegBinary}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var process = Process.Start(processInfo);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            // Log the error but do not prevent the application from starting.
            Console.WriteLine($"Warning: Could not set execute permissions for FFmpeg: {ex.Message}");
        }
    }
}

// Register all required services for the application, including controllers, Swagger, authentication, and custom services.
builder.Services.AddControllers();

// Enable OpenAPI/Swagger for API documentation and testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EduVision API", Version = "v1" });

    // Add JWT Bearer authentication to Swagger UI so that protected endpoints can be tested.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// In development, load sensitive configuration from a separate secrets file.
// This allows you to keep secrets out of source control.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);
}

//---------------------- Register custom and infrastructure services ----------------------

// Register MongoDB configuration and service for accessing educational content.
builder.Services.AddOptions<MongoDbConfig>()
    .Bind(builder.Configuration.GetSection("MongoDB"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<MongoDbService>();

// Register Gemini AI configuration and service for slide generation.
builder.Services.AddOptions<GeminiConfig>()
    .Bind(builder.Configuration.GetSection("Gemini"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<GeminiService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<IOptions<GeminiConfig>>();
    var logger = sp.GetRequiredService<ILogger<GeminiService>>();
    var mongoDbService = sp.GetRequiredService<MongoDbService>();
    return new GeminiService(httpClient, options, logger, mongoDbService);
});

// Register other application services for dependency injection.
builder.Services.AddHttpClient();
builder.Services.AddScoped<JwtService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddSingleton<CloudinaryImageService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();

// Register PayOS payment gateway client for handling payment operations.
var configuration = builder.Configuration;
var clientId = configuration["PayOS:ClientId"];
var apiKey = configuration["PayOS:ApiKey"];
var checksumKey = configuration["PayOS:ChecksumKey"];
builder.Services.AddSingleton(new PayOS(clientId, apiKey, checksumKey));

// Register services for presentation and media generation.
builder.Services.AddSingleton<RevealJsGenerator>();
builder.Services.AddSingleton<AzureBlobStorageService>();
builder.Services.AddSingleton<SlideCaptureService>();
builder.Services.AddSingleton<VideoGenerationService>();

// Register configuration for screenshot API integration.
builder.Services.Configure<ScreenshotApiConfig>(
    builder.Configuration.GetSection("ScreenshotApi"));

// Register Entity Framework Core context for SQL Server database access.
builder.Services.AddDbContext<EduVisionContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register text-to-speech service for generating audio from text.
builder.Services.AddSingleton<TextToSpeechService>();

// Configure JWT authentication for securing API endpoints.
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Enable authorization policies for role-based access control.
builder.Services.AddAuthorization();

// Configure CORS to allow requests from the frontend application.
// This is necessary for browser-based clients to interact with the API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder
            .WithOrigins("http://127.0.0.1:5500", "http://localhost:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

// Enable Swagger UI for API exploration and testing.
app.UseSwagger();
app.UseSwaggerUI();

// Set up a global exception handler to return a generic error response for unhandled exceptions.
// This prevents leaking sensitive error details to clients.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\": \"An unexpected error occurred.\"}");
    });
});

// Enforce HTTPS for all requests to improve security.
app.UseHttpsRedirection();

// Use custom middleware for handling application-specific exceptions.
app.UseMiddleware<ExceptionMiddleware>();

// Enable CORS for frontend clients.
app.UseCors("AllowFrontend");

// Enable authentication and authorization for protected endpoints.
app.UseAuthentication();
app.UseAuthorization();

// Map controller routes to endpoints.
app.MapControllers();

// Start the web application.
app.Run();