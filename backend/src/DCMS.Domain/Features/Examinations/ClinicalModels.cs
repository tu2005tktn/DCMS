namespace DCMS.Domain.Features.Examinations;

public sealed class VisitExamination
{
    public Guid VisitId { get; set; }
    public string ContentJson { get; set; } = "{}";
    public bool Confirmed { get; set; }
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExaminationReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public Guid VisitId { get; set; }
    public string Fingerprint { get; set; } = "";
}

public sealed class ClinicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public Guid VisitId { get; set; }
    public Guid? DossierId { get; set; }
    public Guid? PlanId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string DocumentType { get; set; } = "Other";
    public string Source { get; set; } = "Clinic";
    public DateOnly SourceDate { get; set; }
    public string? Description { get; set; }
    public string FileState { get; set; } = "PendingScan";
    public string? RejectReason { get; set; }
    public int UploaderId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class DocumentReview
{
    public Guid DocumentId { get; set; }
    public Guid VisitId { get; set; }
    public int ReviewedBy { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}

public sealed class DocumentAudit
{
    public long Id { get; set; }
    public Guid DocumentId { get; set; }
    public int? ActorId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
}

public sealed class DocumentReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "[]";
}
