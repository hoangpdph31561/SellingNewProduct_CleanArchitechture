using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SellingNewProduct.Infrastructure.Messaging.Abstractions;

namespace SellingNewProduct.Infrastructure.Messaging.Services;

/// <summary>Binds the <c>Gateways:Smtp</c> configuration section: the real SMTP host to send through.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Gateways:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>STARTTLS (587) when true; implicit SSL (465) when false.</summary>
    public bool UseStartTls { get; set; } = true;

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SellingNewProduct";
}

/// <summary>
/// Production email gateway: sends a real message over SMTP with MailKit. Selected instead of
/// <see cref="LoggingEmailSender"/> when <c>Gateways:UseReal</c> is true. The command worker still
/// runs this through the shared Polly pipeline, so a dead/slow SMTP host trips the circuit breaker
/// (timeout + retry + fail-fast) instead of blocking the RabbitMQ consumer.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions myOptions;
    private readonly ILogger<SmtpEmailSender> myLogger;

    public SmtpEmailSender(IOptions<SmtpOptions> theOptions, ILogger<SmtpEmailSender> theLogger)
    {
        myOptions = theOptions.Value;
        myLogger = theLogger;
    }

    public async Task SendAsync(EmailMessage theMessage, CancellationToken theCancellationToken = default)
    {
        var aMime = new MimeMessage();
        aMime.From.Add(new MailboxAddress(myOptions.FromName, myOptions.FromAddress));
        aMime.To.Add(MailboxAddress.Parse(theMessage.To));
        aMime.Subject = theMessage.Subject;
        aMime.Body = new TextPart("html") { Text = theMessage.Body };

        using var aClient = new SmtpClient();

        var aSocketOptions = myOptions.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
        await aClient.ConnectAsync(myOptions.Host, myOptions.Port, aSocketOptions, theCancellationToken);

        if (!string.IsNullOrEmpty(myOptions.User))
        {
            await aClient.AuthenticateAsync(myOptions.User, myOptions.Password, theCancellationToken);
        }

        await aClient.SendAsync(aMime, theCancellationToken);
        await aClient.DisconnectAsync(quit: true, theCancellationToken);

        myLogger.LogInformation("📧 SMTP ▶ sent '{Subject}' to {To} via {Host}.", theMessage.Subject, theMessage.To, myOptions.Host);
    }
}
