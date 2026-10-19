using DCMS.Application.Features.Authentication;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed class UnavailableEmailVerificationDelivery : IEmailVerificationDelivery
{
    public Task<bool> SendAsync(string destination, string code, int lifetimeMinutes, CancellationToken ct) =>
        Task.FromResult(false);
}
