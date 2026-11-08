namespace DCMS.Domain.Features.Treatment;

// One lab order (Phiếu labo) per treatment-sheet activity. Rules: docs/specs/LAB-ORDER.md.
public sealed class LabOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public Guid DossierId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public string? Code { get; set; }
    public DateOnly OrderedOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public DateOnly? DeliveredOn { get; set; }
    public DateOnly? SentOn { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public string Status { get; set; } = LabOrderStatus.NotSent;
    public int DentistId { get; set; }
    public Guid? LabProviderId { get; set; }
    public Guid? LabWorkTypeId { get; set; }
    public string WorkName { get; set; } = "";
    public string? MarginOuter { get; set; }
    public string? MarginInner { get; set; }
    public string? MarginSide { get; set; }
    public string[] BridgeShapes { get; set; } = [];
    public bool IsChild { get; set; }
    public string[] Teeth { get; set; } = [];
    public Dictionary<string, string> Shade { get; set; } = [];
    public string? Note { get; set; }
    public string? Material { get; set; }
    public int Quantity { get; set; } = 1;
    public long TotalFeeVnd { get; set; }
    public long PaidVnd { get; set; }
    public string? Warranty { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int UpdatedBy { get; set; }
}

public static class LabOrderStatus
{
    public const string NotSent = "NotSent", Sent = "Sent", Received = "Received", Delivered = "Delivered", Rework = "Rework";
    public static readonly string[] All = [NotSent, Sent, Received, Delivered, Rework];
    private static readonly Dictionary<string, string[]> Next = new()
    {
        [NotSent] = [Sent], [Sent] = [Received, Rework], [Received] = [Delivered, Rework], [Delivered] = [Rework], [Rework] = [Sent],
    };
    public static IReadOnlyList<string> NextOf(string status) => Next.TryGetValue(status, out var n) ? n : [];
    public static bool CanMove(string from, string to) => NextOf(from).Contains(to);
}

// Append-only status history of a lab order. Rules: docs/specs/LAB-ORDER-MANAGEMENT.md.
public sealed class LabOrderStatusLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LabOrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
    public int ChangedBy { get; set; }
    public string? Note { get; set; }
}
