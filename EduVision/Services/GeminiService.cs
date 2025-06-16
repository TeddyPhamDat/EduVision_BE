using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using EduVision.Models.Config;
using EduVision.Models.DTO;

namespace EduVision.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<GeminiService> _logger;
        private readonly IMongoDbService _mongoDbService;

        public GeminiService(
            HttpClient httpClient,
            IOptions<GeminiConfig> config,
            ILogger<GeminiService> logger,
            IMongoDbService mongoDbService)
        {
            _httpClient = httpClient;
            _apiKey = config.Value.ApiKey;
            _logger = logger;
            _mongoDbService = mongoDbService ?? throw new ArgumentNullException(nameof(mongoDbService));
        }

        public async Task<SlideGenerationResultDto> GenerateEducationSlidesAsync(string subject, string chapter, int? grade = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("API key is missing.");
                return new SlideGenerationResultDto
                {
                    ErrorCode = "MissingApiKey",
                    ErrorMessage = "API key is missing.",
                    HttpStatusCode = 401 // Unauthorized
                };
            }

            if (_mongoDbService == null)
            {
                _logger.LogWarning("MongoDB service not available. Using standard prompt without context.");
                return new SlideGenerationResultDto
                {
                    ErrorCode = "MongoDbServiceUnavailable",
                    ErrorMessage = "MongoDB service is not available.",
                    HttpStatusCode = 503 // Service Unavailable
                };
            }
            try
            {
                var content = await _mongoDbService.GetContentAsync(subject, chapter, grade);
                var metadata = await _mongoDbService.GetMetadataAsync(subject, chapter, grade);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("No content found for {Subject}, {Chapter}, Grade {Grade}. Using standard prompt.",
                        subject, chapter, grade);
                }

                _logger.LogInformation("Found {Length} characters of content for {Subject}, {Chapter}, Grade {Grade}",
                    content.Length, subject, chapter, grade);

                string trimmedContent = content;
                if (content.Length > 8000)
                {
                    trimmedContent = content.Substring(0, 8000) + "... (content truncated due to length)";
                    _logger.LogInformation("Content truncated from {OriginalLength} to 8000 characters", content.Length);
                }

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
                return new SlideGenerationResultDto
                {
                    ErrorCode = "Exception",
                    ErrorMessage = $"Error generating slides: {ex.Message}",
                    HttpStatusCode = 500 // Internal Server Error
                };
            }
        }

        private async Task<SlideGenerationResultDto> SendGeminiRequestAsync(string prompt)
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
                        break;
                    }

                    if ((int)response.StatusCode == 401)
                    {
                        _logger.LogError("Unauthorized: Invalid API key.");
                        return new SlideGenerationResultDto
                        {
                            ErrorCode = "Unauthorized",
                            ErrorMessage = "Invalid API key.",
                            HttpStatusCode = 401
                        };
                    }
                    if ((int)response.StatusCode == 403)
                    {
                        _logger.LogError("Forbidden: Access denied.");
                        return new SlideGenerationResultDto
                        {
                            ErrorCode = "Forbidden",
                            ErrorMessage = "Access denied.",
                            HttpStatusCode = 403
                        };
                    }
                    if ((int)response.StatusCode == 429)
                    {
                        _logger.LogError("Too Many Requests: Rate limit exceeded.");
                        return new SlideGenerationResultDto
                        {
                            ErrorCode = "RateLimitExceeded",
                            ErrorMessage = "Rate limit exceeded.",
                            HttpStatusCode = 429
                        };
                    }
                    if ((int)response.StatusCode == 503)
                    {
                        retryCount++;
                        if (retryCount > maxRetries)
                        {
                            _logger.LogError("Gemini API repeatedly unavailable (503) after {MaxRetries} retries.", maxRetries);
                            return new SlideGenerationResultDto
                            {
                                ErrorCode = "ApiUnavailable",
                                ErrorMessage = $"Gemini API unavailable (503) after {maxRetries} retries.",
                                HttpStatusCode = 503
                            };
                        }
                        _logger.LogWarning("Gemini API unavailable (503). Retrying {RetryCount}/{MaxRetries}...", retryCount, maxRetries);
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }
                    else
                    {
                        _logger.LogError("Gemini API failed: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
                        return new SlideGenerationResultDto
                        {
                            ErrorCode = "ApiError",
                            ErrorMessage = $"Gemini API failed: {response.StatusCode} - {responseBody}",
                            HttpStatusCode = (int)response.StatusCode
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request failed.");
                    return new SlideGenerationResultDto
                    {
                        ErrorCode = "RequestException",
                        ErrorMessage = $"Request failed: {ex.Message}",
                        HttpStatusCode = 500
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
                        // Remove code block markers if present
                        text = text.Trim();
                        if (text.StartsWith("```"))
                        {
                            var firstNewline = text.IndexOf('\n');
                            if (firstNewline >= 0)
                                text = text.Substring(firstNewline + 1);
                            if (text.EndsWith("```"))
                                text = text.Substring(0, text.Length - 3);
                            text = text.Trim();
                        }

                        // Try to extract the first JSON array from the text
                        int start = text.IndexOf('[');
                        int end = text.LastIndexOf(']');
                        if (start >= 0 && end > start)
                        {
                            string jsonArray = text.Substring(start, end - start + 1);
                            try
                            {
                                var slides = JsonSerializer.Deserialize<List<LessonSlideDto>>(jsonArray, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                return new SlideGenerationResultDto
                                {
                                    ErrorCode = "Success",
                                    Slides = slides ?? new List<LessonSlideDto>(),
                                    HttpStatusCode = 200
                                };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to parse slides JSON. Raw text: {Text}", text);
                                return new SlideGenerationResultDto
                                {
                                    ErrorCode = "ParseException",
                                    ErrorMessage = $"Failed to extract slide content: {ex.Message}",
                                    HttpStatusCode = 502
                                };
                            }
                        }
                        else
                        {
                            _logger.LogError("No JSON array found in Gemini response. Raw text: {Text}", text);
                            return new SlideGenerationResultDto
                            {
                                ErrorCode = "ParseError",
                                ErrorMessage = "No JSON array found in Gemini response.",
                                HttpStatusCode = 502
                            };
                        }
                    }
                    else
                    {
                        _logger.LogError("Extracted text is null or empty.");
                        return new SlideGenerationResultDto
                        {
                            ErrorCode = "ParseError",
                            ErrorMessage = "Extracted text is null or empty.",
                            HttpStatusCode = 502 // Bad Gateway (invalid response from upstream)
                        };
                    }
                }
                else
                {
                    _logger.LogError("Unexpected Gemini response structure.");
                    return new SlideGenerationResultDto
                    {
                        ErrorCode = "ParseError",
                        ErrorMessage = "Unexpected Gemini response structure.",
                        HttpStatusCode = 502
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract slide content.");
                return new SlideGenerationResultDto
                {
                    ErrorCode = "ParseException",
                    ErrorMessage = $"Failed to extract slide content: {ex.Message}",
                    HttpStatusCode = 502
                };
            }
        }
    }
}