using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments.Interfaces;

namespace DCMS.Application.Features.Appointments;

public class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepository;

    public AppointmentService(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetAppointmentsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? branchId = null,
        int? dentistId = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _appointmentRepository.GetAppointmentsAsync(
            startDate, endDate, branchId, dentistId, status,
            pageNumber, pageSize, cancellationToken);
    }

    public async Task<AppointmentDetailDto?> GetAppointmentByIdAsync(
        int appointmentId,
        CancellationToken cancellationToken = default)
    {
        return await _appointmentRepository.GetAppointmentByIdAsync(appointmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(
        int userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _appointmentRepository.GetMyAppointmentsAsync(
            userId, startDate, endDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<int> CreateAppointmentAsync(
        DateTime startTime,
        int durationInMinutes,
        string service,
        string reason,
        string source,
        int patientId,
        int? relativeId,
        int dentistId,
        int branchId,
        int chairId,
        CancellationToken cancellationToken = default)
    {
        return await _appointmentRepository.CreateAppointmentAsync(
            startTime, durationInMinutes, service, reason, source,
            patientId, relativeId, dentistId, branchId, chairId,
            cancellationToken);
    }

    public async Task UpdateAppointmentAsync(
        int appointmentId,
        DateTime startTime,
        int durationInMinutes,
        string service,
        string reason,
        string source,
        int? relativeId,
        CancellationToken cancellationToken = default)
    {
        await _appointmentRepository.UpdateAppointmentAsync(
            appointmentId, startTime, durationInMinutes, service, reason, source,
            relativeId, cancellationToken);
    }

    public async Task CancelAppointmentAsync(
        int appointmentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _appointmentRepository.CancelAppointmentAsync(appointmentId, reason, cancellationToken);
    }

    public async Task<bool> IsTimeSlotAvailableAsync(
        DateTime startTime,
        int durationInMinutes,
        int branchId,
        int dentistId,
        int chairId,
        int? excludeAppointmentId = null,
        CancellationToken cancellationToken = default)
    {
        return await _appointmentRepository.IsTimeSlotAvailableAsync(
            startTime, durationInMinutes, branchId, dentistId, chairId,
            excludeAppointmentId, cancellationToken);
    }
}
