using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Eva.Configuration;

namespace Eva.Services
{
    public interface ITurnstileService
    {
        Task<bool> VerifyTokenAsync(string token);
    }

    public class TurnstileService : ITurnstileService
    {
        private readonly HttpClient _httpClient;
        private readonly TurnstileSettings _settings;
        private readonly ILogger<TurnstileService> _logger;

        public TurnstileService(HttpClient httpClient, IOptions<TurnstileSettings> options, ILogger<TurnstileService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<bool> VerifyTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Turnstile verification failed: Token is null or empty.");
                return false;
            }

            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", _settings.SecretKey),
                    new KeyValuePair<string, string>("response", token)
                });

                var response = await _httpClient.PostAsync(_settings.ApiUrl, content);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var turnstileResult = JsonSerializer.Deserialize<TurnstileResponse>(jsonResponse);

                if (turnstileResult != null && turnstileResult.Success)
                {
                    return true;
                }

                _logger.LogWarning("Turnstile verification failed. Result: {Result}", jsonResponse);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Turnstile verification.");
                return false;
            }
        }
    }

    public class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }
}