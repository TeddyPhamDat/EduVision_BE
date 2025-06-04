using EduVision.DBContext;
using EduVision.Models;
using EduVision.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xabe.FFmpeg;

////FFmpeg.SetExecutablesPath("path/to/ffmpeg-folder");
//FFmpeg.SetExecutablesPath(@"C:\tools\bin");
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    //.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);

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
builder.Services.AddSwaggerGen();

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
builder.Services.AddSingleton<CloudinaryImageService>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

app.UseAuthorization();

app.MapControllers();

app.Run();
