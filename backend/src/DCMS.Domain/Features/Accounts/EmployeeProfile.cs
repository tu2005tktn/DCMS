namespace DCMS.Domain.Features.Accounts;

public sealed class EmployeeProfile
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
}
