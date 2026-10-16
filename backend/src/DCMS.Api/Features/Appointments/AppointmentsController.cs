using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments.Interfaces;

namespace DCMS.Api.Features.Appointments;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetAppointments(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? branchId,
        [FromQuery] int? dentistId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var appointments = await _appointmentService.GetAppointmentsAsync(
            startDate, endDate, branchId, dentistId, status, 
            pageNumber, pageSize, cancellationToken);

        return Ok(appointments);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDetailDto>> GetAppointment(
        int id,
        CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentService.GetAppointmentByIdAsync(id, cancellationToken);

        if (appointment == null)
            return NotFound();

        return Ok(appointment);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetMyAppointments(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, we would get the user ID from the authentication context
        // For now, we'll use a placeholder - this should be replaced with actual user identification
        int userId = 1; // Placeholder - should come from User.Identity or similar

        var appointments = await _appointmentService.GetMyAppointmentsAsync(
            userId, startDate, endDate, pageNumber, pageSize, cancellationToken);

        return Ok(appointments);
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateAppointment(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointmentId = await _appointmentService.CreateAppointmentAsync(
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

        return CreatedAtAction(
            nameof(GetAppointment),
            new { id = appointmentId },
            appointmentId);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateAppointment(
        int id,
        [FromBody] UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        await _appointmentService.UpdateAppointmentAsync(
            id,
            request.StartTime,
            request.DurationInMinutes,
            request.Service,
            request.Reason,
            request.Source,
            request.RelativeId,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteAppointment(
        int id,
        [FromQuery] string reason,
        CancellationToken cancellationToken = default)
    {
        await _appointmentService.CancelAppointmentAsync(id, reason, cancellationToken);
        return NoContent();
    }
}

// Request DTOs
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