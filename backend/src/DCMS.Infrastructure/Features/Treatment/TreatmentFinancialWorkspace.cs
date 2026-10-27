using DCMS.Application.Common;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Read-only financial projection. No counseling, diagnosis, clinical notes or author drafts.
public sealed class TreatmentFinancialWorkspace(DcmsDbContext db, UserManager<ApplicationUser> users,
    ITreatmentFinancialSummaryPort finance) : ITreatmentFinancialWorkspace
{
    public async Task<IReadOnlyList<FinancialDossier>> ListAsync(int actorId, int patientId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" }
            || !AccountRoles.Effective(await users.GetRolesAsync(user)).Contains(AccountRoles.ClinicAssistant)
            || !(await users.GetClaimsAsync(user)).Any(c => c.Type == "permission" && c.Value == "finance.read"))
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
        var employee = await db.Employees.SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null) throw new BusinessException(403, SystemMessages.Get("MSG06"));
        var branches = db.Set<SchedulingMember>().Where(x => x.EmployeeId == employee.Id && x.Active).Select(x => x.BranchId);
        var rows = await db.TreatmentDossiers.AsNoTracking().Where(x => x.PatientId == patientId
            && x.State != "Deleted" && branches.Contains(x.BranchId)).OrderByDescending(x => x.UpdatedAt)
            .Select(x => new { x.Id, x.Code, x.State, x.BranchId }).ToListAsync(ct);
        var result = new List<FinancialDossier>();
        foreach (var row in rows) result.Add(new(row.Id, row.Code, row.State,
            await finance.ReadAsync(patientId, row.BranchId, row.Id, ct)));
        return result;
    }
}
