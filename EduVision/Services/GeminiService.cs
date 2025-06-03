using EduVision.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace EduVision.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(HttpClient httpClient, IOptions<GeminiConfig> config, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _apiKey = config.Value.ApiKey;
            _logger = logger;
        }

        public async Task<List<LessonSlide>> GenerateSlidesAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("API key is missing.");
                return new List<LessonSlide>
                {
                    new LessonSlide { Title = "Error", Content = "API key is missing." }
                };
            }

            var fullPrompt = $$"""
            Create a JSON array of 5 slides. Each slide should have:
            - "title": a short title of the slide
            - "content": a 2-3 sentence explanation

            Topic: {{prompt}}

            Format:
            [
              { "title": "Slide 1", "content": "..." },
              { "title": "Slide 2", "content": "..." },
              ...
            ]
            """;

            var requestBody = new
            {
                contents = new[]
                {
                    new {
                        parts = new[] {
                            new { text = fullPrompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

            const int maxRetries = 10;
            int retryCount = 0;
            int delayMs = 1000;

            HttpResponseMessage response = null!;
            string responseBody = string.Empty;

            while (retryCount <= maxRetries)
            {
                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                    responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        break; // Success, exit retry loop
                    }

                    // Retry only on 503 (Service Unavailable)
                    if ((int)response.StatusCode == 503)
                    {
                        retryCount++;
                        if (retryCount > maxRetries)
                        {
                            _logger.LogError("Gemini API repeatedly unavailable (503) after {MaxRetries} retries.", maxRetries);
                            return new List<LessonSlide>
                            {
                                new LessonSlide { Title = "Error", Content = $"Gemini API unavailable (503) after {maxRetries} retries." }
                            };
                        }
                        _logger.LogWarning("Gemini API unavailable (503). Retrying {RetryCount}/{MaxRetries}...", retryCount, maxRetries);
                        await Task.Delay(delayMs);
                        delayMs *= 2; // Exponential backoff
                        continue;
                    }
                    else
                    {
                        // For other errors, do not retry
                        _logger.LogError("Gemini API failed: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
                        return new List<LessonSlide>
                        {
                            new LessonSlide { Title = "Error", Content = $"Gemini API failed: {response.StatusCode} - {responseBody}" }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request failed.");
                    return new List<LessonSlide>
                    {
                        new LessonSlide { Title = "Error", Content = $"Request failed: {ex.Message}" }
                    };
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentProp) &&
                    contentProp.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    var text = textElement.GetString();

                    // Remove Markdown code block markers if present
                    if (text != null)
                    {
                        text = text.Trim();
                        if (text.StartsWith("```"))
                        {
                            var firstNewline = text.IndexOf('\n');
                            if (firstNewline >= 0)
                            {
                                text = text.Substring(firstNewline + 1);
                            }
                            if (text.EndsWith("```"))
                            {
                                text = text.Substring(0, text.Length - 3);
                            }
                            text = text.Trim();
                        }
                    }

                    var slides = JsonSerializer.Deserialize<List<LessonSlide>>(text,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return slides ?? new List<LessonSlide>();
                }
                else
                {
                    _logger.LogError("Unexpected Gemini response structure.");
                    return new List<LessonSlide>
                    {
                        new LessonSlide { Title = "Parse Error", Content = "Unexpected Gemini response structure." }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract slide content.");
                return new List<LessonSlide>
                {
                    new LessonSlide { Title = "Parse Error", Content = $"Failed to extract slide content: {ex.Message}" }
                };
            }
        }
    }
}