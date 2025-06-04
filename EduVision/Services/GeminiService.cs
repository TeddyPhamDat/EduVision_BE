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
        private readonly MongoDbService? _mongoDbService;

        public GeminiService(
            HttpClient httpClient,
            IOptions<GeminiConfig> config,
            ILogger<GeminiService> logger,
            MongoDbService? mongoDbService = null)
        {
            _httpClient = httpClient;
            _apiKey = config.Value.ApiKey;
            _logger = logger;
            _mongoDbService = mongoDbService;
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

            return await SendGeminiRequestAsync(fullPrompt);
        }

        public async Task<List<LessonSlide>> GenerateEducationSlidesAsync(string subject, string chapter, int? grade = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("API key is missing.");
                return new List<LessonSlide>
                {
                    new LessonSlide { Title = "Error", Content = "API key is missing." }
                };
            }

            if (_mongoDbService == null)
            {
                _logger.LogWarning("MongoDB service not available. Using standard prompt without context.");
                return await GenerateSlidesAsync($"{subject} - {chapter}");
            }

            try
            {
                // Get content from MongoDB based on subject, chapter, and grade
                var content = await _mongoDbService.GetContentAsync(subject, chapter, grade);
                var metadata = await _mongoDbService.GetMetadataAsync(subject, chapter, grade);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("No content found for {Subject}, {Chapter}, Grade {Grade}. Using standard prompt.",
                        subject, chapter, grade);
                    return await GenerateSlidesAsync($"{subject} - {chapter}");
                }

                _logger.LogInformation("Found {Length} characters of content for {Subject}, {Chapter}, Grade {Grade}",
                    content.Length, subject, chapter, grade);

                // Trim content if it's too long for the API
                string trimmedContent = content;
                if (content.Length > 8000)
                {
                    trimmedContent = content.Substring(0, 8000) + "... (content truncated due to length)";
                    _logger.LogInformation("Content truncated from {OriginalLength} to 8000 characters", content.Length);
                }

                // Create a prompt that uses the educational content as context
                string title = metadata?.Title ?? chapter;
                string fullPrompt = $$"""
                Create a JSON array of 5 educational slides about the Vietnamese subject "{{subject}}" chapter "{{title}}".
                Use the following textbook content as the authoritative source for the slides:

                {{trimmedContent}}

                Each slide should have:
                - "title": a concise, descriptive title in Vietnamese
                - "content": a 4-5 sentence explanation in Vietnamese that summarizes key concepts from the textbook

                The slides should cover the most important points from the textbook content, presented in a logical order.

                Format:
                [
                  { "title": "Slide 1 Title", "content": "Slide 1 content..." },
                  { "title": "Slide 2 Title", "content": "Slide 2 content..." },
                  ...
                ]
                """;

                return await SendGeminiRequestAsync(fullPrompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating slides with MongoDB content for {Subject}, {Chapter}, Grade {Grade}",
                    subject, chapter, grade);
                return await GenerateSlidesAsync($"{subject} - {chapter}");
            }
        }

        private async Task<List<LessonSlide>> SendGeminiRequestAsync(string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new {
                        parts = new[] {
                            new { text = prompt }
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

                    if (!string.IsNullOrEmpty(text))
                    {
                        var slides = JsonSerializer.Deserialize<List<LessonSlide>>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return slides ?? new List<LessonSlide>();
                    }
                    else
                    {
                        _logger.LogError("Extracted text is null or empty.");
                        return new List<LessonSlide>
                        {
                            new LessonSlide { Title = "Parse Error", Content = "Extracted text is null or empty." }
                        };
                    }
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