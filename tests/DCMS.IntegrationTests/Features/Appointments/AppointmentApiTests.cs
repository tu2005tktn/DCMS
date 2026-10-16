using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DCMS.Application.Features.Appointments.Dtos;
using Xunit;

namespace DCMS.IntegrationTests.Features.Appointments;

public class AppointmentApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AppointmentApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAppointmentsEndpoint_Should_ReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/appointments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentByIdEndpoint_Should_ReturnNotFound_ForNonExistentId()
    {
        // Act
        var response = await _client.GetAsync("/api/appointments/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointmentEndpoint_Should_ReturnBadRequest_ForInvalidData()
    {
        // Arrange
        var invalidAppointment = new
        {
            // Missing required fields
            startTime = DateTime.Now,
            // durationInMinutes, service, reason, source, patientId, dentistId, branchId, chairId are missing
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/appointments", invalidAppointment);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Expecting validation error
    }
}