using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Eva.Configuration;

namespace Eva.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            if (!_settings.EnableEmailSend)
            {
                _logger.LogInformation("Email sending is disabled in configuration. Skipping email to {ToEmail}", toEmail);
                return;
            }

            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;
                email.Body = new TextPart(TextFormat.Html) { Text = htmlMessage };

                using var smtp = new SmtpClient();

                // Connect to the SMTP server (e.g., Office365)
                await smtp.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, SecureSocketOptions.StartTls);

                // Authenticate using the specific AuthEmail and App Password
                await smtp.AuthenticateAsync(_settings.AuthEmail, _settings.SmtpPassword);

                // Send the payload
                await smtp.SendAsync(email);

                // Disconnect cleanly
                await smtp.DisconnectAsync(true);

                _logger.LogInformation("Successfully sent email to {ToEmail} with subject '{Subject}'", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}. SMTP Server: {SmtpServer}:{SmtpPort}, Auth User: {AuthUser}",
                    toEmail, _settings.SmtpServer, _settings.SmtpPort, _settings.AuthEmail);
                // We rethrow the exception so Hangfire marks the job as failed and triggers its retry logic
                throw;
            }
        }
    }
}