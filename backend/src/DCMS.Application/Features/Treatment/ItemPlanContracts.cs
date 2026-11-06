namespace DCMS.Application.Features.Treatment;

public sealed record ItemPlanRowDto(Guid Id, int Position, DateOnly? Date, string[] Teeth,
    int ServiceId, string ServiceName, int? EstimatedMinutes, string? Note,
    int? DentistId, string? DentistName, int? AssistantId, string? AssistantName, long CostVnd);
public sealed record ItemPlanDto(Guid Id, int BranchId, string Name, int Version, int ItemCount,
    long TotalCostVnd, IReadOnlyList<ItemPlanRowDto> Items);
public sealed record ItemPlanListDto(Guid Id, int BranchId, string Name, int Version, int ItemCount, long TotalCostVnd);
public sealed record ItemPlanPage(IReadOnlyList<ItemPlanListDto> Items, int Total, int Page, int PageSize);
public sealed record ItemPlanCreate(int BranchId, string Name, Guid RequestKey);
public sealed record ItemPlanRename(string Name, int ExpectedVersion, Guid RequestKey);
public sealed record ItemPlanDelete(int ExpectedVersion, Guid RequestKey);
public sealed record ItemPlanDeleted(Guid Id, int Version, bool Deleted);
public sealed record ItemPlanRowInput(DateOnly? Date, string[] Teeth, int ServiceId, int? EstimatedMinutes,
    string? Note, int? DentistId, int? AssistantId, decimal CostVnd, int ExpectedVersion, Guid RequestKey);
public sealed record ItemPlanServiceOption(int Id, string Name, long? ReferenceUnitPriceVnd);
public sealed record ItemPlanMemberOption(int Id, string Name, bool CanDentist, bool CanAssistant);
public sealed record ItemPlanOptions(IReadOnlyList<ItemPlanServiceOption> Services, IReadOnlyList<ItemPlanMemberOption> Members);
public interface IItemPlanService
{
    Task<ItemPlanPage> ListAsync(int actor, int branchId, string? keyword, int page, int pageSize, CancellationToken ct);
    Task<ItemPlanOptions> OptionsAsync(int actor, int branchId, CancellationToken ct);
    Task<ItemPlanDto> GetAsync(int actor, Guid id, CancellationToken ct);
    Task<ItemPlanDto> CreateAsync(int actor, ItemPlanCreate command, CancellationToken ct);
    Task<ItemPlanDto> RenameAsync(int actor, Guid id, ItemPlanRename command, CancellationToken ct);
    Task<ItemPlanDeleted> DeleteAsync(int actor, Guid id, ItemPlanDelete command, CancellationToken ct);
    Task<ItemPlanDto> SaveRowAsync(int actor, Guid id, Guid? rowId, ItemPlanRowInput command, CancellationToken ct);
    Task<ItemPlanDto> DeleteRowAsync(int actor, Guid id, Guid rowId, ItemPlanDelete command, CancellationToken ct);
}
