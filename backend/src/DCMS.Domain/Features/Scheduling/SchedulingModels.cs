namespace DCMS.Domain.Features.Scheduling;

public sealed class SchedulingBranch
{
    public int SlotIntervalMinutes { get; set; } = 15;
    public int BookingSettingsVersion { get; set; } = 1;
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
}
public sealed class SchedulingMember
{
    public int BranchId { get; set; }
    public int EmployeeId { get; set; }
    public bool Active { get; set; } = true;
    public bool CanDentist { get; set; }
    public bool CanManageSchedules { get; set; }
}
public sealed class SchedulingChair
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public int OpenMinute { get; set; } = 480;
    public int CloseMinute { get; set; } = 1080;
}
public sealed class SchedulingService
{
    public bool IsInitialExamination { get; set; }
    public bool PublicBookable { get; set; } = true;
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = "";
    public int PublicDurationMinutes { get; set; } = 30;
    public bool Active { get; set; } = true;
}
public sealed class WorkingWeek
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int BranchId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly WeekStart { get; set; }
    public int Version { get; set; } = 1;
    public int PublishedBy { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public List<WorkingPeriod> Periods { get; set; } = [];
}

// Immutable publications; a later revision on the same effective date supersedes the earlier one.
public sealed class WorkingScheduleRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int EmployeeId { get; set; }
    public int Version { get; set; }
    public DateOnly Date { get; set; }
    public string Kind { get; set; } = "Template";
    public string Reason { get; set; } = "";
    public int PublishedBy { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public List<RevisionPeriod> Periods { get; set; } = [];
}
public sealed class RevisionPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RevisionId { get; set; }
    public int BranchId { get; set; }
    public int Day { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string Kind { get; set; } = "Work";
    public string Reason { get; set; } = "";
}
public sealed class WorkingPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WeekId { get; set; }
    public int Day { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string Kind { get; set; } = "Work";
    public string Reason { get; set; } = "";
}
// Shared persistence seam for TV2; no appointment creation API is exposed by S09.
public sealed class ResourceReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public int BranchId { get; set; }
    public int DentistId { get; set; }
    public int? AssistantId { get; set; }
    public int ChairId { get; set; }
    public int? ServiceId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? AttendeeKey { get; set; }
    public string AttendeeLabel { get; set; } = "";
    public string Status { get; set; } = "Confirmed";
}
public sealed class ScheduleReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "";
}

public readonly record struct SchedulePeriod(int Day, int StartMinute, int EndMinute, string Kind, string Reason = "");
public static class ScheduleRules
{
    public static bool Overlaps(int start, int end, int otherStart, int otherEnd) => start < otherEnd && otherStart < end;
    public static bool ValidDuration(int minutes, bool isPublic) => minutes is >= 1 and <= 480;
    public static bool ValidWeek(DateOnly weekStart, IReadOnlyList<SchedulePeriod> periods)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday || periods.Count > 140) return false;
        foreach (var p in periods)
        {
            if (p.Day is < 0 or > 6 || p.StartMinute < 0 || p.EndMinute > 1440 || p.StartMinute >= p.EndMinute
                || p.StartMinute % 15 != 0 || p.EndMinute % 15 != 0 || p.Kind is not ("Work" or "Break" or "Block")
                || p.Reason == null || p.Reason.Length > 500 || (p.Kind != "Work" && string.IsNullOrWhiteSpace(p.Reason))) return false;
            if (p.Kind != "Work" && !periods.Any(w => w.Day == p.Day && w.Kind == "Work" && w.StartMinute <= p.StartMinute && w.EndMinute >= p.EndMinute)) return false;
        }
        for (var i = 0; i < periods.Count; i++)
            for (var j = i + 1; j < periods.Count; j++)
                if (periods[i].Day == periods[j].Day && (periods[i].Kind == "Work") == (periods[j].Kind == "Work")
                    && Overlaps(periods[i].StartMinute, periods[i].EndMinute, periods[j].StartMinute, periods[j].EndMinute)) return false;
        return true;
    }
    public static bool Covers(IEnumerable<SchedulePeriod> periods, int day, int start, int end) =>
        periods.Any(p => p.Day == day && p.Kind == "Work" && p.StartMinute <= start && p.EndMinute >= end)
        && !periods.Any(p => p.Day == day && p.Kind != "Work" && Overlaps(start, end, p.StartMinute, p.EndMinute));
}
