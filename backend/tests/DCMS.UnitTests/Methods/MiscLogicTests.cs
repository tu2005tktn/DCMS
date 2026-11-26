using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Features.Examinations;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of UnitConversionRules, ScreenGrants, SettingsGrants, HttpDentalAiClient, GoogleTokenVerifier, and SettingsAuditService.
/// </summary>
public sealed class MiscLogicTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            return send(request, cancellationToken);
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    // ================= UnitConversionRules =================
    [Theory]
    [InlineData("UT-MSC-01 Nguyên x nguyên", "2", "10", false, "20")]
    [InlineData("UT-MSC-02 Thập phân rút gọn số 0 cuối", "1.50", "2.0", false, "3")]
    [InlineData("UT-MSC-03 Vật liệu đúng 3 chữ số thập phân", "0.125", "1", false, "0.125")]
    [InlineData("UT-MSC-04 Thuốc quy ra số nguyên hợp lệ", "0.5", "10", true, "5")]
    public void UT_MSC_UnitConversion_ToBase_hop_le(string caseName, string qty, string factor, bool medicine, string expected)
    {
        _ = caseName;
        UnitConversionRules.ValidateFactor(factor);
        Assert.Equal(expected, UnitConversionRules.ToBase(qty, factor, medicine));
    }

    [Theory]
    [InlineData("UT-MSC-05 Chuỗi rỗng", "", "1", false, "Nhập số thập phân dương.")]
    [InlineData("UT-MSC-06 Ký hiệu khoa học", "1e2", "1", false, "Nhập số thập phân dương, không dùng ký hiệu mũ.")]
    [InlineData("UT-MSC-07 Số âm", "-1", "1", false, "Nhập số thập phân dương, không dùng ký hiệu mũ.")]
    [InlineData("UT-MSC-08 Bằng 0", "0", "1", false, "Giá trị phải lớn hơn 0.")]
    [InlineData("UT-MSC-09 Quá 3 chữ số thập phân với vật liệu", "0.1234", "1", false, "Số lượng gốc tối đa 3 chữ số thập phân.")]
    [InlineData("UT-MSC-10 Thuốc không ra nguyên", "0.5", "3", true, "Thuốc phải quy ra đơn vị gốc nguyên.")]
    public void UT_MSC_UnitConversion_ToBase_tu_choi(string caseName, string qty, string factor, bool medicine, string expectedMessage)
    {
        _ = caseName;
        var ex = Assert.Throws<ArgumentException>(() => UnitConversionRules.ToBase(qty, factor, medicine));
        Assert.Equal(expectedMessage, ex.Message);
    }

    // ================= ScreenGrants & SettingsGrants =================
    [Theory]
    [InlineData("UT-MSC-11 Quyền vật liệu cho Bác sĩ", "materials.stock.read", new[] { AccountRoles.Dentist }, true)]
    [InlineData("UT-MSC-12 Quyền vật liệu cho Trợ lý bị chặn", "materials.stock.read", new[] { AccountRoles.ClinicAssistant }, false)]
    [InlineData("UT-MSC-13 Cấu hình chung cho Admin", "settings.general.manage", new[] { AccountRoles.Admin }, true)]
    [InlineData("UT-MSC-14 Danh mục thuốc chỉ Owner/Admin", "catalog.medicines.create", new[] { AccountRoles.Dentist }, false)]
    [InlineData("UT-MSC-15 Danh mục vật liệu cho Bác sĩ", "catalog.materials.create", new[] { AccountRoles.Dentist }, true)]
    [InlineData("UT-MSC-16 Đọc tài liệu lâm sàng chỉ Bác sĩ", "documents.review", new[] { AccountRoles.ClinicAssistant }, false)]
    [InlineData("UT-MSC-17 Tải tài liệu cho Trợ lý", "documents.upload", new[] { AccountRoles.ClinicAssistant }, true)]
    [InlineData("UT-MSC-18 Duyệt thương mại cho Admin", "treatment.commercial.approve", new[] { AccountRoles.Admin }, true)]
    [InlineData("UT-MSC-19 Điều trị chỉ Bác sĩ", "treatment.dossiers.create", new[] { AccountRoles.ClinicAssistant }, false)]
    [InlineData("UT-MSC-20 Nhận kết quả CLS cho Trợ lý", "investigations.results.receive", new[] { AccountRoles.ClinicAssistant }, true)]
    [InlineData("UT-MSC-21 Quyền không nằm trong nhóm trả false nếu không tồn tại", "unknown.key", new[] { AccountRoles.ClinicOwner }, false)]
    [InlineData("UT-MSC-22 Chủ phòng khám mở mọi quyền settings", "settings.audit.read", new[] { AccountRoles.ClinicOwner }, true)]
    [InlineData("UT-MSC-23 Chi nhánh chỉ Owner/Admin", "branches.manage", new[] { AccountRoles.Dentist }, false)]
    [InlineData("UT-MSC-24 Trợ lý chỉ xem đơn thuốc không kê", "prescriptions.read", new[] { AccountRoles.ClinicAssistant }, true)]
    [InlineData("UT-MSC-25 Trợ lý không được kê đơn thuốc", "prescriptions.manage", new[] { AccountRoles.ClinicAssistant }, false)]
    [InlineData("UT-MSC-26 Admin xem chi phí nhật ký", "finance.costs.read", new[] { AccountRoles.Admin }, true)]
    [InlineData("UT-MSC-27 Bác sĩ xem bệnh nhân", "patients.read", new[] { AccountRoles.Dentist }, true)]
    public void UT_MSC_SettingsGrants_RoleAllows(string caseName, string permission, string[] roles, bool expected)
    {
        _ = caseName;
        Assert.Equal(expected, SettingsGrants.RoleAllows(permission, roles));
    }

    [Fact]
    public void UT_MSC_28_ScreenGrants_Expand_va_SettingsGrants_Legacy()
    {
        var expanded = ScreenGrants.Expand(["accounts.manage", "patients.manage", "patient-links.verify"]);
        Assert.Contains("accounts.audit", expanded);
        Assert.Contains("patients.archive", expanded);
        Assert.Contains("patient-links.read", expanded);

        Assert.True(SettingsGrants.IsSettings("catalog.materials.read"));
        Assert.False(SettingsGrants.IsSettings("patients.read"));
        Assert.True(ScreenGrants.LegacySettings("catalog.providers.read", [AccountRoles.Dentist]));
        Assert.False(ScreenGrants.LegacySettings("catalog.medicines.read", [AccountRoles.Dentist]));

        var ownerLegacy = SettingsGrants.Legacy([AccountRoles.ClinicOwner]);
        Assert.Contains("finance.costs.read", ownerLegacy);
        var dentistLegacy = SettingsGrants.Legacy([AccountRoles.Dentist]);
        Assert.Contains("catalog.materials.read", dentistLegacy);
        Assert.DoesNotContain("catalog.medicines.read", dentistLegacy);
        Assert.DoesNotContain("finance.costs.read", dentistLegacy);
    }

    // ================= HttpDentalAiClient =================
    [Fact]
    public async Task UT_MSC_29_HttpDentalAiClient_chua_cau_hinh_va_cac_loi_mang_timeout_du_lieu()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var unconfigured = new HttpDentalAiClient(new StubFactory(handler), new ConfigurationBuilder().Build());
        var ex1 = await Assert.ThrowsAsync<DentalAiUnavailableException>(() => unconfigured.AnalyzeXRayAsync([1, 2], "image/png", "x.png", CancellationToken.None));
        Assert.Equal("Dịch vụ AI chưa được cấu hình.", ex1.Message);

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DentalAi:BaseUrl"] = "http://ai.local:8090",
            ["DentalAi:ApiKey"] = "key-1",
            ["DentalAi:TimeoutSeconds"] = "5"
        }).Build();

        var errClient = new HttpDentalAiClient(new StubFactory(handler), cfg);
        var ex2 = await Assert.ThrowsAsync<DentalAiUnavailableException>(() => errClient.AnalyzeXRayAsync([1, 2], "image/png", "x.png", CancellationToken.None));
        Assert.Contains("500", ex2.Message);
        Assert.Equal("key-1", handler.LastRequest!.Headers.GetValues("X-Api-Key").Single());

        var netClient = new HttpDentalAiClient(new StubFactory(new StubHandler((_, _) => throw new HttpRequestException("down"))), cfg);
        var ex3 = await Assert.ThrowsAsync<DentalAiUnavailableException>(() => netClient.AnalyzeXRayAsync([1], "image/png", "x.png", CancellationToken.None));
        Assert.Equal("Không kết nối được dịch vụ AI.", ex3.Message);

        var timeoutClient = new HttpDentalAiClient(new StubFactory(new StubHandler((_, _) => throw new OperationCanceledException("timeout"))), cfg);
        var ex4 = await Assert.ThrowsAsync<DentalAiUnavailableException>(() => timeoutClient.AnalyzeXRayAsync([1], "image/png", "x.png", CancellationToken.None));
        Assert.Equal("Dịch vụ AI phản hồi quá thời gian.", ex4.Message);

        var badBodyClient = new HttpDentalAiClient(new StubFactory(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"schemaVersion":"1","models":[],"image":{"width":0,"height":0},"findings":[]}""", Encoding.UTF8, "application/json") }))), cfg);
        var ex5 = await Assert.ThrowsAsync<DentalAiUnavailableException>(() => badBodyClient.AnalyzeXRayAsync([1], "image/png", "x.png", CancellationToken.None));
        Assert.Equal("Dịch vụ AI trả dữ liệu không hợp lệ.", ex5.Message);
    }

    [Fact]
    public async Task UT_MSC_30_HttpDentalAiClient_loc_hop_khong_hop_le_va_cat_chuoi_qua_dai()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "models": [{"kind":"caries","name":"yolov8-caries","sha256":"abc123"}],
          "image": {"width": 1000, "height": 800},
          "device": "cpu",
          "elapsedMs": -5,
          "findings": [
            {"findingId":"F1","kind":"caries","label":"Sâu răng","confidence":0.87654,"box":{"x1":10,"y1":20,"x2":200,"y2":150},"jaw":"upper","side":"left"},
            {"findingId":"F2","kind":"caries","label":"Ngoài khung","confidence":0.9,"box":{"x1":10,"y1":20,"x2":1200,"y2":150},"jaw":"upper","side":"left"},
            {"findingId":"F3","kind":"caries","label":"Độ tin cậy sai","confidence":1.5,"box":{"x1":10,"y1":20,"x2":100,"y2":100},"jaw":"upper","side":"left"}
          ]
        }
        """;
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DentalAi:BaseUrl"] = "http://ai.local:8090" }).Build();
        var client = new HttpDentalAiClient(new StubFactory(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }))), cfg);

        var result = await client.AnalyzeXRayAsync([1, 2, 3], "image/jpeg", "xray.jpg", CancellationToken.None);
        Assert.Equal(("1.0", "yolov8-caries", "abc123", 1000, 800, 0, "cpu"),
            (result.SchemaVersion, result.ModelName, result.ModelSha256, result.ImageWidth, result.ImageHeight, result.ElapsedMs, result.Device));
        var finding = Assert.Single(result.Findings);
        Assert.Equal(("F1", 0.8765, "upper", "left"), (finding.FindingId, finding.Confidence, finding.Jaw, finding.Side));
    }

    // ================= GoogleTokenVerifier =================
    private static (string JwksJson, string Token) MakeGoogleJwt(string kid, string aud, string sub = "sub-1", string email = "user@example.com", string verified = "true", string? name = "Nguyễn A")
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = kid };
        var pub = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = kid });
        var jwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = pub.Kty, kid = pub.Kid, use = "sig", alg = "RS256", n = pub.N, e = pub.E } } });
        var handler = new JsonWebTokenHandler();
        var claims = new Dictionary<string, object> { ["sub"] = sub, ["email"] = email, ["email_verified"] = verified };
        if (name != null) claims["name"] = name;
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = aud,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
        return (jwks, token);
    }

    [Fact]
    public async Task UT_MSC_31_GoogleTokenVerifier_xac_thuc_cache_xoay_khoa_va_tu_choi_loi()
    {
        var unconfigured = new GoogleTokenVerifier(new StubFactory(new StubHandler((_, _) => throw new NotImplementedException())), new ConfigurationBuilder().Build());
        var e503 = await CreateTestKit.ErrorOf(() => unconfigured.VerifyAsync("tok", CancellationToken.None));
        Assert.Equal((503, "GOOGLE_NOT_CONFIGURED"), (e503.Status, e503.Response.Code));

        var (jwks1, tok1) = MakeGoogleJwt("k1", "client-1");
        var (jwks2, tok2) = MakeGoogleJwt("k2", "client-1", name: null);
        var (jwksUnverified, tokUnverified) = MakeGoogleJwt("k1", "client-1", verified: "false");
        _ = jwksUnverified;
        var currentJwks = jwks1;
        var handler = new StubHandler((_, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(currentJwks, Encoding.UTF8, "application/json") };
            resp.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(2) };
            return Task.FromResult(resp);
        });
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Google:WebClientId"] = "client-1" }).Build();
        var verifier = new GoogleTokenVerifier(new StubFactory(handler), cfg);

        var eEmpty = await CreateTestKit.ErrorOf(() => verifier.VerifyAsync(" ", CancellationToken.None));
        Assert.Equal((401, "GOOGLE_INVALID_TOKEN"), (eEmpty.Status, eEmpty.Response.Code));

        // First call fetches JWKS, second reuses cache.
        var id1 = await verifier.VerifyAsync(tok1, CancellationToken.None);
        Assert.Equal(("sub-1", "user@example.com", "Nguyễn A"), (id1.Subject, id1.Email, id1.FullName));
        _ = await verifier.VerifyAsync(tok1, CancellationToken.None);
        Assert.Equal(1, handler.Calls);

        // Key rotation forces refresh and falls back name -> email.
        currentJwks = jwks2;
        var id2 = await verifier.VerifyAsync(tok2, CancellationToken.None);
        Assert.Equal("user@example.com", id2.FullName);
        Assert.Equal(2, handler.Calls);

        // Unverified email / bad token rejected.
        currentJwks = jwks1;
        var eBad = await CreateTestKit.ErrorOf(() => verifier.VerifyAsync(tokUnverified, CancellationToken.None));
        Assert.Equal((401, "GOOGLE_INVALID_TOKEN"), (eBad.Status, eBad.Response.Code));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class SettingsAuditServiceTests
{
    private sealed class Access(PermissionState state) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(state);
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => Task.FromResult(state);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void UT_MSC_32_SettingsAuditService_Clean_an_bi_mat_va_chi_phi()
    {
        Assert.Null(SettingsAuditService.Clean(" ", true));
        Assert.Null(SettingsAuditService.Clean("{invalid", true));
        var json = """{"name":"A","password":"123","bankAccount":"99","unitCost":500,"nested":[{"apiToken":"x","costPrice":10,"ok":1}]}""";
        var noCost = SettingsAuditService.Clean(json, costs: false)!.AsObject();
        Assert.False(noCost.ContainsKey("password"));
        Assert.False(noCost.ContainsKey("bankAccount"));
        Assert.False(noCost.ContainsKey("unitCost"));
        Assert.False(noCost["nested"]![0]!.AsObject().ContainsKey("apiToken"));
        Assert.False(noCost["nested"]![0]!.AsObject().ContainsKey("costPrice"));
        Assert.Equal(1, noCost["nested"]![0]!.AsObject()["ok"]!.GetValue<int>());

        var withCost = SettingsAuditService.Clean(json, costs: true)!.AsObject();
        Assert.Equal(500, withCost["unitCost"]!.GetValue<int>());
        Assert.False(withCost.ContainsKey("password"));
    }

    [Fact]
    public async Task UT_MSC_33_SettingsAuditService_ListAsync_phan_quyen_loc_section_va_chi_nhanh()
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId();
        var other = CreateTestKit.NextId();
        var staff = await CreateTestKit.SeedStaffAsync(db, "aud" + n, AccountRoles.ClinicOwner, n);
        var day = new DateOnly(2031, 4, 10);
        var at = new DateTimeOffset(2031, 4, 10, 3, 0, 0, TimeSpan.Zero); // 10:00 clinic time

        var refEntry = new ReferenceEntry { Kind = "diagnoses", Code = "D" + n, Name = "Chẩn đoán " + n };
        db.ReferenceEntries.Add(refEntry);
        db.SystemCatalogChanges.AddRange(
            new SystemCatalogChange { RequestKey = Guid.NewGuid(), ActorId = staff.Id, Command = "GeneralSettingsUpdate", ResourceId = Guid.NewGuid(), Fingerprint = "f", BeforeJson = """{"reason":"Cập nhật chung","secretKey":"x"}""", ResultJson = """{"unitCost":100}""", OccurredAt = at },
            new SystemCatalogChange { RequestKey = Guid.NewGuid(), ActorId = staff.Id, Command = "ReferenceCreate", ResourceId = refEntry.Id, Fingerprint = "f", ResultJson = """{"kind":"diagnoses","reason":"Thêm"}""", OccurredAt = at.AddMinutes(1) },
            new SystemCatalogChange { RequestKey = Guid.NewGuid(), ActorId = staff.Id, Command = "providers:create", ResourceId = Guid.NewGuid(), Fingerprint = "f", ResultJson = """{"ok":true}""", OccurredAt = at.AddMinutes(2) },
            new SystemCatalogChange { RequestKey = Guid.NewGuid(), ActorId = staff.Id, Command = "MaterialCreate", ResourceId = Guid.NewGuid(), Fingerprint = "f", ResultJson = """{"ok":true}""", OccurredAt = at.AddMinutes(3) }
        );
        db.AddRange(
            new SettingsAudit { ActorId = staff.Id, BranchIds = [n], Section = AuditSections.AccessGroups, Target = "access-groups:1", Action = "GroupCreate", Reason = "Tạo nhóm", BeforeJson = "{}", AfterJson = """{"cost":50}""", At = at.AddMinutes(4) },
            new SettingsAudit { ActorId = staff.Id, BranchIds = [other], Section = AuditSections.AccessGroups, Target = "access-groups:2", Action = "GroupCreate", Reason = "Chi nhánh khác", BeforeJson = "{}", AfterJson = "{}", At = at.AddMinutes(5) }
        );
        await db.SaveChangesAsync();

        var noPerm = new SettingsAuditService(db, new Access(new(false, true, [], [], [n])));
        Assert.Equal((403, "MSG06"), ((await CreateTestKit.ErrorOf(() => noPerm.ListAsync(staff.Id, null, null, null, 1, 20, CancellationToken.None))).Status,
            (await CreateTestKit.ErrorOf(() => noPerm.ListAsync(staff.Id, null, null, null, 1, 20, CancellationToken.None))).Response.Code));

        var ownerSvc = new SettingsAuditService(db, new Access(new(true, true, [AccountRoles.ClinicOwner], ["settings.audit.read", "access-groups.read", "finance.costs.read"], [n, other])));
        Assert.Equal(400, (await CreateTestKit.ErrorOf(() => ownerSvc.ListAsync(staff.Id, null, null, null, 0, 20, CancellationToken.None))).Status);
        Assert.Equal(400, (await CreateTestKit.ErrorOf(() => ownerSvc.ListAsync(staff.Id, "bad", null, null, 1, 20, CancellationToken.None))).Status);
        Assert.Equal(400, (await CreateTestKit.ErrorOf(() => ownerSvc.ListAsync(staff.Id, null, day.AddDays(1), day, 1, 20, CancellationToken.None))).Status);

        var ownerPage = await ownerSvc.ListAsync(staff.Id, null, day, day, 1, 20, CancellationToken.None);
        Assert.Equal(5, ownerPage.Sections.Count);
        Assert.True(ownerPage.Total >= 6);
        var refAudit = ownerPage.Items.Single(x => x.Action == "ReferenceCreate");
        Assert.StartsWith("diagnoses:", refAudit.Target);
        var genAudit = ownerPage.Items.Single(x => x.Action == "GeneralSettingsUpdate");
        Assert.Equal("Cập nhật chung", genAudit.Reason);
        Assert.Equal(100, genAudit.After!["unitCost"]!.GetValue<int>());

        // Non-owner dentist with access-groups.read on branch n only: cannot view General section, cannot see branch `other`, costs stripped.
        var dentistSvc = new SettingsAuditService(db, new Access(new(false, true, [AccountRoles.Dentist], ["settings.audit.read", "access-groups.read", "catalog.diagnoses.read"], [n])));
        Assert.Equal(403, (await CreateTestKit.ErrorOf(() => dentistSvc.ListAsync(staff.Id, AuditSections.General, day, day, 1, 20, CancellationToken.None))).Status);
        var agPage = await dentistSvc.ListAsync(staff.Id, AuditSections.AccessGroups, day, day, 1, 20, CancellationToken.None);
        Assert.Contains(agPage.Items, x => x.Target == "access-groups:1" && !x.After!.AsObject().ContainsKey("cost"));
        Assert.DoesNotContain(agPage.Items, x => x.Target == "access-groups:2");
    }
}
