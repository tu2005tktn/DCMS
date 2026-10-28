namespace DCMS.Application.Features.Authentication;

public sealed record StaffIdlePreviewRequest(int StaffIdleTimeoutMinutes);
public sealed record StaffIdlePreviewDto(int StaffIdleTimeoutMinutes, int AffectedActiveSessions,
    DateTimeOffset EvaluatedAt);
public sealed record StaffIdleStatusDto(int StaffIdleTimeoutMinutes, DateTimeOffset LastInteractiveActivityAt,
    DateTimeOffset DeadlineAt, DateTimeOffset ServerNow);

public interface IStaffIdleService
{
    Task<StaffIdlePreviewDto> PreviewAsync(StaffIdlePreviewRequest request, CancellationToken ct);
    Task<StaffIdleStatusDto> StatusAsync(int actorId, Guid sessionId, CancellationToken ct);
    Task<StaffIdleStatusDto> ActivityAsync(int actorId, Guid sessionId, CancellationToken ct);
}
