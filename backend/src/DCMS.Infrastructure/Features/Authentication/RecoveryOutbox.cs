using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Authentication;

public interface IRecoveryOutbox
{
    Task RecordAsync(int accountId, RecoveryChannel channel, string destination, string provider,
        string status, string? providerMessageId, CancellationToken ct = default);
}

// Delivery attempts are recorded so a failed send can be followed up without ever storing a secret.
public sealed class RecoveryOutboxWriter(DcmsDbContext db) : IRecoveryOutbox
{
    public async Task RecordAsync(int accountId, RecoveryChannel channel, string destination, string provider,
        string status, string? providerMessageId, CancellationToken ct = default)
    {
        db.RecoveryOutbox.Add(new RecoveryOutbox
        {
            AccountId = accountId,
            ChannelType = channel.ToString(),
            Destination = destination,
            Provider = provider,
            Status = status,
            ProviderMessageId = providerMessageId,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}