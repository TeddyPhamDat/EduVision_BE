using EduVision.Models;
using EduVision.Services;
using Xabe.FFmpeg;

//FFmpeg.SetExecutablesPath("path/to/ffmpeg-folder");
FFmpeg.SetExecutablesPath(@"C:\tools\bin");
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    //.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Secrets/appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//------------------------------------------------------------// Add custom services
builder.Services.AddOptions<GeminiConfig>()
    .Bind(builder.Configuration.GetSection("Gemini"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddSingleton<CloudinaryImageService>();
builder.Services.AddSingleton<RevealJsGenerator>();
builder.Services.AddSingleton<TextToSpeechService>();
builder.Services.AddSingleton<AzureBlobStorageService>();
builder.Services.AddSingleton<SlideCaptureService>();
builder.Services.AddSingleton<VideoGenerationService>();
builder.Services.Configure<ScreenshotApiConfig>(
    builder.Configuration.GetSection("ScreenshotApi"));

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
