namespace DCMS.Application.Features.Finance;

public sealed record RevenueTotals(long ReceivedVnd, long CashVnd, long TransferVnd, long PendingVnd);
public sealed record RevenueDay(DateOnly Date, long CashVnd, long TransferVnd);
public sealed record RevenueItem(Guid Id, string Source, Guid? SourceId, int PatientId, string PatientName,
    int BranchId, string BranchName, DateTimeOffset ReceivedAt, string Method, long AmountVnd);
public sealed record RevenueReport(DateOnly From, DateOnly To, RevenueTotals Totals,
    RevenueDay[] Daily, int Total, RevenueItem[] Items);

public interface IRevenueService
{
    Task<RevenueReport> GetAsync(int actor, DateOnly? from, DateOnly? to, int? branchId,
        int page, int pageSize, CancellationToken ct);
}
