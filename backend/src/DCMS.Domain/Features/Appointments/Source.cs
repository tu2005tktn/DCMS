using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public sealed class Source : BaseEntity<int>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Constructor for creating new sources
    public Source(string name, string description)
    {
        Name = name;
        Description = description;
    }

    // Constructor for loading existing sources (EF Core)
    private Source() { }

    // Methods to update source properties
    public void UpdateDetails(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}