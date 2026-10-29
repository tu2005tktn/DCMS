using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Finance;
namespace DCMS.Api.Features.Finance;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);
        var g = endpoints.MapGroup("/api/finance").RequireAuthorization().WithTags("Clinical service billing");
        g.MapGet("/examination-services",async(HttpContext c,int branchId,IExaminationFeeService s,CancellationToken ct)=>Results.Ok(await s.ServicesAsync(Actor(c),branchId,ct)));
  g.MapPost("/examination-services",async(HttpContext c,SaveExaminationService command,IExaminationFeeService s,CancellationToken ct)=>Results.Ok(await s.SaveServiceAsync(Actor(c),null,command,ct)));
  g.MapPut("/examination-services/{id:int}",async(HttpContext c,int id,SaveExaminationService command,IExaminationFeeService s,CancellationToken ct)=>Results.Ok(await s.SaveServiceAsync(Actor(c),id,command,ct)));
  g.MapGet("/visits/{visitId:guid}/examination-fee",async(HttpContext c,Guid visitId,IExaminationFeeService s,CancellationToken ct)=>Results.Ok(await s.GetAsync(Actor(c),visitId,ct)));
  g.MapPost("/visits/{visitId:guid}/examination-fee",async(HttpContext c,Guid visitId,SetExaminationFee command,IExaminationFeeService s,CancellationToken ct)=>Results.Ok(await s.SetAsync(Actor(c),visitId,command,ct)));
  g.MapGet("/capabilities", async (HttpContext c, IFinanceService s, CancellationToken ct) => Results.Ok(await s.CapabilitiesAsync(Actor(c), ct)));
        g.MapGet("/services", async (HttpContext c, int branchId, bool? includeInactive, IFinanceService s, CancellationToken ct) => Results.Ok(await s.ServicesAsync(Actor(c), branchId, includeInactive ?? false, ct)));
        g.MapPost("/services", async (HttpContext c, SaveInvestigationService command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.SaveServiceAsync(Actor(c), null, command, ct)));
        g.MapPut("/services/{id:int}", async (HttpContext c, int id, SaveInvestigationService command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.SaveServiceAsync(Actor(c), id, command, ct)));
        g.MapGet("/providers", async (HttpContext c, IFinanceService s, CancellationToken ct) => Results.Ok(await s.ProvidersAsync(Actor(c), ct)));
        g.MapPost("/providers", async (HttpContext c, CreateProvider command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.CreateProviderAsync(Actor(c), command, ct)));
        g.MapGet("/queue", async (HttpContext c, int branchId, IFinanceService s, CancellationToken ct) => Results.Ok(await s.QueueAsync(Actor(c), branchId, ct)));
        g.MapGet("/visits/{visitId:guid}", async (HttpContext c, Guid visitId, IFinanceService s, CancellationToken ct) => Results.Ok(await s.BillAsync(Actor(c), visitId, ct)));
        g.MapPost("/visits/{visitId:guid}/payments", async (HttpContext c, Guid visitId, CollectPayment command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.CollectAsync(Actor(c), visitId, command, ct)));
        g.MapPost("/payments/{id:guid}/confirm", async (HttpContext c, Guid id, ConfirmPayment command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.ConfirmAsync(Actor(c), id, command, ct)));
        g.MapPost("/payments/{id:guid}/cancel", async (HttpContext c, Guid id, CancelPayment command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.CancelAsync(Actor(c), id, command, ct)));
        g.MapGet("/receipt-preview", async (HttpContext c, int branchId, IFinanceService s, CancellationToken ct) => Results.Ok(await s.PreviewReceiptAsync(Actor(c), branchId, ct)));
        g.MapGet("/payments/{id:guid}/print", async (HttpContext c, Guid id, IFinanceService s, CancellationToken ct) => Results.Ok(await s.ReceiptPrintAsync(Actor(c), id, ct)));
        g.MapPost("/payments/{id:guid}/print", async (HttpContext c, Guid id, RecordReceiptPrint command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.RecordPrintAsync(Actor(c), id, command, ct)));
        g.MapGet("/branches/{branchId:int}/receipt-sequence", async (HttpContext c, int branchId, IFinanceService s, CancellationToken ct) => Results.Ok(await s.ReceiptSequenceAsync(Actor(c), branchId, ct)));
        g.MapPut("/branches/{branchId:int}/receipt-sequence", async (HttpContext c, int branchId, UpdateReceiptSequence command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.UpdateReceiptSequenceAsync(Actor(c), branchId, command, ct)));
        var orders = endpoints.MapGroup("/api/visits/{visitId:guid}/investigations/{id:guid}/billing").RequireAuthorization();
        orders.MapGet("", async (HttpContext c, Guid visitId, Guid id, IFinanceService s, CancellationToken ct) => Results.Ok(await s.BillingAsync(Actor(c), visitId, id, ct)));
        orders.MapPost("/quote", async (HttpContext c, Guid visitId, Guid id, QuoteInvestigation command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.QuoteAsync(Actor(c), visitId, id, command, ct)));
        orders.MapPost("/perform", async (HttpContext c, Guid visitId, Guid id, PerformInvestigation command, IFinanceService s, CancellationToken ct) => Results.Ok(await s.PerformAsync(Actor(c), visitId, id, command, ct)));
    }
}
