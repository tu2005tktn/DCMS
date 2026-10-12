using System.Data;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed partial class AccountService(DcmsDbContext db, UserManager<ApplicationUser> users) : IAccountService
{
    public async Task<AccountPage> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize || search?.Length > 160) Invalid("Phân trang hoặc tìm kiếm");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.NormalizedUserName!.Contains(term) || x.FullName.ToUpper().Contains(term)
                || (x.NormalizedEmail != null && x.NormalizedEmail.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var roles = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            where ids.Contains(ur.UserId) select new { ur.UserId, role.Name }).ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking().Where(x => ids.Contains(x.AccountId)).ToListAsync(ct);
        return new(items.Select(x => Map(x, roles.Where(r => r.UserId == x.Id).Select(r => r.Name!),
            employees.SingleOrDefault(e => e.AccountId == x.Id)?.EmployeeCode)).ToArray(), total, page, pageSize);
    }

    public async Task<AccountChange> CreateAsync(int actorId, CreateStaffRequest request, CancellationToken ct)
    {
        ValidateDetails(request.UserName, request.FullName, request.Email, request.Phone);
        ValidateRoles(request.Roles);
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || !EmployeeCodePattern().IsMatch(request.EmployeeCode))
            Invalid("Mã nhân viên");
        if (string.IsNullOrWhiteSpace(request.Password)) Invalid("Mật khẩu");
        if (request.Password.Length > 256) Invalid("Mật khẩu");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAdminAsync(actorId, ct);
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(), FullName = request.FullName.Trim(), Email = Clean(request.Email),
            PhoneNumber = Clean(request.Phone), AccountType = "Staff", CreatedAt = now, UpdatedAt = now
        };
        Check(await users.CreateAsync(user, request.Password));
        Check(await users.AddToRolesAsync(user, request.Roles.Distinct()));
        var employee = new EmployeeProfile
        {
            AccountId = user.Id, EmployeeCode = request.EmployeeCode.Trim().ToUpperInvariant(), FullName = user.FullName
        };
        db.Employees.Add(employee);
        var account = Map(user, request.Roles, employee.EmployeeCode);
        AddAudit(actorId, user.Id, "CreateStaff", "Tạo tài khoản nhân viên", null, account);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(account, SystemMessages.Get("MSG14"));
    }

    public async Task<AccountChange> UpdateAsync(int actorId, int id, UpdateStaffRequest request, CancellationToken ct)
    {
        ValidateDetails(request.UserName, request.FullName, request.Email, request.Phone);
        ValidateRoles(request.Roles);
        ValidateReason(request.Reason);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAdminAsync(actorId, ct);
        var user = await GetAsync(id, ct);
        if (user.AccountType != "Staff") Forbidden();
        CheckVersion(user, request.Version);
        var employee = await db.Employees.SingleAsync(x => x.AccountId == id, ct);
        var oldRoles = await users.GetRolesAsync(user);
        var before = Map(user, oldRoles, employee.EmployeeCode);
        user.UserName = request.UserName.Trim();
        user.FullName = request.FullName.Trim();
        user.Email = Clean(request.Email);
        user.PhoneNumber = Clean(request.Phone);
        // A contact edit must never imply new contact verification.
        if (before.Email != user.Email) user.EmailConfirmed = false;
        if (before.Phone != user.PhoneNumber) user.PhoneNumberConfirmed = false;
        user.Version++;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        Check(await users.UpdateAsync(user));
        Check(await users.RemoveFromRolesAsync(user, oldRoles.Except(request.Roles)));
        Check(await users.AddToRolesAsync(user, request.Roles.Except(oldRoles)));
        employee.FullName = user.FullName;
        var account = Map(user, request.Roles, employee.EmployeeCode);
        AddAudit(actorId, id, "UpdateStaff", request.Reason.Trim(), before, account);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(account, SystemMessages.Get("MSG14"));
    }

    public async Task<AccountChange> SetStateAsync(int actorId, int id, AccountStateRequest request, CancellationToken ct)
    {
        ValidateReason(request.Reason);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await RequireAdminAsync(actorId, ct);
        var user = await GetAsync(id, ct);
        CheckVersion(user, request.Version);
        var roles = await users.GetRolesAsync(user);
        var employeeCode = await db.Employees.Where(x => x.AccountId == id).Select(x => x.EmployeeCode).SingleOrDefaultAsync(ct);
        var before = Map(user, roles, employeeCode);
        user.IsActive = !request.Locked;
        user.Version++;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        Check(await users.UpdateAsync(user));
        if (request.Locked)
        {
            var now = DateTimeOffset.UtcNow;
            await db.AuthSessions.Where(x => x.AccountId == id && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        }
        var account = Map(user, roles, employeeCode);
        AddAudit(actorId, id, request.Locked ? "Lock" : "Unlock", request.Reason.Trim(), before, account);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(account, SystemMessages.Get(request.Locked ? "MSG15" : "MSG16"));
    }

    public async Task<IReadOnlyList<AccountAuditDto>> AuditAsync(int id, CancellationToken ct) =>
        await db.AccountAudits.AsNoTracking().Where(x => x.TargetId == id).OrderByDescending(x => x.Id).Take(100)
            .Select(x => new AccountAuditDto(x.Id, x.ActorId, x.TargetId, x.OccurredAt, x.Action,
                x.Reason, x.BeforeJson, x.AfterJson)).ToListAsync(ct);

    private async Task RequireAdminAsync(int actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive, ct);
        if (actor == null || !await users.IsInRoleAsync(actor, AccountRoles.Admin)) Forbidden();
    }

    private async Task<ApplicationUser> GetAsync(int id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new BusinessException(404, SystemMessages.Get("MSG01"));

    private static void CheckVersion(ApplicationUser user, int version)
    {
        if (user.Version != version) throw new BusinessException(409, SystemMessages.Get("MSG111"));
    }

    private static void ValidateDetails(string? name, string? fullName, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Tên đăng nhập"));
        if (!UserNamePattern().IsMatch(name)) Invalid("Tên đăng nhập");
        if (string.IsNullOrWhiteSpace(fullName)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Họ tên"));
        if (fullName.Length > 160) throw new BusinessException(400, SystemMessages.Get("MSG03", "Họ tên", 160));
        if (!string.IsNullOrWhiteSpace(email) && (email.Length > 254 || !MailAddress.TryCreate(email, out var mail) || mail.Address != email)) Invalid("Email");
        if (!string.IsNullOrWhiteSpace(phone) && !PhonePattern().IsMatch(phone)) Invalid("Số điện thoại");
    }

    private static void ValidateRoles(string[]? roles)
    {
        if (roles == null || roles.Length == 0 || roles.Length > AccountRoles.Staff.Length
            || roles.Distinct().Count() != roles.Length || roles.Any(x => !AccountRoles.Staff.Contains(x))) Forbidden();
    }

    private static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Lý do"));
        if (reason.Length > 500) throw new BusinessException(400, SystemMessages.Get("MSG03", "Lý do", 500));
    }

    private static void Check(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code == "ConcurrencyFailure")) throw new BusinessException(409, SystemMessages.Get("MSG111"));
        if (result.Errors.Any(x => x.Code.StartsWith("Duplicate", StringComparison.Ordinal))) throw new BusinessException(409, SystemMessages.Get("MSG139"));
        if (result.Errors.Any(x => x.Code.StartsWith("Password", StringComparison.Ordinal))) throw new BusinessException(400, SystemMessages.Get("MSG109"));
        Invalid("Thông tin tài khoản");
    }

    private void AddAudit(int actor, int target, string action, string reason, AccountDto? before, AccountDto after) =>
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = actor, TargetId = target, Action = action, Reason = reason, OccurredAt = DateTimeOffset.UtcNow,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after)
        });

    private static AccountDto Map(ApplicationUser user, IEnumerable<string> roles, string? code) =>
        new(user.Id, user.UserName!, user.FullName, user.Email, user.PhoneNumber, user.AccountType, user.IsActive,
            user.Version, roles.Order(StringComparer.Ordinal).ToArray(), code);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Invalid(string field) => throw new BusinessException(400, SystemMessages.Get("MSG04", field));
    private static void Forbidden() => throw new BusinessException(403, SystemMessages.Get("MSG06"));
    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,64}$")] private static partial Regex UserNamePattern();
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,40}$")] private static partial Regex EmployeeCodePattern();
    [GeneratedRegex(@"^\+?[0-9 ()-]{7,24}$")] private static partial Regex PhonePattern();
}
