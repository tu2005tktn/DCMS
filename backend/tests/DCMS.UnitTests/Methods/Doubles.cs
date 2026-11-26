using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Settings;

namespace DCMS.UnitTests.Methods;

/// <summary>Shared test doubles of the method-level tests (fixed clock, branch permission, availability, appointment queries).</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class TestBranchAccess(params int[] branchIds) : ISettingsAccess
{
    public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], branchIds));
    public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
    public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class TestAvailability : IAvailabilityService
{
    public Func<ResourceCheck, AvailabilitySlot>? OnCheck;
    public ResourceCheck? LastCheck;
    public Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct) => throw new NotSupportedException();
    public Task<AvailabilityResult> QueryForAppointmentAsync(AvailabilityQuery query, Guid appointmentId, int? patientId, CancellationToken ct, bool requireLeadTime = true) => throw new NotSupportedException();
    public Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct)
    {
        LastCheck = request;
        return Task.FromResult(OnCheck != null ? OnCheck(request)
            : new AvailabilitySlot(request.Start, request.Start.AddMinutes(request.DurationMinutes), request.ChairId ?? 1, request.DentistId, null, request.ExpectedScheduleVersion, null));
    }
    public Func<Task>? OnValidate;
    public Task ValidateReceptionAsync(Guid appointmentId, int patientId, CancellationToken ct) => OnValidate?.Invoke() ?? Task.CompletedTask;
}

internal sealed class TestQueries : IAppointmentQueryService
{
    public StaffAppointmentDetail? Detail;
    public Task<StaffAppointmentDetail> DetailAsync(int actorId, Guid id, CancellationToken ct) => Task.FromResult(Detail!);
    public AppointmentOptions? Options;
    public Task<AppointmentOptions> OptionsAsync(int actorId, CancellationToken ct) => Options != null ? Task.FromResult(Options) : throw new NotSupportedException();
    public Task<CalendarView> CalendarAsync(int actorId, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status, CancellationToken ct) => throw new NotSupportedException();
    public Task<AppointmentListPage> ListAsync(int actorId, StaffAppointmentQuery query, CancellationToken ct) => throw new NotSupportedException();
    public Task<AppointmentListPage> ForPatientAsync(int actorId, int patientId, string scope, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<MyAppointmentPage> MineAsync(int accountId, MyAppointmentQuery query, CancellationToken ct) => throw new NotSupportedException();
    public Task<MyAppointmentDetail> MineDetailAsync(int accountId, string code, CancellationToken ct) => throw new NotSupportedException();
    public Task<MyAppointmentDetail> ChangeDetailAsync(int accountId, string code, CancellationToken ct) => throw new NotSupportedException();
}
