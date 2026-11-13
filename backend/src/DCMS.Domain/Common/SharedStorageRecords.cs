namespace DCMS.Domain.Common;

public abstract class CommandReceiptRecord
{
    public Guid StorageId { get; set; } = Guid.NewGuid();
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}

public abstract class AuditRecord
{
    public Guid StorageId { get; set; } = Guid.NewGuid();
}

public abstract class PrescriptionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
