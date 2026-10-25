using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class ServicePriceService(DcmsDbContext db, UserManager<ApplicationUser> users) : IServicePriceService
{
    private static BusinessException Error(string code, int status) => new(status, SystemMessages.Get(code));

    private async Task AuthorizeAsync(int actorId, int branchId, bool manage, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        if (manage && !roles.Contains(AccountRoles.Admin) && !roles.Contains(AccountRoles.ClinicOwner)
            && !roles.Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        if (roles.Contains(AccountRoles.Admin)) return;
        var isOwner = roles.Contains(AccountRoles.ClinicOwner);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.Set<SchedulingMember>().AsNoTracking().AnyAsync(x =>
                x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active &&
                (!manage || isOwner || x.CanDentist), ct))
            throw Error("MSG06", 403);
    }

    public async Task<IReadOnlyList<ServicePriceDto>> ListAsync(int actorId, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actorId.ToString());
        if (account is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(account));
        if (!roles.Contains(AccountRoles.Admin) && !roles.Contains(AccountRoles.ClinicOwner)
            && !roles.Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var query = db.Set<SchedulingService>().AsNoTracking().Where(x => x.Active);
        if (!roles.Contains(AccountRoles.Admin))
        {
            var isOwner = roles.Contains(AccountRoles.ClinicOwner);
            var employeeId = await db.Employees.AsNoTracking().Where(x => x.AccountId == actorId)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
            if (employeeId == null) throw Error("MSG06", 403);
            var branches = db.Set<SchedulingMember>().AsNoTracking().Where(x =>
                x.EmployeeId == employeeId.Value && x.Active &&
                (isOwner || x.CanDentist)).Select(x => x.BranchId);
            query = query.Where(x => branches.Contains(x.BranchId));
        }
        return await query.OrderBy(x => x.BranchId).ThenBy(x => x.Name)
            .Select(x => new ServicePriceDto(x.Id, x.BranchId, x.Name,
                x.ReferenceUnitPriceVnd, x.PriceVersion)).ToArrayAsync(ct);
    }

    public async Task<ServicePriceDto> GetAsync(int actorId, int serviceId, CancellationToken ct)
    {
        var service = await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId, ct)
            ?? throw Error("MSG113", 404);
        await AuthorizeAsync(actorId, service.BranchId, false, ct);
        return new(service.Id, service.BranchId, service.Name, service.ReferenceUnitPriceVnd, service.PriceVersion);
    }

    public async Task<ServicePriceDto> SaveAsync(int actorId, int serviceId, SaveServicePriceRequest request,
        CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.ReferenceUnitPriceVnd < 0 || request.ExpectedVersion < 1)
            throw Error("MSG04", 400);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)serviceId + 905000L})", ct);
        var service = await db.Set<SchedulingService>().SingleOrDefaultAsync(x => x.Id == serviceId, ct)
            ?? throw Error("MSG113", 404);
        await AuthorizeAsync(actorId, service.BranchId, true, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { serviceId, request }))));
        var receipt = await db.Set<ScheduleReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.ActorId != actorId || receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<ServicePriceDto>(receipt.ResultJson)!;
        }
        if (service.PriceVersion != request.ExpectedVersion) throw Error("MSG111", 409);
        service.ReferenceUnitPriceVnd = request.ReferenceUnitPriceVnd;
        service.PriceVersion++;
        await db.SaveChangesAsync(ct);
        var result = new ServicePriceDto(service.Id, service.BranchId, service.Name,
            service.ReferenceUnitPriceVnd, service.PriceVersion);
        db.Add(new ScheduleReceipt { ActorId = actorId, RequestKey = request.RequestKey,
            Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
