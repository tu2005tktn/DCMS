using DCMS.Domain.Features.Scheduling;
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
    }
}
