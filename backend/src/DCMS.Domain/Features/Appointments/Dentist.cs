using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public sealed class Dentist : BaseEntity<int>
{
    public string FullName { get; private set; } = string.Empty;
    public string Specialty { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Constructor for creating new dentists
    public Dentist(string fullName, string specialty, string phone)
    {
        FullName = fullName;
        Specialty = specialty;
        Phone = phone;
    }

    // Constructor for loading existing dentists (EF Core)
    private Dentist() { }

    // Methods to update dentist properties
    public void UpdateDetails(string fullName, string specialty, string phone)
    {
        FullName = fullName;
        Specialty = specialty;
        Phone = phone;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}