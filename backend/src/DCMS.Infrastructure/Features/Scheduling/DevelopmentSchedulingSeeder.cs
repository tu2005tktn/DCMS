using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed class DevelopmentSchedulingSeeder(DcmsDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);
        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Name == "S09 - Chi nhánh thử nghiệm", ct);
        if (branch == null)
        {
            branch = new() { Name = "S09 - Chi nhánh thử nghiệm" }; db.Add(branch); await db.SaveChangesAsync(ct);
            db.AddRange(new SchedulingChair { BranchId = branch.Id, Name = "Ghế thử nghiệm 1" }, new SchedulingChair { BranchId = branch.Id, Name = "Ghế thử nghiệm 2" },
                new SchedulingChair { BranchId = branch.Id, Name = "Ghế thử nghiệm 3" });
            db.AddRange(new SchedulingService { BranchId = branch.Id, Name = "Khám", PublicDurationMinutes = 30, IsInitialExamination = true },
                new SchedulingService { BranchId = branch.Id, Name = "Điều trị thử nghiệm", PublicDurationMinutes = 60, PublicBookable = false });
        }
        var employees = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" || user.UserName == "owner.demo" || user.UserName == "assistant.demo" || user.UserName == "dual.demo"
            select new { Employee = employee, user.UserName }).ToArrayAsync(ct);
        // Upgrade only the original demo clinic fixture. Never create three chairs per branch.
        if (!db.Set<SchedulingChair>().Local.Any(x => x.BranchId == branch.Id && x.Name == "Ghế thử nghiệm 3")
            && !await db.Set<SchedulingChair>().AnyAsync(x => x.BranchId == branch.Id && x.Name == "Ghế thử nghiệm 3", ct))
            db.Add(new SchedulingChair { BranchId = branch.Id, Name = "Ghế thử nghiệm 3" });
        foreach (var item in employees)
        {
            if (!await db.Set<SchedulingMember>().AnyAsync(x => x.BranchId == branch.Id && x.EmployeeId == item.Employee.Id, ct))
                db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = item.Employee.Id, CanDentist = item.UserName != "assistant.demo", CanManageSchedules = item.UserName == "owner.demo" });
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}
