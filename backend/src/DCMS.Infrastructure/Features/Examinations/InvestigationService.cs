using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class InvestigationService(DcmsDbContext db, ClinicalAccess access,
    IClinicalDocumentService documents, TimeProvider clock) : IInvestigationService
{
    public async Task<InvestigationPage> ListAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        var visit = await access.VisitAsync(actorId, visitId, false, ct);
        var dentist = await access.BranchAsync(actorId, visit.BranchId, false, ct);
        var own = await db.Employees.AnyAsync(x => x.Id == visit.DentistId && x.AccountId == actorId, ct);
        var active = visit.Status == VisitStatuses.InProgress;
        var confirmed = await db.Set<VisitExamination>().AnyAsync(x => x.VisitId == visitId && x.Confirmed, ct);
        var rows = await db.Set<InvestigationOrder>().AsNoTracking().Where(x => x.VisitId == visitId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var users = await db.Users.Where(x => rows.Select(r => r.CreatedBy).Contains(x.Id)
            || rows.Select(r => r.ResultBy).Contains(x.Id) || rows.Select(r => r.ReviewedBy).Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        string? Name(int? id) => id.HasValue && users.TryGetValue(id.Value, out var name) ? name : null;
        var items = new List<InvestigationDto>();
        foreach (var row in rows) {
            var files = new List<DocumentDto>();
            foreach (var id in await db.Set<ClinicalDocument>().AsNoTracking().Where(x => x.InvestigationId == row.Id).OrderBy(x => x.UploadedAt).Select(x => x.Id).ToArrayAsync(ct))
                files.Add(await documents.DetailAsync(actorId, id, ct));
            var actions = new List<string>();
            if (active && row.State == "Ordered" && (!dentist || own)) actions.AddRange(["upload", "result"]);
            if (active && own && row.State == "ResultAvailable") actions.Add("review");
            if (active && own && row.State is "Ordered" or "ResultAvailable") actions.Add("cancel");
            items.Add(new(row.Id, visitId, row.Kind, row.Name, row.Region, row.Reason, row.RequiredForDiagnosis,
                row.State, row.Version, Name(row.CreatedBy) ?? "", row.CreatedAt, row.ResultText, Name(row.ResultBy), row.ResultAt,
                row.Interpretation, Name(row.ReviewedBy), row.ReviewedAt, row.CancellationReason, row.CancelledAt,
                JsonSerializer.Deserialize<Guid[]>(row.ResultDocumentIdsJson)!, files, actions));
        }
        return new(visitId, visit.Version, active && own && !confirmed, items);
    }
    public Task<InvestigationPage> CreateAsync(int actorId, Guid visitId, CreateInvestigation request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, null, request.PatientId, request.ExpectedVisitVersion, null, request.RequestKey, "CreateInvestigation", request, ct);
    public Task<InvestigationPage> ResultAsync(int actorId, Guid visitId, Guid id, ReceiveInvestigationResult request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, id, request.PatientId, request.ExpectedVisitVersion, request.ExpectedVersion, request.RequestKey, "ReceiveInvestigationResult", request, ct);
    public Task<InvestigationPage> ReviewAsync(int actorId, Guid visitId, Guid id, ReviewInvestigation request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, id, request.PatientId, request.ExpectedVisitVersion, request.ExpectedVersion, request.RequestKey, "ReviewInvestigation", request, ct);
    public Task<InvestigationPage> CancelAsync(int actorId, Guid visitId, Guid id, CancelInvestigation request, CancellationToken ct) =>
        CommandAsync(actorId, visitId, id, request.PatientId, request.ExpectedVisitVersion, request.ExpectedVersion, request.RequestKey, "CancelInvestigation", request, ct);
    private async Task<InvestigationPage> CommandAsync(int actorId, Guid visitId, Guid? id, int patientId, int expectedVisit,
        int? expectedOrder, Guid key, string action, object payload, CancellationToken ct)
    {
        if (key == Guid.Empty || patientId <= 0 || expectedVisit <= 0 || expectedOrder is <= 0) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var visit = await access.VisitAsync(actorId, visitId, action != "ReceiveInvestigationResult", ct);
        if (visit.PatientId != patientId) throw Error("MSG113", 404);
        if (action == "ReceiveInvestigationResult") {
            var dentist = await access.BranchAsync(actorId, visit.BranchId, false, ct);
            if (dentist && !await db.Employees.AnyAsync(x => x.Id == visit.DentistId && x.AccountId == actorId, ct)) throw Error("MSG06", 403);
        }
        var row = id.HasValue ? await db.Set<InvestigationOrder>().SingleOrDefaultAsync(x => x.Id == id && x.VisitId == visitId, ct) ?? throw Error("MSG113",404) : null;
        var fingerprint = ExaminationService.Hash(new { visitId, id, action, payload });
        var receipt = await db.Set<ExaminationReceipt>().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == key, ct);
        if (receipt != null) {
            if (receipt.VisitId != visitId || receipt.Fingerprint != fingerprint) throw Error("MSG111",409);
            await tx.CommitAsync(ct); return await ListAsync(actorId, visitId, ct);
        }
        if (visit.Version != expectedVisit || row != null && row.Version != expectedOrder) throw Error("MSG111",409);
        if (visit.Status != VisitStatuses.InProgress) throw Error("MSG42",409);
        int? sourceExaminationVersion = null; string? amendmentReason = null;
        var before = row == null ? "{}" : JsonSerializer.Serialize(row, ExaminationService.Json);
        if (payload is CreateInvestigation create) {
            if (await db.Set<VisitExamination>().AnyAsync(x => x.VisitId == visitId && x.Confirmed, ct)) {
                if (string.IsNullOrWhiteSpace(create.AmendmentReason) || create.AmendmentReason.Length > 1000)
                    throw new BusinessException(400,SystemMessages.Get("MSG02",field:"lý do bổ sung chỉ định sau xác nhận khám") with {Field="amendmentReason"});
                sourceExaminationVersion = await db.Set<VisitAudit>().CountAsync(x => x.VisitId == visitId && new[]{"SaveExamination","ConfirmExamination","AmendExamination"}.Contains(x.Action),ct);
                if (create.ExpectedExaminationVersion != sourceExaminationVersion) throw Error("MSG111",409);
                amendmentReason = create.AmendmentReason.Trim();
            }
            if (!new[] {"XRay","Imaging","Lab","Pathology","Other"}.Contains(create.Kind) || string.IsNullOrWhiteSpace(create.Name) || create.Name.Length > 160
                || create.Region?.Length > 200 || string.IsNullOrWhiteSpace(create.Reason) || create.Reason.Length > 1000
                || await db.Set<InvestigationOrder>().CountAsync(x => x.VisitId == visitId, ct) >= 100) throw Error("MSG04");
            row = new InvestigationOrder { VisitId = visitId, Kind = create.Kind, Name = create.Name.Trim(), Region = create.Region?.Trim(),
                Reason = create.Reason.Trim(), RequiredForDiagnosis = create.RequiredForDiagnosis, CreatedBy = actorId,
                CreatedAt = clock.GetUtcNow(), CreateRequestKey = key };
            db.Add(row);
        } else if (payload is ReceiveInvestigationResult result) {
            if (row!.State != "Ordered") throw Error("MSG42",409);
            if (result.ResultText?.Length > 4000) throw Error("MSG04");
            var ready = await db.Set<ClinicalDocument>().Where(x => x.InvestigationId == row.Id && x.FileState == "Ready").Select(x => x.Id).ToArrayAsync(ct);
            if (string.IsNullOrWhiteSpace(result.ResultText) && ready.Length == 0)
                throw new BusinessException(400, SystemMessages.Get("MSG02",field:"nội dung kết quả hoặc tệp sẵn sàng"));
            row.ResultText = string.IsNullOrWhiteSpace(result.ResultText) ? null : result.ResultText.Trim();
            row.ResultDocumentIdsJson = JsonSerializer.Serialize(ready); row.State = "ResultAvailable";
            row.ResultBy = actorId; row.ResultAt = clock.GetUtcNow(); row.Version++;
        } else if (payload is ReviewInvestigation review) {
            if (row!.State != "ResultAvailable") throw Error("MSG42",409);
            if (review.Interpretation?.Length > 4000) throw Error("MSG04");
            var fileIds = JsonSerializer.Deserialize<Guid[]>(row.ResultDocumentIdsJson)!;
            if (await db.Set<ClinicalDocument>().CountAsync(x => fileIds.Contains(x.Id) && x.InvestigationId == row.Id && x.FileState == "Ready",ct) != fileIds.Length
                || fileIds.Length == 0 && string.IsNullOrWhiteSpace(row.ResultText)) throw Error("MSG42",409);
            foreach (var file in await db.Set<ClinicalDocument>().Where(x => fileIds.Contains(x.Id)).ToArrayAsync(ct)) {
                if (!await db.Set<DocumentReview>().AnyAsync(x => x.DocumentId == file.Id && x.VisitId == visitId,ct)) {
                    db.Add(new DocumentReview { DocumentId = file.Id, VisitId = visitId, ReviewedBy = actorId, ReviewedAt = clock.GetUtcNow() });
                    file.Version++; db.Add(new DocumentAudit { DocumentId = file.Id, ActorId = actorId, Action = "ClinicallyReviewed", OccurredAt = clock.GetUtcNow() });
                }
            }
            row.Interpretation = review.Interpretation?.Trim(); row.ReviewedBy = actorId; row.ReviewedAt = clock.GetUtcNow(); row.State = "Reviewed"; row.Version++;
        } else if (payload is CancelInvestigation cancel) {
            if (row!.State is not ("Ordered" or "ResultAvailable")) throw Error("MSG42",409);
            if (string.IsNullOrWhiteSpace(cancel.Reason) || cancel.Reason.Length > 1000) throw Error("MSG04");
            row.CancellationReason = cancel.Reason.Trim(); row.CancelledBy = actorId; row.CancelledAt = clock.GetUtcNow(); row.State = "Cancelled"; row.Version++;
        }
        visit.Version++;
        var after = JsonSerializer.SerializeToNode(row,ExaminationService.Json)!;
        if(sourceExaminationVersion.HasValue){after["sourceExaminationVersion"]=sourceExaminationVersion.Value;after["amendmentReason"]=amendmentReason;}
        db.Add(new VisitAudit { VisitId = visitId, ActorAccountId = actorId, Action = action, OccurredAt = clock.GetUtcNow(),
            BeforeJson = before, AfterJson = after.ToJsonString(ExaminationService.Json) });
        db.Add(new ExaminationReceipt { ActorId = actorId, RequestKey = key, VisitId = visitId, Fingerprint = fingerprint });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ListAsync(actorId, visitId, ct);
    }
}
