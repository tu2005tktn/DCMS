using System.Text.RegularExpressions;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Authentication;

/// <summary>
/// Shared S02/S03 contact adapter (Vinh owns the S02 contract; this class only consumes it).
/// No production provider is configured yet, so this implementation is a DEVELOPMENT adapter:
/// it records an outbox entry for follow-up and clearly never claims a real message was sent.
/// It is only registered in Development (see AddInfrastructure) and never in Production.
/// </summary>
public sealed partial class DevelopmentContactDeliveryAdapter(
    IRecoveryOutbox outbox,
    IHostEnvironment environment,
    ILogger<DevelopmentContactDeliveryAdapter> logger) : IContactDeliveryAdapter
{
    // The adapter is registered per request scope, so an instance-scoped dictionary would lose the
    // delivered proof before the Development-only aid endpoint can hand it back in a later request.
    // Process memory is shared between scopes for this Development-only aid; nothing is persisted.
    private static readonly Lock Gate = new();
    private static readonly Dictionary<int, string> Delivered = [];

    public RecoveryChannel DetectChannel(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return RecoveryChannel.Unknown;
        var value = contact.Trim();
        if (EmailPattern().IsMatch(value)) return RecoveryChannel.Email;
        if (PhonePattern().IsMatch(value)) return RecoveryChannel.Phone;
        return RecoveryChannel.Unknown;
    }

    public bool IsValid(string? contact) => DetectChannel(contact) != RecoveryChannel.Unknown;

    public string Normalize(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact)) return "";
        var value = contact.Trim();
        return DetectChannel(value) == RecoveryChannel.Email ? value.ToLowerInvariant() : value;
    }

    /// <summary>
    /// Last proof handed to this account, kept in process memory for Development only.
    ///
    /// This is a local developer aid so the recovery flow can actually be completed without a real
    /// mail provider: the proof is never written to the database, the outbox, the logs or any audit
    /// entry. It is reachable only through the Development-only endpoint that is registered next to
    /// this adapter, and the adapter itself is not registered outside Development.
    /// </summary>
    public string? TryGetProof(int accountId)
    {
        lock (Gate) { return Delivered.TryGetValue(accountId, out var proof) ? proof : null; }
    }

    public void Forget(int accountId)
    {
        lock (Gate) { Delivered.Remove(accountId); }
    }

    public async Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination,
        string proof, string resetLink, CancellationToken ct)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                "The development contact adapter must not run outside Development; configure a real recovery channel.");

        lock (Gate) { Delivered[accountId] = proof; }

        // Proof and link stay out of the log: only their length is recorded as a sanity check.
        logger.LogInformation(
            "[DEVELOPMENT ADAPTER - NO REAL MESSAGE SENT] account={AccountId} channel={Channel} " +
            "destination={Destination} proofLength={ProofLength} linkLength={LinkLength}",
            accountId, channel, destination, proof.Length, resetLink.Length);

        await outbox.RecordAsync(accountId, channel, destination, "development-adapter",
            "logged-only", providerMessageId: null, ct);
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"^\+?[0-9 ()-]{7,24}$")]
    private static partial Regex PhonePattern();
}
