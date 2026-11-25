using DCMS.Application.Features.SystemSettings;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>S58 general settings and the patient code sequence (docs/specs/S58-GENERAL-SETTINGS.md).</summary>
[Collection(ApiCollection.Name)]
public sealed class GeneralSettingsApiTests(ApiHost host)
{
    private const string General = "/api/system-settings/settings/general";
    private const string Sequence = "/api/system-settings/settings/patient-code-sequence";
    private const string Owner = "Signed in as the clinic owner owner.demo.";

    [Fact, Case("GEN-01", "View the general settings and the setting sections the user may manage", Owner,
        "Step 1: Call GET /api/system-settings/settings/capabilities.", "Step 2: Call GET /api/system-settings/settings/general.",
        "Step 3: Call GET /api/system-settings/settings/general/history.")]
    public async Task Admin_reads_general_settings_capabilities_and_history()
    {
        var client = await host.ClientAsync("owner.demo");

        var capabilities = await client.GetAsync("/api/system-settings/settings/capabilities");
        var general = await client.GetAsync(General);
        var history = await client.GetAsync($"{General}/history?page=1&pageSize=20");

        Assert.Equal(200, capabilities.Status);
        Assert.Contains("general", capabilities.Body);
        Assert.Equal(200, general.Status);
        Assert.True(general.Json<GeneralSettingsDto>().Settings.StaffIdleTimeoutMinutes > 0);
        Assert.Equal(200, history.Status);
    }

