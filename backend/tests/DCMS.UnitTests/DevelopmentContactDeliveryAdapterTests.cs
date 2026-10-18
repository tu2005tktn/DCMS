using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DCMS.UnitTests;

/// <summary>
/// Guards the boundary of the development contact adapter: it must never pretend a real message was
/// sent, and it must refuse to run outside Development.
/// </summary>
public sealed class DevelopmentContactDeliveryAdapterTests
{
    private sealed class StubOutbox : IRecoveryOutbox
    {
        public List<(int AccountId, RecoveryChannel Channel, string Destination, string Provider, string Status)> Entries { get; } = [];
        public Task RecordAsync(int accountId, RecoveryChannel channel, string destination, string provider,
            string status, string? providerMessageId, CancellationToken ct = default)
        {
            Entries.Add((accountId, channel, destination, provider, status));
            return Task.CompletedTask;
        }
    }

    private sealed class StubEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? "Development" : "Production";
        public string ApplicationName { get; set; } = "DCMS.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public bool IsDevelopment() => isDevelopment;
        public bool IsProduction() => !isDevelopment;
    }

    private static DevelopmentContactDeliveryAdapter Create(StubOutbox outbox, StubEnvironment environment) =>
        new(outbox, environment, NullLogger<DevelopmentContactDeliveryAdapter>.Instance);

    [Fact]
    public void DetectsEmailAndPhoneAndRejectsAnythingElse()
    {
        var adapter = Create(new StubOutbox(), new StubEnvironment(true));
        Assert.Equal(RecoveryChannel.Email, adapter.DetectChannel("user@example.com"));
        Assert.Equal(RecoveryChannel.Phone, adapter.DetectChannel("+84 901 234 567"));
        Assert.Equal(RecoveryChannel.Unknown, adapter.DetectChannel("not-a-contact"));
        Assert.Equal(RecoveryChannel.Unknown, adapter.DetectChannel(""));
        Assert.False(adapter.IsValid("nope"));
        Assert.True(adapter.IsValid("user@example.com"));
    }

    [Fact]
    public void NormalizesEmailToLowercaseAndLeavesPhoneIntact()
    {
        var adapter = Create(new StubOutbox(), new StubEnvironment(true));
        Assert.Equal("user@example.com", adapter.Normalize("  User@Example.COM "));
        Assert.Equal("+84 901 234 567", adapter.Normalize(" +84 901 234 567 "));
        Assert.Equal("", adapter.Normalize(""));
    }

    [Fact]
    public async Task DeliveryIsRecordedAsLoggedOnlyAndNeverClaimsASentMessage()
    {
        var outbox = new StubOutbox();
        var adapter = Create(outbox, new StubEnvironment(true));
        const string proof = "123456";
        const string link = "https://clinic.example/recover/7";

        var sent = await adapter.SendRecoveryAsync(42, RecoveryChannel.Email, "user@example.com", proof, link, CancellationToken.None);

        Assert.True(sent, "The adapter reports success, but only as 'handed to the outbox'.");
        var entry = Assert.Single(outbox.Entries);
        Assert.Equal(42, entry.AccountId);
        Assert.Equal(RecoveryChannel.Email, entry.Channel);
        // A development outbox row is explicitly not a delivery confirmation.
        Assert.Equal("development-adapter", entry.Provider);
        Assert.Equal("logged-only", entry.Status);
        // The proof and the link must never enter the recorded entry.
        Assert.DoesNotContain(proof, entry.Destination, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdapterRefusesToRunOutsideDevelopment()
    {
        var adapter = Create(new StubOutbox(), new StubEnvironment(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SendRecoveryAsync(1, RecoveryChannel.Email, "user@example.com", "123456", "", CancellationToken.None));
    }
}
