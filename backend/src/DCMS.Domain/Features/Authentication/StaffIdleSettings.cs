namespace DCMS.Domain.Features.Authentication;

public sealed class StaffActivityCommandReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid RequestKey { get; set; }
    public int ExpectedVersion { get; set; }
    public string ResultJson { get; set; } = "";
}
