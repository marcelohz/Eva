using System.ComponentModel.DataAnnotations;

namespace Eva.Configuration
{
    public class TurnstileSettings
    {
        [Required(ErrorMessage = "Turnstile SecretKey is required for CAPTCHA validation.")]
        public string SecretKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Turnstile SiteKey is required for CAPTCHA rendering.")]
        public string SiteKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Turnstile ApiUrl is required.")]
        [Url(ErrorMessage = "Turnstile ApiUrl must be a valid URL.")]
        public string ApiUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    }
}