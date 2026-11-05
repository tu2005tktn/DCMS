namespace DCMS.Application.Features.Treatment;

// Read-only report over treatment-sheet activities grouped by dossier. Rules and API: docs/specs/WORK-SERVICE-STATISTICS.md.
public sealed record WorkStatisticsLine(Guid ActivityId, string ServiceName, long CostVnd, long PaidVnd, long DueVnd);
public sealed record WorkStatisticsDossier(Guid DossierId, string DossierCode, DateOnly StartedOn, int PatientId, string PatientCode, string PatientName,
    DateTimeOffset? NextAppointmentAt, string DentistName, IReadOnlyList<string> AssistantNames, long CostVnd, long PaidVnd, long DueVnd,
    IReadOnlyList<WorkStatisticsLine> Lines);
public sealed record WorkStatisticsTotals(long CostVnd, long PaidVnd, long DueVnd);
public sealed record WorkStatisticsPage(IReadOnlyList<WorkStatisticsDossier> Items, int Total, int Page, int PageSize, WorkStatisticsTotals Totals, int LineCount = 0);
public interface IWorkStatisticsService
{
    Task<WorkStatisticsPage> ListAsync(int actorId, DateOnly? from, DateOnly? to, int? branchId, string? q, int page, int pageSize, CancellationToken ct);
}
