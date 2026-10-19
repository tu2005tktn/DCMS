using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed class BookingSettingsService(DcmsDbContext db, UserManager<ApplicationUser> users) : IBookingSettingsService
{
    private static BusinessException Error(string code, int status) => new(status, SystemMessages.Get(code));
    public async Task<BranchOption[]> OptionsAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff") throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        var dentist = roles.Contains(AccountRoles.Dentist);
        return await (from member in db.Set<SchedulingMember>().AsNoTracking()
            join employee in db.Employees on member.EmployeeId equals employee.Id
            join branch in db.Set<SchedulingBranch>() on member.BranchId equals branch.Id
            where employee.AccountId == actorId && member.Active && branch.Active && (dentist || member.CanManageSchedules)
            orderby branch.Name select new BranchOption(branch.Id, branch.Name)).ToArrayAsync(ct);
    }
    private async Task Authorize(int actorId, int branchId, CancellationToken ct)
    {
        if (!(await OptionsAsync(actorId, ct)).Any(x => x.Id == branchId)) throw Error("MSG06", 403);
    }
    private async Task<BookingSettings> Read(int branchId, CancellationToken ct)
    {
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleAsync(x => x.Id == branchId, ct);
        return new(branchId, branch.SlotIntervalMinutes, branch.BookingSettingsVersion,
            await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == branchId).OrderBy(x => x.Id)
                .Select(x => new BookingServiceSetting(x.Id, x.Name, x.PublicDurationMinutes, x.Active, x.PublicBookable, x.IsInitialExamination)).ToArrayAsync(ct));
    }
    public async Task<BookingSettings> GetAsync(int actorId, int branchId, CancellationToken ct)
    {
        await Authorize(actorId, branchId, ct); return await Read(branchId, ct);
    }
    public async Task<BookingSettings> SaveAsync(int actorId, int branchId, SaveBookingSettings command, CancellationToken ct)
    {
        if (command.RequestKey == Guid.Empty || command.ExpectedVersion < 1 || command.SlotIntervalMinutes is < 1 or > 120
            || command.Services == null || command.Services.Length > 500
            || command.Services.Any(x => x == null || x.Id < 0 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 160
                || !ScheduleRules.ValidDuration(x.DurationMinutes, true))
            || command.Services.Where(x => x.Id > 0).Select(x => x.Id).Distinct().Count() != command.Services.Count(x => x.Id > 0))
            throw Error("MSG04", 400);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { action = "booking-settings", branchId, command }))));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);
        await Authorize(actorId, branchId, ct);
        var receipt = await db.Set<ScheduleReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == command.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<BookingSettings>(receipt.ResultJson)!;
        }
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == branchId, ct);
        if (branch.BookingSettingsVersion != command.ExpectedVersion) throw Error("MSG111", 409);
        var existing = await db.Set<SchedulingService>().Where(x => x.BranchId == branchId).ToListAsync(ct);
        if (!existing.Select(x => x.Id).Order().SequenceEqual(command.Services.Where(x => x.Id > 0).Select(x => x.Id).Order())) throw Error("MSG04", 400);
        foreach (var item in command.Services)
        {
            var service = item.Id == 0 ? new SchedulingService { BranchId = branchId } : existing.Single(x => x.Id == item.Id);
            service.Name = service.IsInitialExamination ? "Khám" : item.Name.Trim();
            service.PublicDurationMinutes = item.DurationMinutes; service.Active = item.Active;
            service.PublicBookable = service.IsInitialExamination;
            if (item.Id == 0) db.Add(service);
        }
        if (!existing.Any(x => x.IsInitialExamination)) db.Add(new SchedulingService {
            BranchId = branchId, Name = "Khám", IsInitialExamination = true, PublicBookable = true, PublicDurationMinutes = 30 });
        branch.SlotIntervalMinutes = command.SlotIntervalMinutes; branch.BookingSettingsVersion++;
        await db.SaveChangesAsync(ct);
        var result = await Read(branchId, ct);
        db.Add(new ScheduleReceipt { ActorId = actorId, RequestKey = command.RequestKey, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }
}
