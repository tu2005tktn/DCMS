using System.Net;
using System.Net.Mail;
using System.Text;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
// MailKit.Security is imported for SecureSocketOptions only. Do not add `using MailKit;` at file scope:
// its MailMessage / MailAddress would collide with System.Net.Mail above and make the seam ambiguous.

namespace DCMS.Infrastructure.Features.Authentication;

/// <summary>
/// SMTP settings for S03 recovery email. Values come from environment variables (Smtp__*), never from Git.
/// Gmail: host smtp.gmail.com, port 465 with implicit SSL, or port 587 with STARTTLS; the password is an
/// App Password, never the account password.
/// </summary>
public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "DCMS - Phòng khám nha khoa";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);

    /// <summary>
    /// Port 465 is the implicit-SSL port: TLS is negotiated before the SMTP conversation starts, which
    /// System.Net.Mail.SmtpClient cannot do. Every other port uses STARTTLS when SSL is enabled.
    /// </summary>
    public SecureSocketOptions SocketOptions =>
        Port == 465 ? SecureSocketOptions.SslOnConnect
        : EnableSsl ? SecureSocketOptions.StartTlsWhenAvailable
        : SecureSocketOptions.None;
}

// Seam over the SMTP transport so the adapter can be tested without a mail server. The contract stays on
// System.Net.Mail.MailMessage because that is the message shape the delivery adapter builds; MailKit is an
// implementation detail of the Gmail transport below.
public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken ct);
}

/// <summary>
/// Gmail SMTP transport built on MailKit. MailKit is required rather than SmtpClient because Gmail offers
/// implicit SSL on port 465, and SmtpClient only speaks STARTTLS. Connection and authentication failures are
/// surfaced to the caller so the adapter can record a real failure instead of claiming a send.
/// </summary>
public sealed class SmtpMailSender(IOptions<SmtpSettings> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        using var mime = ToMimeMessage(message, settings);
        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, settings.SocketOptions, ct);
            if (!string.IsNullOrEmpty(settings.UserName))
                await client.AuthenticateAsync(settings.UserName, settings.Password, ct);
            await client.SendAsync(mime, ct);
        }
        finally
        {
            // Never leave an authenticated session open; DisconnectAsync is safe on a never-connected client.
            if (client.IsConnected) await client.DisconnectAsync(quit: true, ct);
        }
    }

    // Fully qualified: MailKit and System.Net.Mail both ship types called MailMessage / MailAddress, and the
    // seam above is the System.Net.Mail one, so the conversion must not rely on an ambiguous using.
    private static MimeMessage ToMimeMessage(System.Net.Mail.MailMessage message, SmtpSettings settings)
    {
        var mime = new MimeMessage();
        // MailMessage.From is a single nullable MailAddress (unlike To/Cc/Bcc, which are collections).
        if (message.From is { } from)
            mime.From.Add(new MailboxAddress(
                string.IsNullOrEmpty(from.DisplayName) ? settings.FromName : from.DisplayName, from.Address));
        AddAll(message.To, mime.To);
        // System.Net.Mail spells this property CC (uppercase); MimeKit calls it Cc.
        AddAll(message.CC, mime.Cc);
        AddAll(message.Bcc, mime.Bcc);
        mime.Subject = message.Subject;
        // MimeKit picks the charset itself; the body is declared UTF-8 by the caller.
        mime.Body = new TextPart(message.IsBodyHtml ? TextFormat.Html : TextFormat.Plain)
        {
            Text = message.Body,
            ContentTransferEncoding = ContentEncoding.Base64
        };
        return mime;
    }

    private static void AddAll(System.Net.Mail.MailAddressCollection source, MimeKit.InternetAddressList target)
    {
        foreach (System.Net.Mail.MailAddress address in source)
            target.Add(new MailboxAddress(address.DisplayName, address.Address));
    }
}

