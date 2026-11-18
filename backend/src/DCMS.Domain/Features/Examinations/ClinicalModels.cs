namespace DCMS.Domain.Features.Examinations;

public sealed class VisitExamination
{
    public Guid VisitId { get; set; }
    public string ContentJson { get; set; } = "{}";
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExaminationReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public Guid VisitId { get; set; }
}

public sealed class ClinicalDocumentMetadata
{
    public string Source { get; set; } = "Clinic";
    public string? Description { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string? RejectReason { get; set; }
    public Guid? InvestigationId { get; set; }
}

public sealed class ClinicalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? DossierId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string DocumentType { get; set; } = "Other";
    public DateOnly SourceDate { get; set; }
    public string FileState { get; set; } = "PendingScan";
    public int UploaderId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int Version { get; set; } = 1;
    public ClinicalDocumentMetadata Metadata { get; set; } = new();
    public string Source { get => Metadata.Source; set => Metadata.Source = value; }
    public string? Description { get => Metadata.Description; set => Metadata.Description = value; }
    public long SizeBytes { get => Metadata.SizeBytes; set => Metadata.SizeBytes = value; }
    public string Sha256 { get => Metadata.Sha256; set => Metadata.Sha256 = value; }
    public string? RejectReason { get => Metadata.RejectReason; set => Metadata.RejectReason = value; }
    public Guid? InvestigationId { get => Metadata.InvestigationId; set => Metadata.InvestigationId = value; }
    public List<DocumentReview> Reviews { get; set; } = [];
    // Immutable AI decision-support results (logical AI_ANALYSIS); newest appended last. docs/specs/AI-CARIES-DETECTION.vi.md
    public List<DocumentAiAnalysis> AiAnalyses { get; set; } = [];
}

public sealed class DocumentAiAnalysis
{
    public Guid AnalysisId { get; set; }
    public string Kind { get; set; } = "Caries";
    public string SchemaVersion { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string ModelSha256 { get; set; } = "";
    public string InputSha256 { get; set; } = "";
    public int RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int ElapsedMs { get; set; }
    public string Device { get; set; } = "";
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<DocumentAiFinding> Findings { get; set; } = [];
}

public sealed class DocumentAiFinding
{
    public string FindingId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public double Confidence { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Jaw { get; set; } = "";
    public string Side { get; set; } = "";
}

public sealed class DocumentReview
{
    public Guid DocumentId { get; set; }
    public Guid VisitId { get; set; }
    public int ReviewedBy { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}

public sealed class DocumentAudit : DCMS.Domain.Common.AuditRecord
{
    public long Id { get; set; }
    public Guid DocumentId { get; set; }
    public int? ActorId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
}

public sealed class DocumentReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
}

public sealed class ClinicalSourceArchive : DCMS.Domain.Common.AuditRecord
{
    public string BeforeJson { get; set; } = "{}";
}
