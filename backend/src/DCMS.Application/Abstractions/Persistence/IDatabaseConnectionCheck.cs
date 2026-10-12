namespace DCMS.Application.Abstractions.Persistence;

public interface IDatabaseConnectionCheck
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
