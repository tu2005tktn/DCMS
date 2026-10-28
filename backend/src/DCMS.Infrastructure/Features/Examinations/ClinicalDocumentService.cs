using System.Security.Cryptography;
using System.Text.Json;
using DCMS.Application.Features.Examinations;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static DCMS.Infrastructure.Features.Examinations.ClinicalAccess;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ClinicalDocumentService(DcmsDbContext db, ClinicalAccess access, IConfiguration config,
    IClinicalFileScanner scanner, TimeProvider clock) : IClinicalDocumentService
{
    public static readonly DocumentConstraints Limits = new(10, 20 * 1024 * 1024,
        ["image/jpeg", "image/png", "application/pdf"], 1000);
    private static readonly string[] Types = ["XRay", "IntraoralPhoto", "LabResult", "ConsentForm", "Referral", "Other"];
    private string PathFor(Guid id) => Path.Combine(StorageRoot(), id.ToString("N"));
    private string StorageRoot()
    {
        var path = config["ClinicalDocuments:StoragePath"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw Error("MSG144", 503);
        var root = Path.GetFullPath(path);
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent) {
            if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                && (root.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase) || root.StartsWith(directory.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) throw Error("MSG144", 503);
        }
        return root;
    }
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
    private void Audit(Guid id, int? actor, string action, string? note = null) => db.Add(new DocumentAudit {
        DocumentId = id, ActorId = actor, Action = action, Note = note, OccurredAt = clock.GetUtcNow() });
    private async Task<Visit> ContextAsync(int actor, int patient, Guid visit, Guid? dossier, Guid? plan, CancellationToken ct)
    {
        var v = await access.VisitAsync(actor, visit, false, ct);
        if (v.PatientId != patient) throw Error("MSG113", 404);
        if (dossier.HasValue || plan.HasValue) {
            if (!await db.TreatmentDossiers.AnyAsync(x => x.PatientId == patient && x.BranchId == v.BranchId
                && (!dossier.HasValue || x.Id == dossier) && (!plan.HasValue || x.PlanId == plan), ct)) throw Error("MSG113", 404);
        }
        return v;
    }
    private async Task<ClinicalDocument> RowAsync(int actor, Guid id, CancellationToken ct)
    {
        var d = await db.Set<ClinicalDocument>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await ContextAsync(actor, d.PatientId, d.VisitId, d.DossierId, d.PlanId, ct); return d;
    }
    private async Task<DocumentDto> DtoAsync(ClinicalDocument d, CancellationToken ct)
    {
        var uploader = await db.Users.Where(x => x.Id == d.UploaderId).Select(x => x.FullName).SingleAsync(ct);
        var dossier = d.DossierId.HasValue ? await db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == d.DossierId, ct) : null;
        var reviews = await db.Set<DocumentReview>().AsNoTracking().Where(x => x.DocumentId == d.Id)
            .Select(x => new DocumentReviewDto(x.VisitId, x.ReviewedBy, x.ReviewedAt)).ToArrayAsync(ct);
        return new(d.Id, d.Version, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType, d.Source, d.SourceDate,
            d.Description, new(d.VisitId, $"Lượt {d.VisitId.ToString()[..8]}", d.DossierId, dossier?.Code, d.PlanId,
                d.PlanId.HasValue ? "Kế hoạch điều trị" : null, d.InvestigationId, d.InvestigationId.HasValue ? await db.Set<InvestigationOrder>().Where(x => x.Id == d.InvestigationId).Select(x => x.Name).SingleAsync(ct) : null), uploader, d.UploadedAt, d.FileState, d.RejectReason,
            d.FileState == "Ready" ? ["view", "download"] : [], reviews);
    }
    public async Task<object> ContextsAsync(int actorId, int patientId, CancellationToken ct)
    {
        var branches = await access.BranchesAsync(actorId, ct);
        var patient = await db.Patients.AsNoTracking().Where(x => x.Id == patientId).Select(x => new { x.Id, x.PatientCode, x.FullName, x.BirthDate }).SingleOrDefaultAsync(ct) ?? throw Error("MSG113", 404);
        var visits = await db.Visits.AsNoTracking().Where(x => x.PatientId == patientId && branches.Contains(x.BranchId)).OrderByDescending(x => x.ArrivalAt).ToArrayAsync(ct);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId && branches.Contains(x.BranchId)).ToArrayAsync(ct);
        if (visits.Length == 0 && dossiers.Length == 0) throw Error("MSG113", 404);
        return new { patient, visits = visits.Select(x => new { x.Id, Label = $"Lượt {x.ArrivalAt.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}", Date = x.ArrivalAt }),
            dossiers = dossiers.Select(x => new { x.Id, Label = x.Code }), plans = dossiers.Select(x => new { Id = x.PlanId, Label = x.Code + " / kế hoạch", DossierId = x.Id }) };
    }
    public async Task<DocumentPage> ListAsync(int actorId, DocumentQuery q, CancellationToken ct)
    {
        if (q.PatientId <= 0 || q.Page < 1 || q.Page > 100000 || !new[] {20,50,100}.Contains(q.PageSize) || q.Type != null && !Types.Contains(q.Type) || q.From > q.To) throw Error("MSG04");
        var branches = await access.BranchesAsync(actorId, ct);
        if (q.VisitId.HasValue) await ContextAsync(actorId, q.PatientId, q.VisitId.Value, q.DossierId, q.PlanId, ct);
        else if (!await db.Visits.AnyAsync(x => x.PatientId == q.PatientId && branches.Contains(x.BranchId), ct)) throw Error("MSG113", 404);
        if ((q.DossierId.HasValue || q.PlanId.HasValue) && !await db.TreatmentDossiers.AnyAsync(x => x.PatientId == q.PatientId && branches.Contains(x.BranchId)
            && (!q.DossierId.HasValue || x.Id == q.DossierId) && (!q.PlanId.HasValue || x.PlanId == q.PlanId), ct)) throw Error("MSG113", 404);
        var query = db.Set<ClinicalDocument>().AsNoTracking().Where(x => x.PatientId == q.PatientId
            && db.Visits.Any(v => v.Id == x.VisitId && branches.Contains(v.BranchId))
            && (!q.VisitId.HasValue || x.VisitId == q.VisitId) && (!q.DossierId.HasValue || x.DossierId == q.DossierId)
            && (!q.PlanId.HasValue || x.PlanId == q.PlanId) && (q.Type == null || x.DocumentType == q.Type)
            && (!q.From.HasValue || x.SourceDate >= q.From) && (!q.To.HasValue || x.SourceDate <= q.To));
        var total = await query.CountAsync(ct); var list = new List<DocumentDto>();
        foreach (var d in await query.OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct)) list.Add(await DtoAsync(d, ct));
        var canUpload = await db.Visits.AnyAsync(x => x.PatientId == q.PatientId && branches.Contains(x.BranchId)
            && (!q.VisitId.HasValue || x.Id == q.VisitId) && (x.Status == VisitStatuses.Queued || x.Status == VisitStatuses.InProgress), ct);
        return new(list, total, q.Page, q.PageSize, Limits, canUpload ? ["upload"] : []);
    }
    public async Task<DocumentDto> DetailAsync(int actorId, Guid documentId, CancellationToken ct) => await DtoAsync(await RowAsync(actorId, documentId, ct), ct);
    public async Task<IReadOnlyList<DocumentHistoryDto>> HistoryAsync(int actorId, Guid documentId, CancellationToken ct)
    {
        await RowAsync(actorId, documentId, ct);
        return await (from a in db.Set<DocumentAudit>() join u in db.Users on a.ActorId equals u.Id into us from u in us.DefaultIfEmpty()
            where a.DocumentId == documentId orderby a.OccurredAt, a.Id select new DocumentHistoryDto(a.Action, u == null ? "Hệ thống" : u.FullName, a.OccurredAt, a.Note)).ToArrayAsync(ct);
    }
    private static string? FileError(DocumentUpload file)
    {
        var b = file.Content; var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (b.LongLength == 0 || b.LongLength > Limits.MaxBytes || file.FileName.Length > 255 || file.FileName.Any(char.IsControl)
            || file.FileName.Contains('/') || file.FileName.Contains('\\')) return "Tệp rỗng, quá lớn hoặc tên không hợp lệ.";
        var valid = file.ContentType switch {
            "image/jpeg" => ext is ".jpg" or ".jpeg" && b.Length >= 4 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff && b[^2] == 0xff && b[^1] == 0xd9,
            "image/png" => ext == ".png" && b.Length >= 24 && b.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) && b.AsSpan(12,4).SequenceEqual("IHDR"u8),
            "application/pdf" => ext == ".pdf" && b.Length >= 12 && b.AsSpan(0,5).SequenceEqual("%PDF-"u8) && System.Text.Encoding.ASCII.GetString(b.AsSpan(Math.Max(0,b.Length-1024))).Contains("%%EOF"),
            _ => false };
        return valid ? null : "Chỉ nhận JPEG/PNG/PDF có MIME, phần mở rộng và chữ ký nội dung phù hợp.";
    }
    private sealed record BatchEntry(Guid? Id, string? Error);
    public async Task<UploadBatchResult> UploadAsync(int actorId, DocumentMetadata m, IReadOnlyList<DocumentUpload> files, CancellationToken ct)
    {
        if (m.RequestKey == Guid.Empty || files.Count is < 1 or > 10 || !Types.Contains(m.DocumentType) || !new[] {"Clinic","External"}.Contains(m.Source)
            || m.Description?.Length > 1000 || m.ConsentVersion != null || m.SourceDate.Year < 1900
            || m.SourceDate > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var visit = await ContextAsync(actorId, m.PatientId, m.VisitId, m.DossierId, m.PlanId, ct);
        InvestigationOrder? investigation = null;
        if (m.InvestigationId.HasValue) {
            investigation = await db.Set<InvestigationOrder>().SingleOrDefaultAsync(x => x.Id == m.InvestigationId && x.VisitId == m.VisitId,ct) ?? throw Error("MSG113",404);
            var dentist = await access.BranchAsync(actorId,visit.BranchId,false,ct);
            if (dentist && !await db.Employees.AnyAsync(x => x.Id == visit.DentistId && x.AccountId == actorId,ct)) throw Error("MSG06",403);
        }
        var hash = ExaminationService.Hash(new { m, files = files.Select(x => new { x.FileName, x.ContentType, Hash = Convert.ToHexString(SHA256.HashData(x.Content)) }) });
        var receipt = await db.Set<DocumentReceipt>().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == m.RequestKey, ct);
        if (receipt != null) {
            if (receipt.Fingerprint != hash) throw Error("MSG111", 409);
            var entries = JsonSerializer.Deserialize<BatchEntry[]>(receipt.ResultJson)!; var replay = new List<DocumentUploadResult>();
            foreach (var entry in entries) replay.Add(new(entry.Id.HasValue ? await DetailAsync(actorId, entry.Id.Value, ct) : null, entry.Error));
            await tx.CommitAsync(ct); return new(replay.Count == 1 ? replay[0].Document : null, replay);
        }
        if (visit.Status is not (VisitStatuses.Queued or VisitStatuses.InProgress)) throw Error("MSG42", 409);
        if (investigation != null && investigation.State != "Ordered") throw Error("MSG42",409);
        var results = new List<DocumentUploadResult>(); var ids = new List<BatchEntry>(); var written = new List<string>();
        try {
            foreach (var f in files) {
                var error = FileError(f); if (error != null) { results.Add(new(null, error)); ids.Add(new(null, error)); continue; }
                var d = new ClinicalDocument { PatientId = m.PatientId, VisitId = m.VisitId, DossierId = m.DossierId, PlanId = m.PlanId, InvestigationId = m.InvestigationId,
                    FileName = f.FileName, ContentType = f.ContentType, SizeBytes = f.Content.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(f.Content)),
                    DocumentType = m.DocumentType, Source = m.Source, SourceDate = m.SourceDate, Description = m.Description,
                    UploaderId = actorId, UploadedAt = clock.GetUtcNow() };
                Directory.CreateDirectory(StorageRoot()); var path = PathFor(d.Id);
                await File.WriteAllBytesAsync(path, f.Content, ct); written.Add(path);
                d.FileState = await scanner.ScanAsync(f.Content, ct);
                if (d.FileState is not ("Ready" or "Rejected")) d.FileState = "PendingScan";
                if (d.FileState == "Rejected") d.RejectReason = "Tệp không đạt kiểm tra an toàn.";
                db.Add(d); Audit(d.Id, actorId, "Uploaded");
                if (d.FileState != "PendingScan") Audit(d.Id, null, d.FileState == "Ready" ? "ScanPassed" : "ScanRejected");
                ids.Add(new(d.Id, null)); results.Add(new(await DtoAsync(d, ct), null));
            }
            if (investigation != null && ids.Any(x => x.Id.HasValue)) {
                var before = JsonSerializer.Serialize(new { investigation.Version, VisitVersion = visit.Version });
                investigation.Version++; visit.Version++;
                db.Add(new VisitAudit { VisitId = visit.Id, ActorAccountId = actorId, Action = "InvestigationDocumentUploaded", OccurredAt = clock.GetUtcNow(), BeforeJson = before,
                    AfterJson = JsonSerializer.Serialize(new { investigation.Id, investigation.Version, VisitVersion = visit.Version, DocumentIds = ids.Where(x => x.Id.HasValue).Select(x => x.Id) }) });
            }
            db.Add(new DocumentReceipt { ActorId = actorId, RequestKey = m.RequestKey, Fingerprint = hash, ResultJson = JsonSerializer.Serialize(ids) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        } catch { foreach (var path in written) { try { File.Delete(path); } catch (IOException) { } } throw; }
        return new(results.Count == 1 ? results[0].Document : null, results);
    }
    public async Task<DocumentContent> ContentAsync(int actorId, Guid documentId, bool inline, CancellationToken ct)
    {
        var d = await RowAsync(actorId, documentId, ct); if (d.FileState != "Ready") throw Error("MSG42", 409);
        byte[] bytes; try { bytes = await File.ReadAllBytesAsync(PathFor(d.Id), ct); } catch (IOException) { throw Error("MSG144", 503); }
        if (Convert.ToHexString(SHA256.HashData(bytes)) != d.Sha256) throw Error("MSG42", 409);
        Audit(d.Id, actorId, inline ? "Viewed" : "Downloaded"); await db.SaveChangesAsync(ct);
        return new(bytes, d.ContentType, d.FileName);
    }
    public async Task<DocumentDto> ReviewAsync(int actorId, Guid documentId, ReviewDocument r, CancellationToken ct)
    {
        if (r.RequestKey == Guid.Empty || r.ExpectedVersion <= 0) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var d = await RowAsync(actorId, documentId, ct); var v = await access.VisitAsync(actorId, r.VisitId, true, ct);
        if (r.PatientId != d.PatientId || v.PatientId != d.PatientId || v.BranchId != (await db.Visits.SingleAsync(x => x.Id == d.VisitId, ct)).BranchId) throw Error("MSG113", 404);
        var hash = ExaminationService.Hash(new { documentId, r });
        var receipt = await db.Set<DocumentReceipt>().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == r.RequestKey, ct);
        if (receipt != null && receipt.Fingerprint != hash) throw Error("MSG111", 409);
        if (d.FileState != "Ready") throw Error("MSG42", 409);
        if (receipt == null) {
            if (d.Version != r.ExpectedVersion) throw Error("MSG111", 409);
            if (v.Status != VisitStatuses.InProgress) throw Error("MSG42", 409);
            if (!await db.Set<DocumentReview>().AnyAsync(x => x.DocumentId == d.Id && x.VisitId == v.Id, ct)) {
                db.Add(new DocumentReview { DocumentId = d.Id, VisitId = v.Id, ReviewedBy = actorId, ReviewedAt = clock.GetUtcNow() });
                d.Version++; Audit(d.Id, actorId, "ClinicallyReviewed");
            }
            db.Add(new DocumentReceipt { ActorId = actorId, RequestKey = r.RequestKey, Fingerprint = hash });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct); return await DtoAsync(d, ct);
    }
    public async Task ScanPendingAsync(CancellationToken ct)
    {
        // Network work is outside the transaction; the guarded update below handles concurrent workers.
        foreach (var d in await db.Set<ClinicalDocument>().AsNoTracking().Where(x => x.FileState == "PendingScan").OrderBy(x => x.UploadedAt).Take(20).ToArrayAsync(ct)) {
            byte[] bytes; try { bytes = await File.ReadAllBytesAsync(PathFor(d.Id), ct); } catch (IOException) { continue; }
            var state = Convert.ToHexString(SHA256.HashData(bytes)) == d.Sha256 ? await scanner.ScanAsync(bytes, ct) : "Rejected";
            if (state is not ("Ready" or "Rejected")) continue;
            await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
            var row = await db.Set<ClinicalDocument>().SingleAsync(x => x.Id == d.Id, ct);
            if (row.FileState == "PendingScan") { row.FileState = state; row.Version++; row.RejectReason = state == "Rejected" ? "Tệp không đạt kiểm tra an toàn." : null;
                Audit(row.Id, null, state == "Ready" ? "ScanPassed" : "ScanRejected"); await db.SaveChangesAsync(ct); }
            await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        }
    }
}
