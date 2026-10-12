namespace DCMS.Domain.Features.Accounts;

public sealed class AccountAudit
{
    public long Id { get; set; }
    public int ActorId { get; set; }
    public int TargetId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
}
