using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Lab order per treatment-sheet activity. See docs/specs/LAB-ORDER.md.
public sealed partial class TreatmentDossierService
{
    private static readonly string[] LabMargins = ["Vai", "Vát", "Lưỡi dao", "Mỏng dần"];
    private static readonly string[] LabBridgeShapes = ["saddle", "semi-saddle", "ovoid", "t-ridge"];
    private static readonly HashSet<string> AdultTeeth = BuildTeeth([1, 2, 3, 4], 8), ChildTeeth = BuildTeeth([5, 6, 7, 8], 5);
    private static HashSet<string> BuildTeeth(int[] quadrants, int count) =>
        quadrants.SelectMany(q => Enumerable.Range(1, count).Select(n => $"{q}{n}")).ToHashSet();

    private static LabOrderDto ToDto(LabOrder x) => new(x.Id, x.ActivityId, x.Code, x.OrderedOn, x.DueOn, x.DeliveredOn, x.DentistId, x.LabProviderId,
        x.LabWorkTypeId, x.WorkName, x.MarginOuter, x.MarginInner, x.MarginSide, x.BridgeShapes, x.IsChild, x.Teeth, x.Shade, x.Note, x.Material,
        x.Quantity, x.TotalFeeVnd, x.PaidVnd, x.Warranty, x.Version, x.Status, x.SentOn, x.ReceivedOn);

    private static bool IsLab(CatalogProvider p) => p.Type == "Lab" || p.Type == "Both";

    public async Task<IReadOnlyList<LabOrderSummary>> LabOrdersAsync(int actorId, Guid dossierId, CancellationToken ct)
    {
        await DossierAsync(actorId, dossierId, ct);
        return await db.LabOrders.AsNoTracking().Where(x => x.DossierId == dossierId)
            .Select(x => new LabOrderSummary(x.ActivityId, x.WorkName, x.DueOn, x.DeliveredOn)).ToListAsync(ct);
    }

    private async Task<ClinicalActivity> LabActivityAsync(Guid dossierId, Guid activityId, CancellationToken ct) =>
        await db.ClinicalActivities.AsNoTracking().SingleOrDefaultAsync(a => a.Id == activityId && a.DossierId == dossierId, ct)
            ?? throw Error("MSG113", 404);

