using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Appointments.Dtos;
using DCMS.Application.Features.Appointments.Interfaces;
using DCMS.Domain.Features.Appointments;
using Moq;
using Xunit;

namespace DCMS.UnitTests.Features.Appointments;

public class AppointmentServiceTests
{
    private readonly Mock<IAppointmentService> _mockAppointmentService;
    private readonly AppointmentService _appointmentService;
    private readonly Mock<DcmsDbContext> _mockDbContext;

    public AppointmentServiceTests()
    {
        _mockDbContext = new Mock<DcmsDbContext>();
        _appointmentService = new AppointmentService(_mockDbContext.Object);
    }

    [Fact]
    public async Task CreateAppointmentAsync_Should_ReturnAppointmentId_When_TimeSlotIsAvailable()
    {
        // Arrange
        var startTime = new DateTime(2026, 10, 20, 14, 0, 0);
        var durationInMinutes = 30;
        var service = "Cleaning";
        var reason = "Regular checkup";
        var source = "phone";
        var patientId = 1;
        var relativeId = 2;
        var dentistId = 3;
        var branchId = 1;
        var chairId = 1;

        _mockDbContext.Setup(x => x.Appointments.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()))
            .Returns((Appointment appointment, CancellationToken token) => 
            {
                appointment.Id = 1; // Simulate ID generation
                return ValueTask.CompletedTask;
            });

        _mockDbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Mock the IsTimeSlotAvailableAsync method to return true
        var isTimeSlotAvailableMock = new Mock<IAppointmentService>();
        isTimeSlotAvailableMock.Setup(x => x.IsTimeSlotAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _appointmentService.CreateAppointmentAsync(
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

        // Assert
        Assert.Equal(1, result);
        _mockDbContext.Verify(x => x.Appointments.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDbContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAppointmentAsync_Should_ThrowException_When_TimeSlotIsNotAvailable()
    {
        // Arrange
        var startTime = new DateTime(2026, 10, 20, 14, 0, 0);
        var durationInMinutes = 30;
        var service = "Cleaning";
        var reason = "Regular checkup";
        var source = "phone";
        var patientId = 1;
        var relativeId = 2;
        var dentistId = 3;
        var branchId = 1;
        var chairId = 1;

        // Mock the IsTimeSlotAvailableAsync method to return false
        var isTimeSlotAvailableMock = new Mock<IAppointmentService>();
        isTimeSlotAvailableMock.Setup(x => x.IsTimeSlotAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _appointmentService.CreateAppointmentAsync(
                startTime,
                durationInMinutes,
                service,
                reason,
                source,
                patientId,
                relativeId,
                dentistId,
                branchId,
                chairId));
    }

    [Fact]
    public async Task GetAppointmentByIdAsync_Should_ReturnAppointmentDetailDto_When_AppointmentExists()
    {
        // Arrange
        var appointmentId = 1;
        var expectedAppointment = new Appointment
        {
            Id = appointmentId,
            StartTime = new DateTime(2026, 10, 20, 14, 0, 0),
            EndTime = new DateTime(2026, 10, 20, 14, 30, 0),
            DurationInMinutes = 30,
            Service = "Cleaning",
            Reason = "Regular checkup",
            Source = "phone",
            PatientId = 1,
            RelativeId = 2,
            DentistId = 3,
            BranchId = 1,
            ChairId = 1,
            Status = "Scheduled",
            IsAttendanceConfirmed = false,
            Version = 1
        };

        _mockDbContext.Setup(x => x.Appointments.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAppointment);

        // Act
        var result = await _appointmentService.GetAppointmentByIdAsync(appointmentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(appointmentId, result.Id);
        Assert.Equal("Cleaning", result.Service);
        Assert.Equal("Regular checkup", result.Reason);
        Assert.Equal("phone", result.Source);
    }

    [Fact]
    public async Task GetAppointmentByIdAsync_Should_ReturnNull_When_AppointmentDoesNotExist()
    {
        // Arrange
        var appointmentId = 999; // Non-existent ID

        _mockDbContext.Setup(x => x.Appointments.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);

        // Act
        var result = await _appointmentService.GetAppointmentByIdAsync(appointmentId);

        // Assert
        Assert.Null(result);
    }
}