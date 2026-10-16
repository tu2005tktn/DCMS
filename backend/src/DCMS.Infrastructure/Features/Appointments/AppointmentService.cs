using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments.Interfaces;

namespace DCMS.Infrastructure.Features.Appointments
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IAppointmentService _applicationService;

        public AppointmentService(IAppointmentService applicationService)
        {
            _applicationService = applicationService;
        }

        public Task<IReadOnlyList<AppointmentDto>> GetAppointmentsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? branchId = null,
            int? dentistId = null,
            string? status = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.GetAppointmentsAsync(startDate, endDate, branchId, dentistId, status, pageNumber, pageSize, cancellationToken);
        }

        public Task<AppointmentDetailDto?> GetAppointmentByIdAsync(
            int appointmentId,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.GetAppointmentByIdAsync(appointmentId, cancellationToken);
        }

        public Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(
            int userId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.GetMyAppointmentsAsync(userId, startDate, endDate, pageNumber, pageSize, cancellationToken);
        }

        public Task<int> CreateAppointmentAsync(
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
            return _applicationService.CreateAppointmentAsync(startTime, durationInMinutes, service, reason, source, patientId, relativeId, dentistId, branchId, chairId, cancellationToken);
        }

        public Task UpdateAppointmentAsync(
            int appointmentId,
            DateTime startTime,
            int durationInMinutes,
            string service,
            string reason,
            string source,
            int? relativeId,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.UpdateAppointmentAsync(appointmentId, startTime, durationInMinutes, service, reason, source, relativeId, cancellationToken);
        }

        public Task CancelAppointmentAsync(
            int appointmentId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.CancelAppointmentAsync(appointmentId, reason, cancellationToken);
        }

        public Task<bool> IsTimeSlotAvailableAsync(
            DateTime startTime,
            int durationInMinutes,
            int branchId,
            int dentistId,
            int chairId,
            int? excludeAppointmentId = null,
            CancellationToken cancellationToken = default)
        {
            return _applicationService.IsTimeSlotAvailableAsync(startTime, durationInMinutes, branchId, dentistId, chairId, excludeAppointmentId, cancellationToken);
        }
    }
}
