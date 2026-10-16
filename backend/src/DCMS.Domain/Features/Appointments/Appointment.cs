using System;
using DCMS.Domain.Common;

namespace DCMS.Domain.Features.Appointments;

public class Appointment : BaseEntity<int>
{
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public int DurationInMinutes { get; private set; }
    public string Service { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty; // phone/Zalo/Facebook/in person/app
    
    // Relationships
    public int PatientId { get; private set; }
    public int? RelativeId { get; private set; } // For relatives scheduling appointments
    public int DentistId { get; private set; }
    public int BranchId { get; private set; }
    public int ChairId { get; private set; }
    
    // Status and metadata
    public string Status { get; private set; } = string.Empty; // Scheduled, Completed, Cancelled, etc.
    public bool IsAttendanceConfirmed { get; private set; } // Independent lifecycle flag
    public DateTime? PlannedEndTime { get; private set; }
    public string? ExceptionReason { get; private set; } // When appointment is within 60 minutes
    
    // Concurrency
    public int Version { get; private set; }

    // Constructor for creating new appointments
    public Appointment(
        DateTime startTime,
        int durationInMinutes,
        string service,
        string reason,
        string source,
        int patientId,
        int? relativeId,
        int dentistId,
        int branchId,
        int chairId)
    {
        StartTime = startTime;
        DurationInMinutes = durationInMinutes;
        EndTime = startTime.AddMinutes(durationInMinutes);
        Service = service;
        Reason = reason;
        Source = source;
        PatientId = patientId;
        RelativeId = relativeId;
        DentistId = dentistId;
        BranchId = branchId;
        ChairId = chairId;
        Status = "Scheduled"; // Default status
        IsAttendanceConfirmed = false;
        Version = 1; // Initial version
    }

    // Constructor for loading existing appointments (EF Core)
    private Appointment() { }

    // Methods to update appointment properties
    public void UpdateSchedule(DateTime startTime, int durationInMinutes)
    {
        StartTime = startTime;
        DurationInMinutes = durationInMinutes;
        EndTime = startTime.AddMinutes(durationInMinutes);
        Version++;
    }

    public void UpdateDetails(string service, string reason)
    {
        Service = service;
        Reason = reason;
        Version++;
    }

    public void UpdateSource(string source)
    {
        Source = source;
        Version++;
    }

    public void MarkAttendanceConfirmed(bool confirmed)
    {
        IsAttendanceConfirmed = confirmed;
        Version++;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
        Version++;
    }

    public void SetPlannedEndTime(DateTime? plannedEndTime)
    {
        PlannedEndTime = plannedEndTime;
        Version++;
    }

    public void SetExceptionReason(string? exceptionReason)
    {
        ExceptionReason = exceptionReason;
        Version++;
    }
}