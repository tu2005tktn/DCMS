using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.FamilyMembers;
using Microsoft.AspNetCore.Mvc;
namespace DCMS.Api.Features.FamilyMembers;
public static class FamilyMemberEndpoints
{
    public static void MapFamilyMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/me/family-members").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("Customer family members");
        group.MapGet("", async (HttpContext context, IFamilyMemberService service, string? keyword, int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(AuthenticationEndpoints.AccountId(context.User), keyword, page ?? 1, pageSize ?? 20, ct)));
        group.MapGet("/{id:guid}", async (HttpContext context, IFamilyMemberService service, Guid id, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        group.MapPost("", async (HttpContext context, IFamilyMemberService service, CreateFamilyMember command, CancellationToken ct) => {
            var result = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), command, ct);
            return result.Replayed ? Results.Ok(result.Member) : Results.Created($"/api/me/family-members/{result.Member.Id}", result.Member);
        });
        group.MapPut("/{id:guid}", async (HttpContext context, IFamilyMemberService service, Guid id, UpdateFamilyMember command, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(AuthenticationEndpoints.AccountId(context.User), id, command, ct)));
        group.MapDelete("/{id:guid}", async (HttpContext context, IFamilyMemberService service, Guid id, [FromBody] DeleteFamilyMember command, CancellationToken ct) =>
            Results.Ok(await service.DeleteAsync(AuthenticationEndpoints.AccountId(context.User), id, command, ct)));
    }
}
