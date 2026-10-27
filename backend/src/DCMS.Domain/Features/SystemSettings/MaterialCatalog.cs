namespace DCMS.Domain.Features.SystemSettings;

public sealed class MaterialCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class MaterialCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public Guid CategoryId { get; set; }
    public MaterialCategory Category { get; set; } = null!;
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class SystemCatalogChange
{
    public Guid RequestKey { get; set; }
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string ResultJson { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
