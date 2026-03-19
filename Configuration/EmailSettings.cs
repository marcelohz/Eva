using System.ComponentModel.DataAnnotations;

namespace Eva.Configuration
{
    public class EmailSettings
    {
        public bool EnableEmailSend { get; set; } = false;

        [Required(ErrorMessage = "SmtpServer is required to send emails.")]
        public string SmtpServer { get; set; } = string.Empty;

        [Range(1, 65535, ErrorMessage = "SmtpPort must be a valid port number between 1 and 65535.")]
        public int SmtpPort { get; set; }

        [Required(ErrorMessage = "SenderName is required.")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "SenderEmail is required.")]
        [EmailAddress(ErrorMessage = "SenderEmail must be a valid email format.")]
        public string SenderEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "SmtpPassword is required for SMTP authentication.")]
        public string SmtpPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "AuthEmail is required for SMTP authentication.")]
        [EmailAddress(ErrorMessage = "AuthEmail must be a valid email format.")]
        public string AuthEmail { get; set; } = string.Empty;
    }
}