using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public sealed class Service : BaseEntity<int>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int DurationInMinutes { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Constructor for creating new services
    public Service(string name, string description, int durationInMinutes, decimal price)
    {
        Name = name;
        Description = description;
        DurationInMinutes = durationInMinutes;
        Price = price;
    }

    // Constructor for loading existing services (EF Core)
    private Service() { }

    // Methods to update service properties
    public void UpdateDetails(string name, string description, int durationInMinutes, decimal price)
    {
        Name = name;
        Description = description;
        DurationInMinutes = durationInMinutes;
        Price = price;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}