using System.Text.Json;

namespace DCMS.Application.Features.Treatment;

public sealed record CreatePayOsOrderRequest(DateOnly PaidOn, int CollectorId, string? Note,
    IReadOnlyList<DossierPaymentLineInput> Lines, Guid RequestKey);
public sealed record CreateAdvancePayOsOrderRequest(Guid RequestKey, string Kind, long AmountVnd,
    string Payer, string? Note);
public sealed record PayOsOrderDto(Guid Id, long OrderCode, string State, long AmountVnd,
    DateOnly PaidOn, string? QrCode, string? BankBin, string? AccountNumber, string? AccountName,
    DateTimeOffset ExpiresAt, Guid? PaymentId, IReadOnlyList<DossierPaymentLineInput> Lines);
public interface IPayOsOrderService
{
    Task<PayOsOrderDto> CreateAsync(int actorId, Guid dossierId, CreatePayOsOrderRequest request, CancellationToken ct);
    Task<PayOsOrderDto> GetAsync(int actorId, Guid dossierId, Guid orderId, CancellationToken ct);
    Task<PayOsOrderDto?> ActiveAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<PayOsOrderDto> CreateAdvanceAsync(int actorId, Guid dossierId, CreateAdvancePayOsOrderRequest request, CancellationToken ct);
    Task<PayOsOrderDto?> ActiveAdvanceAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<PayOsOrderDto> GetAdvanceAsync(int actorId, Guid dossierId, Guid orderId, CancellationToken ct);
    Task ReceiveWebhookAsync(JsonElement payload, CancellationToken ct);
}
