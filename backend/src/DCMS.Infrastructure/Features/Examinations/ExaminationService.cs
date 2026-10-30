using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Reception;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ExaminationService(DcmsDbContext db, ClinicalAccess access, IReceptionService reception,
    IClinicalDocumentService documents, TimeProvider clock, IInvestigationService investigations, DCMS.Application.Features.Finance.IExaminationFeeService fees) : IExaminationService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static readonly string[] SaveActions = ["SaveExamination", "ConfirmExamination", "AmendExamination"];
    private static ExaminationDto Dto(VisitExamination e, int version = 0) => new(JsonSerializer.Deserialize<ExaminationContent>(e.ContentJson, Json)!, e.Confirmed, e.UpdatedBy, e.UpdatedAt, e.VisitId, version);
    public async Task<ExaminationWorkspace> GetAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        var v = await access.VisitAsync(actorId, visitId, false, ct);
        var row = await db.Set<VisitExamination>().AsNoTracking().SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        var previous = await (from e in db.Set<VisitExamination>().AsNoTracking() join visit in db.Visits on e.VisitId equals visit.Id
            where visit.PatientId == v.PatientId && visit.BranchId == v.BranchId && visit.Id != v.Id && e.Confirmed
            orderby e.UpdatedAt descending select e).FirstOrDefaultAsync(ct);
        var prior = previous == null ? null : Dto(previous).Content;
        var initial = new ExaminationContent(v.ArrivalReason ?? v.Note, prior == null ? new() : prior.MedicalHistory with { Verified = false },
            prior == null ? new() : prior.Allergies with { Verified = false }, prior == null ? new() : prior.Medications with { Verified = false }, [], null, null);
        var own = await db.Employees.AnyAsync(x => x.Id == v.DentistId && x.AccountId == actorId, ct);
        var canWrite = own && await access.BranchAsync(actorId, v.BranchId, false, ct);
        string[] actions = canWrite ? v.Status == VisitStatuses.Queued ? ["start"]
            : v.Status == VisitStatuses.InProgress && row?.Confirmed != true ? ["save", "confirm"]
            : row?.Confirmed == true && v.Status == VisitStatuses.InProgress ? ["amend", "complete"]
            : row?.Confirmed == true && v.Status == VisitStatuses.Completed ? (Dto(row).Content.Disposition == "ProceedToTreatment" ? ["handoff", "amend"] : ["amend"]) : [] : [];
        var visitResult = await reception.VisitAsync(actorId, visitId, ct);
        var patient = await db.Patients.AsNoTracking().Where(p => p.Id == v.PatientId)
            .Select(p => new { p.Id, p.PatientCode, p.FullName, p.BirthDate, p.Gender }).SingleAsync(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        int? age = null;
        if (patient.BirthDate is { } birth && birth <= today) {
            var years = today.Year - birth.Year;
            age = birth.AddYears(years) > today ? years - 1 : years;
        }
        var orders = (await investigations.ListAsync(actorId,visitId,ct)).Items;
        if(orders.Any(x=>x.RequiredForDiagnosis && x.State is not ("Reviewed" or "Cancelled"))) actions = actions.Where(x=>x!="handoff").ToArray();
        var examinationVersion = await db.Set<VisitAudit>().CountAsync(x => x.VisitId == visitId && SaveActions.Contains(x.Action), ct);
        return new(visitResult, row == null ? null : Dto(row, examinationVersion), initial, actions,
            new(patient.Id, patient.PatientCode, patient.FullName, patient.BirthDate, patient.Gender, age), Investigations: orders);
    }
    public Task<ExaminationWorkspace> StartAsync(int actorId, Guid visitId, StartExamination request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Start", request, null, ct);
    public Task<ExaminationWorkspace> SaveAsync(int actorId, Guid visitId, SaveExamination request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Save", request, request, ct);
    public Task<ExaminationWorkspace> AmendAsync(int actorId, Guid visitId, AmendExamination request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Amend", request,
            new(request.PatientId, request.ExpectedVersion, request.RequestKey, request.Content, true), ct);
    public Task<ExaminationWorkspace> CompleteAsync(int actorId, Guid visitId, CompleteExaminationVisit request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Complete", request, null, ct);

    private async Task<ExaminationWorkspace> CommandAsync(int actorId, Guid visitId, int patientId, int version, Guid key,
        string action, object payload, SaveExamination? save, CancellationToken ct)
    {
        if (key == Guid.Empty || patientId <= 0 || version <= 0) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var v = await access.VisitAsync(actorId, visitId, true, ct);
        if (v.PatientId != patientId) throw Error("MSG113", 404);
        if(v.Purpose==AttendancePurposes.TreatmentContinuation && action!="Amend")
            throw new BusinessException(409,new SystemMessage("TREATMENT_VISIT_REQUIRED","Lượt này dành cho điều trị. Dùng các buổi trong hồ sơ điều trị để bắt đầu hoặc kết thúc, không tạo phiếu khám mới."));
        var hash = Hash(new { visitId, action, payload });
        var receipt = await db.Set<ExaminationReceipt>().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == key, ct);
        if (receipt != null) {
            if (receipt.VisitId != visitId || receipt.Fingerprint != hash) throw Error("MSG111", 409);
            await tx.CommitAsync(ct); return (await GetAsync(actorId, visitId, ct)) with { Replayed = true };
        }
        if (v.Version != version) throw Error("MSG111", 409);
        var a = await db.Appointments.SingleAsync(x => x.Id == v.AppointmentId, ct);
        var old = JsonSerializer.Serialize(new { v.Status, v.Version }, Json);
        var e = await db.Set<VisitExamination>().SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        if (action == "Complete") {
            var request = (CompleteExaminationVisit)payload;
            if (!request.WorkReviewed || e?.Confirmed != true || v.Status != VisitStatuses.InProgress || a.Status != "InProgress") throw new BusinessException(409, SystemMessages.Get("MSG239", field: "hoàn tất phiếu khám và kiểm tra công việc trước khi kết thúc lượt"));
            if (await db.ClinicalActivities.AnyAsync(x => x.VisitId == visitId && x.State == "Draft", ct)) throw new BusinessException(409, SystemMessages.Get("MSG239", field: "lượt còn hoạt động điều trị nháp"));
            if (await db.Set<InvestigationOrder>().AnyAsync(x => x.VisitId == visitId && x.State != "Reviewed" && x.State != "Cancelled", ct))
                throw new BusinessException(409, SystemMessages.Get("MSG239",field:"còn chỉ định cận lâm sàng chưa được bác sĩ đọc hoặc hủy") with { Field = "investigations" });
            var content = Dto(e).Content;
            if (!HasDiagnosis(content) || string.IsNullOrWhiteSpace(content.Disposition)) throw new BusinessException(409, SystemMessages.Get("MSG239", field: "phiếu khám còn thiếu chẩn đoán hoặc hướng xử trí"));
            if (await db.Set<InvestigationOrder>().AnyAsync(x => x.VisitId == visitId && x.State != "Cancelled" && (x.BillingMode == "Clinic" || x.BillingMode == "OutsourcedClinic") && x.PerformedAt == null,ct))
                throw new BusinessException(409, SystemMessages.Get("MSG42") with { Message = "Còn dịch vụ cận lâm sàng đã lập phí chưa xác nhận thực hiện. Hãy xác nhận thực hiện hoặc hủy chỉ định." });
            await fees.OnCompletionAsync(actorId,v,request.ExaminationFee,ct);
            v.Status = VisitStatuses.Completed; a.Status = "Completed"; a.Version++;
        } else if (save == null) {
            if (v.Status != VisitStatuses.Queued || a.Status != "CheckedIn") throw Error("MSG42", 409);
            v.Status = VisitStatuses.InProgress; a.Status = "InProgress"; a.Version++;
        } else {
            if (action == "Amend") {
                var amendment = (AmendExamination)payload;
                if (e?.Confirmed != true || (v.Status != VisitStatuses.InProgress && v.Status != VisitStatuses.Completed)) throw Error("MSG42", 409);
                if (string.IsNullOrWhiteSpace(amendment.Reason) || amendment.Reason.Length > 1000) throw new BusinessException(400, SystemMessages.Get("MSG04", field: "Lý do đính chính (tối đa 1000 ký tự)"));
            } else if (v.Status != VisitStatuses.InProgress || a.Status != "InProgress" || e?.Confirmed == true) throw Error("MSG42", 409);
            if (save.Confirm && await db.Set<InvestigationOrder>().AnyAsync(x => x.VisitId == visitId && x.RequiredForDiagnosis && x.State != "Reviewed" && x.State != "Cancelled", ct))
                throw new BusinessException(409, SystemMessages.Get("MSG239",field:"cần nhận và đọc kết quả cận lâm sàng trước khi hoàn tất khám") with { Field = "investigations" });
            Validate(save.Content, save.Confirm);
            await ValidateDiagnosisReferences(save.Content,e == null ? null : JsonSerializer.Deserialize<ExaminationContent>(e.ContentJson,Json),ct);
            save = save with { Content = NormalizeDiagnoses(save.Content) };
            old = e?.ContentJson ?? "{}";
            if (e == null) { e = new VisitExamination { VisitId = visitId }; db.Add(e); }
            e.ContentJson = JsonSerializer.Serialize(save.Content, Json); e.Confirmed = save.Confirm;
            e.UpdatedAt = clock.GetUtcNow(); e.UpdatedBy = actorId;
        }
        v.Version++;
        var auditAction = action == "Amend" ? "AmendExamination" : action == "Complete" ? "CompleteVisit" : save?.Confirm == true ? "ConfirmExamination" : action + "Examination";
        var nextExaminationVersion = save == null ? 0 : 1 + await db.Set<VisitAudit>().CountAsync(x => x.VisitId == visitId && SaveActions.Contains(x.Action), ct);
        db.Add(new VisitAudit { VisitId = v.Id, ActorAccountId = actorId, Action = auditAction,
            OccurredAt = clock.GetUtcNow(), BeforeJson = old, AfterJson = save == null
                ? JsonSerializer.Serialize(new { v.Status, v.Version, completedAt = action == "Complete" ? clock.GetUtcNow() : (DateTimeOffset?)null }, Json)
                : JsonSerializer.Serialize(new { examinationVersion = nextExaminationVersion, content = save.Content, confirmed = save.Confirm, reason = (payload as AmendExamination)?.Reason }, Json) });
        db.Add(new ExaminationReceipt { ActorId = actorId, RequestKey = key, VisitId = visitId, Fingerprint = hash });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actorId, visitId, ct);
    }
    private async Task ValidateDiagnosisReferences(ExaminationContent content,ExaminationContent? previous,CancellationToken ct)
    {
        var references=content.DiagnosisReferences;
        if(references==null)return;
        if(references.Count>30 || references.Any(x=>x==null) || references.Select(x=>new{x.Id,x.Version,x.Name}).Distinct().Count()!=references.Count)throw Error("MSG04");
        var names=EffectiveDiagnoses(content);
        foreach(var reference in references){
            if(!names.Contains(reference.Name,StringComparer.OrdinalIgnoreCase))throw Error("MSG04");
            if(previous?.DiagnosisReferences?.Contains(reference)==true)continue;
            var entry=await db.ReferenceEntries.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==reference.Id&&x.Kind=="diagnoses",ct)??throw Error("MSG113",404);
            if(!entry.IsActive)throw Error("MSG202",409);
            if(entry.Version!=reference.Version||entry.Name!=reference.Name||entry.Code!=reference.Code)throw Error("MSG111",409);
        }
    }
    private static string[] EffectiveDiagnoses(ExaminationContent c) =>
        (c.UnscopedDiagnoses ?? c.Diagnoses ?? (string.IsNullOrWhiteSpace(c.Diagnosis) ? Array.Empty<string>() : new[] { c.Diagnosis }))
        .Concat(c.Findings.SelectMany(f => f.Diagnoses ?? Array.Empty<string>()))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static bool HasDiagnosis(ExaminationContent c) => EffectiveDiagnoses(c).Length > 0;
    private static bool NormalOnly(ExaminationContent c) => EffectiveDiagnoses(c) is [var only] && only.Equals("Chưa phát hiện bất thường", StringComparison.OrdinalIgnoreCase);
    private static ExaminationContent NormalizeDiagnoses(ExaminationContent c)
    {
        if (c.UnscopedDiagnoses == null && !c.Findings.Any(f => f.Diagnoses is { Count: > 0 })) {
            if (c.Diagnoses == null) return string.IsNullOrWhiteSpace(c.Conclusion) ? c with { Conclusion = c.Diagnosis } : c;
            var oldValues = c.Diagnoses.Select(value => value.Trim()).ToArray();
            var oldText = string.Join("; ", oldValues);
            return c with { Diagnoses = oldValues, Diagnosis = oldText, Conclusion = oldText };
        }
        var unscoped = (c.UnscopedDiagnoses ?? c.Diagnoses ?? (string.IsNullOrWhiteSpace(c.Diagnosis) ? Array.Empty<string>() : new[] { c.Diagnosis })).Select(value => value.Trim()).ToArray();
        var values = EffectiveDiagnoses(c);
        var text = string.Join("; ", values);
        return c with { UnscopedDiagnoses = unscoped, Findings = c.Findings.Select(f => f with { Diagnoses = f.Diagnoses?.Select(value => value.Trim()).ToArray() }).ToArray(), Diagnoses = values, Diagnosis = text, Conclusion = text };
    }
    private static void ValidateDiagnosisList(IReadOnlyList<string>? values)
    {
        if (values != null && (values.Count > 30 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 4000)
            || values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count
            || string.Join("; ", values.Select(value => value.Trim())).Length > 4000)) throw Error("MSG04");
    }
    private static void Validate(ExaminationContent? c, bool confirm)
    {
        static void Required(bool missing, string label, string field) {
            if(missing) throw new BusinessException(400, SystemMessages.Get("MSG02",field:label) with { Field=field });
        }
        if (c == null || c.Symptoms?.Length > 4000 || c.Diagnosis?.Length > 4000 || c.Notes?.Length > 8000 || c.Findings == null || c.Findings.Count > 100) throw Error("MSG04");
        ValidateDiagnosisList(c.Diagnoses);
        ValidateDiagnosisList(c.UnscopedDiagnoses);
        foreach (var fact in new[] { c.MedicalHistory, c.Allergies, c.Medications })
            if (fact == null || fact.Detail?.Length > 4000) throw Error("MSG04");
        for (var index=0;index<c.Findings.Count;index++) {
            var f=c.Findings[index];
            if (f == null || f.Tooth is { } tooth && !(tooth / 10 is >= 1 and <= 4 && tooth % 10 is >= 1 and <= 8 || tooth / 10 is >= 5 and <= 8 && tooth % 10 is >= 1 and <= 5)
                || (f.Surfaces is { Count: > 0 } && (f.Tooth == null || f.Surfaces.Count > 5 || f.Surfaces.Distinct().Count() != f.Surfaces.Count || f.Surfaces.Any(surface => surface is not ("O" or "B" or "L" or "M" or "D"))))
                || f.Condition == null || f.Region?.Length > 200 || f.Condition?.Length > 2000) throw Error("MSG04");
            ValidateDiagnosisList(f.Diagnoses);
            if(confirm) {
                Required(f.Tooth==null && string.IsNullOrWhiteSpace(f.Region),$"vùng khám cho kết quả số {index+1}",$"findings.{index}.region");
                Required(string.IsNullOrWhiteSpace(f.Condition) && f.Diagnoses is not { Count: > 0 },$"tình trạng / kết quả khám số {index+1}",$"findings.{index}.condition");
            }
        }
        var effective = EffectiveDiagnoses(c);
        if (effective.Length > 30 || string.Join("; ", effective).Length > 4000) throw Error("MSG04");
        if (c.Conclusion?.Length > 4000 || c.Advice?.Length > 4000 || c.ReturnNeed?.Length > 2000 || c.ReferralDestination?.Length > 1000
            || c.Disposition != null && !new[] { "ConsultationOnly", "FollowUp", "Referral", "DeferredDecision", "ProceedToTreatment" }.Contains(c.Disposition)) throw Error("MSG04");
        if(confirm) {
            Required(string.IsNullOrWhiteSpace(c.Symptoms),"lý do khám / triệu chứng","symptoms");
            Required(!HasDiagnosis(c),"chẩn đoán / kết luận cuối cùng",c.Diagnoses == null ? "diagnosis" : "diagnoses");
            Required(c.Findings.Count==0 && !NormalOnly(c),"ít nhất một kết quả khám răng hoặc vùng (dùng Thêm vùng khám nếu khám tổng quát)","findings");
            Required(string.IsNullOrWhiteSpace(c.Advice),"dặn dò / lời khuyên","advice");
            Required(c.Disposition==null,"hướng xử trí","disposition");
            Required(c.Disposition=="FollowUp" && string.IsNullOrWhiteSpace(c.ReturnNeed),"nhu cầu quay lại","returnNeed");
            Required(c.Disposition=="Referral" && string.IsNullOrWhiteSpace(c.ReferralDestination),"nơi / lý do chuyển khám","referralDestination");
        }
    }
    public async Task<IReadOnlyList<ExaminationHistory>> HistoryAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await reception.VisitAsync(actorId, visitId, ct);
        var v = await access.VisitAsync(actorId, visitId, false, ct);
        var rows = await (from e in db.Set<VisitExamination>().AsNoTracking() join visit in db.Visits on e.VisitId equals visit.Id
            where visit.PatientId == v.PatientId && visit.BranchId == v.BranchId && visit.Id != visitId
            orderby visit.ArrivalAt descending select new { visit.Id, visit.ArrivalAt, Examination = e,
                Version = db.Set<VisitAudit>().Count(a => a.VisitId == visit.Id && SaveActions.Contains(a.Action)) }).Take(100).ToArrayAsync(ct);
        return rows.Select(x => new ExaminationHistory(x.Id, x.ArrivalAt, Dto(x.Examination, x.Version))).ToArray();
    }
    public async Task<ClinicalHandoff> HandoffAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await access.VisitAsync(actorId, visitId, true, ct);
        var w = await GetAsync(actorId, visitId, ct);
        if (w.Examination?.Confirmed != true || (w.Visit.Status != VisitStatuses.InProgress && !(w.Visit.Status == VisitStatuses.Completed && w.Examination.Content.Disposition == "ProceedToTreatment"))) throw Error("MSG42", 409);
        if(w.Investigations?.Any(x=>x.RequiredForDiagnosis && x.State is not ("Reviewed" or "Cancelled"))==true) throw new BusinessException(409,SystemMessages.Get("MSG239",field:"cần nhận và đọc kết quả cận lâm sàng bổ sung") with {Field="investigations"});
        var files = new List<DocumentDto>();
        for (var pageNumber = 1; ; pageNumber++) {
            var page = await documents.ListAsync(actorId, new(w.Visit.PatientId, VisitId: visitId, Page: pageNumber, PageSize: 100), ct);
            files.AddRange(page.Items); if (files.Count >= page.Total) break;
        }
        return new(visitId, w.Visit.PatientId, w.Visit.BranchId, w.Visit.VisitVersion, w.Examination.Content,
            w.Examination.Content.Findings.Where(x => x.Tooth.HasValue).Select(x => x.Tooth!.Value).Distinct().ToArray(), files, w.Visit.Source, w.Examination.Version, w.Visit.Status);
    }

    public async Task<IReadOnlyList<PatientExamination>> ListAsync(int actorId, int patientId, CancellationToken ct)
    {
        var branches = await access.BranchesAsync(actorId, ct);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct)) throw Error("MSG113", 404);
        var rows = await (from v in db.Visits.AsNoTracking()
            join a in db.Appointments on v.AppointmentId equals a.Id
            join b in db.Set<DCMS.Domain.Features.Scheduling.SchedulingBranch>() on v.BranchId equals b.Id
            join d in db.Employees on v.DentistId equals d.Id
            join e in db.Set<VisitExamination>() on v.Id equals e.VisitId into exams
            from e in exams.DefaultIfEmpty()
            where v.PatientId == patientId && branches.Contains(v.BranchId)
                && (v.Purpose == AttendancePurposes.InitialExamination || v.Purpose == AttendancePurposes.PeriodicOrNewProblem || e != null)
            orderby v.ArrivalAt descending, v.Id
            select new { Visit = v, a.Code, BranchName = b.Name, DentistName = d.FullName, Examination = e,
                Version = db.Set<VisitAudit>().Count(audit => audit.VisitId == v.Id && SaveActions.Contains(audit.Action)) })
            .Take(100).ToArrayAsync(ct);
        return rows.Select(x => new PatientExamination(x.Visit.Id, x.Code, x.Visit.BranchId, x.BranchName, x.DentistName,
            x.Visit.ArrivalAt, x.Visit.Status, x.Examination == null ? null : Dto(x.Examination, x.Version))).ToArray();
    }

    public async Task<IReadOnlyList<ExaminationVersion>> VersionsAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await access.VisitAsync(actorId, visitId, false, ct);
        var audits = await db.Set<VisitAudit>().AsNoTracking().Where(a => a.VisitId == visitId && SaveActions.Contains(a.Action))
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToArrayAsync(ct);
        return audits.Select((a, i) => {
            using var doc = JsonDocument.Parse(a.AfterJson);
            var root = doc.RootElement;
            var wrapped = root.TryGetProperty("examinationVersion", out var version);
            var content = (wrapped ? root.GetProperty("content") : root).Deserialize<ExaminationContent>(Json)!;
            return new ExaminationVersion(wrapped ? version.GetInt32() : i + 1, content,
                a.Action != "SaveExamination", a.ActorAccountId, a.OccurredAt,
                wrapped && root.TryGetProperty("reason", out var reason) ? reason.GetString() : null);
        }).OrderByDescending(v => v.Version).ToArray();
    }
}
