using EduVision.DBContext;
using EduVision.Models.Config;
using EduVision.Models.DTO.Response;
using EduVision.Services.AI;
using EduVision.Services.Authentication;
using EduVision.Services.Data;
using EduVision.Services.Media;
using EduVision.Services.Messaging;
using EduVision.Services.Presentation;
using EduVision.Services.Storage;
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

// Register all required services for the application, including controllers, Swagger, authentication, and custom services.
builder.Services.AddControllers();

// Enable OpenAPI/Swagger for API documentation and testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "EduVision API",
        Version = "v2",
        Description = "EduVision API provides endpoints for generating educational slides and video lessons using AI. Use the endpoints below to create, manage, and retrieve educational content."
    });

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

    // Enable XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// In development, load sensitive configuration from a separate secrets file.
// This allows you to keep secrets out of source control.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);
}

//---------------------- Register custom and infrastructure services ----------------------

// Register Kafka producer service for slide generation events.
builder.Services.Configure<KafkaConfig>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<KafkaProducerService>();

// Register Kafka consumer service for processing slide generation requests.
builder.Services.AddHostedService<SlideGenerationConsumer>();

// Register Kafka consumer service for processing video results
builder.Services.AddHostedService<VideoResultConsumer>();

// Register MongoDB configuration and service for accessing educational content.
builder.Services.AddOptions<MongoDbConfig>()
    .Bind(builder.Configuration.GetSection("MongoDB"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<IMongoDbService, MongoDbService>();

// Register Gemini AI configuration and service for slide generation.
builder.Services.AddOptions<GeminiConfig>()
    .Bind(builder.Configuration.GetSection("Gemini"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<IGeminiService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<IOptions<GeminiConfig>>();
    var logger = sp.GetRequiredService<ILogger<GeminiService>>();
    var mongoDbService = sp.GetRequiredService<IMongoDbService>();
    return new GeminiService(httpClient, options, logger, mongoDbService);
});

// Register other application services for dependency injection.
builder.Services.AddHttpClient();
builder.Services.AddScoped<JwtService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IQuotaService, QuotaService>();

// Register PayOS payment gateway client for handling payment operations.
var configuration = builder.Configuration;
var clientId = configuration["PayOS:ClientId"];
var apiKey = configuration["PayOS:ApiKey"];
var checksumKey = configuration["PayOS:ChecksumKey"];
builder.Services.AddSingleton(new PayOS(clientId, apiKey, checksumKey));

// Register services for presentation and media generation.
builder.Services.AddScoped<SlideImageSelectorService>();
builder.Services.AddScoped<RevealJsGenerator>();
builder.Services.AddSingleton<AzureBlobStorageService>();
builder.Services.AddScoped<IImageStorageService, AzureBlobImageStorage>();
builder.Services.AddScoped<SlideCaptureService>();
builder.Services.AddScoped<IVideoStorageService, AzureBlobVideoStorage>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Register configuration for screenshot API integration.
builder.Services.Configure<ScreenshotApiConfig>(
    builder.Configuration.GetSection("ScreenshotApi"));

// Register Entity Framework Core context for SQL Server database access.
builder.Services.AddDbContext<EduVisionContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register text-to-speech service for generating audio from text.
builder.Services.AddScoped<TextToSpeechService>();

// Register Firebase Cloud Messaging service for sending notifications.
builder.Services.AddHttpClient<FirebaseCloudMessagingService>();

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
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Enhanced logging configuration for Azure
builder.Logging.ClearProviders();

// Add Azure-specific logging for production
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddAzureWebAppDiagnostics();
}

// Configure detailed logging
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

// Configure activity tracking
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId;
});

var app = builder.Build();

// Enable Swagger UI for API exploration and testing.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v2/swagger.json", "EduVision API v2");
    // c.RoutePrefix = string.Empty; // Uncomment to serve at root
});

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

//app.UseCors("AllowFrontend");
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Map controller routes to endpoints.
app.MapControllers();

// Start the web application.
app.Run();