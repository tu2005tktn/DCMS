using DCMS.Application.Features.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>System settings catalogs: lab work types, material groups and materials (button "Thêm mới").</summary>
[Collection(ApiCollection.Name)]
public sealed class CatalogTests(ApiHost host)
{
    private const string LabRoot = "/api/system-settings/settings/catalogs/lab-work-types";
    private const string Medicines = "/api/system-settings/settings/catalogs/medicines";
    private const string Groups = "/api/system-settings/material-categories";
    private const string Materials = "/api/system-settings/materials";
    private const string Admin = "Clinic owner owner.demo is signed in.";

    private static string Suffix() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    // ---- lab work types ----

    private static CreateSupplyRequest<LabWorkTypeInput> Lab(string? code = null, string? name = null, string? unit = "Tooth", long? price = 1500000, string? warranty = null) =>
        new(Guid.NewGuid(), new LabWorkTypeInput(code ?? "LW-" + Suffix(), name ?? "Mão răng sứ " + Suffix(), null, unit, price, warranty));
    private static CreateSupplyRequest<MedicineInput> Medicine() =>
        new(Guid.NewGuid(), new MedicineInput("MED-" + Suffix(), "SYNTHETIC thuốc kê đơn " + Suffix(), null, null, null, "viên", null, null, 0, true, false));

    [Fact, Case("MED-01", "A dentist can add a medicine while prescribing without catalog-management rights",
        "Nha sĩ (dentist.demo) đã đăng nhập.",
        "Step 1: Sign in as dentist.demo without an access-group assignment.",
        "Step 2: Create a prescription-eligible medicine through the shared catalog API.",
        "Step 3: Verify the medicine was created and the dentist cannot edit catalog entries.")]
    public async Task Dentist_can_add_a_prescribable_medicine_but_cannot_manage_catalog()
    {
        var client = await host.ClientAsync("dentist.demo");
        var request = Medicine();

        var response = await client.PostAsync(Medicines, request);

        Assert.Equal(201, response.Status);
        var created = response.Json<MedicineDto>();
        Assert.Equal(request.Entry!.Code, created.Code);
        Assert.True(created.PrescriptionEligible);
        var update = new UpdateSupplyRequest<MedicineInput>(created.Version, Guid.NewGuid(), "SYNTHETIC preserve catalog", request.Entry);
        Assert.Equal(403, (await client.PutAsync($"{Medicines}/{created.Id}", update)).Status);
    }

    [Fact, Case("LWT-01", "A clinic owner creates a lab work type", Admin,
        "Step 1: Sign in as owner.demo.",
        "Step 2: Call POST /api/system-settings/settings/catalogs/lab-work-types with code, name, unit and reference price.",
        "Step 3: Read the record back from the database.")]
    public async Task Admin_creates_a_lab_work_type()
    {
        var body = Lab();
        var client = await host.ClientAsync("owner.demo");

        var response = await client.PostAsync(LabRoot, body);

        Assert.Equal(201, response.Status);
        var stored = await host.QueryAsync(db => db.LabWorkTypes.AsNoTracking().SingleAsync(x => x.Code == body.Entry!.Code));
        Assert.Equal(body.Entry!.Name, stored.Name);
    }

