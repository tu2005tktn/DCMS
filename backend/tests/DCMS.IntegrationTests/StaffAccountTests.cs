using DCMS.Application.Features.Accounts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Staff account management: POST /api/accounts (screen "Tài khoản nhân viên").</summary>
[Collection(ApiCollection.Name)]
public sealed class StaffAccountTests(ApiHost host)
{
    private const string Admin = "An administrator (Admin) is signed in.";

    private static CreateStaffRequest Body(string userName, string employeeCode, string password = "Abcdef1!x", string[]? roles = null) =>
        new(userName, password, "Nguyễn Văn An", employeeCode, Guid.NewGuid().ToString("N")[..10] + "@dcms.vn", "09" + Random.Shared.Next(10000000, 99999999), roles ?? ["ClinicAssistant"]);

    [Fact, Case("ACC-01", "Admin creates a staff account with valid data", Admin,
        "Step 1: Sign in as admin.demo.",
        "Step 2: Call POST /api/accounts with a new user name, password, full name, employee code, email, phone and role ClinicAssistant.",
        "Step 3: Read the account back from the database.")]
    public async Task Admin_creates_a_staff_account()
    {
        var name = "nv." + Guid.NewGuid().ToString("N")[..8];
        var client = await host.ClientAsync("admin.demo");

        var response = await client.PostAsync("/api/accounts", Body(name, "NV" + name[3..]));

        Assert.Equal(201, response.Status);
        var stored = await host.QueryAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.UserName == name));
        Assert.Equal("Staff", stored.AccountType);
        Assert.Equal(["ClinicAssistant"], stored.AssignedRoles);
    }

    [Fact, Case("ACC-02", "A user name that already exists is rejected", Admin + " Account \"nv.trung\" already exists.",
        "Step 1: Create the account \"nv.trung\".",
        "Step 2: Call POST /api/accounts again with the same user name and a different employee code.")]
    public async Task Duplicate_user_name_is_rejected()
    {
        var client = await host.ClientAsync("admin.demo");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = "nv.trung" + suffix;
        Assert.Equal(201, (await client.PostAsync("/api/accounts", Body(name, "NVA" + suffix))).Status);

        var response = await client.PostAsync("/api/accounts", Body(name, "NVB" + suffix));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("ACC-03", "An employee code that already exists is rejected", Admin + " An account with employee code \"NVDUP\" already exists.",
        "Step 1: Create a staff account with employee code NVDUP.",
        "Step 2: Call POST /api/accounts with a new user name and the same employee code.")]
    public async Task Duplicate_employee_code_is_rejected()
    {
        var client = await host.ClientAsync("admin.demo");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(201, (await client.PostAsync("/api/accounts", Body("nv.a" + suffix, "DUP" + suffix))).Status);

        var response = await client.PostAsync("/api/accounts", Body("nv.b" + suffix, "DUP" + suffix));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("ACC-04", "A user name with a space is rejected", Admin,
        "Step 1: Call POST /api/accounts with UserName = \"nv an\".")]
    public async Task User_name_with_a_space_is_rejected()
    {
        var client = await host.ClientAsync("admin.demo");

        var response = await client.PostAsync("/api/accounts", Body("nv an", "NV" + Guid.NewGuid().ToString("N")[..8]));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("ACC-05", "A staff member who is not an admin cannot create accounts", "ClinicAssistant (assistant.demo) is signed in.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST /api/accounts with valid data.")]
    public async Task Non_admin_cannot_create_accounts()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/accounts", Body("nv." + Guid.NewGuid().ToString("N")[..8], "NV" + Guid.NewGuid().ToString("N")[..8]));

        Assert.Equal(403, response.Status);
    }

    [Fact, Case("ACC-06", "A request without a token is rejected", "No user is signed in.",
        "Step 1: Call POST /api/accounts without an Authorization header.")]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/accounts", Body("nv.anon", "NVANON"));

        Assert.Equal(401, response.Status);
    }
}
