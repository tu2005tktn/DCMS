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

    public void UpdateRelativeId(int? relativeId)
    {
        RelativeId = relativeId;
        Version++;
    }
}
namespace DCMS.Domain.Features.Appointments;

public static class AppointmentRules
{
    public static bool PublicLeadTimeValid(DateTimeOffset now, DateTimeOffset start) => start >= now.AddMinutes(60);
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public string Code { get; set; } = "";
    public int BranchId { get; set; }
    public int? BookerAccountId { get; set; }
    public string BookerName { get; set; } = "";
    public string? BookerPhone { get; set; }
    public string? BookerEmail { get; set; }
    public int? PatientId { get; set; }
    public string AttendeeName { get; set; } = "";
    public DateOnly? AttendeeBirthDate { get; set; }
    public string AttendeeRelation { get; set; } = "Self";
    public string Kind { get; set; } = "Standard";
    public string Source { get; set; } = "App";
    public int? ServiceId { get; set; }
    public string? Reason { get; set; }
    public int DentistId { get; set; }
    public int? AssistantId { get; set; }
    public int ChairId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public Guid? FollowUpId { get; set; }
    public string? ExceptionReason { get; set; }
    public string Status { get; set; } = "Confirmed";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppointmentReceipt
{
    public Guid RequestKey { get; set; }
    public string ActorScope { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid AppointmentId { get; set; }
    public string ResultJson { get; set; } = "";
}

// Written with the booking transaction; the dispatcher is owned by TV5.
public sealed class AppointmentOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "AppointmentConfirmed";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}