    [Fact, Case("GEN-02", "Preview and save the general settings, reject a stale version, then restore a version from history", Owner,
        "Step 1: Read the current settings (version V).",
        "Step 2: Call POST general/preview with a new idle sign-out time.",
        "Step 3: Call PUT general with ExpectedVersion = V (creates version V1).",
        "Step 4: Call PUT again with ExpectedVersion = V (stale version).",
        "Step 5: Save the original settings again (version V2).",
        "Step 6: Call POST general/restore-preview, then POST general/restore to version V1.",
        "Step 7: Save the original settings again so other tests are not affected.")]
    public async Task General_settings_preview_save_stale_and_restore()
    {
        var client = await host.ClientAsync("owner.demo");
        var current = (await client.GetAsync(General)).Json<GeneralSettingsDto>();
        var original = current.Settings;
        var changed = original with { StaffIdleTimeoutMinutes = original.StaffIdleTimeoutMinutes == 30 ? 60 : 30 };

        var preview = await client.PostAsync($"{General}/preview", new PreviewGeneralRequest(current.Version, changed));
        var saved = await client.PutAsync(General, new SaveGeneralRequest(current.Version, Guid.NewGuid(), "Đổi thời gian tự đăng xuất", changed));
        var stale = await client.PutAsync(General, new SaveGeneralRequest(current.Version, Guid.NewGuid(), "Lưu bằng phiên bản cũ", changed));
        var v1 = saved.Json<GeneralSettingsDto>();
        var v2 = (await client.PutAsync(General, new SaveGeneralRequest(v1.Version, Guid.NewGuid(), "Trả về giá trị cũ", original))).Json<GeneralSettingsDto>();
        var restorePreview = await client.PostAsync($"{General}/restore-preview", new RestorePreviewRequest(v1.Version, v2.Version));
        var restored = await client.PostAsync($"{General}/restore", new RestoreGeneralRequest(v1.Version, v2.Version, Guid.NewGuid(), "Dùng lại cấu hình phiên bản trước"));
        var v3 = restored.Json<GeneralSettingsDto>();
        var cleanup = await client.PutAsync(General, new SaveGeneralRequest(v3.Version, Guid.NewGuid(), "Dọn dữ liệu test", original));

        Assert.Equal(200, preview.Status);
        Assert.Contains("staffIdleTimeoutMinutes", preview.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(200, saved.Status);
        Assert.Equal(changed.StaffIdleTimeoutMinutes, v1.Settings.StaffIdleTimeoutMinutes);
        Assert.Equal(409, stale.Status);
        Assert.Equal(200, restorePreview.Status);
        Assert.Equal(200, restored.Status);
        Assert.Equal(changed.StaffIdleTimeoutMinutes, v3.Settings.StaffIdleTimeoutMinutes);
        Assert.Equal(200, cleanup.Status);
    }

    [Fact, Case("GEN-03", "Invalid general settings are rejected", Owner,
        "Step 1: Call PUT general with idle sign-out time = 0 (only 5, 10, 20, 30, 60, 90, 120 minutes are allowed).", "Step 2: Call PUT general with a patient code format that does not exist.")]
    public async Task Invalid_general_settings_are_rejected()
    {
        var client = await host.ClientAsync("owner.demo");
        var current = (await client.GetAsync(General)).Json<GeneralSettingsDto>();

        var zeroIdle = await client.PutAsync(General, new SaveGeneralRequest(current.Version, Guid.NewGuid(), "Sai",
            current.Settings with { StaffIdleTimeoutMinutes = 0 }));
        var badMode = await client.PutAsync(General, new SaveGeneralRequest(current.Version, Guid.NewGuid(), "Sai",
            current.Settings with { PatientCode = new PatientCodeSettings("Random", "BN") }));
        var noReason = await client.PutAsync(General, new SaveGeneralRequest(current.Version, Guid.NewGuid(), "", current.Settings));

        Assert.Equal(400, zeroIdle.Status);
        Assert.Equal(400, badMode.Status);
        Assert.Equal(400, noReason.Status);
    }

    [Fact, Case("GEN-04", "Only the clinic owner or an administrator can view the general settings", "Signed in as dentist.demo, or not signed in.",
        "Step 1: The dentist calls GET general.", "Step 2: Call GET general without signing in.")]
    public async Task General_settings_require_owner_or_admin()
    {
        var dentist = await host.ClientAsync("dentist.demo");
        var anonymous = await host.ClientAsync();

        Assert.Equal(403, (await dentist.GetAsync(General)).Status);
        Assert.Equal(401, (await anonymous.GetAsync(General)).Status);
    }

    [Fact, Case("SEQ-01", "View the next patient code number and move it forward", "Signed in as owner.demo.",
        "Step 1: Call GET patient-code-sequence.", "Step 2: Call POST patient-code-sequence/preview with a number lower than the current one.",
        "Step 3: Call PUT patient-code-sequence with a number higher than the current one.")]
    public async Task Patient_code_sequence_only_moves_forward()
    {
        var client = await host.ClientAsync("owner.demo");
        var current = (await client.GetAsync(Sequence)).Json<PatientCodeSequenceDto>();

        var backwards = await client.PostAsync($"{Sequence}/preview", new PatientCodeSequencePreviewRequest(current.NextNumber, Math.Max(1, current.NextNumber - 1)));
        var forward = await client.PutAsync(Sequence, new UpdatePatientCodeSequenceRequest(current.NextNumber, Guid.NewGuid(), "Bỏ qua dải mã cũ", current.NextNumber + 5));
        var after = await client.GetAsync(Sequence);

        Assert.Equal(200, backwards.Status);
        if (current.NextNumber > 1) Assert.False(backwards.Json<PatientCodeSequencePreview>().Allowed);
        Assert.Equal(200, forward.Status);
        Assert.Equal(current.NextNumber + 5, after.Json<PatientCodeSequenceDto>().NextNumber);
    }

    [Fact, Case("GEN-05", "The patient form reads the required-field policy", "Signed in as assistant.demo.",
        "Step 1: Call GET /api/patients/form-policy.")]
    public async Task Patient_form_policy_follows_general_settings()
    {
        var client = await host.ClientAsync("assistant.demo");

        var policy = await client.GetAsync("/api/patients/form-policy");

        Assert.Equal(200, policy.Status);
        Assert.Contains("requiredFields", policy.Body);
    }
}