    [Fact, Case("LWT-02", "A code left empty is rejected", Admin, "Step 1: Call POST .../lab-work-types with Code = \"\".")]
    public async Task Empty_code_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(LabRoot, Lab(code: ""));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("LWT-03", "A code with a space is rejected", Admin, "Step 1: Call POST .../lab-work-types with Code = \"LW 01\".")]
    public async Task Code_with_space_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(LabRoot, Lab(code: "LW 01"));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("LWT-04", "A name left empty is rejected", Admin, "Step 1: Call POST .../lab-work-types with Name = \"\".")]
    public async Task Empty_name_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(LabRoot, Lab(name: ""));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("LWT-05", "A unit that was not chosen is rejected", Admin, "Step 1: Call POST .../lab-work-types with Unit empty.")]
    public async Task Missing_unit_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(LabRoot, Lab(unit: null));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("LWT-06", "A negative reference price is rejected", Admin, "Step 1: Call POST .../lab-work-types with ReferencePrice = -1.")]
    public async Task Negative_price_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(LabRoot, Lab(price: -1));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("LWT-07", "A code that already exists is rejected", Admin + " A lab work type with the code exists.",
        "Step 1: Create a lab work type.",
        "Step 2: Call POST .../lab-work-types again with the same code (lower case) and a different name.")]
    public async Task Duplicate_code_is_rejected()
    {
        var first = Lab();
        var client = await host.ClientAsync("owner.demo");
        Assert.Equal(201, (await client.PostAsync(LabRoot, first)).Status);

        var response = await client.PostAsync(LabRoot, Lab(code: first.Entry!.Code!.ToLowerInvariant()));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("LWT-08", "A clinic assistant cannot create a lab work type", "Clinic assistant assistant.demo is signed in.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST .../lab-work-types with valid data.")]
    public async Task Assistant_is_rejected_for_lab_work_types()
    {
        var client = await host.ClientAsync("assistant.demo");
        var response = await client.PostAsync(LabRoot, Lab());
        Assert.Equal(403, response.Status);
    }

    // ---- material groups ----

    [Fact, Case("CAT-01", "A clinic owner creates a material group", Admin,
        "Step 1: Sign in as owner.demo.",
        "Step 2: Call POST /api/system-settings/material-categories with a group name.",
        "Step 3: Read the group back from the database.")]
    public async Task Admin_creates_a_group()
    {
        var name = "Vật liệu trám " + Suffix();
        var client = await host.ClientAsync("owner.demo");

        var response = await client.PostAsync(Groups, new CreateCategoryRequest(name, Guid.NewGuid()));

        Assert.Equal(201, response.Status);
        Assert.Equal(1, await host.QueryAsync(db => db.MaterialCategories.CountAsync(x => x.Name == name)));
    }

    [Fact, Case("CAT-02", "A group name left empty is rejected", Admin, "Step 1: Call POST .../material-categories with Name = \"\".")]
    public async Task Empty_group_name_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(Groups, new CreateCategoryRequest("", Guid.NewGuid()));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("CAT-03", "A group name of 161 characters is rejected", Admin, "Step 1: Call POST .../material-categories with a 161-character Name.")]
    public async Task Long_group_name_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(Groups, new CreateCategoryRequest(new string('A', 161), Guid.NewGuid()));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("CAT-04", "A group name that already exists is rejected", Admin + " A group with the name exists.",
        "Step 1: Create a group.",
        "Step 2: Call POST .../material-categories again with the same name in lower case.")]
    public async Task Duplicate_group_name_is_rejected()
    {
        var name = "Dụng cụ " + Suffix();
        var client = await host.ClientAsync("owner.demo");
        Assert.Equal(201, (await client.PostAsync(Groups, new CreateCategoryRequest(name, Guid.NewGuid()))).Status);

        var response = await client.PostAsync(Groups, new CreateCategoryRequest(name.ToLowerInvariant(), Guid.NewGuid()));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("CAT-05", "A clinic assistant cannot create a material group", "Clinic assistant assistant.demo is signed in.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST .../material-categories with a valid name.")]
    public async Task Assistant_is_rejected_for_groups()
    {
        var client = await host.ClientAsync("assistant.demo");
        var response = await client.PostAsync(Groups, new CreateCategoryRequest("Nhóm " + Suffix(), Guid.NewGuid()));
        Assert.Equal(403, response.Status);
    }

    // ---- materials ----

    private async Task<Guid> NewGroupAsync(ApiClient client) =>
        (await client.PostAsync(Groups, new CreateCategoryRequest("Nhóm " + Suffix(), Guid.NewGuid()))).Json<MaterialCategoryDto>().Id;

    [Fact, Case("MAT-01", "A clinic owner creates a material", Admin + " A material group exists.",
        "Step 1: Create a material group.",
        "Step 2: Call POST /api/system-settings/materials with name, group and usage unit.",
        "Step 3: Read the material back from the database.")]
    public async Task Admin_creates_a_material()
    {
        var client = await host.ClientAsync("owner.demo");
        var group = await NewGroupAsync(client);
        var name = "Composite " + Suffix();

        var response = await client.PostAsync(Materials, new CreateMaterialRequest(name, group, Guid.NewGuid(), "g"));

        Assert.Equal(201, response.Status);
        var stored = await host.QueryAsync(db => db.MaterialCatalogItems.AsNoTracking().SingleAsync(x => x.Name == name));
        Assert.Equal(group, stored.CategoryId);
    }

    [Fact, Case("MAT-02", "A material name left empty is rejected", Admin + " A material group exists.", "Step 1: Call POST .../materials with Name = \"\".")]
    public async Task Empty_material_name_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(Materials, new CreateMaterialRequest("", await NewGroupAsync(client), Guid.NewGuid()));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("MAT-03", "A usage unit of 41 characters is rejected", Admin + " A material group exists.", "Step 1: Call POST .../materials with a 41-character UsageUnit.")]
    public async Task Long_usage_unit_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(Materials, new CreateMaterialRequest("Composite " + Suffix(), await NewGroupAsync(client), Guid.NewGuid(), new string('g', 41)));
        Assert.Equal(400, response.Status);
    }

    [Fact, Case("MAT-04", "A material group that does not exist is rejected", Admin, "Step 1: Call POST .../materials with a random CategoryId.")]
    public async Task Unknown_group_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var response = await client.PostAsync(Materials, new CreateMaterialRequest("Composite " + Suffix(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(404, response.Status);
    }

    [Fact, Case("MAT-05", "A material name that already exists in the group is rejected", Admin + " A material exists in the group.",
        "Step 1: Create a material.",
        "Step 2: Call POST .../materials again with the same name in lower case and the same group.")]
    public async Task Duplicate_material_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var group = await NewGroupAsync(client);
        var name = "Composite " + Suffix();
        Assert.Equal(201, (await client.PostAsync(Materials, new CreateMaterialRequest(name, group, Guid.NewGuid()))).Status);

        var response = await client.PostAsync(Materials, new CreateMaterialRequest(name.ToLowerInvariant(), group, Guid.NewGuid()));

        Assert.Equal(409, response.Status);
    }

    [Fact, Case("MAT-06", "A clinic assistant cannot create a material", "Clinic assistant assistant.demo is signed in.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST .../materials with valid data.")]
    public async Task Assistant_is_rejected_for_materials()
    {
        var client = await host.ClientAsync("assistant.demo");
        var response = await client.PostAsync(Materials, new CreateMaterialRequest("Composite " + Suffix(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(403, response.Status);
    }
}


