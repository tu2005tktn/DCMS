using DCMS.Application.Features.FamilyMembers;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Customer family members: POST /api/me/family-members (screen "Người thân của tôi").</summary>
[Collection(ApiCollection.Name)]
public sealed class FamilyMemberTests(ApiHost host)
{
    private const string Customer = "Customer guest.demo is signed in.";

    private static CreateFamilyMember Body(string name = "Nguyễn Thị Lan", string relationship = "Mother", Guid? key = null, DateOnly? birth = null,
        string? gender = "FEMALE", string? phone = "0912345678") =>
        new(name, relationship, birth ?? new DateOnly(1965, 3, 12), key ?? Guid.NewGuid(), gender, phone);

    [Fact, Case("FAM-01", "A customer adds a family member", Customer,
        "Step 1: Sign in as guest.demo.",
        "Step 2: Call POST /api/me/family-members with full name, relationship and birth date.")]
    public async Task Customer_adds_a_family_member()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body());

        Assert.Equal(201, response.Status);
    }

    [Fact, Case("FAM-02", "Sending the same request twice adds one family member only", Customer,
        "Step 1: Call POST /api/me/family-members with a request key.",
        "Step 2: Call it again with the same request key and the same data.")]
    public async Task Same_request_key_adds_one_member()
    {
        var body = Body(name: "Nguyễn Văn Bình " + Guid.NewGuid().ToString("N")[..6]);
        var client = await host.ClientAsync("guest.demo");
        var first = await client.PostAsync("/api/me/family-members", body);

        var second = await client.PostAsync("/api/me/family-members", body);

        Assert.Equal(201, first.Status);
        Assert.Contains(body.FullName, second.Body);
        var list = await client.GetAsync("/api/me/family-members?keyword=" + Uri.EscapeDataString(body.FullName));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(list.Body, "\"fullName\"").Count);
    }

    [Fact, Case("FAM-03", "A full name left empty is rejected", Customer,
        "Step 1: Call POST /api/me/family-members with FullName = \"\".")]
    public async Task Empty_name_is_rejected()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body(name: ""));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("FAM-04", "A relationship outside the allowed list is rejected", Customer,
        "Step 1: Call POST /api/me/family-members with Relationship = \"Friend\".")]
    public async Task Unknown_relationship_is_rejected()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body(relationship: "Friend"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("FAM-05", "A birth date in the future is rejected", Customer,
        "Step 1: Call POST /api/me/family-members with BirthDate = 2099-01-01.")]
    public async Task Future_birth_date_is_rejected()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body(birth: new DateOnly(2099, 1, 1)));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("FAM-08", "A gender left empty is rejected", Customer,
        "Step 1: Call POST /api/me/family-members with Gender empty.")]
    public async Task Missing_gender_is_rejected()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body(gender: null));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("FAM-09", "A contact phone with a wrong format is rejected", Customer,
        "Step 1: Call POST /api/me/family-members with ContactPhone = \"12345\".")]
    public async Task Wrong_contact_phone_is_rejected()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/me/family-members", Body(phone: "12345"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("FAM-06", "A staff account cannot use the customer family list", "Dentist (dentist.demo) is signed in.",
        "Step 1: Sign in as dentist.demo.",
        "Step 2: Call POST /api/me/family-members with valid data.")]
    public async Task Staff_account_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync("/api/me/family-members", Body());

        Assert.Equal(403, response.Status);
    }

    [Fact, Case("FAM-07", "A request without a token is rejected", "No user is signed in.",
        "Step 1: Call POST /api/me/family-members without an Authorization header.")]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/me/family-members", Body());

        Assert.Equal(401, response.Status);
    }
}
