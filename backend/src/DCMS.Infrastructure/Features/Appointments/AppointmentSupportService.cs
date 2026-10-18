using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DCMS.Infrastructure.Features.Appointments;

// UC-06 A1 support requests (S16 #8/#9). Access and the 12-hour window come from the shared read side, so the
// same rules decide what the patient sees and what they may request. Creating or withdrawing a request never
// touches the appointment or its reservation. Contract: docs/implementation/UC05-06-S14-S16.md §4.4.
public sealed class AppointmentSupportService(DcmsDbContext db, IAppointmentQueryService queries, TimeProvider clock)
    : IAppointmentSupportService
{
    private const int ReasonMax = 500;
    private static BusinessException Error(string code, int status = 400, string field = "Thông tin", int max = 0) =>
        new(status, SystemMessages.Get(code, field, max));

    public async Task<SupportRequestOutcome> CreateAsync(int accountId, string code, CreateSupportRequest request, CancellationToken ct)
    {
        var kind = request.Kind?.Trim().ToUpperInvariant();
        var reason = request.Reason?.Trim();
        if (request.RequestKey == Guid.Empty) throw Error("MSG04", field: "Mã yêu cầu");
        if (kind == null || !SupportRequestKinds.All.Contains(kind)) throw Error("MSG04", field: "Loại yêu cầu");
        if (string.IsNullOrEmpty(reason)) throw Error("MSG02", field: "Lý do");
        if (reason.Length > ReasonMax) throw Error("MSG03", field: "Lý do", max: ReasonMax);

        // Throws MSG102 for a code outside the account's own bookings or verified self appointments.
        var detail = await queries.MineDetailAsync(accountId, code, ct);
        var appointmentId = await db.Appointments.AsNoTracking().Where(x => x.Code == code).Select(x => x.Id).SingleAsync(ct);

        var previous = await db.AppointmentSupportRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.RequestKey == request.RequestKey, ct);
        if (previous != null)
        {
            if (previous.AppointmentId != appointmentId || previous.Kind != kind || previous.Reason != reason) throw Error("MSG111", 409);
            return new(new(Dto(previous, code), SystemMessages.Get("MSG33")), true);
        }
        if (!detail.Eligibility.CanRequestSupport) throw Error("MSG31", 409);
        if (await db.AppointmentSupportRequests.AnyAsync(x => x.AppointmentId == appointmentId && x.AccountId == accountId
                && x.Status == SupportRequestStatuses.Pending, ct)) throw Error("MSG111", 409);

        var now = clock.GetUtcNow();
        var entity = new AppointmentSupportRequest
        {
            AppointmentId = appointmentId, AccountId = accountId, Kind = kind, Reason = reason,
            RequestKey = request.RequestKey, CreatedAt = now
        };
        db.AppointmentSupportRequests.Add(entity);
        await SaveWithEventAsync(entity, "AppointmentSupportRequested", now, ct);
        return new(new(Dto(entity, code), SystemMessages.Get("MSG33")), false);
    }

    public async Task<SupportRequestResult> WithdrawAsync(int accountId, int id, WithdrawSupportRequest request, CancellationToken ct)
    {
        // Another account's request is indistinguishable from a missing one.
        var entity = await db.AppointmentSupportRequests.SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId, ct)
            ?? throw Error("MSG102", 404);
        if (entity.Version != request.ExpectedVersion || entity.Status != SupportRequestStatuses.Pending) throw Error("MSG111", 409);
        var now = clock.GetUtcNow();
        entity.Status = SupportRequestStatuses.Withdrawn;
        entity.WithdrawnAt = now;
        entity.Version++;
        await SaveWithEventAsync(entity, "AppointmentSupportWithdrawn", now, ct);
        var code = await db.Appointments.AsNoTracking().Where(x => x.Id == entity.AppointmentId).Select(x => x.Code).SingleAsync(ct);
        return new(Dto(entity, code), null);
    }

    public async Task<IReadOnlyList<SupportRequestDto>> MineAsync(int accountId, string? appointmentCode, CancellationToken ct)
    {
        var query = from request in db.AppointmentSupportRequests.AsNoTracking()
                    join appointment in db.Appointments.AsNoTracking() on request.AppointmentId equals appointment.Id
                    where request.AccountId == accountId
                    select new { request, appointment.Code };
        if (!string.IsNullOrWhiteSpace(appointmentCode)) query = query.Where(x => x.Code == appointmentCode.Trim());
        var rows = await query.OrderByDescending(x => x.request.CreatedAt).ThenByDescending(x => x.request.Id).Take(100).ToListAsync(ct);
        return rows.Select(x => Dto(x.request, x.Code)).ToArray();
    }

    // The event never carries the free-text reason; the dispatcher (TV5) reads the request itself.
    private async Task SaveWithEventAsync(AppointmentSupportRequest entity, string eventType, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
            db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent
            {
                AggregateId = entity.AppointmentId, EventType = eventType, OccurredAt = now,
                PayloadJson = JsonSerializer.Serialize(new { requestId = entity.Id, appointmentId = entity.AppointmentId,
                    kind = entity.Kind, status = entity.Status, version = entity.Version })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw Error("MSG111", 409); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Error("MSG111", 409);
        }
    }

    private static SupportRequestDto Dto(AppointmentSupportRequest x, string code) =>
        new(x.Id, code, x.Kind, x.Reason, x.Status, x.CreatedAt, x.WithdrawnAt ?? x.DecidedAt, x.PublicNote, x.Version);
}
