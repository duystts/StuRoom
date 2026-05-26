using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace StuRoom.Services;

/// <summary>
/// Real SMTP email sender. Set "Smtp:Host" in config (or user secrets) to enable.
/// If Host is empty/missing, emails are skipped silently (dev fallback).
/// </summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtp = config.GetSection("Smtp");
        var host  = smtp["Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            // Dev mode — just log
            logger.LogInformation("[EMAIL-DEV] To: {To} | Subject: {Subject}", email, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, int.Parse(smtp["Port"] ?? "587"))
            {
                EnableSsl   = bool.Parse(smtp["EnableSsl"] ?? "true"),
                Credentials = new NetworkCredential(smtp["User"], smtp["Password"])
            };

            using var mail = new MailMessage(smtp["From"] ?? smtp["User"]!, email)
            {
                Subject    = subject,
                Body       = htmlMessage,
                IsBodyHtml = true
            };

            await client.SendMailAsync(mail);
            logger.LogInformation("[EMAIL] Sent to {To}: {Subject}", email, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[EMAIL] Failed to send to {To}: {Subject}", email, subject);
        }
    }
}
