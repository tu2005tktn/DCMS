using System.Net.Mail;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed partial class RegistrationService(DcmsDbContext db, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<int>> roles) : IRegistrationService
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var fullName = request.FullName?.Trim();
        var phone = request.PhoneNumber?.Trim();
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(fullName)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Họ tên"));
        if (fullName.Length > 160) throw new BusinessException(400, SystemMessages.Get("MSG03", "Họ tên", 160));
        if (string.IsNullOrWhiteSpace(phone)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Số điện thoại"));
        if (!VietnameseMobilePattern().IsMatch(phone)) throw new BusinessException(400, SystemMessages.Get("MSG04", "Số điện thoại"));
        if (string.IsNullOrWhiteSpace(email)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Email"));
        if (email.Length > 254) throw new BusinessException(400, SystemMessages.Get("MSG03", "Email", 254));
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new BusinessException(400, SystemMessages.Get("MSG04", "Email"));
        if (string.IsNullOrEmpty(request.Password)) throw new BusinessException(400, SystemMessages.Get("MSG02", "Mật khẩu"));
        if (request.Password.Length > 256) throw new BusinessException(400, SystemMessages.Get("MSG109"));
        if (string.IsNullOrEmpty(request.ConfirmPassword))
            throw new BusinessException(400, SystemMessages.Get("MSG02", "Xác nhận mật khẩu"));
        if (request.ConfirmPassword != request.Password)
            throw new BusinessException(400, SystemMessages.Get("MSG04", "Xác nhận mật khẩu"));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await roles.RoleExistsAsync(AccountRoles.Guest))
            throw new BusinessException(503, SystemMessages.Get("MSG144"));
        if (await users.FindByNameAsync(phone) != null)
            throw new BusinessException(409, SystemMessages.Get("MSG139"));

        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = phone, PhoneNumber = phone, PhoneNumberConfirmed = false,
            Email = email, EmailConfirmed = false,
            FullName = fullName, AccountType = "Public", IsActive = true,
            CreatedAt = now, UpdatedAt = now
        };
        Ensure(await users.CreateAsync(user, request.Password));
        Ensure(await users.AddToRoleAsync(user, AccountRoles.Guest));
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = user.Id, TargetId = user.Id, Action = "SelfRegister", Reason = "Đăng ký tài khoản công khai",
            OccurredAt = now, AfterJson = "{\"accountType\":\"Public\",\"role\":\"Guest\",\"phoneNumberConfirmed\":false}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new RegisterResponse(user.Id, phone, SystemMessages.Get("MSG11"));
    }

    private static void Ensure(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code.StartsWith("Duplicate", StringComparison.Ordinal)))
            throw new BusinessException(409, SystemMessages.Get("MSG139"));
        if (result.Errors.Any(x => x.Code.StartsWith("Password", StringComparison.Ordinal)))
            throw new BusinessException(400, SystemMessages.Get("MSG109"));
        throw new BusinessException(400, SystemMessages.Get("MSG04", "Thông tin tài khoản"));
    }

    [GeneratedRegex(@"^0[35789][0-9]{8}$")]
    private static partial Regex VietnameseMobilePattern();
}
