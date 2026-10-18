namespace DCMS.Domain.Features.Authentication;

public enum RecoveryChannel
{
    Unknown = 0,
    Email = 1,
    Phone = 2,
    Link = 3
}

public enum RecoveryResult
{
    Succeeded = 0,
    InvalidProof = 1,
    ExpiredProof = 2,
    UsedProof = 3,
    OverAttemptLimit = 4,
    OverResendLimit = 5,
    PolicyViolation = 6,
    DeliveryFailure = 7,
    Blocked = 8,
    ServiceError = 9
}