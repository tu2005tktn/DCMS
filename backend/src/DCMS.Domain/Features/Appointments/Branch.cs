using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public sealed class Branch : BaseEntity<int>
{
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Constructor for creating new branches
    public Branch(string name, string address, string phone)
    {
        Name = name;
        Address = address;
        Phone = phone;
    }

    // Constructor for loading existing branches (EF Core)
    private Branch() { }

    // Methods to update branch properties
    public void UpdateDetails(string name, string address, string phone)
    {
        Name = name;
        Address = address;
        Phone = phone;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}