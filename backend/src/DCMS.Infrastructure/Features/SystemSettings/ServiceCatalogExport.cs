using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// DCMS-132 (docs/specs/S58-DCMS-132-EXPORT-AUDIT.vi.md §4): services in the branches the actor may read; prices need finance.read.
public sealed class ServiceCatalogExport(DcmsDbContext db, ISettingsAccess access) : IServiceCatalogExport
{
    public async Task<byte[]> ExportAsync(int actorId, string? q, string? status, CancellationToken ct)
    {
        var state = await access.RequireAsync(actorId, "catalog.services.export", ct);
        if (q?.Length > 100) throw new BusinessException(400, SystemMessages.Get("MSG03", "Từ khóa", 100) with { Field = "q" });
        var term = q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        var rows = from s in db.Set<SchedulingService>().AsNoTracking()
                   join b in db.Set<SchedulingBranch>().AsNoTracking() on s.BranchId equals b.Id
                   where state.BranchIds.Contains(s.BranchId) && (term == "" || s.Name.ToUpper().Contains(term))
                   select new { s, Branch = b.Name };
        rows = (status ?? "Active") switch
        {
            "Active" => rows.Where(x => x.s.Active), "Inactive" => rows.Where(x => !x.s.Active), "All" => rows,
            _ => throw new BusinessException(400, SystemMessages.Get("MSG04", "Trạng thái") with { Field = "status" })
        };
        var list = await rows.OrderBy(x => x.Branch).ThenBy(x => x.s.Name).ThenBy(x => x.s.Id).Take(10_000).ToListAsync(ct);
        var prices = state.Permissions.Contains("finance.read");
        string[] header = ["Chi nhánh", "Tên dịch vụ", "Loại", "Đơn vị", "Thời lượng đặt lịch (phút)", "Cho đặt online", "Trạng thái"];
        if (prices) header = [.. header, "Giá tham khảo (VND)", "Phiên bản giá"];
        return Xlsx.Write("Dịch vụ", [header, .. list.Select(x =>
        {
            string?[] cells = [x.Branch, x.s.Name, x.s.IsInitialExamination ? "Khám" : x.s.IsInvestigationService ? "Cận lâm sàng" : "Điều trị",
                x.s.Unit, x.s.PublicDurationMinutes.ToString(), x.s.PublicBookable ? "Có" : "Không", x.s.Active ? "Đang dùng" : "Ngừng dùng"];
            return prices ? [.. cells, x.s.ReferenceUnitPriceVnd?.ToString() ?? "Chưa xác định", x.s.PriceVersion.ToString()] : cells;
        })]);
    }
}
