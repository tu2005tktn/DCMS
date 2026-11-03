namespace DCMS.Application.Features.Treatment;

public sealed record NextVisitAppointment(Guid Id, string Code, DateTimeOffset StartAt);
public sealed record NextVisitCandidate(Guid DossierId, string DossierCode, Guid ItemId, int ServiceId, string ServiceName,
    string? Region, DateOnly? EstimatedStart, int DurationMinutes, int DentistId, string DentistName, int BranchId,
    string Status, Guid? FollowUpId, NextVisitAppointment? Appointment, DateOnly? EarliestOn = null, DateOnly? LatestOn = null);
public sealed record NextVisitCandidates(IReadOnlyList<NextVisitCandidate> Items);
