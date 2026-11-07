namespace DCMS.Application.Features.Prescriptions;

public sealed record CustomerPrescriptionSummary(Guid Id, DateOnly PrescribedOn, string BranchName,
    string Diagnosis, string PrescriberName, bool RequiresExternalPurchase);
public sealed record CustomerPrescriptionPage(CustomerPrescriptionSummary[] Items, int Total, int Page, int PageSize);
public sealed record CustomerPrescriptionItem(string MedicineName, string? Strength, string? DosageForm,
    string Unit, int Quantity, string Instructions, bool IsExternalPurchase);
public sealed record CustomerPrescriptionDetail(Guid Id, DateOnly PrescribedOn, string BranchName,
    string Diagnosis, string? OtherDiagnosis, string? Advice, string PrescriberName,
    bool RequiresExternalPurchase, CustomerPrescriptionItem[] Items);

public interface ICustomerPrescriptionService
{
    Task<CustomerPrescriptionPage> ListAsync(int accountId, int page, CancellationToken ct);
    Task<CustomerPrescriptionDetail> DetailAsync(int accountId, Guid id, CancellationToken ct);
}
