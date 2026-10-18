using DCMS.Application.Common;

namespace DCMS.Application.Features.Appointments;

// UC-06 support requests (S16 #8/#9). Contract: docs/implementation/UC05-06-S14-S16.md §4.4.
public sealed record SupportRequestDto(int Id, string AppointmentCode, string Kind, string Reason, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, string? PublicNote, int Version);
public sealed record CreateSupportRequest(Guid RequestKey, string? Kind, string? Reason);
public sealed record WithdrawSupportRequest(int ExpectedVersion);
public sealed record SupportRequestResult(SupportRequestDto Request, SystemMessage? Notification);
public sealed record SupportRequestOutcome(SupportRequestResult Result, bool Replayed);

public interface IAppointmentSupportService
{
    Task<SupportRequestOutcome> CreateAsync(int accountId, string code, CreateSupportRequest request, CancellationToken ct);
    Task<SupportRequestResult> WithdrawAsync(int accountId, int id, WithdrawSupportRequest request, CancellationToken ct);
    Task<IReadOnlyList<SupportRequestDto>> MineAsync(int accountId, string? appointmentCode, CancellationToken ct);
}
