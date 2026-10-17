using System;

namespace DCMS.Application.Features.Appointments.Dtos;

public class AppointmentDetailDto : AppointmentDto
{
    // Additional detailed information that might be needed for edit forms
    // but still excluding clinical/financial information as per requirements
    
    // Could include additional metadata, audit info, etc.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int UpdatedBy { get; set; }
}