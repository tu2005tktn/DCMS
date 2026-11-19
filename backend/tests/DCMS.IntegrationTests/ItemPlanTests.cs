using DCMS.Application.Features.Treatment;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>
/// Treatment item plans (screen "Kế hoạch điều trị"): the plan itself (POST /api/treatment-templates/item-plans)
/// and the rows added to it (button "+ Thêm hạng mục", POST .../{id}/items).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ItemPlanTests(ApiHost host)
{
    private const string Root = "/api/treatment-templates/item-plans";
    private const string Dentist = "Nha sĩ (dentist.demo) đã đăng nhập, làm việc tại chi nhánh A.";

    private async Task<ItemPlanDto> NewPlanAsync(ApiClient client) =>
        (await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, "Kế hoạch " + Guid.NewGuid().ToString("N")[..8], Guid.NewGuid()))).Json<ItemPlanDto>();

    private async Task<int> ServiceIdAsync(ApiClient client) =>
        (await client.GetAsync($"{Root}/options?branchId={host.BranchA}")).Json<ItemPlanOptions>().Services.First().Id;

    private static ItemPlanRowInput Row(int version, int serviceId, string[]? teeth = null, int? minutes = 30, decimal cost = 300000, string? note = null) =>
        new(null, teeth ?? ["25"], serviceId, minutes, note, null, null, cost, version, Guid.NewGuid());

    // ---- the plan ----

    [Fact, Case("PLN-01", "A dentist creates a treatment plan", Dentist,
        "Step 1: Sign in as dentist.demo.",
        "Step 2: Call POST /api/treatment-templates/item-plans with the branch and a plan name.",
        "Step 3: Read the plan back from the database.")]
    public async Task Dentist_creates_a_plan()
    {
        var name = "Cạo vôi răng " + Guid.NewGuid().ToString("N")[..6];
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, name, Guid.NewGuid()));

        Assert.Equal(200, response.Status);
        var stored = await host.QueryAsync(db => db.TreatmentTemplates.AsNoTracking().SingleAsync(x => x.Name == name));
        Assert.Equal("ItemPlan", stored.DefinitionKind);
        Assert.Equal(host.BranchA, stored.BranchId);
    }

    [Fact, Case("PLN-02", "Sending the same request twice creates one plan only", Dentist,
        "Step 1: Call POST /api/treatment-templates/item-plans with a request key.",
        "Step 2: Call it again with the same request key and the same data.")]
    public async Task Same_request_key_creates_one_plan()
    {
        var request = new ItemPlanCreate(host.BranchA, "Trám răng " + Guid.NewGuid().ToString("N")[..6], Guid.NewGuid());
        var client = await host.ClientAsync("dentist.demo");
        var first = await client.PostAsync(Root, request);

        var second = await client.PostAsync(Root, request);

        Assert.Equal(first.Json<ItemPlanDto>().Id, second.Json<ItemPlanDto>().Id);
        Assert.Equal(1, await host.QueryAsync(db => db.TreatmentTemplates.CountAsync(x => x.Name == request.Name)));
    }

    [Fact, Case("PLN-03", "A plan name left empty is rejected", Dentist,
        "Step 1: Call POST /api/treatment-templates/item-plans with Name = \"\".")]
    public async Task Empty_name_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, "", Guid.NewGuid()));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("PLN-04", "A plan name of 161 characters is rejected", Dentist,
        "Step 1: Call POST /api/treatment-templates/item-plans with a 161-character Name.")]
    public async Task Long_name_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, new string('A', 161), Guid.NewGuid()));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("PLN-05", "A dentist cannot create a plan in a branch they do not work in", "Nha sĩ (dentist.demo) đã đăng nhập, chỉ làm việc tại chi nhánh A.",
        "Step 1: Call POST /api/treatment-templates/item-plans with the id of branch B.")]
    public async Task Other_branch_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchB, "Kế hoạch chi nhánh B", Guid.NewGuid()));

        Assert.Equal(403, response.Status);
        Assert.Equal("MSG06", response.Code);
    }

    [Fact, Case("PLN-06", "A clinic assistant cannot create a plan", "Trợ lý (assistant.demo) đã đăng nhập.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST /api/treatment-templates/item-plans.")]
    public async Task Assistant_is_rejected()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, "Kế hoạch trợ lý", Guid.NewGuid()));

        Assert.Equal(403, response.Status);
    }

    [Fact, Case("PLN-07", "A request without a token is rejected", "No user is signed in.",
        "Step 1: Call POST /api/treatment-templates/item-plans without an Authorization header.")]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync(Root, new ItemPlanCreate(host.BranchA, "Kế hoạch", Guid.NewGuid()));

        Assert.Equal(401, response.Status);
    }

    // ---- the rows ("+ Thêm hạng mục") ----

    [Fact, Case("ROW-01", "A dentist adds an item to a plan", Dentist + " The plan exists.",
        "Step 1: Create a plan.",
        "Step 2: Read the services of the branch (GET .../options).",
        "Step 3: Call POST .../{id}/items with teeth, service, estimated minutes and cost.")]
    public async Task Dentist_adds_an_item()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);
        var service = await ServiceIdAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, service));

        Assert.Equal(200, response.Status);
        var saved = response.Json<ItemPlanDto>();
        Assert.Equal(1, saved.ItemCount);
        Assert.Equal(300000, saved.TotalCostVnd);
    }

    [Fact, Case("ROW-02", "Sending the same item twice adds one item only", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with a request key.",
        "Step 2: Call it again with the same request key and the same data.")]
    public async Task Same_request_key_adds_one_item()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);
        var row = Row(plan.Version, await ServiceIdAsync(client));
        Assert.Equal(200, (await client.PostAsync($"{Root}/{plan.Id}/items", row)).Status);

        var second = await client.PostAsync($"{Root}/{plan.Id}/items", row);

        Assert.Equal(200, second.Status);
        Assert.Equal(1, await host.QueryAsync(db => db.TreatmentTemplateItems.CountAsync(x => x.TemplateId == plan.Id)));
    }

    [Fact, Case("ROW-03", "A tooth code that does not exist is rejected", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with Teeth = [\"99\"].")]
    public async Task Invalid_tooth_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, await ServiceIdAsync(client), teeth: ["99"]));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("ROW-04", "An estimated time of 481 minutes is rejected", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with EstimatedMinutes = 481.")]
    public async Task Too_long_estimate_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, await ServiceIdAsync(client), minutes: 481));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("ROW-05", "A negative cost is rejected", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with CostVnd = -1.")]
    public async Task Negative_cost_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, await ServiceIdAsync(client), cost: -1));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("ROW-06", "A note of 1001 characters is rejected", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with a 1001-character Note.")]
    public async Task Long_note_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, await ServiceIdAsync(client), note: new string('a', 1001)));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("ROW-07", "A service that was not selected is rejected", Dentist + " The plan exists.",
        "Step 1: Call POST .../{id}/items with ServiceId = 0.")]
    public async Task Missing_service_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, 0));

        Assert.Equal(400, response.Status);
        Assert.Equal("ITEM_PLAN_VALIDATION", response.Code);
    }

    [Fact, Case("ROW-08", "An item sent with an old plan version is rejected", Dentist + " The plan exists and was changed after it was loaded.",
        "Step 1: Create a plan (version 1) and add an item (the version becomes 2).",
        "Step 2: Call POST .../{id}/items again with ExpectedVersion = 1.")]
    public async Task Old_version_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(client);
        var service = await ServiceIdAsync(client);
        Assert.Equal(200, (await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, service))).Status);

        var response = await client.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, service));

        Assert.Equal(409, response.Status);
        Assert.Equal("MSG111", response.Code);
    }

    [Fact, Case("ROW-09", "A plan that does not exist is rejected", Dentist,
        "Step 1: Call POST .../{id}/items with a random plan id.")]
    public async Task Unknown_plan_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync($"{Root}/{Guid.NewGuid()}/items", Row(1, 1));

        Assert.Equal(404, response.Status);
        Assert.Equal("MSG113", response.Code);
    }

    [Fact, Case("ROW-10", "A clinic assistant cannot add an item", "Trợ lý (assistant.demo) đã đăng nhập; plan exists.",
        "Step 1: Create a plan as the dentist.",
        "Step 2: Sign in as assistant.demo and call POST .../{id}/items.")]
    public async Task Assistant_is_rejected_for_items()
    {
        var dentist = await host.ClientAsync("dentist.demo");
        var plan = await NewPlanAsync(dentist);
        var assistant = await host.ClientAsync("assistant.demo");

        var response = await assistant.PostAsync($"{Root}/{plan.Id}/items", Row(plan.Version, await ServiceIdAsync(dentist)));

        Assert.Equal(403, response.Status);
    }
}
