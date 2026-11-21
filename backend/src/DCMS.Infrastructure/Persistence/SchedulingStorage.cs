using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public sealed partial class DcmsDbContext
{
    public IQueryable<SchedulingMember> SchedulingMembers => Set<SchedulingBranch>().AsNoTracking().SelectMany(x => x.Members);
    public IQueryable<SchedulingChair> SchedulingChairs => Set<SchedulingBranch>().AsNoTracking().SelectMany(x => x.Chairs);
    public IQueryable<BranchChair> BranchChairs => SchedulingChairs.Where(x => x.Code != null).Select(x => new BranchChair
    {
        Id = x.Id, BranchId = x.BranchId, Code = x.Code!, Name = x.Name, IsActive = x.Active,
        Version = x.Version, OpenMinute = x.OpenMinute, CloseMinute = x.CloseMinute
    });

    public Task<int> NextChairIdAsync(CancellationToken ct) => Database.SqlQueryRaw<int>(
        "SELECT nextval('scheduling_chair_seq')::integer AS \"Value\"").SingleAsync(ct);

    public async Task<SchedulingChair?> LoadChairAsync(int branchId, int chairId, CancellationToken ct = default) =>
        (await Set<SchedulingBranch>().SingleAsync(x => x.Id == branchId, ct)).Chairs.SingleOrDefault(x => x.Id == chairId);

    public async Task<SchedulingMember> LoadMemberAsync(int branchId, int employeeId, CancellationToken ct = default) =>
        (await Set<SchedulingBranch>().SingleAsync(x => x.Id == branchId, ct)).Members.Single(x => x.EmployeeId == employeeId);

    public async Task<SchedulingMember> LoadMemberAsync(int employeeId, CancellationToken ct = default) =>
        (await Set<SchedulingBranch>().SingleAsync(x => x.Members.Any(m => m.EmployeeId == employeeId), ct))
            .Members.Single(x => x.EmployeeId == employeeId);

    public Task AddSchedulingDataAsync(IEnumerable<object> rows) => AddSchedulingDataAsync(rows.ToArray());

    public async Task AddSchedulingDataAsync(params object[] rows)
    {
        foreach (var row in rows)
        {
            var branchId = row switch
            {
                SchedulingMember value => value.BranchId,
                SchedulingChair value => value.BranchId,
                BranchChair value => value.BranchId,
                _ => 0
            };
            if (branchId == 0) { Add(row); continue; }
            var owner = await Set<SchedulingBranch>().FindAsync(branchId)
                ?? throw new InvalidOperationException("The branch must exist before its resources are added.");
            switch (row)
            {
                case SchedulingMember member:
                    if (owner.Members.Any(x => x.EmployeeId == member.EmployeeId)) throw new InvalidOperationException("Duplicate branch member.");
                    owner.Members.Add(member); break;
                case SchedulingChair chair:
                    if (chair.Id == 0) chair.Id = await NextChairIdAsync(default);
                    owner.Chairs.Add(chair); break;
                case BranchChair profile:
                    var resource = owner.Chairs.Single(x => x.Id == profile.Id);
                    resource.Code = profile.Code; resource.Name = profile.Name; resource.Active = profile.IsActive;
                    resource.Version = profile.Version; resource.OpenMinute = profile.OpenMinute; resource.CloseMinute = profile.CloseMinute;
                    break;
            }
        }
    }

    public async Task<AppointmentSupportRequest?> LoadSupportRequestAsync(string code, CancellationToken ct)
    {
        var owner = await Appointments.SingleOrDefaultAsync(x => x.SupportRequests.Any(r => r.Code == code), ct);
        return owner?.SupportRequests.Single(x => x.Code == code);
    }

    public async Task<AppointmentSupportRequest> LoadSupportRequestAsync(Guid id, CancellationToken ct)
    {
        var owner = await Appointments.SingleAsync(x => x.SupportRequests.Any(r => r.Id == id), ct);
        return owner.SupportRequests.Single(x => x.Id == id);
    }

    private void PrepareSchedulingChanges()
    {
        foreach (var branch in ChangeTracker.Entries<SchedulingBranch>().Where(x => x.State == EntityState.Modified).ToArray())
            if (!branch.Property(x => x.Version).IsModified)
                branch.Entity.Version = branch.Property(x => x.Version).OriginalValue + 1;
    }
}
