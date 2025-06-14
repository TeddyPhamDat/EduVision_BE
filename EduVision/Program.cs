using EduVision.DBContext;
using EduVision.Models.Config;
using EduVision.Models.DTO.Response;
using EduVision.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Net.payOS;
using System.Diagnostics;
using System.Text;
using Xabe.FFmpeg;


////FFmpeg.SetExecutablesPath("path/to/ffmpeg-folder");
//FFmpeg.SetExecutablesPath(@"C:\tools\bin");
var builder = WebApplication.CreateBuilder(args);

//builder.Configuration
//    //.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
//    .AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Configure FFmpeg path from configuration
var ffmpegPath = builder.Configuration["FFmpeg:ExecutablesPath"];
if (!string.IsNullOrEmpty(ffmpegPath))
{
    // Convert relative path to absolute for Azure
    if (!Path.IsPathRooted(ffmpegPath))
    {
        ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), ffmpegPath);
    }
    FFmpeg.SetExecutablesPath(ffmpegPath);
}

// After setting the FFmpeg path in production
if (!builder.Environment.IsDevelopment())
{
    var ffmpegBinary = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ffmpeg", "ffmpeg");
    if (File.Exists(ffmpegBinary))
    {
        try
        {
            // Set execute permission for Linux
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
            // Log the error but don't fail startup
            Console.WriteLine($"Warning: Could not set execute permissions for FFmpeg: {ex.Message}");
        }
    }
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EduVision API", Version = "v1" });

    // Add JWT Bearer
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

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);
}

//------------------------------------------------------------// Add custom services
// Register MongoDbConfig before MongoDbService
builder.Services.AddOptions<MongoDbConfig>()
    .Bind(builder.Configuration.GetSection("MongoDB"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register MongoDbService as a singleton
builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddOptions<GeminiConfig>()
    .Bind(builder.Configuration.GetSection("Gemini"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Update the registration for GeminiService
builder.Services.AddSingleton<GeminiService>(sp => 
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var options = sp.GetRequiredService<IOptions<GeminiConfig>>();
    var logger = sp.GetRequiredService<ILogger<GeminiService>>();
    var mongoDbService = sp.GetRequiredService<MongoDbService>();
    return new GeminiService(httpClient, options, logger, mongoDbService);
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<JwtService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddSingleton<CloudinaryImageService>();
builder.Services.AddScoped<IQuotaService,QuotaService>();
//builder.Services.AddHttpClient<PayOSClient>();
var configuration = builder.Configuration;
var clientId = configuration["PayOS:ClientId"];
var apiKey = configuration["PayOS:ApiKey"];
var checksumKey = configuration["PayOS:ChecksumKey"];

builder.Services.AddSingleton(new PayOS(clientId, apiKey, checksumKey));

builder.Services.AddSingleton<RevealJsGenerator>();
builder.Services.AddSingleton<AzureBlobStorageService>();
builder.Services.AddSingleton<SlideCaptureService>();
builder.Services.AddSingleton<VideoGenerationService>();


builder.Services.Configure<ScreenshotApiConfig>(
    builder.Configuration.GetSection("ScreenshotApi"));

builder.Services.AddDbContext<EduVisionContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Replace the existing TextToSpeechService registration
builder.Services.AddSingleton<TextToSpeechService>();

// Add authentication
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

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder
            .WithOrigins("http://127.0.0.1:5500", "http://localhost:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // n?u c?n g?i cookie/token
});


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\": \"An unexpected error occurred.\"}");
    });
});

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
