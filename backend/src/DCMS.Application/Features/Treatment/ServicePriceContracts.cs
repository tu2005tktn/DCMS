namespace DCMS.Application.Features.Treatment;

public sealed record ServicePriceDto(int ServiceId, int BranchId, string Name,
    long? ReferenceUnitPriceVnd, int PriceVersion);
public sealed record SaveServicePriceRequest(int ExpectedVersion, long? ReferenceUnitPriceVnd,
    Guid RequestKey);

public interface IServicePriceService
{
    Task<IReadOnlyList<ServicePriceDto>> ListAsync(int actorId, CancellationToken ct);
    Task<ServicePriceDto> GetAsync(int actorId, int serviceId, CancellationToken ct);
    Task<ServicePriceDto> SaveAsync(int actorId, int serviceId, SaveServicePriceRequest request,
        CancellationToken ct);
}
