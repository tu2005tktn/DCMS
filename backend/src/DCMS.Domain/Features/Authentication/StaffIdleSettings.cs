namespace DCMS.Domain.Features.Authentication;

public sealed class StaffActivityCommandReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int ExpectedVersion { get; set; }
}
