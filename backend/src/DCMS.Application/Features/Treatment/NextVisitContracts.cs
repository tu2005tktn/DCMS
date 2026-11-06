namespace DCMS.Application.Features.Treatment;

public sealed record NextVisitAppointment(Guid Id, string Code, DateTimeOffset StartAt);
public sealed record NextVisitCandidate(Guid DossierId, string DossierCode, Guid ItemId, int ServiceId, string ServiceName,
    string? Region, DateOnly? EstimatedStart, int DurationMinutes, int DentistId, string DentistName, int BranchId,
    string Status, Guid? FollowUpId, NextVisitAppointment? Appointment, DateOnly? EarliestOn = null, DateOnly? LatestOn = null,
    DateOnly? TargetOn = null, DateOnly? WindowFrom = null, DateOnly? WindowTo = null);
public sealed record NextVisitCandidates(IReadOnlyList<NextVisitCandidate> Items);

public sealed record PlannedDateChange(Guid ItemId, string ServiceName, string? Region, DateOnly? CurrentPlannedOn,
    DateOnly ProposedPlannedOn, bool IsBookedItem);
public sealed record PlannedDatePreview(int DossierVersion, IReadOnlyList<PlannedDateChange> Items);
public sealed record PlannedDateInput(Guid ItemId, DateOnly PlannedOn);
public sealed record ApplyPlannedDatesRequest(int ExpectedVersion, IReadOnlyList<PlannedDateInput> Changes, Guid RequestKey);
