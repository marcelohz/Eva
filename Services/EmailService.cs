using MailKit.Security;
using Microsoft.Extensions.Hosting; // Required for environment checks
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Eva.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }

    public class EmailSettings
    {
        public bool EnableEmailSend { get; set; } = false;
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;
        private readonly IHostEnvironment _env;

        public EmailService(
            IOptions<EmailSettings> settings,
            ILogger<EmailService> logger,
            IHostEnvironment env)
        {
            _settings = settings.Value;
            _logger = logger;
            _env = env; // Injected to check if we are in Production
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            // FAIL-SAFE: The mock is ONLY allowed if we are explicitly in Development 
            // AND the toggle is set to false. In Production, this is always bypassed.
            if (_env.IsDevelopment() && !_settings.EnableEmailSend)
            {
                _logger.LogWarning("==========================================================");
                _logger.LogWarning("MOCK EMAIL (SENDING DISABLED IN DEV CONFIG)");
                _logger.LogWarning("TO: {ToEmail}", toEmail);
                _logger.LogWarning("SUBJECT: {Subject}", subject);
                _logger.LogWarning("BODY: {Body}", htmlMessage);
                _logger.LogWarning("==========================================================");

                return;
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_settings.SenderEmail, _settings.SmtpPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}