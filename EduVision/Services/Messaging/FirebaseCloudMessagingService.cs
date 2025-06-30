using Google.Apis.Auth.OAuth2;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace EduVision.Services.Messaging
{
    public class FirebaseCloudMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectId;
        private readonly GoogleCredential _googleCredential;
        private readonly string _serviceAccountJson;

        public FirebaseCloudMessagingService(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _projectId = config["Firebase:ProjectId"];

            // Lấy service account từ cấu hình và serialize thành JSON string
            var serviceAccountSection = config.GetSection("Firebase:ServiceAccount");
            var serviceAccountObject = serviceAccountSection.Get<Dictionary<string, object>>();
            _serviceAccountJson = JsonSerializer.Serialize(serviceAccountObject);
            // Load service account credentials từ JSON
            _googleCredential = GoogleCredential.FromJson(_serviceAccountJson)
                                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        public async Task SendSlideGeneratedAsync(string fcmToken, string slideUrl)
        {
            // Get OAuth2 access token
            var accessToken = await _googleCredential.UnderlyingCredential
                .GetAccessTokenForRequestAsync("https://www.googleapis.com/auth/firebase.messaging");

            // Build the message
            var message = new
            {
                message = new
                {
                    token = fcmToken,
                    notification = new  // Thêm notification
                    {
                        title = "Slide đã sẵn sàng!",
                        body = "Slide mới đã được tạo thành công."
                    },
                    data = new
                    {
                        type = "slide_generated", // ✅ Giữ nguyên
                        slideUrl = slideUrl
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(message);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send FCM message: {response.StatusCode}, {error}");
            }
        }

        public async Task SendVideoGeneratedAsync(string fcmToken, string videoUrl)
        {
            // Get OAuth2 access token
            var accessToken = await _googleCredential.UnderlyingCredential
                .GetAccessTokenForRequestAsync("https://www.googleapis.com/auth/firebase.messaging");

            // Build the message
            var message = new
            {
                message = new
                {
                    token = fcmToken,
                    notification = new  // Thêm notification để hiển thị đẹp hơn
                    {
                        title = "Video đã sẵn sàng!",
                        body = "Video mới đã được tạo thành công."
                    },
                    data = new
                    {
                        type = "video_generated", // ✅ Sửa thành "video_generated"
                        videoUrl = videoUrl
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(message);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send FCM message: {response.StatusCode}, {error}");
            }
        }
    }
}
