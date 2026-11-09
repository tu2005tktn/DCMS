using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.FamilyMembers;

namespace DCMS.Api.Features.FamilyMembers;

public static class FamilyRecordEndpoints
{
    public static void MapFamilyRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var own = endpoints.MapGroup("/api/me/family-members/{memberId:guid}")
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("Family records");
        own.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        own.MapGet("/record-access", async (HttpContext c, Guid memberId, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.OverviewAsync(AuthenticationEndpoints.AccountId(c.User), memberId, ct)));
        own.MapPost("/record-access/requests", async (HttpContext c, Guid memberId, FamilyAccessRequest r, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.SubmitAsync(AuthenticationEndpoints.AccountId(c.User), memberId, r, ct)));
        own.MapPost("/record-access/requests/{requestId:guid}/withdraw", async (HttpContext c, Guid memberId, Guid requestId, FamilyAccessWithdraw r, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.WithdrawAsync(AuthenticationEndpoints.AccountId(c.User), memberId, requestId, r, ct)));
        own.MapGet("/appointments", async (HttpContext c, Guid memberId, int? page, int? pageSize, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.AppointmentsAsync(AuthenticationEndpoints.AccountId(c.User), memberId, page ?? 1, pageSize ?? 20, ct)));
        own.MapGet("/record", async (HttpContext c, Guid memberId, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.HeaderAsync(AuthenticationEndpoints.AccountId(c.User), memberId, ct)));
        own.MapGet("/record/{section}", async (HttpContext c, Guid memberId, string section, int? page, int? pageSize, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.RecordsAsync(AuthenticationEndpoints.AccountId(c.User), memberId, section, page ?? 1, pageSize ?? 20, ct)));
        own.MapGet("/record/documents/{documentId:guid}/content", async (HttpContext c, Guid memberId, Guid documentId, IFamilyRecordService s, CancellationToken ct) => {
            var file = await s.DocumentAsync(AuthenticationEndpoints.AccountId(c.User), memberId, documentId, ct);
            return Results.File(file.Bytes, file.ContentType, file.FileName);
        });

        var staff = endpoints.MapGroup("/api/family-record-reviews").RequireAuthorization(PatientLinkEndpoints.VerifyPolicy).WithTags("Family record verification");
        staff.AddEndpointFilter(async (context, next) => { context.HttpContext.Response.Headers.CacheControl = "no-store"; return await next(context); });
        staff.MapGet("", async (HttpContext c, string? status, string? keyword, int? page, int? pageSize, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.QueueAsync(AuthenticationEndpoints.AccountId(c.User), status, keyword, page ?? 1, pageSize ?? 20, ct)));
        staff.MapGet("/{id:guid}", async (HttpContext c, Guid id, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.DetailAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        staff.MapGet("/{id:guid}/patients", async (HttpContext c, Guid id, string? keyword, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.CandidatesAsync(AuthenticationEndpoints.AccountId(c.User), id, keyword, ct)));
        staff.MapGet("/{id:guid}/audit", async (HttpContext c, Guid id, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.AuditAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        staff.MapPost("/{id:guid}/verify", async (HttpContext c, Guid id, VerifyFamilyAccess r, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.VerifyAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        staff.MapPost("/{id:guid}/reject", async (HttpContext c, Guid id, DecideFamilyAccess r, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.DecideAsync(AuthenticationEndpoints.AccountId(c.User), id, false, r, ct)));
        staff.MapPost("/{id:guid}/revoke", async (HttpContext c, Guid id, DecideFamilyAccess r, IFamilyRecordService s, CancellationToken ct) =>
            Results.Ok(await s.DecideAsync(AuthenticationEndpoints.AccountId(c.User), id, true, r, ct)));
    }
}
