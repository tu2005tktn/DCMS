using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Records the fee of an activity the dossier sheet has just added (docs/specs/CLINICAL-ACTIVITY-TABLE-SIMPLIFICATION.vi.md).
public sealed class ClinicalWorkService(DcmsDbContext db, UserManager<ApplicationUser> users, IClinicalChargePort charges,
    TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess settingsAccess) : IClinicalWorkService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400) =>
        new(status, SystemMessages.Get(code));
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));

    private async Task<int> DentistAsync(int actorId, int branchId, CancellationToken ct)
    {
        var scope = await settingsAccess.ResolveAsync(actorId, ct);
        if (scope.Assigned && !scope.Owner && !scope.BranchIds.Contains(branchId)) throw Error("MSG06", 403);
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff" ||
            !AccountRoles.Effective(await users.GetRolesAsync(user)).Contains(AccountRoles.Dentist))
            throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null || !await db.SchedulingMembers.AsNoTracking().AnyAsync(x =>
            x.BranchId == branchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct))
            throw Error("MSG06", 403);
        return employee.Id;
    }

    // Only the outer command owns its transaction; recording the activity and its fee are atomic.
    public async Task<ActivityDto> PostWithinTransactionAsync(int actorId, Guid id, PostActivityRequest request, CancellationToken ct, ClinicalFeeRequest? feeSnapshot = null)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("An owning transaction is required.");
        if (request.RequestKey == Guid.Empty) throw Error("MSG04");
        var receipt = await db.ClinicalCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.ActorId != actorId || receipt.Command != "ActivityPost" || receipt.Fingerprint != Hash(new { id, request }))
                throw Error("MSG111", 409);
            return ActivityDto.From(await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == receipt.ResultId, ct));
        }
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var row = await db.ClinicalActivities.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        var dossier = await db.TreatmentDossiers.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == row.DossierId, ct)
            ?? throw Error("MSG113", 404);
        var employee = await DentistAsync(actorId, dossier.BranchId, ct);
        TreatmentItem? item = null;
        if (row.ItemId.HasValue)
            item = dossier.Items.SingleOrDefault(x => x.Id == row.ItemId.Value) ?? throw Error("MSG113", 404);
        if (employee != dossier.DentistId || row.PerformerId != employee || dossier.State != "Open" || row.Version != request.ExpectedVersion)
            throw Error("MSG111", 409);
        if (row.PriceSnapshotVnd == 0) row.FeeStatus = "NotRequested";
        else if (row.Unit == "gói") row.FeeStatus = "NeedsRule";
        else
        {
            var gross = TreatmentPlanRules.Gross(row.Quantity, checked((long)row.PriceSnapshotVnd));
            if (item != null)
            {
                var approved = item.DiscountType is "Fixed" or "Percent" && item.DiscountValue is > 0
                    ? TreatmentPlanRules.Discount(TreatmentPlanRules.Gross(item.Quantity, item.AgreedUnitPriceVnd), item.DiscountType, item.DiscountValue.Value) : 0;
                var earlier = db.ClinicalActivities.Where(x => x.ItemId == item.Id && x.Id != row.Id);
                var used = await earlier.SumAsync(x => x.DiscountSnapshotVnd, ct);
                var share = checked((long)decimal.Round(approved * row.Quantity / item.Quantity, 0, MidpointRounding.AwayFromZero));
                if (await earlier.SumAsync(x => x.Quantity, ct) + row.Quantity == item.Quantity) share = approved - used;
                row.DiscountSnapshotVnd = feeSnapshot?.ApprovedDiscountVnd ?? Math.Min(gross, Math.Max(0, Math.Min(approved - used, share)));
            }
            else
            {
                row.DiscountSnapshotVnd = Math.Min(gross, Math.Max(0, feeSnapshot?.ApprovedDiscountVnd ?? row.DiscountSnapshotVnd));
            }
            var fee = await charges.PostInTransactionAsync(feeSnapshot ?? new ClinicalFeeRequest($"clinical-fee:{row.Id}", row.Id, row.Version + 1,
                dossier.PatientId, dossier.BranchId, row.DossierId, row.ItemId, row.PlanVersion, "Separate",
                row.Quantity, row.Unit, checked((long)row.PriceSnapshotVnd), gross, row.DiscountSnapshotVnd,
                item?.DiscountType, item?.DiscountValue, item?.DiscountReason, null,
                row.DiscountSnapshotVnd > 0 ? actorId : null, row.DiscountSnapshotVnd > 0 ? clock.GetUtcNow() : null, actorId), ct);
            if (fee.Status is not ("Pending" or "Posted")) throw Error("MSG144", 503);
            row.FeeStatus = fee.Status;
        }
        row.Version++;
        db.TreatmentProgressionEvents.Add(new TreatmentProgressionEvent
        {
            AggregateId = row.Id, Kind = "clinical.activity.posted",
            StableKey = $"clinical-activity-posted:{row.Id}", ActorId = actorId, OccurredAt = clock.GetUtcNow(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                activityId = row.Id, activityVersion = row.Version, dossier.PatientId, dossier.BranchId,
                row.DossierId, row.ItemId, row.PlanVersion, row.Quantity, row.Unit
            }, Json)
        });
        db.ClinicalCommandReceipts.Add(new ClinicalCommandReceipt
        {
            RequestKey = request.RequestKey, ActorId = actorId, Command = "ActivityPost",
            Fingerprint = Hash(new { id, request }), ResultId = id
        });
        await db.SaveChangesAsync(ct);
        return ActivityDto.From(row);
    }
}
