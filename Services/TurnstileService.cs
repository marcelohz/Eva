using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eva.Services
{
    public interface ITurnstileService
    {
        // Added '?' to explicitly allow nulls, satisfying the compiler warning
        Task<bool> VerifyTokenAsync(string? token);
    }

    public class TurnstileSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string SiteKey { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    }

    public class TurnstileService : ITurnstileService
    {
        private readonly HttpClient _httpClient;
        private readonly TurnstileSettings _settings;

        public TurnstileService(HttpClient httpClient, IOptions<TurnstileSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<bool> VerifyTokenAsync(string? token)
        {
            // Now gracefully handles nulls without compiler warnings
            if (string.IsNullOrEmpty(token)) return false;

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", _settings.SecretKey),
                new KeyValuePair<string, string>("response", token)
            });

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content);
            if (!response.IsSuccessStatusCode) return false;

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TurnstileResponse>(jsonString);

            return result?.Success ?? false;
        }

        private class TurnstileResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }
    }
}