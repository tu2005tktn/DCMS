using DCMS.Application.Features.SystemSettings;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>
/// S58 system settings: supply catalogs (medicines, providers, lab work types) and reference catalogs.
/// Docs: docs/specs/S58-SUPPLY-CATALOGS.md, docs/specs/S58-REFERENCE-CATALOGS.md, docs/testing/INTEGRATION-TEST-COVERAGE.vi.md.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SystemSettingsCatalogApiTests(ApiHost host)
{
    private const string Settings = "/api/system-settings/settings";
    private const string Medicines = Settings + "/catalogs/medicines";
    private const string Owner = "Signed in as the clinic owner owner.demo.";

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static MedicineInput Medicine(string code, string? name = null, long? price = 15000) =>
        new(code, name ?? "Paracetamol " + code, "Paracetamol", "500mg", "Viên nén", "viên", "Hộp 10 vỉ x 10 viên",
            "Uống sau ăn", price, true, false);

    private async Task<MedicineDto> CreateMedicineAsync(ApiClient client, string? code = null)
    {
        var response = await client.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), Medicine(code ?? Unique("TH"))));
        Assert.True(response.Status == 201, response.Body);
        return response.Json<MedicineDto>();
    }

    [Fact, Case("MED-01", "The clinic owner adds a new medicine to the catalog", Owner,
        "Step 1: Call POST /api/system-settings/settings/catalogs/medicines with code, name, active ingredient, unit and reference price.",
        "Step 2: Call GET /api/system-settings/settings/catalogs/medicines/{id}.")]
    public async Task Admin_creates_and_reads_a_medicine()
    {
        var client = await host.ClientAsync("owner.demo");
        var created = await CreateMedicineAsync(client);

        var read = await client.GetAsync($"{Medicines}/{created.Id}");

        Assert.Equal(200, read.Status);
        var dto = read.Json<MedicineDto>();
        Assert.Equal(created.Code, dto.Code);
        Assert.True(dto.Active);
        Assert.Equal(15000, dto.ReferencePrice);
    }

    [Fact, Case("MED-02", "The medicine list can be searched by keyword", Owner,
        "Step 1: Add a new medicine.", "Step 2: Call GET /api/system-settings/settings/catalogs/medicines?q={medicine code}.")]
    public async Task Medicine_list_filters_by_keyword()
    {
        var client = await host.ClientAsync("owner.demo");
        var created = await CreateMedicineAsync(client);

        var list = await client.GetAsync($"{Medicines}?q={created.Code}&status=Active&page=1&pageSize=20");

        Assert.Equal(200, list.Status);
        Assert.Contains(created.Code, list.Body);
    }

    [Fact, Case("MED-03", "Two medicines with the same code cannot be added", Owner,
        "Step 1: Add a medicine with code X.", "Step 2: Add another medicine with the same code X.")]
    public async Task Duplicate_medicine_code_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var created = await CreateMedicineAsync(client);

        var duplicate = await client.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), Medicine(created.Code, "Tên khác " + created.Code)));

        Assert.InRange(duplicate.Status, 400, 409);
        Assert.NotNull(duplicate.Message);
    }

    [Fact, Case("MED-04", "A medicine without a name or with a negative price is rejected", Owner,
        "Step 1: Call POST medicines with an empty name.", "Step 2: Call POST medicines with a negative reference price.")]
    public async Task Invalid_medicine_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");

        var noName = await client.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), Medicine(Unique("TH"), "")));
        var negative = await client.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), Medicine(Unique("TH"), price: -1)));
        var empty = await client.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), null));

        Assert.Equal(400, noName.Status);
        Assert.Equal(400, negative.Status);
        Assert.Equal(400, empty.Status);
    }

    [Fact, Case("MED-05", "Editing a medicine raises its version; an edit with a stale version is rejected", Owner,
        "Step 1: Add a medicine.", "Step 2: Call PUT medicines/{id} with the current ExpectedVersion and a new price.",
        "Step 3: Call PUT again with the old ExpectedVersion.", "Step 4: Call GET medicines/{id}/history.")]
    public async Task Medicine_update_uses_optimistic_version_and_keeps_history()
    {
        var client = await host.ClientAsync("owner.demo");
        var created = await CreateMedicineAsync(client);

        var updated = await client.PutAsync($"{Medicines}/{created.Id}",
            new UpdateSupplyRequest<MedicineInput>(created.Version, Guid.NewGuid(), "Cập nhật giá", Medicine(created.Code, created.Name, 20000)));
        var stale = await client.PutAsync($"{Medicines}/{created.Id}",
            new UpdateSupplyRequest<MedicineInput>(created.Version, Guid.NewGuid(), "Cập nhật lần 2", Medicine(created.Code, created.Name, 25000)));
        var history = await client.GetAsync($"{Medicines}/{created.Id}/history");

        Assert.Equal(200, updated.Status);
        Assert.Equal(20000, updated.Json<MedicineDto>().ReferencePrice);
        Assert.True(updated.Json<MedicineDto>().Version > created.Version);
        Assert.Equal(409, stale.Status);
        Assert.Equal(200, history.Status);
        Assert.Contains("Cập nhật giá", history.Body);
    }

    [Fact, Case("MED-06", "Deactivate and then reactivate a medicine", Owner,
        "Step 1: Add a medicine.", "Step 2: Call POST medicines/{id}/state-preview with Active = false.",
        "Step 3: Call POST medicines/{id}/state with Active = false.", "Step 4: Call POST state again with Active = true.")]
    public async Task Medicine_can_be_deactivated_and_restored()
    {
        var client = await host.ClientAsync("owner.demo");
        var created = await CreateMedicineAsync(client);

        var preview = await client.PostAsync($"{Medicines}/{created.Id}/state-preview", new ReferenceStatePreviewRequest(false));
        var off = await client.PostAsync($"{Medicines}/{created.Id}/state", new ReferenceStateRequest(created.Version, Guid.NewGuid(), false, "Hết hàng"));
        var offDto = off.Json<MedicineDto>();
        var on = await client.PostAsync($"{Medicines}/{created.Id}/state", new ReferenceStateRequest(offDto.Version, Guid.NewGuid(), true, "Có hàng lại"));

        Assert.Equal(200, preview.Status);
        Assert.Equal(200, off.Status);
        Assert.False(offDto.Active);
        Assert.Equal(200, on.Status);
        Assert.True(on.Json<MedicineDto>().Active);
    }

    [Fact, Case("MED-07", "Export the medicine catalog to an Excel file", Owner,
        "Step 1: Call GET /api/system-settings/settings/catalogs/medicines/export.")]
    public async Task Medicine_catalog_exports_to_excel()
    {
        var client = await host.ClientAsync("owner.demo");
        await CreateMedicineAsync(client);

        var export = await client.GetAsync($"{Settings}/catalogs/medicines/export?status=Active");

        Assert.Equal(200, export.Status);
        Assert.StartsWith("PK", export.Body); // .xlsx is a zip archive
    }

    [Fact, Case("MED-08", "Dentists and customers cannot manage the medicine catalog", "Signed in as dentist.demo or guest.demo, or not signed in.",
        "Step 1: The dentist calls POST medicines.", "Step 2: The customer calls GET medicines.", "Step 3: Call GET medicines without signing in.")]
    public async Task Medicine_catalog_requires_settings_permission()
    {
        var dentist = await host.ClientAsync("dentist.demo");
        var guest = await host.ClientAsync("guest.demo");
        var anonymous = await host.ClientAsync();

        var create = await dentist.PostAsync(Medicines, new CreateSupplyRequest<MedicineInput>(Guid.NewGuid(), Medicine(Unique("TH"))));
        var guestList = await guest.GetAsync(Medicines);
        var anonymousList = await anonymous.GetAsync(Medicines);

        Assert.Equal(403, create.Status);
        Assert.Equal(403, guestList.Status);
        Assert.Equal(401, anonymousList.Status);
    }

    [Fact, Case("PRV-01", "Add a provider and a lab work type linked to that provider", Owner,
        "Step 1: Call POST /api/system-settings/settings/catalogs/providers with type Lab.",
        "Step 2: Call POST /api/system-settings/settings/catalogs/lab-work-types with the new ProviderId.",
        "Step 3: Call GET providers filtered by type Lab.")]
    public async Task Provider_and_lab_work_type_are_created_together()
    {
        var client = await host.ClientAsync("owner.demo");
        var code = Unique("NCC");

        var provider = await client.PostAsync($"{Settings}/catalogs/providers", new CreateSupplyRequest<ProviderInput>(Guid.NewGuid(),
            new ProviderInput(code, "Labo " + code, ProviderTypes.Lab, "12 Lê Lợi, Hà Nội", "0901234567", "labo@example.com")));
        Assert.True(provider.Status == 201, provider.Body);
        var providerDto = provider.Json<ProviderDto>();
        var labWork = await client.PostAsync($"{Settings}/catalogs/lab-work-types", new CreateSupplyRequest<LabWorkTypeInput>(Guid.NewGuid(),
            new LabWorkTypeInput(Unique("LB"), "Răng sứ " + code, providerDto.Id, "Tooth", 1_500_000, "5 năm")));
        var list = await client.GetAsync($"{Settings}/catalogs/providers?type=Lab&q={code}");

        Assert.Equal(201, labWork.Status);
        Assert.Equal(providerDto.Id, labWork.Json<LabWorkTypeDto>().ProviderId);
        Assert.Equal(200, list.Status);
        Assert.Contains(code, list.Body);
    }

    [Fact, Case("PRV-02", "A provider with an invalid type or email is rejected", Owner,
        "Step 1: Call POST providers with an invalid type.", "Step 2: Call POST providers with a malformed email.")]
    public async Task Invalid_provider_is_rejected()
    {
        var client = await host.ClientAsync("owner.demo");

        var badType = await client.PostAsync($"{Settings}/catalogs/providers", new CreateSupplyRequest<ProviderInput>(Guid.NewGuid(),
            new ProviderInput(Unique("NCC"), "Nhà cung cấp lỗi", "Unknown", null, null, null)));
        var badEmail = await client.PostAsync($"{Settings}/catalogs/providers", new CreateSupplyRequest<ProviderInput>(Guid.NewGuid(),
            new ProviderInput(Unique("NCC"), "Nhà cung cấp lỗi 2", ProviderTypes.Supplier, null, null, "not-an-email")));

        Assert.Equal(400, badType.Status);
        Assert.Equal(400, badEmail.Status);
    }

    [Theory, Case("REF-01", "Each reference catalog can be opened", Owner,
        "Step 1: Call GET /api/system-settings/settings/catalogs/{kind} for each catalog kind.")]
    [InlineData(ReferenceKinds.Diagnoses)]
    [InlineData(ReferenceKinds.PatientSources)]
    [InlineData(ReferenceKinds.Occupations)]
    [InlineData(ReferenceKinds.IntakeHistory)]
    [InlineData(ReferenceKinds.CareTypes)]
    [InlineData(ReferenceKinds.ExpenseCategories)]
    [InlineData(ReferenceKinds.CustomerGroups)]
    public async Task Every_reference_catalog_can_be_listed(string kind)
    {
        var client = await host.ClientAsync("owner.demo");

        var list = await client.GetAsync($"{Settings}/catalogs/{kind}?page=1&pageSize=50");

        Assert.Equal(200, list.Status);
    }

    [Fact, Case("REF-02", "Add, edit, deactivate and view the history of a diagnosis item", Owner,
        "Step 1: Call POST catalogs/diagnoses.", "Step 2: Call PUT catalogs/diagnoses/{id} to change the description.",
        "Step 3: Call POST state-preview, then POST state with Active = false.", "Step 4: Call GET catalogs/diagnoses/{id}/history.")]
    public async Task Diagnosis_entry_full_lifecycle()
    {
        var client = await host.ClientAsync("owner.demo");
        var code = Unique("CD");
        var path = $"{Settings}/catalogs/{ReferenceKinds.Diagnoses}";

        var created = await client.PostAsync(path, new CreateReferenceRequest(Guid.NewGuid(), new ReferenceEntryInput(code, "Viêm lợi " + code, "Viêm lợi cấp")));
        Assert.True(created.Status == 201, created.Body);
        var entry = created.Json<ReferenceEntryDto>();
        var updated = await client.PutAsync($"{path}/{entry.Id}", new UpdateReferenceRequest(entry.Version, Guid.NewGuid(), "Bổ sung mô tả",
            new ReferenceEntryInput(code, entry.Name, "Viêm lợi cấp tính")));
        var updatedEntry = updated.Json<ReferenceEntryDto>();
        var preview = await client.PostAsync($"{path}/{entry.Id}/state-preview", new ReferenceStatePreviewRequest(false));
        var off = await client.PostAsync($"{path}/{entry.Id}/state", new ReferenceStateRequest(updatedEntry.Version, Guid.NewGuid(), false, "Không dùng nữa"));
        var read = await client.GetAsync($"{path}/{entry.Id}");
        var history = await client.GetAsync($"{path}/{entry.Id}/history");

        Assert.Equal(200, updated.Status);
        Assert.Equal("Viêm lợi cấp tính", updatedEntry.Description);
        Assert.Equal(200, preview.Status);
        Assert.Equal(200, off.Status);
        Assert.False(read.Json<ReferenceEntryDto>().Active);
        Assert.Equal(200, history.Status);
        Assert.Contains("Bổ sung mô tả", history.Body);
    }

    [Fact, Case("REF-03", "Reference catalogs reject duplicate names, stale versions and unknown catalog kinds", Owner,
        "Step 1: Add two items with the same name in one catalog; add an item without a code.", "Step 2: Edit an item with a stale version.",
        "Step 3: Call GET catalogs/khong-ton-tai (a catalog kind that does not exist).")]
    public async Task Reference_catalog_rejects_duplicates_stale_versions_and_unknown_kinds()
    {
        var client = await host.ClientAsync("owner.demo");
        var path = $"{Settings}/catalogs/{ReferenceKinds.Occupations}";
        var code = Unique("NN");
        var name = "Kỹ sư " + code;

        var first = await client.PostAsync(path, new CreateReferenceRequest(Guid.NewGuid(), new ReferenceEntryInput(code, name, null)));
        Assert.True(first.Status == 201, first.Body);
        var entry = first.Json<ReferenceEntryDto>();
        var duplicate = await client.PostAsync(path, new CreateReferenceRequest(Guid.NewGuid(), new ReferenceEntryInput(Unique("NN"), name, null)));
        var noCode = await client.PostAsync(path, new CreateReferenceRequest(Guid.NewGuid(), new ReferenceEntryInput(null, name + " khác", null)));
        await client.PutAsync($"{path}/{entry.Id}", new UpdateReferenceRequest(entry.Version, Guid.NewGuid(), "Đổi tên", new ReferenceEntryInput(code, name + " A", null)));
        var stale = await client.PutAsync($"{path}/{entry.Id}", new UpdateReferenceRequest(entry.Version, Guid.NewGuid(), "Đổi tên lần 2", new ReferenceEntryInput(code, name + " B", null)));
        var unknown = await client.GetAsync($"{Settings}/catalogs/khong-ton-tai");

        Assert.InRange(duplicate.Status, 400, 409);
        Assert.Equal(400, noCode.Status);
        Assert.Equal(409, stale.Status);
        Assert.InRange(unknown.Status, 400, 404);
    }

    [Fact, Case("REF-04", "Get catalog options for a form; a request without a catalog kind is rejected", Owner,
        "Step 1: Call GET /api/system-settings/reference-options?kind=diagnoses.", "Step 2: Call it with kind=patient-sources and a keyword.", "Step 3: Call it without kind.")]
    public async Task Reference_options_are_served_for_forms()
    {
        var client = await host.ClientAsync("owner.demo");

        var diagnoses = await client.GetAsync($"/api/system-settings/reference-options?kind={ReferenceKinds.Diagnoses}");
        var sources = await client.GetAsync($"/api/system-settings/reference-options?kind={ReferenceKinds.PatientSources}&search=a");
        var noKind = await client.GetAsync("/api/system-settings/reference-options");

        Assert.Equal(200, diagnoses.Status);
        Assert.Equal(200, sources.Status);
        Assert.Equal(400, noKind.Status);
    }
}
