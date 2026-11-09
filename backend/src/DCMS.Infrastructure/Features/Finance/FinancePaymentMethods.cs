using DCMS.Domain.Features.Finance;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Finance;

public static class FinancePaymentMethods
{
    public static readonly IReadOnlyDictionary<string, string> Builtin = new Dictionary<string, string>
    {
        ["Cash"] = "Tiền mặt", ["Transfer"] = "Chuyển khoản", ["POS"] = "Thẻ/POS",
        ["EWallet"] = "Ví điện tử", ["Other"] = "Khác",
    };

    public static async Task<bool> IsAllowedAsync(DcmsDbContext db, int branchId, string method, CancellationToken ct) =>
        Builtin.ContainsKey(method) || await db.Set<FinancePaymentMethod>().AsNoTracking()
            .AnyAsync(x => x.BranchId == branchId && x.Code == method && x.Active, ct);
}
