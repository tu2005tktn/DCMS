using System.Text.Json;
using DCMS.Api.Features.Authentication;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using Microsoft.AspNetCore.Http.Features;

namespace DCMS.Api.Features.Examinations;

public static class ExaminationEndpoints
{
    public static void MapExaminationEndpoints(this IEndpointRouteBuilder app)
    {
        var visits = app.MapGroup("/api/visits/{visitId:guid}").RequireAuthorization().WithTags("DCMS-112 S19");
        visits.MapGet("/examination", async (HttpContext c, Guid visitId, IExaminationService s, CancellationToken ct) => Results.Ok(await s.GetAsync(AuthenticationEndpoints.AccountId(c.User), visitId, ct)));
        visits.MapPost("/start", async (HttpContext c, Guid visitId, StartExamination r, IExaminationService s, CancellationToken ct) => Results.Ok(await s.StartAsync(AuthenticationEndpoints.AccountId(c.User), visitId, r, ct)));
        visits.MapPut("/examination", async (HttpContext c, Guid visitId, SaveExamination r, IExaminationService s, CancellationToken ct) => Results.Ok(await s.SaveAsync(AuthenticationEndpoints.AccountId(c.User), visitId, r, ct)));
        visits.MapGet("/clinical-history", async (HttpContext c, Guid visitId, IExaminationService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(AuthenticationEndpoints.AccountId(c.User), visitId, ct)));
        visits.MapGet("/clinical-handoff", async (HttpContext c, Guid visitId, IExaminationService s, CancellationToken ct) => Results.Ok(await s.HandoffAsync(AuthenticationEndpoints.AccountId(c.User), visitId, ct)));
        app.MapGet("/api/patients/{patientId:int}/document-contexts", async (HttpContext c, int patientId, IClinicalDocumentService s, CancellationToken ct) => Results.Ok(await s.ContextsAsync(AuthenticationEndpoints.AccountId(c.User), patientId, ct))).RequireAuthorization();
        var docs = app.MapGroup("/api/clinical-documents").RequireAuthorization().WithTags("DCMS-112 S20");
        docs.MapGet("", async (HttpContext c, int patientId, Guid? visitId, Guid? dossierId, Guid? planId, string? type, DateOnly? from, DateOnly? to, int? page, int? pageSize, IClinicalDocumentService s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(AuthenticationEndpoints.AccountId(c.User), new(patientId, visitId, dossierId, planId, type, from, to, page ?? 1, pageSize ?? 20), ct)));
        docs.MapGet("/{id:guid}", async (HttpContext c, Guid id, IClinicalDocumentService s, CancellationToken ct) => Results.Ok(await s.DetailAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        docs.MapGet("/{id:guid}/history", async (HttpContext c, Guid id, IClinicalDocumentService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        docs.MapPost("/{id:guid}/review", async (HttpContext c, Guid id, ReviewDocument r, IClinicalDocumentService s, CancellationToken ct) => Results.Ok(await s.ReviewAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        docs.MapGet("/{id:guid}/content", async (HttpContext c, Guid id, string? disposition, IClinicalDocumentService s, CancellationToken ct) => {
            var content = await s.ContentAsync(AuthenticationEndpoints.AccountId(c.User), id, disposition == "inline", ct);
            c.Response.Headers.CacheControl = "no-store"; c.Response.Headers.XContentTypeOptions = "nosniff";
            c.Response.Headers.ContentSecurityPolicy = "sandbox";
            return Results.File(content.Bytes, content.ContentType, disposition == "inline" ? null : content.FileName);
        });
        docs.MapPost("", UploadAsync);
    }
    private static async Task<IResult> UploadAsync(HttpContext c, IClinicalDocumentService s, CancellationToken ct)
    {
        const long maxRequest = 210L * 1024 * 1024;
        var limit = c.Features.Get<IHttpMaxRequestBodySizeFeature>(); if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = maxRequest;
        if (!c.Request.HasFormContentType || c.Request.ContentLength > maxRequest) return Results.BadRequest(SystemMessages.Get("MSG04"));
        IFormCollection form;
        try { form = await c.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = maxRequest, MemoryBufferThreshold = 65536 }, ct); }
        catch (InvalidDataException) { return Results.BadRequest(SystemMessages.Get("MSG04")); }
        var metadataFile = form.Files.GetFile("metadata");
        if (metadataFile?.Length > 16384 || form["metadata"].ToString().Length > 16384) return Results.BadRequest(SystemMessages.Get("MSG04"));
        string json;
        if (metadataFile != null) { using var reader = new StreamReader(metadataFile.OpenReadStream()); json = await reader.ReadToEndAsync(ct); }
        else json = form["metadata"].ToString();
        DocumentMetadata? metadata;
        try { metadata = JsonSerializer.Deserialize<DocumentMetadata>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { return Results.BadRequest(SystemMessages.Get("MSG04")); }
        var files = form.Files.Where(x => x.Name is "file" or "files").ToArray();
        if (metadata == null || files.Length is < 1 or > 10 || files.Any(x => x.Length > 20L * 1024 * 1024)) return Results.BadRequest(SystemMessages.Get("MSG04"));
        var uploads = new List<DocumentUpload>();
        foreach (var f in files) { using var memory = new MemoryStream(); await f.CopyToAsync(memory, ct); uploads.Add(new(f.FileName, f.ContentType, memory.ToArray())); }
        var result = await s.UploadAsync(AuthenticationEndpoints.AccountId(c.User), metadata, uploads, ct);
        if (result.Documents.Count == 1 && result.Document == null) return Results.BadRequest(new SystemMessage("MSG04", result.Documents[0].Error!));
        return Results.Json(result, statusCode: 201);
    }
}
