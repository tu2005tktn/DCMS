namespace DCMS.Domain.Features.Patients;

// Minimal PATIENT_PROFILE from the logical ERD. Records are created at first check-in (UC-07), never by account linking.
public sealed class PatientProfile
{
    public int Id { get; set; }
    public string PatientCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}
