using DCMS.Application.Common;
namespace DCMS.Application.Features.SystemSettings;

// Purchase log of materials/instruments. Rules and API: docs/specs/MATERIAL-PURCHASES.md.
public sealed record MaterialPurchaseDto(Guid Id, DateOnly EnteredOn, DateOnly? ReceivedOn, DateOnly? ExpiresOn, Guid MaterialId, string MaterialName, string CategoryName,
    decimal Quantity, string Unit, long UnitPriceVnd, long TotalVnd, long PaidVnd, long DueVnd, int Version);
public sealed record MaterialPurchaseTotals(long TotalVnd, long PaidVnd, long DueVnd);
public sealed record MaterialPurchasePage(IReadOnlyList<MaterialPurchaseDto> Items, int Total, int Page, int PageSize, MaterialPurchaseTotals Totals);
public sealed record CreateMaterialPurchaseRequest(DateOnly EnteredOn, DateOnly? ReceivedOn, DateOnly? ExpiresOn, Guid MaterialId, decimal Quantity, string Unit, long UnitPriceVnd, long PaidVnd, Guid RequestKey);
public sealed record UpdateMaterialPurchaseRequest(DateOnly EnteredOn, DateOnly? ReceivedOn, DateOnly? ExpiresOn, Guid MaterialId, decimal Quantity, string Unit, long UnitPriceVnd, long PaidVnd, int ExpectedVersion, Guid RequestKey);
public static class MaterialPurchaseMessages
{
    public static SystemMessage PaidExceedsTotal => new("PURCHASE_PAID_EXCEEDS_TOTAL", "Số đã thanh toán không được lớn hơn Tổng.") { Field = "paidVnd" };
    public static SystemMessage MaterialInactive => new("PURCHASE_MATERIAL_INACTIVE", "Dụng cụ/vật liệu đã bị xóa. Vui lòng chọn mục khác.") { Field = "materialId" };
    public static SystemMessage InvalidDate(string field) => new("PURCHASE_DATE_INVALID", field == "expiresOn" ? "Hạn dùng không được trước Ngày nhập." : "Ngày nhận không được trước Ngày nhập hoặc ở tương lai.") { Field = field };
}
public interface IMaterialPurchaseService
{
    Task<MaterialPurchasePage> ListAsync(int actorId, string? q, DateOnly? from, DateOnly? to, string? payment, int page, int pageSize, CancellationToken ct);
    Task<MaterialPurchaseDto> GetAsync(int actorId, Guid id, CancellationToken ct);
    Task<MaterialPurchaseDto> CreateAsync(int actorId, CreateMaterialPurchaseRequest request, CancellationToken ct);
    Task<MaterialPurchaseDto> UpdateAsync(int actorId, Guid id, UpdateMaterialPurchaseRequest request, CancellationToken ct);
    Task<MaterialPurchaseDto> DeleteAsync(int actorId, Guid id, DeleteCatalogRequest request, CancellationToken ct);
}
