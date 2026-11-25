namespace DCMS.Application.Features.Authentication;

public sealed record StaffIdleStatusDto(int StaffIdleTimeoutMinutes, DateTimeOffset LastInteractiveActivityAt,
    DateTimeOffset DeadlineAt, DateTimeOffset ServerNow, int ActivityVersion);
public sealed record StaffIdleActivityRequest(int ExpectedVersion, Guid RequestKey);

public interface IStaffIdleService
{
    Task<StaffIdleStatusDto> StatusAsync(int actorId, Guid sessionId, CancellationToken ct);
    Task<StaffIdleStatusDto> ActivityAsync(int actorId, Guid sessionId, StaffIdleActivityRequest request, CancellationToken ct);
}
