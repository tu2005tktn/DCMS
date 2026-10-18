using System.Net;
using System.Net.Mail;
using System.Text;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DCMS.Infrastructure.Features.Authentication;

// SMTP settings for S03 recovery email. Values come from environment variables (Smtp__*), never from Git.
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
}

// Seam over the SMTP transport so the adapter can be tested without a mail server.
public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken ct);
}

public sealed class SmtpMailSender(IOptions<SmtpSettings> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrEmpty(settings.UserName)) client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        await client.SendMailAsync(message, ct);
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
    ILogger<SmtpContactDeliveryAdapter> logger) : IContactDeliveryAdapter
{
    public const string Subject = "Mã khôi phục tài khoản DCMS";

    public RecoveryChannel DetectChannel(string contact) => RecoveryContactFormat.DetectChannel(contact);
    public bool IsValid(string contact) => DetectChannel(contact) != RecoveryChannel.Unknown;
    public string Normalize(string contact) => RecoveryContactFormat.Normalize(contact);

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
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
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
        var body = new StringBuilder()
            .AppendLine("Xin chào,")
            .AppendLine()
            .AppendLine("Bạn (hoặc ai đó) vừa yêu cầu khôi phục tài khoản DCMS gắn với địa chỉ email này.")
            .AppendLine()
            .AppendLine($"Mã xác minh của bạn: {proof}")
            .AppendLine()
            .AppendLine($"Mã có hiệu lực trong {minutes} phút và chỉ dùng được một lần.");
        if (!string.IsNullOrWhiteSpace(resetLink)) body.AppendLine().AppendLine($"Hoặc mở liên kết: {resetLink}");
        body.AppendLine()
            .AppendLine("Nếu bạn không yêu cầu khôi phục, hãy bỏ qua email này; mật khẩu hiện tại của bạn không thay đổi.")
            .AppendLine("Không chia sẻ mã này với bất kỳ ai, kể cả nhân viên phòng khám.")
            .AppendLine()
            .AppendLine("DCMS - Phòng khám nha khoa");
        var settings = smtp.Value;
        return new MailMessage(new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8), new MailAddress(address))
        {
            Subject = Subject, SubjectEncoding = Encoding.UTF8, Body = body.ToString(), BodyEncoding = Encoding.UTF8, IsBodyHtml = false
        };
    }
}
