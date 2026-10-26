using DCMS.Application.Features.Treatment;

namespace DCMS.Infrastructure.Features.Treatment;

// DCMS-101 replaces this adapter when a read-only ledger summary is available.
public sealed class UnavailableTreatmentFinancialSummaryPort : ITreatmentFinancialSummaryPort
{
    public Task<TreatmentFinancialSummary?> ReadAsync(int patientId, int branchId, Guid dossierId,
        CancellationToken ct) => Task.FromResult<TreatmentFinancialSummary?>(null);
}
