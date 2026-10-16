using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments.Dtos;

namespace DCMS.Application.Features.Appointments.Interfaces;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentDto>> GetAppointmentsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? branchId = null,
        int? dentistId = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<AppointmentDetailDto?> GetAppointmentByIdAsync(
        int appointmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(
        int userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<int> CreateAppointmentAsync(
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
        CancellationToken cancellationToken = default);

    Task UpdateAppointmentAsync(
        int appointmentId,
        DateTime startTime,
        int durationInMinutes,
        string service,
        string reason,
        string source,
        int? relativeId,
        CancellationToken cancellationToken = default);

    Task CancelAppointmentAsync(
        int appointmentId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> IsTimeSlotAvailableAsync(
        DateTime startTime,
        int durationInMinutes,
        int branchId,
        int dentistId,
        int chairId,
        int? excludeAppointmentId = null,
        CancellationToken cancellationToken = default);
}