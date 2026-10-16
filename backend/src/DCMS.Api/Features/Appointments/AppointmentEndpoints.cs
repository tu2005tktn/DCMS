using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments.Interfaces;

namespace DCMS.Api.Features.Appointments;

public static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/appointments")
            .WithTags("Appointments");

        group.MapGet("/", GetAppointmentsAsync)
            .WithName("GetAppointments")
            .WithSummary("Get appointments with filtering and pagination");

        group.MapGet("/{id:int}", GetAppointmentByIdAsync)
            .WithName("GetAppointmentById")
            .WithSummary("Get appointment by ID");

        group.MapGet("/me", GetMyAppointmentsAsync)
            .WithName("GetMyAppointments")
            .WithSummary("Get current user's appointments");

        group.MapPost("/", CreateAppointmentAsync)
            .WithName("CreateAppointment")
            .WithSummary("Create a new appointment");

        group.MapPut("/{id:int}", UpdateAppointmentAsync)
            .WithName("UpdateAppointment")
            .WithSummary("Update an existing appointment");

        group.MapDelete("/{id:int}", DeleteAppointmentAsync)
            .WithName("DeleteAppointment")
            .WithSummary("Cancel an appointment");

        return endpoints;
    }

    private static async Task<IResult> GetAppointmentsAsync(
        IAppointmentService appointmentService,
        [AsParameters] AppointmentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var appointments = await appointmentService.GetAppointmentsAsync(
            filter.StartDate,
            filter.EndDate,
            filter.BranchId,
            filter.DentistId,
            filter.Status,
            filter.PageNumber,
            filter.PageSize,
            cancellationToken);

        return Results.Ok(appointments);
    }

    private static async Task<IResult> GetAppointmentByIdAsync(
        int id,
        IAppointmentService appointmentService,
        CancellationToken cancellationToken)
    {
        var appointment = await appointmentService.GetAppointmentByIdAsync(id, cancellationToken);

        if (appointment == null)
            return Results.NotFound();

        return Results.Ok(appointment);
    }

    private static async Task<IResult> GetMyAppointmentsAsync(
        IAppointmentService appointmentService,
        [AsParameters] AppointmentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        // In a real implementation, we would get the user ID from the authentication context
        // For now, we'll use a placeholder - this should be replaced with actual user identification
        int userId = 1; // Placeholder - should come from User.Identity or similar

        var appointments = await appointmentService.GetMyAppointmentsAsync(
            userId,
            filter.StartDate,
            filter.EndDate,
            filter.PageNumber,
            filter.PageSize,
            cancellationToken);

        return Results.Ok(appointments);
    }

    private static async Task<IResult> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        IAppointmentService appointmentService,
        CancellationToken cancellationToken)
    {
        var appointmentId = await appointmentService.CreateAppointmentAsync(
            request.StartTime,
            request.DurationInMinutes,
            request.Service,
            request.Reason,
            request.Source,
            request.PatientId,
            request.RelativeId,
            request.DentistId,
            request.BranchId,
            request.ChairId,
            cancellationToken);

        return Results.Created($"/api/appointments/{appointmentId}", appointmentId);
    }

    private static async Task<IResult> UpdateAppointmentAsync(
        int id,
        UpdateAppointmentRequest request,
        IAppointmentService appointmentService,
        CancellationToken cancellationToken)
    {
        await appointmentService.UpdateAppointmentAsync(
            id,
            request.StartTime,
            request.DurationInMinutes,
            request.Service,
            request.Reason,
            request.Source,
            request.RelativeId,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAppointmentAsync(
        int id,
        [FromQuery] string reason,
        IAppointmentService appointmentService,
        CancellationToken cancellationToken)
    {
        await appointmentService.CancelAppointmentAsync(id, reason, cancellationToken);
        return Results.NoContent();
    }
}

// Request DTOs for filtering
public class AppointmentFilterRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? BranchId { get; set; }
    public int? DentistId { get; set; }
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// Request DTOs (same as in controller but duplicated for endpoint mapping)
public class CreateAppointmentRequest
{
    public DateTime StartTime { get; set; }
    public int DurationInMinutes { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public int? RelativeId { get; set; }
    public int DentistId { get; set; }
    public int BranchId { get; set; }
    public int ChairId { get; set; }
}

public class UpdateAppointmentRequest
{
    public DateTime StartTime { get; set; }
    public int DurationInMinutes { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? RelativeId { get; set; }
}