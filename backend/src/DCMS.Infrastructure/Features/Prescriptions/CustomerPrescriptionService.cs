using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Prescriptions;

public sealed class CustomerPrescriptionService(DcmsDbContext db) : ICustomerPrescriptionService
{
    private const int PageSize = 20;

    private async Task<int> OwnPatientIdAsync(int accountId, CancellationToken ct)
    {
        var patientId = await db.PatientAccountLinks.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Status == PatientLinkStatus.Verified && x.PatientId != null)
            .Select(x => x.PatientId).SingleOrDefaultAsync(ct);
        return patientId ?? throw new BusinessException(403, SystemMessages.Get("MSG06"));
    }

    public async Task<CustomerPrescriptionPage> ListAsync(int accountId, int page, CancellationToken ct)
    {
        if (page < 1) throw new BusinessException(400, SystemMessages.Get("MSG04"));
        var patientId = await OwnPatientIdAsync(accountId, ct);
        var query = db.Set<PatientPrescription>().AsNoTracking().Where(x => x.PatientId == patientId);
        var total = await query.CountAsync(ct);
        var rows = await (from prescription in query
                          join branch in db.Set<SchedulingBranch>() on prescription.BranchId equals branch.Id
                          join dentist in db.Employees on prescription.PrescriberEmployeeId equals dentist.Id
                          orderby prescription.PrescribedOn descending, prescription.CreatedAt descending, prescription.Id descending
                          select new CustomerPrescriptionSummary(prescription.Id, prescription.PrescribedOn,
                              branch.Name, prescription.Diagnosis, dentist.FullName, prescription.RequiresExternalPurchase))
            .Skip((page - 1) * PageSize).Take(PageSize).ToArrayAsync(ct);
        return new(rows, total, page, PageSize);
    }

    public async Task<CustomerPrescriptionDetail> DetailAsync(int accountId, Guid id, CancellationToken ct)
    {
        var patientId = await OwnPatientIdAsync(accountId, ct);
        var doc = await db.Set<PatientPrescription>().AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId, ct)
            ?? throw new BusinessException(404, SystemMessages.Get("MSG113"));
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Id == doc.BranchId).Select(x => x.Name).SingleAsync(ct);
        var dentist = await db.Employees.AsNoTracking().Where(x => x.Id == doc.PrescriberEmployeeId).Select(x => x.FullName).SingleAsync(ct);
        return new(doc.Id, doc.PrescribedOn, branch, doc.Diagnosis, doc.OtherDiagnosis, doc.Advice,
            dentist, doc.RequiresExternalPurchase, doc.Items.OrderBy(x => x.Position)
                .Select(x => new CustomerPrescriptionItem(x.MedicineName, x.Strength, x.DosageForm,
                    x.Unit, x.Quantity, x.Instructions, x.IsExternalPurchase)).ToArray());
    }
}
