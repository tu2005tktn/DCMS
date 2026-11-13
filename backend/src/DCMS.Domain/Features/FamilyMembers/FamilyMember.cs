namespace DCMS.Domain.Features.FamilyMembers;

public sealed class FamilyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OwnerAccountId { get; set; }
    public string FullName { get; set; } = "";
    public string Relationship { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? ContactPhone { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class FamilyMemberReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int OwnerAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
