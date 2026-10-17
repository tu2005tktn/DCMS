using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public sealed class Chair : BaseEntity<int>
{
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public int BranchId { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Constructor for creating new chairs
    public Chair(string name, string location, int branchId)
    {
        Name = name;
        Location = location;
        BranchId = branchId;
    }

    // Constructor for loading existing chairs (EF Core)
    private Chair() { }

    // Methods to update chair properties
    public void UpdateDetails(string name, string location)
    {
        Name = name;
        Location = location;
    }

    public void UpdateBranch(int branchId)
    {
        BranchId = branchId;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}