/// <summary>
/// S03 recovery delivery through a real SMTP server. Only the Email channel is delivered; the code is
/// placed in the message body and never written to logs, the outbox or audit.
/// </summary>
public sealed class SmtpContactDeliveryAdapter(
    IMailSender sender,
    IRecoveryOutbox outbox,
    IOptions<SmtpSettings> smtp,
    IOptions<RecoverySettings> recovery,
    ILogger<SmtpContactDeliveryAdapter> logger) : IContactDeliveryAdapter, IEmailVerificationDelivery
{
    public const string Subject = "Mã khôi phục tài khoản DCMS";

    public RecoveryChannel DetectChannel(string contact) => RecoveryContactFormat.DetectChannel(contact);
    public bool IsValid(string contact) => DetectChannel(contact) != RecoveryChannel.Unknown;
    public string Normalize(string contact) => RecoveryContactFormat.Normalize(contact);

    public async Task<bool> SendAsync(string destination, string code, int lifetimeMinutes, CancellationToken ct)
    {
        var address = RecoveryContactFormat.Normalize(destination);
        if (RecoveryContactFormat.DetectChannel(address) != RecoveryChannel.Email) return false;
        var settings = smtp.Value;
        using var message = new MailMessage(
            new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8), new MailAddress(address))
        {
            Subject = "Xác minh email tài khoản DCMS",
            SubjectEncoding = Encoding.UTF8,
            Body = $"Mã xác minh email của bạn: {code}\nMã có hiệu lực trong {lifetimeMinutes} phút và chỉ dùng một lần.\nNếu bạn không yêu cầu, hãy bỏ qua email này.",
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        try
        {
            await sender.SendAsync(message, ct);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
        {
            logger.LogWarning("Email verification delivery failed: {ExceptionType}", ex.GetType().Name);
            return false;
        }
    }

    public async Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination,
        string proof, string resetLink, CancellationToken ct)
    {
        var address = RecoveryContactFormat.Normalize(destination);
        if (RecoveryContactFormat.DetectChannel(address) != RecoveryChannel.Email)
        {
            // No SMS provider exists; report a failure instead of pretending the code was sent.
            await outbox.RecordAsync(accountId, channel, destination, "smtp", "unsupported-channel", providerMessageId: null, ct);
            return false;
        }

        using var message = Build(address, proof, resetLink);
        try
        {
            await sender.SendAsync(message, ct);
        }
        // SmtpException stays on the list because the seam is still System.Net.Mail: a transport behind that
        // seam may raise it, and the unit tests stub the seam with it. The MailKit types below are the real
        // hierarchy in MailKit 4.18: ProtocolException covers SmtpProtocolException, CommandException covers
        // SmtpCommandException, and Security.AuthenticationException (which derives from System.Exception, not
        // from System.Security.Authentication.AuthenticationException) is what a wrong App Password raises.
        // ServiceNotConnected/ServiceNotAuthenticated derive from InvalidOperationException and are covered by it.
        catch (Exception ex) when (ex is SmtpException or IOException or InvalidOperationException
            or MailKit.ProtocolException or MailKit.CommandException
            or MailKit.Security.AuthenticationException or MailKit.Security.SslHandshakeException
            or System.Net.Sockets.SocketException)
        {
            // Only the failure type is logged; the code and the message body stay out of the log.
            logger.LogWarning("Recovery email delivery failed for account {AccountId}: {ExceptionType}", accountId, ex.GetType().Name);
            await outbox.RecordAsync(accountId, RecoveryChannel.Email, address, "smtp", "failed", providerMessageId: null, ct);
            return false;
        }
        logger.LogInformation("Recovery email sent for account {AccountId}", accountId);
        await outbox.RecordAsync(accountId, RecoveryChannel.Email, address, "smtp", "sent", providerMessageId: null, ct);
        return true;
    }

    private MailMessage Build(string address, string proof, string resetLink)
    {
        var minutes = recovery.Value.TokenLifetimeMinutes;
        var settings = smtp.Value;

        // Plain-text twin of every sentence, so a client that ignores HTML still reads the code.
        var html = new StringBuilder()
            .AppendLine("<!DOCTYPE html><html><body style=\"margin:0;padding:0;background:#f4f6f8;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">")
            .AppendLine("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f6f8;padding:24px 0;\"><tr><td align=\"center\">")
            .AppendLine("<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;width:100%;background:#ffffff;border-radius:8px;border:1px solid #e5e7eb;\">")
            .AppendLine("<tr><td style=\"padding:28px 32px 8px 32px;\">")
            .AppendLine("<h1 style=\"margin:0;font-size:20px;color:#111827;\">Mã khôi phục tài khoản DCMS</h1>")
            .AppendLine("</td></tr>")
            .AppendLine("<tr><td style=\"padding:0 32px 4px 32px;\">")
            .AppendLine("<p style=\"margin:0 0 16px 0;font-size:15px;color:#374151;line-height:1.6;\">Xin chào,</p>")
            .AppendLine("<p style=\"margin:0 0 16px 0;font-size:15px;color:#374151;line-height:1.6;\">Bạn (hoặc ai đó) vừa yêu cầu khôi phục tài khoản DCMS gắn với địa chỉ email này.</p>")
            .AppendLine("</td></tr>")
            .AppendLine("<tr><td style=\"padding:4px 32px 4px 32px;\">")
            .AppendLine("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;\"><tr><td align=\"center\" style=\"padding:20px;\">")
            .AppendLine("<p style=\"margin:0 0 8px 0;font-size:12px;letter-spacing:1px;color:#6b7280;text-transform:uppercase;\">Mã xác minh của bạn</p>")
            .AppendLine($"<p style=\"margin:0;font-size:32px;font-weight:bold;letter-spacing:8px;color:#111827;\">{WebUtility.HtmlEncode(proof)}</p>")
            .AppendLine("</td></tr></table>")
            .AppendLine("</td></tr>")
            .AppendLine("<tr><td style=\"padding:16px 32px 4px 32px;\">")
            .AppendLine($"<p style=\"margin:0 0 16px 0;font-size:15px;color:#374151;line-height:1.6;\">Mã có hiệu lực trong {minutes} phút và chỉ dùng được một lần.</p>");

        if (!string.IsNullOrWhiteSpace(resetLink))
        {
            var href = WebUtility.HtmlEncode(resetLink);
            html.AppendLine("<p style=\"margin:0 0 16px 0;font-size:15px;line-height:1.6;\">")
                .AppendLine($"<a href=\"{href}\" style=\"color:#2563eb;\">Hoặc mở {href}</a>")
                .AppendLine("</p>");
        }

        html.AppendLine("</td></tr>")
            .AppendLine("<tr><td style=\"padding:4px 32px 4px 32px;\">")
            .AppendLine("<p style=\"margin:0 0 16px 0;font-size:15px;color:#374151;line-height:1.6;\">Nếu bạn không yêu cầu khôi phục, hãy bỏ qua email này; mật khẩu hiện tại của bạn không thay đổi.</p>")
            .AppendLine("<p style=\"margin:0 0 16px 0;font-size:15px;color:#374151;line-height:1.6;\">Không chia sẻ mã này với bất kỳ ai, kể cả nhân viên phòng khám.</p>")
            .AppendLine("<p style=\"margin:0;font-size:14px;color:#6b7280;\">DCMS - Phòng khám nha khoa</p>")
            .AppendLine("</td></tr>")
            .AppendLine("</table></td></tr></table></body></html>");

        return new MailMessage(new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8), new MailAddress(address))
        {
            Subject = Subject, SubjectEncoding = Encoding.UTF8,
            Body = html.ToString(), BodyEncoding = Encoding.UTF8, IsBodyHtml = true
        };
    }
}
