using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    IClinicalDocumentService documents, TimeProvider clock) : IExaminationService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static ExaminationDto Dto(VisitExamination e) => new(JsonSerializer.Deserialize<ExaminationContent>(e.ContentJson, Json)!, e.Confirmed, e.UpdatedBy, e.UpdatedAt);
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
            : row?.Confirmed == true ? ["handoff"] : [] : [];
        return new(await reception.VisitAsync(actorId, visitId, ct), row == null ? null : Dto(row), initial, actions);
    }
    public Task<ExaminationWorkspace> StartAsync(int actorId, Guid visitId, StartExamination request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Start", request, null, ct);
    public Task<ExaminationWorkspace> SaveAsync(int actorId, Guid visitId, SaveExamination request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, request.PatientId, request.ExpectedVersion, request.RequestKey, "Save", request, request, ct);

    private async Task<ExaminationWorkspace> CommandAsync(int actorId, Guid visitId, int patientId, int version, Guid key,
        string action, object payload, SaveExamination? save, CancellationToken ct)
    {
        if (key == Guid.Empty || patientId <= 0 || version <= 0) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var v = await access.VisitAsync(actorId, visitId, true, ct);
        if (v.PatientId != patientId) throw Error("MSG113", 404);
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
        if (save == null) {
            if (v.Status != VisitStatuses.Queued || a.Status != "CheckedIn") throw Error("MSG42", 409);
            v.Status = VisitStatuses.InProgress; a.Status = "InProgress"; a.Version++;
        } else {
            if (v.Status != VisitStatuses.InProgress || a.Status != "InProgress" || e?.Confirmed == true) throw Error("MSG42", 409);
            Validate(save.Content, save.Confirm);
            old = e?.ContentJson ?? "{}";
            if (e == null) { e = new VisitExamination { VisitId = visitId }; db.Add(e); }
            e.ContentJson = JsonSerializer.Serialize(save.Content, Json); e.Confirmed = save.Confirm;
            e.UpdatedAt = clock.GetUtcNow(); e.UpdatedBy = actorId;
        }
        v.Version++;
        db.Add(new VisitAudit { VisitId = v.Id, ActorAccountId = actorId, Action = save?.Confirm == true ? "ConfirmExamination" : action + "Examination",
            OccurredAt = clock.GetUtcNow(), BeforeJson = old, AfterJson = e?.ContentJson ?? JsonSerializer.Serialize(new { v.Status, v.Version }, Json) });
        db.Add(new ExaminationReceipt { ActorId = actorId, RequestKey = key, VisitId = visitId, Fingerprint = hash });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actorId, visitId, ct);
    }
    private static void Validate(ExaminationContent? c, bool confirm)
    {
        if (c == null || c.Symptoms?.Length > 4000 || c.Diagnosis?.Length > 4000 || c.Notes?.Length > 8000 || c.Findings == null || c.Findings.Count > 100) throw Error("MSG04");
        foreach (var fact in new[] { c.MedicalHistory, c.Allergies, c.Medications })
            if (fact == null || !new[] { "Unknown", "None", "Present" }.Contains(fact.State) || fact.Detail?.Length > 4000
                || confirm && (fact.State == "Present" && string.IsNullOrWhiteSpace(fact.Detail) || !fact.Verified)) throw Error("MSG04");
        foreach (var f in c.Findings) {
            if (f == null || f.Tooth is { } tooth && !(tooth / 10 is >= 1 and <= 4 && tooth % 10 is >= 1 and <= 8 || tooth / 10 is >= 5 and <= 8 && tooth % 10 is >= 1 and <= 5)
                || f.Condition == null || confirm && (f.Tooth == null && string.IsNullOrWhiteSpace(f.Region) || string.IsNullOrWhiteSpace(f.Condition)) || f.Region?.Length > 200 || f.Condition?.Length > 2000) throw Error("MSG04");
        }
        if (confirm && (string.IsNullOrWhiteSpace(c.Symptoms) || string.IsNullOrWhiteSpace(c.Diagnosis) || c.Findings.Count == 0)) throw Error("MSG02");
    }
    public async Task<IReadOnlyList<ExaminationHistory>> HistoryAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await reception.VisitAsync(actorId, visitId, ct);
        var v = await access.VisitAsync(actorId, visitId, false, ct);
        var rows = await (from e in db.Set<VisitExamination>().AsNoTracking() join visit in db.Visits on e.VisitId equals visit.Id
            where visit.PatientId == v.PatientId && visit.BranchId == v.BranchId && visit.Id != visitId
            orderby visit.ArrivalAt descending select new { visit.Id, visit.ArrivalAt, Examination = e }).Take(100).ToArrayAsync(ct);
        return rows.Select(x => new ExaminationHistory(x.Id, x.ArrivalAt, Dto(x.Examination))).ToArray();
    }
    public async Task<ClinicalHandoff> HandoffAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await access.VisitAsync(actorId, visitId, true, ct);
        var w = await GetAsync(actorId, visitId, ct);
        if (w.Examination?.Confirmed != true) throw Error("MSG42", 409);
        var files = new List<DocumentDto>();
        for (var pageNumber = 1; ; pageNumber++) {
            var page = await documents.ListAsync(actorId, new(w.Visit.PatientId, VisitId: visitId, Page: pageNumber, PageSize: 100), ct);
            files.AddRange(page.Items); if (files.Count >= page.Total) break;
        }
        return new(visitId, w.Visit.PatientId, w.Visit.BranchId, w.Visit.VisitVersion, w.Examination.Content,
            w.Examination.Content.Findings.Where(x => x.Tooth.HasValue).Select(x => x.Tooth!.Value).Distinct().ToArray(), files, w.Visit.Source);
    }
}
