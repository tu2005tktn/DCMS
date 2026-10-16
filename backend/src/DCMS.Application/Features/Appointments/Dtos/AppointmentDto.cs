using System;

namespace DCMS.Application.Features.Appointments.Dtos;

public class AppointmentDto
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationInMinutes { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    
    // Minimum patient/relative info (no clinical/financial)
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int? RelativeId { get; set; }
    public string? RelativeName { get; set; }
    
    public int DentistId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    public bool IsAttendanceConfirmed { get; set; }
    public DateTime? PlannedEndTime { get; set; }
    public string? ExceptionReason { get; set; }
    
    public int Version { get; set; }
}