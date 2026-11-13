using System.Security.Cryptography;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.FamilyMembers;

public sealed partial class FamilyRecordService
{
    private async Task<FamilyRecordAccess> GrantedAsync(int actor, Guid memberId, string? scope, CancellationToken ct)
    {
        await EnsureDefaultAccessAsync(actor, memberId, ct);
        var row = await db.Set<FamilyRecordAccess>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerAccountId == actor
            && x.FamilyMemberId == memberId && x.Status == "Verified" && x.ExpiresAt > clock.GetUtcNow()
            && x.PatientId != null && x.BranchId != null, ct) ?? throw Denied();
        if (scope != null && !row.Scopes.Contains(scope)) throw Denied();
        if (!await db.Patients.AnyAsync(x => x.Id == row.PatientId && !x.IsArchived, ct)) throw Denied();
        return row;
    }
    private async Task RecordReadAsync(FamilyRecordAccess row, int actor, string action, CancellationToken ct)
    {
        Audit(row, actor, action); await db.SaveChangesAsync(ct);
    }
    public async Task<FamilyRecordHeader> HeaderAsync(int actor, Guid memberId, CancellationToken ct)
    {
        var row = await GrantedAsync(actor, memberId, null, ct);
        var patient = await db.Patients.AsNoTracking().SingleAsync(x => x.Id == row.PatientId, ct);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Id == row.BranchId).Select(x => x.Name).SingleAsync(ct);
        await RecordReadAsync(row, actor, "RecordRead", ct);
        return new(patient.PatientCode, patient.FullName, patient.BirthDate, branch, row.Scopes, row.ExpiresAt!.Value);
    }
    public async Task<FamilyPage<FamilyAppointmentDto>> AppointmentsAsync(int actor, Guid memberId, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize); await MemberAsync(actor, memberId, ct);
        var query = from a in db.Appointments.AsNoTracking() join b in db.Set<SchedulingBranch>() on a.BranchId equals b.Id
            join e in db.Employees on a.DentistId equals e.Id
            where a.BookerAccountId == actor && a.FamilyMemberId == memberId
            orderby a.StartAt descending, a.Id
            select new FamilyAppointmentDto(a.Code, a.StartAt, a.EndAt, a.Status, b.Name, e.FullName);
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct), await query.CountAsync(ct), page, pageSize);
    }

    public async Task<FamilyPage<object>> RecordsAsync(int actor, Guid memberId, string section, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize);
        var scope = section switch { "examinations" => "Examinations", "treatment" => "Treatment", "prescriptions" => "Prescriptions", "documents" => "Documents", _ => throw Missing() };
        var row = await GrantedAsync(actor, memberId, scope, ct);
        var skip = (page - 1) * pageSize;
        object[] items; int total;
        if (scope == "Examinations") {
            var query = from v in db.Visits.AsNoTracking() join e in db.Set<VisitExamination>().AsNoTracking() on v.Id equals e.VisitId
                join dentist in db.Employees on v.DentistId equals dentist.Id join b in db.Set<SchedulingBranch>() on v.BranchId equals b.Id
                where v.PatientId == row.PatientId && v.BranchId == row.BranchId && e.Confirmed && v.Status != "Cancelled"
                orderby v.ArrivalAt descending, v.Id select new { v.Id, v.ArrivalAt, DentistName = dentist.FullName, BranchName = b.Name, e.ContentJson };
            total = await query.CountAsync(ct);
            items = (await query.Skip(skip).Take(pageSize).ToArrayAsync(ct)).Select(x => {
                var content = JsonSerializer.Deserialize<ExaminationContent>(x.ContentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return (object)new FamilyExaminationDto(x.Id, x.ArrivalAt, x.DentistName, x.BranchName, content?.Symptoms,
                    content?.Diagnoses?.ToArray() ?? (string.IsNullOrWhiteSpace(content?.Diagnosis) ? [] : [content.Diagnosis]), content?.Conclusion, content?.Advice);
            }).ToArray();
        } else if (scope == "Treatment") {
            var query = db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).Where(x => x.PatientId == row.PatientId
                && x.BranchId == row.BranchId && x.PlanState == "Committed" && x.State != "Deleted");
            total = await query.CountAsync(ct);
            items = (await query.OrderByDescending(x => x.StartedOn).ThenBy(x => x.Id).Skip(skip).Take(pageSize).ToArrayAsync(ct))
                .Select(x => (object)new FamilyTreatmentDto(x.Id, x.Code, x.Diagnosis, x.State, x.StartedOn,
                    x.Items.Where(i => i.Decision == "Agreed" && !i.SheetDeleted && !i.HiddenFromSheet).OrderBy(i => i.PlannedOrder)
                        .Select(i => new FamilyTreatmentItem(i.ServiceName, i.Region, i.Progress, i.EstimatedStart, i.EstimatedEnd)).ToArray())).ToArray();
        } else if (scope == "Prescriptions") {
            var query = db.Set<ExternalPrescription>().AsNoTracking().Where(x => x.PatientId == row.PatientId
                && x.BranchId == row.BranchId && x.Status == PrescriptionStatuses.Finalized);
            total = await query.CountAsync(ct);
            var prescriptions = await query.OrderByDescending(x => x.FinalizedAt).ThenBy(x => x.Id).Skip(skip).Take(pageSize).ToArrayAsync(ct);
            var ids = prescriptions.Select(x => x.Id).ToArray();
            var lines = prescriptions.SelectMany(x => x.Lines).ToArray();
            items = prescriptions.Select(x => (object)new FamilyPrescriptionDto(x.Id, x.VisitId, x.FinalizedAt, x.Diagnosis, x.GeneralAdvice,
                lines.Where(l => l.PrescriptionId == x.Id).Select(l => new FamilyMedicineDto(l.MedicineName, l.Strength, l.Unit, l.Quantity,
                    l.Route, l.MorningDose, l.NoonDose, l.AfternoonDose, l.EveningDose, l.MealTiming, l.Days, l.Instructions)).ToArray())).ToArray();
        } else {
            var query = Documents(row);
            total = await query.CountAsync(ct);
            items = (await query.OrderByDescending(x => x.SourceDate).ThenBy(x => x.Id).Skip(skip).Take(pageSize)
                .Select(x => new FamilyDocumentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.DocumentType, x.SourceDate)).ToArrayAsync(ct)).Cast<object>().ToArray();
        }
        await RecordReadAsync(row, actor, "Read:" + scope, ct);
        return new(items, total, page, pageSize);
    }

    private IQueryable<ClinicalDocument> Documents(FamilyRecordAccess row) => db.Set<ClinicalDocument>().AsNoTracking().Where(d =>
        d.PatientId == row.PatientId && d.FileState == "Ready"
        && (d.DocumentType == "XRay" || d.DocumentType == "IntraoralPhoto" || d.DocumentType == "LabResult" || d.DocumentType == "Referral" || d.DocumentType == "ConsentForm")
        && (d.VisitId != null
            ? db.Visits.Any(v => v.Id == d.VisitId && v.PatientId == row.PatientId && v.BranchId == row.BranchId && v.Status != "Cancelled"
                && db.Set<VisitExamination>().Any(e => e.VisitId == v.Id && e.Confirmed))
            : db.TreatmentDossiers.Any(t => t.Id == d.DossierId && t.PatientId == row.PatientId && t.BranchId == row.BranchId && t.PlanState == "Committed" && t.State != "Deleted")));

    public async Task<DocumentContent> DocumentAsync(int actor, Guid memberId, Guid documentId, CancellationToken ct)
    {
        var row = await GrantedAsync(actor, memberId, "Documents", ct);
        var document = await Documents(row).SingleOrDefaultAsync(x => x.Id == documentId, ct) ?? throw Missing();
        var root = configuration["ClinicalDocuments:StoragePath"];
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new BusinessException(503, SystemMessages.Get("MSG144"));
        root = Path.GetFullPath(root);
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                && (root.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase) || root.StartsWith(directory.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new BusinessException(503, SystemMessages.Get("MSG144"));
        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(Path.Combine(root, document.Id.ToString("N")), ct); }
        catch (IOException) { throw new BusinessException(503, SystemMessages.Get("MSG144")); }
        if (Convert.ToHexString(SHA256.HashData(bytes)) != document.Sha256) throw new BusinessException(409, SystemMessages.Get("MSG42"));
        await GrantedAsync(actor, memberId, "Documents", ct);
        await RecordReadAsync(row, actor, "DocumentRead", ct);
        return new(bytes, document.ContentType, document.FileName);
    }
}
