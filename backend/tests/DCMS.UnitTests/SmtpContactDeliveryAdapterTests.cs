using System.Net.Mail;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DCMS.UnitTests;

/// <summary>
/// S03 recovery email through SMTP: the code reaches only the verified address, the outbox records the
/// real outcome, and the code never appears in a log line.
/// </summary>
public sealed class SmtpContactDeliveryAdapterTests
{
    private sealed class StubOutbox : IRecoveryOutbox
    {
        public List<(string Destination, string Provider, string Status)> Entries { get; } = [];
        public Task RecordAsync(int accountId, RecoveryChannel channel, string destination, string provider,
            string status, string? providerMessageId, CancellationToken ct = default)
        {
            Entries.Add((destination, provider, status));
            return Task.CompletedTask;
        }
    }

    private sealed class StubSender(Exception? failure = null) : IMailSender
    {
        public List<(string To, string From, string Subject, string Body)> Sent { get; } = [];
        public Task SendAsync(MailMessage message, CancellationToken ct)
        {
            if (failure != null) throw failure;
            Sent.Add((message.To.Single().Address, message.From!.Address, message.Subject, message.Body));
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger : ILogger<SmtpContactDeliveryAdapter>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private static SmtpContactDeliveryAdapter Create(StubSender sender, StubOutbox outbox, CapturingLogger logger) => new(sender, outbox,
        Options.Create(new SmtpSettings { Host = "smtp.test", FromAddress = "no-reply@dcms.local" }),
        Options.Create(new RecoverySettings { TokenLifetimeMinutes = 15 }), logger);

    [Fact]
    public async Task SendsTheCodeToTheNormalizedVerifiedAddressAndRecordsSent()
    {
        var (sender, outbox, logger) = (new StubSender(), new StubOutbox(), new CapturingLogger());
        var sent = await Create(sender, outbox, logger).SendRecoveryAsync(7, RecoveryChannel.Email, " Patient@Example.COM ", "482913", "", default);

        Assert.True(sent);
        var mail = Assert.Single(sender.Sent);
        Assert.Equal("patient@example.com", mail.To);
        Assert.Equal("no-reply@dcms.local", mail.From);
        Assert.Equal(SmtpContactDeliveryAdapter.Subject, mail.Subject);
        Assert.Contains("482913", mail.Body);
        Assert.Contains("15 phút", mail.Body);
        Assert.DoesNotContain("liên kết", mail.Body);
        Assert.Equal(("patient@example.com", "smtp", "sent"), Assert.Single(outbox.Entries));
        Assert.DoesNotContain(logger.Lines, line => line.Contains("482913"));
    }

    [Fact]
    public async Task IncludesTheLinkOnlyWhenOneIsConfigured()
    {
        var sender = new StubSender();
        await Create(sender, new StubOutbox(), new CapturingLogger())
            .SendRecoveryAsync(7, RecoveryChannel.Email, "a@example.com", "ABCDEF", "https://dcms.local/recover?t=x", default);
        Assert.Contains("https://dcms.local/recover?t=x", Assert.Single(sender.Sent).Body);
    }

    [Fact]
    public async Task SmtpFailureIsReportedAsNotSentWithoutLeakingTheCode()
    {
        var (sender, outbox, logger) = (new StubSender(new SmtpException("mailbox unavailable")), new StubOutbox(), new CapturingLogger());
        var sent = await Create(sender, outbox, logger).SendRecoveryAsync(7, RecoveryChannel.Email, "a@example.com", "482913", "", default);

        Assert.False(sent);
        Assert.Equal(("a@example.com", "smtp", "failed"), Assert.Single(outbox.Entries));
        Assert.DoesNotContain(logger.Lines, line => line.Contains("482913"));
    }

    [Fact]
    public async Task PhoneDestinationIsNotDeliveredBecauseNoSmsProviderExists()
    {
        var (sender, outbox) = (new StubSender(), new StubOutbox());
        var sent = await Create(sender, outbox, new CapturingLogger()).SendRecoveryAsync(7, RecoveryChannel.Phone, "0912345678", "482913", "", default);

        Assert.False(sent);
        Assert.Empty(sender.Sent);
        Assert.Equal("unsupported-channel", Assert.Single(outbox.Entries).Status);
    }

    [Fact]
    public void SettingsNeedHostAndSender()
    {
        Assert.False(new SmtpSettings().IsConfigured);
        Assert.False(new SmtpSettings { Host = "smtp.test" }.IsConfigured);
        Assert.True(new SmtpSettings { Host = "smtp.test", FromAddress = "no-reply@dcms.local" }.IsConfigured);
    }
}