    public async Task<LabOrderContext> LabOrderContextAsync(int actorId, Guid dossierId, Guid activityId, CancellationToken ct)
    {
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var activity = await LabActivityAsync(dossierId, activityId, ct);
        var order = await db.LabOrders.AsNoTracking().SingleOrDefaultAsync(x => x.ActivityId == activityId, ct);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == dossier.PatientId, ct) ?? throw Error("MSG113", 404);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        int? age = patient.BirthDate is { } b ? Math.Max(0, today.Year - b.Year - (today < b.AddYears(today.Year - b.Year) ? 1 : 0)) : null;
        var labs = await db.Set<CatalogProvider>().AsNoTracking().Where(x => x.IsActive && (x.Type == "Lab" || x.Type == "Both"))
            .OrderBy(x => x.Name).Select(x => new LabOrderLab(x.Id, x.Name)).ToListAsync(ct);
        var types = await db.LabWorkTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new LabOrderWorkType(x.Id, x.Name, x.ProviderId, x.ReferencePrice, x.Warranty)).ToListAsync(ct);
        var dentists = await DentistOptionsAsync(dossier.BranchId, ct);
        return new(order == null ? null : ToDto(order), new(patient.FullName, patient.PatientCode, age, patient.Gender),
            new(labs, types, dentists, dentists.Any(d => d.Id == activity.PerformerId) ? activity.PerformerId : null));
    }

    private async Task<List<LabOrderDentist>> DentistOptionsAsync(int branchId, CancellationToken ct) =>
        await (from member in db.Set<SchedulingMember>().AsNoTracking()
            join staff in db.Employees.AsNoTracking() on member.EmployeeId equals staff.Id
            where member.BranchId == branchId && member.Active && member.CanDentist orderby staff.FullName
            select new LabOrderDentist(staff.Id, staff.FullName)).ToListAsync(ct);

    public async Task<LabOrderDto> SaveLabOrderAsync(int actorId, Guid dossierId, Guid activityId, SaveLabOrderRequest r, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "treatment.lab.manage", ct);
        var dossier = await DossierAsync(actorId, dossierId, ct);
        var activity = await LabActivityAsync(dossierId, activityId, ct);
        var code = Clean(r.Code); var work = Clean(r.WorkName); var note = Clean(r.Note); var material = Clean(r.Material); var warranty = Clean(r.Warranty);
        var teeth = (r.Teeth ?? []).Select(x => x.Trim()).ToArray(); var shapes = (r.BridgeShapes ?? []).Select(x => x.Trim()).ToArray();
        var shade = (r.Shade ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key.Trim(), x => x.Value.Trim());
        var allowedTeeth = r.IsChild ? ChildTeeth : AdultTeeth;
        if (code?.Length > 40 || work is null || work.Length > 160 || note?.Length > 1000 || material?.Length > 100 || warranty?.Length > 100
            || r.OrderedOn.Year < 2000 || r.DueOn < r.OrderedOn || r.DeliveredOn < r.OrderedOn
            || new[] { r.MarginOuter, r.MarginInner, r.MarginSide }.Any(m => !string.IsNullOrEmpty(m) && !LabMargins.Contains(m))
            || shapes.Any(s => !LabBridgeShapes.Contains(s)) || shapes.Distinct().Count() != shapes.Length
            || teeth.Length > 32 || teeth.Any(t => !allowedTeeth.Contains(t)) || teeth.Distinct().Count() != teeth.Length
            || shade.Any(s => !(s.Key is "1" or "2" or "3" or "4" or "5" or "6") || s.Value.Length > 20)
            || r.Quantity is < 1 or > 999 || r.TotalFeeVnd < 0 || r.PaidVnd < 0 || r.PaidVnd > r.TotalFeeVnd || r.TotalFeeVnd > 9_000_000_000_000_000L)
            throw Error("MSG04");
        if (!(await DentistOptionsAsync(dossier.BranchId, ct)).Any(d => d.Id == r.DentistId)) throw Error("MSG04");
        if (r.LabProviderId is { } lab && !await db.Set<CatalogProvider>().AnyAsync(x => x.Id == lab && x.IsActive && (x.Type == "Lab" || x.Type == "Both"), ct)) throw Error("MSG04");
        if (r.LabWorkTypeId is { } type && !await db.LabWorkTypes.AnyAsync(x => x.Id == type, ct)) throw Error("MSG04");
        if (code != null && await db.LabOrders.AnyAsync(x => x.BranchId == dossier.BranchId && x.Code == code && x.ActivityId != activityId, ct)) throw Error("MSG111", 409);

        var order = await db.LabOrders.SingleOrDefaultAsync(x => x.ActivityId == activityId, ct);
        var now = clock.GetUtcNow();
        if (order == null)
        {
            order = new LabOrder { ActivityId = activityId, DossierId = dossierId, PatientId = activity.PatientId, BranchId = dossier.BranchId, CreatedAt = now, CreatedBy = actorId };
            db.Add(order);
            db.Add(new LabOrderStatusLog { LabOrderId = order.Id, ToStatus = LabOrderStatus.NotSent, ChangedAt = now, ChangedBy = actorId });
        }
        else if (r.ExpectedVersion != order.Version) throw Error("MSG111", 409);
        else order.Version++;
        order.Code = code; order.OrderedOn = r.OrderedOn; order.DueOn = r.DueOn; order.DeliveredOn = r.DeliveredOn; order.DentistId = r.DentistId;
        order.LabProviderId = r.LabProviderId; order.LabWorkTypeId = r.LabWorkTypeId; order.WorkName = work;
        order.MarginOuter = Clean(r.MarginOuter); order.MarginInner = Clean(r.MarginInner); order.MarginSide = Clean(r.MarginSide);
        order.BridgeShapes = shapes; order.IsChild = r.IsChild; order.Teeth = teeth; order.Shade = shade; order.Note = note; order.Material = material;
        order.Quantity = r.Quantity; order.TotalFeeVnd = r.TotalFeeVnd; order.PaidVnd = r.PaidVnd; order.Warranty = warranty;
        order.UpdatedAt = now; order.UpdatedBy = actorId;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw Error("MSG111", 409); }
        return ToDto(order);
    }

    public async Task DeleteLabOrderAsync(int actorId, Guid dossierId, Guid activityId, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "treatment.lab.manage", ct);
        await DossierAsync(actorId, dossierId, ct);
        var order = await db.LabOrders.SingleOrDefaultAsync(x => x.ActivityId == activityId && x.DossierId == dossierId, ct) ?? throw Error("MSG113", 404);
        db.Remove(order); await db.SaveChangesAsync(ct);
    }
}
