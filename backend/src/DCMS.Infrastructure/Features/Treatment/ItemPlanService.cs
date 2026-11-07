using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed class ItemPlanService(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : IItemPlanService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Missing() => new(404, SystemMessages.Get("MSG113"));
    private static BusinessException Invalid(string field) => new(400, new("ITEM_PLAN_VALIDATION", "Thông tin kế hoạch không hợp lệ.") { Field = field });
    private static string Name(string? value) => value?.Trim() is { Length: >= 1 and <= 160 } clean ? clean : throw Invalid("name");
    private async Task Authorize(int actor, int branch, CancellationToken ct)
    {
        var account = await users.FindByIdAsync(actor.ToString());
        if (account is not { IsActive: true, AccountType: "Staff" }
            || !AccountRoles.Effective(await users.GetRolesAsync(account)).Contains(AccountRoles.Dentist)
            || !await (from m in db.Set<SchedulingMember>() join e in db.Employees on m.EmployeeId equals e.Id
                join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
                where e.AccountId == actor && b.Id == branch && b.Active && m.Active && m.CanDentist select m).AnyAsync(ct))
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
    }
    private async Task<TreatmentTemplate> Find(Guid id, CancellationToken ct, bool includeDeleted = false) =>
        await db.TreatmentTemplates.SingleOrDefaultAsync(x => x.Id == id && x.DefinitionKind == "ItemPlan" && (includeDeleted || x.DeletedAt == null), ct) ?? throw Missing();
    private async Task<ItemPlanDto> Dto(TreatmentTemplate plan, CancellationToken ct)
    {
        var rows = await db.Set<TreatmentTemplateItem>().AsNoTracking().Where(x => x.TemplateId == plan.Id && x.DeletedAt == null)
            .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(ct);
        return new(plan.Id, plan.BranchId, plan.Name, plan.Version, rows.Count, rows.Sum(x => x.CostVnd), rows.Select(x =>
            new ItemPlanRowDto(x.Id, x.Position, x.Date, JsonSerializer.Deserialize<string[]>(x.TeethJson) ?? [], x.ServiceId,
                x.ServiceName, x.EstimatedMinutes, x.Note, x.DentistId, x.DentistName, x.AssistantId, x.AssistantName, x.CostVnd)).ToArray());
    }
    public async Task<ItemPlanDto> GetAsync(int actor, Guid id, CancellationToken ct)
    {
        var plan = await Find(id, ct); await Authorize(actor, plan.BranchId, ct); return await Dto(plan, ct);
    }
    public async Task<ItemPlanPage> ListAsync(int actor, int branchId, string? keyword, int page, int pageSize, CancellationToken ct)
    {
        await Authorize(actor, branchId, ct);
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100 || keyword?.Length > 160) throw Invalid("pagination");
        var query = db.TreatmentTemplates.AsNoTracking().Where(x => x.BranchId == branchId && x.DefinitionKind == "ItemPlan" && x.DeletedAt == null);
        var term = keyword?.Trim().ToLowerInvariant(); if (!string.IsNullOrEmpty(term)) query = query.Where(x => x.Name.ToLower().Contains(term));
        var count = await query.CountAsync(ct);
        var rows = db.Set<TreatmentTemplateItem>().Where(x => x.DeletedAt == null);
        var list = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ItemPlanListDto(x.Id, x.BranchId, x.Name, x.Version, rows.Count(r => r.TemplateId == x.Id),
                rows.Where(r => r.TemplateId == x.Id).Sum(r => (long?)r.CostVnd) ?? 0)).ToListAsync(ct);
        return new(list, count, page, pageSize);
    }
    public async Task<ItemPlanOptions> OptionsAsync(int actor, int branchId, CancellationToken ct)
    {
        await Authorize(actor, branchId, ct);
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => x.BranchId == branchId && x.Active)
            .OrderBy(x => x.Name).Select(x => new ItemPlanServiceOption(x.Id, x.Name, x.ReferenceUnitPriceVnd)).ToListAsync(ct);
        var members = await (from m in db.Set<SchedulingMember>() join e in db.Employees on m.EmployeeId equals e.Id
            join a in db.Users on e.AccountId equals a.Id
            where m.BranchId == branchId && m.Active && a.IsActive && a.AccountType == "Staff"
            orderby e.FullName select new ItemPlanMemberOption(e.Id, e.FullName, m.CanDentist, !m.CanDentist)).ToListAsync(ct);
        return new(services, members);
    }
    private async Task<T> Write<T>(int actor, Guid? id, int branch, Guid key, string action, object payload,
        int? version, Func<TreatmentTemplate?, Task<T>> apply, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Invalid("requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var plan = id.HasValue ? await Find(id.Value, ct, true) : null;
        await Authorize(actor, plan?.BranchId ?? branch, ct);
        var command = "ItemPlan." + action;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json))));
        var receipt = await db.TreatmentProgressionReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (receipt != null)
        {
            if (receipt.ActorId != actor || receipt.Command != command || receipt.Fingerprint != hash)
                throw new BusinessException(409, new("ITEM_PLAN_REQUEST_CONFLICT", "Mã yêu cầu đã được dùng cho nội dung khác."));
            return JsonSerializer.Deserialize<T>(receipt.ResultJson, Json)!;
        }
        if (plan?.DeletedAt != null) throw Missing();
        if (plan != null && plan.Version != version) throw new BusinessException(409, SystemMessages.Get("MSG111"));
        var before = plan == null ? null : await Dto(plan, ct);
        if (plan != null) plan.Version++;
        var result = await apply(plan);
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent { AggregateId = plan?.Id ?? ((ItemPlanDto)(object)result!).Id,
            ActorId = actor, Kind = command, OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { before, after = result }, Json) });
        db.TreatmentProgressionReceipts.Add(new TreatmentProgressionReceipt { RequestKey = key, ActorId = actor, Command = command,
            Fingerprint = hash, ResultJson = JsonSerializer.Serialize(result, Json) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public Task<ItemPlanDto> CreateAsync(int actor, ItemPlanCreate command, CancellationToken ct) =>
        Write(actor, null, command.BranchId, command.RequestKey, "Create", command, null, async _ => {
            var plan = new TreatmentTemplate { BranchId = command.BranchId, Name = Name(command.Name), DefinitionKind = "ItemPlan", CreatedBy = actor, CreatedAt = clock.GetUtcNow() };
            db.Add(plan); await db.SaveChangesAsync(ct); return await Dto(plan, ct);
        }, ct);
    public Task<ItemPlanDto> RenameAsync(int actor, Guid id, ItemPlanRename command, CancellationToken ct) =>
        Write(actor, id, 0, command.RequestKey, "Rename", new { id, command }, command.ExpectedVersion, async plan => {
            plan!.Name = Name(command.Name); await db.SaveChangesAsync(ct); return await Dto(plan, ct);
        }, ct);
    public Task<ItemPlanDeleted> DeleteAsync(int actor, Guid id, ItemPlanDelete command, CancellationToken ct) =>
        Write(actor, id, 0, command.RequestKey, "Delete", new { id, command }, command.ExpectedVersion, plan => {
            plan!.DeletedAt = clock.GetUtcNow(); return Task.FromResult(new ItemPlanDeleted(id, plan.Version, true));
        }, ct);
    public Task<ItemPlanDto> DeleteRowAsync(int actor, Guid id, Guid rowId, ItemPlanDelete command, CancellationToken ct) =>
        Write(actor, id, 0, command.RequestKey, "DeleteRow", new { id, rowId, command }, command.ExpectedVersion, async plan => {
            var row = await db.Set<TreatmentTemplateItem>().SingleOrDefaultAsync(x => x.Id == rowId && x.TemplateId == id && x.DeletedAt == null, ct) ?? throw Missing();
            row.DeletedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct); return await Dto(plan!, ct);
        }, ct);
    public Task<ItemPlanDto> SaveRowAsync(int actor, Guid id, Guid? rowId, ItemPlanRowInput command, CancellationToken ct) =>
        Write(actor, id, 0, command.RequestKey, "SaveRow", new { id, rowId, command }, command.ExpectedVersion, async plan => {
            if (command.Teeth == null || command.Teeth.Length > 52 || command.Teeth.Any(x => x == null || !Regex.IsMatch(x, "^(?:[1-4][1-8]|[5-8][1-5])$"))) throw Invalid("teeth");
            if (command.EstimatedMinutes is < 1 or > 480) throw Invalid("estimatedMinutes");
            if (command.Note?.Length > 1000) throw Invalid("note");
            if (command.CostVnd < 0 || command.CostVnd > 999999999999m || decimal.Truncate(command.CostVnd) != command.CostVnd) throw Invalid("costVnd");
            var row = rowId.HasValue ? await db.Set<TreatmentTemplateItem>().SingleOrDefaultAsync(x => x.Id == rowId && x.TemplateId == id && x.DeletedAt == null, ct) ?? throw Missing() : null;
            if (row == null && await db.Set<TreatmentTemplateItem>().CountAsync(x => x.TemplateId == id && x.DeletedAt == null, ct) >= 100) throw Invalid("items");
            // Resolve only new selections: retained inactive sources keep their original snapshots.
            var options = await OptionsAsync(actor, plan!.BranchId, ct);
            var service = options.Services.SingleOrDefault(x => x.Id == command.ServiceId);
            var dentist = options.Members.SingleOrDefault(x => x.Id == command.DentistId && x.CanDentist);
            var assistant = options.Members.SingleOrDefault(x => x.Id == command.AssistantId && x.CanAssistant);
            if ((row == null || row.ServiceId != command.ServiceId) && service == null) throw Invalid("serviceId");
            if (command.DentistId.HasValue && (row == null || row.DentistId != command.DentistId) && dentist == null) throw Invalid("dentistId");
            if (command.AssistantId.HasValue && (row == null || row.AssistantId != command.AssistantId) && assistant == null) throw Invalid("assistantId");
            if (row == null) {
                var max = await db.Set<TreatmentTemplateItem>().Where(x => x.TemplateId == id).MaxAsync(x => (int?)x.Position, ct) ?? 0;
                row = new TreatmentTemplateItem { TemplateId = id, Position = max + 1 }; db.Add(row);
            }
            if (row.ServiceId != command.ServiceId) row.ServiceName = service!.Name;
            if (row.DentistId != command.DentistId) row.DentistName = dentist?.Name;
            if (row.AssistantId != command.AssistantId) row.AssistantName = assistant?.Name;
            row.ServiceId = command.ServiceId; row.DentistId = command.DentistId; row.AssistantId = command.AssistantId;
            row.Date = command.Date; row.TeethJson = JsonSerializer.Serialize(command.Teeth.Distinct().ToArray()); row.EstimatedMinutes = command.EstimatedMinutes;
            row.Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim(); row.CostVnd = (long)command.CostVnd;
            await db.SaveChangesAsync(ct); return await Dto(plan, ct);
        }, ct);
}
