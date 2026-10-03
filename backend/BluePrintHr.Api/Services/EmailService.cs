using System.Net;
using System.Net.Mail;

namespace BluePrintHr.Api.Services;

public interface IEmailService
{
    Task SendAsync(string recipient, string subject, string htmlBody);
}

public sealed class SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string htmlBody)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("Email delivery is not configured; notification to {Recipient} was not sent.", recipient);
            return;
        }

        var port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue("Email:EnableSsl", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(from, recipient, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
