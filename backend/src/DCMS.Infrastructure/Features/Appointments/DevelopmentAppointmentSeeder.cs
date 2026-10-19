using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Appointments;

// Development only: publish a demo working week (this week and next) for dentist.demo so S10/S11/S15 have free slots.
// Never overwrites a week that was already published through S09.
public sealed class DevelopmentAppointmentSeeder(DcmsDbContext db, TimeProvider clock)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Name == "S09 - Chi nhánh thử nghiệm", ct);
        var people = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
                            where user.UserName == "dentist.demo" || user.UserName == "owner.demo"
                            select new { employee.Id, employee.AccountId, user.UserName }).ToListAsync(ct);
        var dentist = people.SingleOrDefault(x => x.UserName == "dentist.demo");
        var owner = people.SingleOrDefault(x => x.UserName == "owner.demo");
        if (branch == null || dentist == null || owner == null) return;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        foreach (var weekStart in new[] { monday, monday.AddDays(7) })
        {
            if (await db.Set<WorkingWeek>().AnyAsync(x => x.BranchId == branch.Id && x.EmployeeId == dentist.Id && x.WeekStart == weekStart, ct)) continue;
            var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = dentist.Id, WeekStart = weekStart,
                PublishedBy = owner.AccountId, PublishedAt = clock.GetUtcNow() };
            for (var day = 0; day < 6; day++)
            {
                week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 480, EndMinute = 1020, Kind = "Work" });
                week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 720, EndMinute = 780, Kind = "Break", Reason = "Nghỉ trưa" });
            }
            db.Add(week);
        }
        await db.SaveChangesAsync(ct);

        if (await db.Appointments.AnyAsync(x => x.Code == "AP-S12-DEMO-1", ct)) return;
        var guest = await db.Users.SingleOrDefaultAsync(x => x.UserName == "guest.demo", ct);
        var chair = await db.Set<SchedulingChair>().Where(x => x.BranchId == branch.Id).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var service = await db.Set<SchedulingService>().Where(x => x.BranchId == branch.Id).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (guest == null || chair == null || service == null) return;
        var localDate = today.AddDays(1);
        while (localDate.DayOfWeek == DayOfWeek.Sunday) localDate = localDate.AddDays(1);
        var baseStart = new DateTimeOffset(localDate.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(7)).ToUniversalTime();
        var examples = new[]
        {
            (AppointmentCode: "AP-S12-DEMO-1", RequestCode: "SR-S12-DEMO-1", Kind: "Change", Status: "Pending", Hour: 0),
            (AppointmentCode: "AP-S12-DEMO-2", RequestCode: "SR-S12-DEMO-2", Kind: "ConfirmToAttend", Status: "Pending", Hour: 1),
            (AppointmentCode: "AP-S12-DEMO-3", RequestCode: "SR-S12-DEMO-3", Kind: "Cancel", Status: "Withdrawn", Hour: 2)
        };
        foreach (var example in examples)
        {
            var id = Guid.NewGuid(); var start = baseStart.AddHours(example.Hour);
            db.Set<ResourceReservation>().Add(new ResourceReservation
            {
                Id = id, Code = "RS-" + id.ToString("N"), BranchId = branch.Id, DentistId = dentist.Id,
                ChairId = chair.Id, ServiceId = service.Id, Start = start, End = start.AddMinutes(30),
                AttendeeLabel = "Bệnh nhân demo S12", Status = "Confirmed"
            });
            db.Appointments.Add(new Appointment
            {
                Id = id, ReservationId = id, Code = example.AppointmentCode, BranchId = branch.Id,
                BookerAccountId = guest.Id, BookerName = guest.FullName, BookerPhone = guest.PhoneNumber,
                AttendeeName = "Bệnh nhân demo S12", DentistId = dentist.Id, ChairId = chair.Id,
                ServiceId = service.Id, StartAt = start, EndAt = start.AddMinutes(30), Status = "Confirmed",
                CreatedAt = clock.GetUtcNow()
            });
            db.AppointmentSupportRequests.Add(new AppointmentSupportRequest
            {
                Code = example.RequestCode, AppointmentId = id, Kind = example.Kind, Status = example.Status,
                RequestKey = Guid.NewGuid(), RequesterAccountId = guest.Id, RequesterName = guest.FullName,
                RequesterPhone = guest.PhoneNumber, Reason = example.Kind == "Change"
                    ? "Tôi muốn đổi sang khung giờ phù hợp hơn." : "Yêu cầu demo S12.",
                ProposedStartAt = example.Kind == "Change" ? start.AddDays(1) : null,
                ProposedReason = example.Kind == "Change" ? "PatientRequest" : null,
                CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow()
            });
            db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
            {
                AggregateId = id, EventType = "appointment.confirmed", PayloadJson = "{}", OccurredAt = clock.GetUtcNow()
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
