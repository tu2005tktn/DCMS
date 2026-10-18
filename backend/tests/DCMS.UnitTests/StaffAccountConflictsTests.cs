using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using Xunit;

namespace DCMS.UnitTests;

public class StaffAccountConflictsTests
{
    [Theory]
    [InlineData("UserNameIndex", "userName")]
    [InlineData("IX_EMPLOYEE_PROFILE_EmployeeCode", "employeeCode")]
    [InlineData("EmailIndex", "email")]
    [InlineData("IX_USER_ACCOUNT_NormalizedPhone", "phone")]
    public void KnownDatabaseConflictsIdentifyOnlyTheField(string constraint, string field)
    {
        var response = StaffAccountConflicts.ForConstraint(constraint);
        Assert.Equal("MSG139", response.Code);
        Assert.Equal(field, response.Field);
        Assert.DoesNotContain(constraint, response.Message);
    }

    [Fact]
    public void GenericAndPublicMessagesDoNotAddAnEmptyField()
    {
        Assert.Equal(SystemMessages.Get("MSG139"), StaffAccountConflicts.ForConstraint("unknown"));
        var json = JsonSerializer.Serialize(SystemMessages.Get("MSG139"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("field", json);
    }
}
