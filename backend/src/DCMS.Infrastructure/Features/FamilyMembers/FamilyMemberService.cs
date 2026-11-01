using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.FamilyMembers;

public sealed class FamilyMemberService(DcmsDbContext db, TimeProvider clock) : IFamilyMemberService
{
    private static readonly string[] Relations = ["Father", "Mother", "Spouse", "Child", "Sibling", "Grandparent", "Other"];
    private static BusinessException Missing() => new(404, new("FAMILY_MEMBER_NOT_FOUND", "Không tìm thấy người thân trong danh sách của bạn."));
    private static BusinessException Invalid(string field) => new(400, new("FAMILY_MEMBER_VALIDATION", $"Thông tin {field} không hợp lệ.") { Field = field });
    private static FamilyMemberDto Dto(FamilyMember member) => new(member.Id, member.FullName, member.Relationship, member.BirthDate, member.Version, member.Gender, member.ContactPhone);
    private static (string? Gender, string? Phone) Contact(string? gender, string? phone)
    {
        var sex = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim().ToUpperInvariant();
        var number = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (sex != null && sex is not ("MALE" or "FEMALE" or "OTHER")) throw Invalid("gender");
        if (number != null && !Regex.IsMatch(number, "^0[35789][0-9]{8}$")) throw Invalid("contactPhone");
        return (sex, number);
    }
    private static void RequireContact(string? gender, string? phone)
    {
        if (gender == null) throw Invalid("gender");
        if (phone == null) throw Invalid("contactPhone");
    }
    private async Task Authorize(int actor, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && x.IsActive && x.AccountType == "Public", ct))
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
    }
    private string Validate(string? name, string? relation, DateOnly? birth, Guid key)
    {
        if (key == Guid.Empty) throw Invalid("requestKey");
        var clean = name?.Trim() ?? "";
        if (clean.Length is < 1 or > 100) throw Invalid("fullName");
        if (!Relations.Contains(relation)) throw Invalid("relationship");
        if (birth > DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime)) throw Invalid("birthDate");
        return clean;
    }
    private IQueryable<FamilyMember> Owned(int actor) => db.Set<FamilyMember>().Where(x => x.OwnerAccountId == actor);
    public async Task<FamilyMemberPage> ListAsync(int actor, string? keyword, int page, int pageSize, CancellationToken ct)
    {
        await Authorize(actor, ct);
        if (page < 1 || page > 1_000_000 || pageSize is < 1 or > 100 || keyword?.Length > 100) throw Invalid("pagination");
        var query = Owned(actor).AsNoTracking().Where(x => x.DeletedAt == null);
        var term = keyword?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term)) query = query.Where(x => x.FullName.ToLower().Contains(term));
        var total = await query.CountAsync(ct);
        var values = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(values.Select(Dto).ToArray(), total, page, pageSize);
    }
    public async Task<FamilyMemberDto> GetAsync(int actor, Guid id, CancellationToken ct)
    {
        await Authorize(actor, ct);
        return Dto(await Owned(actor).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct) ?? throw Missing());
    }
    private async Task<(T Result, bool Replayed)> Write<T>(int actor, Guid key, object payload, Guid? target, Func<Task<T>> apply, CancellationToken ct)
    {
        await Authorize(actor, ct);
        if (key == Guid.Empty) throw Invalid("requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize commands for one owner; the receipt key also has a database unique constraint.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(90121, {actor})", ct);
        await Authorize(actor, ct);
        if (target.HasValue && !await Owned(actor).AnyAsync(x => x.Id == target.Value, ct)) throw Missing();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        var receipt = await db.Set<FamilyMemberReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerAccountId == actor && x.RequestKey == key, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != hash) throw new BusinessException(409, new("FAMILY_MEMBER_REQUEST_CONFLICT", "Mã yêu cầu đã được dùng cho nội dung khác."));
            return (JsonSerializer.Deserialize<T>(receipt.ResultJson)!, true);
        }
        var result = await apply();
        db.Add(new FamilyMemberReceipt { OwnerAccountId = actor, RequestKey = key, Fingerprint = hash,
            ResultJson = JsonSerializer.Serialize(result), CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (result, false);
    }
    public async Task<FamilyMemberCreated> CreateAsync(int actor, CreateFamilyMember command, CancellationToken ct)
    {
        await Authorize(actor, ct);
        var name = Validate(command.FullName, command.Relationship, command.BirthDate, command.RequestKey);
        var (gender, phone) = Contact(command.Gender, command.ContactPhone);
        object payload = gender == null && phone == null ? new { action = "Create", name, command.Relationship, command.BirthDate }
            : new { action = "Create", name, command.Relationship, command.BirthDate, gender, contactPhone = phone };
        var result = await Write(actor, command.RequestKey, payload, null, () =>
        {
            RequireContact(gender, phone);
            var now = clock.GetUtcNow();
            var member = new FamilyMember { OwnerAccountId = actor, FullName = name, Relationship = command.Relationship,
                BirthDate = command.BirthDate, Gender = gender, ContactPhone = phone, CreatedAt = now, UpdatedAt = now };
            db.Add(member); return Task.FromResult(Dto(member));
        }, ct);
        return new(result.Result, result.Replayed);
    }
    public async Task<FamilyMemberDto> UpdateAsync(int actor, Guid id, UpdateFamilyMember command, CancellationToken ct)
    {
        await Authorize(actor, ct);
        var name = Validate(command.FullName, command.Relationship, command.BirthDate, command.RequestKey);
        var (gender, phone) = Contact(command.Gender, command.ContactPhone);
        object payload = gender == null && phone == null ? new { action = "Update", id, name, command.Relationship, command.BirthDate, command.ExpectedVersion }
            : new { action = "Update", id, name, command.Relationship, command.BirthDate, command.ExpectedVersion, gender, contactPhone = phone };
        return (await Write(actor, command.RequestKey, payload, id, async () =>
        {
            RequireContact(gender, phone);
            var member = await Owned(actor).SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct) ?? throw Missing();
            if (member.Version != command.ExpectedVersion) throw new BusinessException(409, SystemMessages.Get("MSG111"));
            member.FullName = name; member.Relationship = command.Relationship; member.BirthDate = command.BirthDate;
            member.Gender = gender; member.ContactPhone = phone;
            member.Version++; member.UpdatedAt = clock.GetUtcNow(); return Dto(member);
        }, ct)).Result;
    }
    public async Task<DeletedFamilyMember> DeleteAsync(int actor, Guid id, DeleteFamilyMember command, CancellationToken ct) =>
        (await Write(actor, command.RequestKey, new { action = "Delete", id, command.ExpectedVersion }, id, async () =>
        {
            var member = await Owned(actor).SingleOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct) ?? throw Missing();
            if (member.Version != command.ExpectedVersion) throw new BusinessException(409, SystemMessages.Get("MSG111"));
            member.DeletedAt = member.UpdatedAt = clock.GetUtcNow(); member.Version++; return new DeletedFamilyMember(id, member.Version, true);
        }, ct)).Result;
}
