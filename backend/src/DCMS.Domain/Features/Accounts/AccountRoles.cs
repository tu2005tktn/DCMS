namespace DCMS.Domain.Features.Accounts;

public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string Dentist = "Dentist";
    public const string ClinicAssistant = "ClinicAssistant";
    public const string ClinicOwner = "ClinicOwner";
    public const string Guest = "Guest";
    public const string Patient = "Patient";
    public static readonly string[] Staff = [Admin, Dentist, ClinicAssistant, ClinicOwner];
    public static readonly string[] All = [.. Staff, Guest, Patient];

    public static string[] Effective(IEnumerable<string> assigned)
    {
        var roles = assigned.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains(ClinicOwner)) roles.Add(Dentist);
        if (roles.Contains(Patient)) roles.Add(Guest);
        return roles.Order(StringComparer.Ordinal).ToArray();
    }
}
