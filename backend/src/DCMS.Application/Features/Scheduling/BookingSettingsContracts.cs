namespace DCMS.Application.Features.Scheduling;

public sealed record BookingServiceSetting(int Id, string Name, int DurationMinutes, bool Active, bool PublicBookable = true, bool IsInitialExamination = false);
public sealed record BookingSettings(int BranchId, int SlotIntervalMinutes, int Version, BookingServiceSetting[] Services);
public sealed record SaveBookingSettings(int ExpectedVersion, Guid RequestKey, int SlotIntervalMinutes, BookingServiceSetting[] Services);
public interface IBookingSettingsService
{
    Task<BranchOption[]> OptionsAsync(int actorId, CancellationToken ct);
    Task<BookingSettings> GetAsync(int actorId, int branchId, CancellationToken ct);
    Task<BookingSettings> SaveAsync(int actorId, int branchId, SaveBookingSettings command, CancellationToken ct);
}
