using DCMS.Application.Common;
namespace DCMS.Application.Features.SystemSettings;

public sealed record MaterialCategoryDto(Guid Id, string Name, int ItemCount, int Version);
public sealed record MaterialDto(Guid Id, string Name, Guid CategoryId, string CategoryName, int Version);
public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record CreateCategoryRequest(string Name, Guid RequestKey);
public sealed record UpdateCategoryRequest(string Name, int ExpectedVersion, Guid RequestKey);
public sealed record CreateMaterialRequest(string Name, Guid CategoryId, Guid RequestKey);
public sealed record UpdateMaterialRequest(string Name, Guid CategoryId, int ExpectedVersion, Guid RequestKey);
public sealed record DeleteCatalogRequest(int ExpectedVersion, Guid RequestKey);
public static class CatalogMessages
{
    public static SystemMessage Duplicate => new("CATALOG_NAME_DUPLICATE", "Tên đã tồn tại. Vui lòng nhập tên khác.") { Field = "name" };
    public static SystemMessage InUse => new("CATALOG_CATEGORY_IN_USE", "Danh mục đang có dụng cụ/vật liệu. Vui lòng chuyển hoặc xóa các mục trước.");
}
public interface ISystemCatalogService
{
    Task<CatalogPage<MaterialCategoryDto>> CategoriesAsync(int actorId, string? q, int page, int pageSize, CancellationToken ct);
    Task<CatalogPage<MaterialDto>> MaterialsAsync(int actorId, string? q, Guid? categoryId, int page, int pageSize, CancellationToken ct);
    Task<MaterialCategoryDto> CategoryAsync(int actorId, Guid id, CancellationToken ct);
    Task<MaterialDto> MaterialAsync(int actorId, Guid id, CancellationToken ct);
    Task<MaterialCategoryDto> CreateCategoryAsync(int actorId, CreateCategoryRequest request, CancellationToken ct);
    Task<MaterialCategoryDto> UpdateCategoryAsync(int actorId, Guid id, UpdateCategoryRequest request, CancellationToken ct);
    Task<MaterialCategoryDto> DeleteCategoryAsync(int actorId, Guid id, DeleteCatalogRequest request, CancellationToken ct);
    Task<MaterialDto> CreateMaterialAsync(int actorId, CreateMaterialRequest request, CancellationToken ct);
    Task<MaterialDto> UpdateMaterialAsync(int actorId, Guid id, UpdateMaterialRequest request, CancellationToken ct);
    Task<MaterialDto> DeleteMaterialAsync(int actorId, Guid id, DeleteCatalogRequest request, CancellationToken ct);
}
