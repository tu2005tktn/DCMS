using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments;
using DCMS.Domain.Common;
using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Appointments
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly DcmsDbContext _dbContext;
        
        public AppointmentRepository(DcmsDbContext dbContext)
        {
            _dbContext = dbContext;
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
            var query = _dbContext.Appointments.AsQueryable();
            
            if (startDate.HasValue)
                query = query.Where(a => a.StartTime >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(a => a.StartTime <= endDate.Value);
            
            if (branchId.HasValue)
                query = query.Where(a => a.BranchId == branchId.Value);
            
            if (dentistId.HasValue)
                query = query.Where(a => a.DentistId == dentistId.Value);
            
            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);
            
            // Apply pagination
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            
            // Project to DTO - simplified version
            return await query.Select(a => new AppointmentDto
            {
                Id = a.Id,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                DurationInMinutes = a.DurationInMinutes,
                Service = a.Service,
                Reason = a.Reason,
                Source = a.Source,
                PatientId = a.PatientId,
                RelativeId = a.RelativeId,
                DentistId = a.DentistId,
                BranchId = a.BranchId,
                ChairId = a.ChairId,
                Status = a.Status,
                IsAttendanceConfirmed = a.IsAttendanceConfirmed,
                Version = a.Version
            }).ToListAsync(cancellationToken);
        }
        
        public async Task<AppointmentDetailDto?> GetAppointmentByIdAsync(
            int appointmentId,
            CancellationToken cancellationToken = default)
        {
            var appointment = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
            
            if (appointment == null)
                return null;
            
            return new AppointmentDetailDto
            {
                Id = appointment.Id,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                DurationInMinutes = appointment.DurationInMinutes,
                Service = appointment.Service,
                Reason = appointment.Reason,
                Source = appointment.Source,
                PatientId = appointment.PatientId,
                RelativeId = appointment.RelativeId,
                DentistId = appointment.DentistId,
                BranchId = appointment.BranchId,
                ChairId = appointment.ChairId,
                Status = appointment.Status,
                IsAttendanceConfirmed = appointment.IsAttendanceConfirmed,
                Version = appointment.Version
            };
        }
        
        public async Task<IReadOnlyList<AppointmentDto>> GetMyAppointmentsAsync(
            int userId,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            // For simplicity, assuming userId maps to PatientId or DentistId
            var query = _dbContext.Appointments.Where(a => 
                a.PatientId == userId || 
                a.DentistId == userId);
            
            if (startDate.HasValue)
                query = query.Where(a => a.StartTime >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(a => a.StartTime <= endDate.Value);
            
            // Apply pagination
            query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            
            return await query.Select(a => new AppointmentDto
            {
                Id = a.Id,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                DurationInMinutes = a.DurationInMinutes,
                Service = a.Service,
                Reason = a.Reason,
                Source = a.Source,
                PatientId = a.PatientId,
                RelativeId = a.RelativeId,
                DentistId = a.DentistId,
                BranchId = a.BranchId,
                ChairId = a.ChairId,
                Status = a.Status,
                IsAttendanceConfirmed = a.IsAttendanceConfirmed,
                Version = a.Version
            }).ToListAsync(cancellationToken);
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
            // Validate that the time slot is available
            var isAvailable = await IsTimeSlotAvailableAsync(
                startTime, durationInMinutes, branchId, dentistId, chairId, 
                cancellationToken: cancellationToken);
            
            if (!isAvailable)
                throw new InvalidOperationException("The selected time slot is not available.");
            
            var appointment = new Appointment(
                startTime,
                durationInMinutes,
                service,
                reason,
                source,
                patientId,
                relativeId,
                dentistId,
                branchId,
                chairId);
            
            _dbContext.Appointments.Add(appointment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            return appointment.Id;
        }
    }
}
