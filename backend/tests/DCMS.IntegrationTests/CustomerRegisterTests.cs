using DCMS.Application.Features.Authentication;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Customer self-registration: POST /api/auth/register (web and mobile "Đăng ký").</summary>
[Collection(ApiCollection.Name)]
public sealed class CustomerRegisterTests(ApiHost host)
{
    private const string Guest = "Anonymous visitor, not signed in.";

    private static string NewPhone() => "09" + Random.Shared.Next(10000000, 99999999);
    private static RegisterRequest Body(string? name = "Lê Văn Nam", string? phone = null, string? email = null, string? password = "Abcdef1!x", string? confirm = null) =>
        new(name, phone ?? NewPhone(), email ?? Guid.NewGuid().ToString("N")[..10] + "@gmail.com", password, confirm ?? password);

    [Fact, Case("REG-01", "A guest registers a customer account with valid data", Guest,
        "Step 1: Call POST /api/auth/register with full name, phone number, email, password and the same confirm password.",
        "Step 2: Read the account back from the database.")]
    public async Task Guest_registers_an_account()
    {
        var phone = NewPhone();
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(phone: phone));

        Assert.Equal(201, response.Status);
        var stored = await host.QueryAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.PhoneNumber == phone));
        Assert.Equal("Public", stored.AccountType);
        Assert.Equal(["Guest"], stored.AssignedRoles);
    }

    [Fact, Case("REG-02", "A phone number that is already registered is rejected", Guest + " An account with the phone number already exists.",
        "Step 1: Register an account.",
        "Step 2: Call POST /api/auth/register again with the same phone number and a different email.")]
    public async Task Duplicate_phone_is_rejected()
    {
        var phone = NewPhone();
        var client = await host.ClientAsync();
        Assert.Equal(201, (await client.PostAsync("/api/auth/register", Body(phone: phone))).Status);

        var response = await client.PostAsync("/api/auth/register", Body(phone: phone));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("REG-03", "An email that is already registered is rejected", Guest + " An account with the email already exists.",
        "Step 1: Register an account.",
        "Step 2: Call POST /api/auth/register again with the same email and a different phone number.")]
    public async Task Duplicate_email_is_rejected()
    {
        var email = Guid.NewGuid().ToString("N")[..10] + "@gmail.com";
        var client = await host.ClientAsync();
        Assert.Equal(201, (await client.PostAsync("/api/auth/register", Body(email: email))).Status);

        var response = await client.PostAsync("/api/auth/register", Body(email: email));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("REG-04", "A full name left empty is rejected", Guest,
        "Step 1: Call POST /api/auth/register with FullName = \"\".")]
    public async Task Empty_name_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(name: ""));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("REG-05", "A phone number with 5 digits is rejected", Guest,
        "Step 1: Call POST /api/auth/register with PhoneNumber = \"12345\".")]
    public async Task Short_phone_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(phone: "12345"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("REG-06", "An email without @ is rejected", Guest,
        "Step 1: Call POST /api/auth/register with Email = \"nam-gmail.com\".")]
    public async Task Email_without_at_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(email: "nam-gmail.com"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("REG-07", "A confirm password that does not match is rejected", Guest,
        "Step 1: Call POST /api/auth/register with Password = \"Abcdef1!x\" and ConfirmPassword = \"Abcdef1!y\".")]
    public async Task Confirm_password_mismatch_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(password: "Abcdef1!x", confirm: "Abcdef1!y"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("REG-08", "A password that is too weak is rejected", Guest,
        "Step 1: Call POST /api/auth/register with Password = \"abc\".")]
    public async Task Weak_password_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/auth/register", Body(password: "abc"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("REG-09", "The registered customer can sign in with the new password", Guest,
        "Step 1: Register an account.",
        "Step 2: Call POST /api/auth/sign-in with the phone number and the password.")]
    public async Task Registered_customer_can_sign_in()
    {
        var phone = NewPhone();
        var client = await host.ClientAsync();
        Assert.Equal(201, (await client.PostAsync("/api/auth/register", Body(phone: phone, password: "Abcdef1!x"))).Status);

        var response = await client.PostAsync("/api/auth/sign-in", new SignInRequest(phone, "Abcdef1!x"));

        Assert.Equal(200, response.Status);
    }
}
