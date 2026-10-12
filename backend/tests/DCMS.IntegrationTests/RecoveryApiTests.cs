using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using DCMS.Api.Features.Authentication;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>
/// UC-02 account recovery integration tests. Each test proves one acceptance criterion against the
/// real API pipeline and a real PostgreSQL schema.
/// </summary>
public sealed class RecoveryApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly string NewPassword = "Zx9!q" + Guid.NewGuid().ToString("N")[..20];

    private async Task<int> AccountIdAsync(string userName)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            """SELECT "Id" FROM "USER_ACCOUNT" WHERE "NormalizedUserName" = @name """, connection);
        command.Parameters.AddWithValue("name", userName.ToUpperInvariant());
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private async Task<(HttpClient Client, SignInResponse Session)> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session);
    }

    /// <summary>Runs request -> verify -> reset and returns to the caller on success.</summary>
    private async Task<(HttpResponseMessage Reset, string Grant)> CompleteRecoveryAsync(string userName, string? password = null)
    {
        var request = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId) ?? throw new InvalidOperationException("No proof was delivered.");
        var verify = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, proof));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var grant = (await verify.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!;

        var reset = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset", new RecoveryResetRequest(
            userName, grant.ResetGrant, password ?? NewPassword, password ?? NewPassword));
        return (reset, grant.ResetGrant);
    }

    private static async Task AssertMessageAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(SystemMessages.Get(code).Message, message.Message);
        Assert.Equal(code, message.Code);
    }

    [Fact]
    public async Task C1_ValidRecoveryChangesPasswordAndOldPasswordNoLongerSignsIn()
    {
        const string userName = "dentist.demo";
        var (reset, _) = await CompleteRecoveryAsync(userName);
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");

        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, fixture.Password)), HttpStatusCode.Unauthorized, "MSG09");
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword))).StatusCode);
    }

    [Fact]
    public async Task C3_ExistingAndMissingAccountsReturnTheSameNeutralResponse()
    {
        var existing = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest("dentist.demo"));
        var missing = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request",
            new RecoveryRequest("no.such.account." + Guid.NewGuid().ToString("N")[..8]));

        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        var existingBody = (await existing.Content.ReadFromJsonAsync<SystemMessage>())!;
        var missingBody = (await missing.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(existingBody, missingBody);
        Assert.Equal("MSG12", existingBody.Code);
        // The body must not carry any account-identifying field.
        var raw = await existing.Content.ReadAsStringAsync();
        Assert.DoesNotContain("dentist.demo", raw, StringComparison.OrdinalIgnoreCase);

        // Similar timing: neither answer may be dramatically faster than the other.
        var existingTimes = new List<double>();
        var missingTimes = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            existingTimes.Add(await TimeRequestAsync("dentist.demo"));
            missingTimes.Add(await TimeRequestAsync("another.missing." + Guid.NewGuid().ToString("N")[..8]));
        }
        existingTimes.Sort(); missingTimes.Sort();
        var ratio = missingTimes[7] / Math.Max(existingTimes[7], 0.0001);
        Assert.True(ratio < 10, $"Missing-account answer was {ratio:F1}x slower than an existing account.");
    }

    private async Task<double> TimeRequestAsync(string userName)
    {
        var watch = Stopwatch.StartNew();
        var response = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        watch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return watch.Elapsed.TotalMilliseconds;
    }

    [Fact]
    public async Task C2_WrongExpiredUsedAndOverLimitProofsAreRejected()
    {
        const string userName = "dentist.demo";
        var client = fixture.CreateClient();
        var request = await client.PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId)!;

        // Wrong proof.
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, "000000")), HttpStatusCode.BadRequest, "MSG108");

        // Expired proof.
        await ExpireActiveTokensAsync(accountId);
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, proof)), HttpStatusCode.BadRequest, "MSG108");

        // A fresh request so the used-proof case can be checked.
        await client.PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        var freshProof = fixture.Delivery.ProofFor(accountId)!;
        var verify = await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, freshProof));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var grant = (await verify.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!;

        var reset = await client.PostAsJsonAsync("/api/auth/recovery/reset", new RecoveryResetRequest(
            userName, grant.ResetGrant, NewPassword, NewPassword));
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");

        // Replaying the used grant must not change anything again (idempotent, no double reset).
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/reset", new RecoveryResetRequest(
            userName, grant.ResetGrant, NewPassword + "x", NewPassword + "x")), HttpStatusCode.BadRequest, "MSG108");
        // Replaying the used proof must fail too.
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, freshProof)), HttpStatusCode.BadRequest, "MSG108");
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword))).StatusCode);
    }

    [Fact]
    public async Task C2_OverTheAttemptLimitRefusesEvenTheCorrectProof()
    {
        const string userName = "assistant.demo";
        var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId)!;

        for (var attempt = 0; attempt < 5; attempt++)
            await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
                new RecoveryVerifyRequest(userName, "000000")), HttpStatusCode.BadRequest, "MSG108");

        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, proof)), HttpStatusCode.BadRequest, "MSG108");

        // The account was never reset.
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword)), HttpStatusCode.Unauthorized, "MSG09");
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, fixture.Password))).StatusCode);
    }

    [Fact]
    public async Task C2_ConcurrentDoubleResetProducesExactlyOneSuccess()
    {
        const string userName = "owner.demo";
        var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId)!;
        var verify = await client.PostAsJsonAsync("/api/auth/recovery/verify", new RecoveryVerifyRequest(userName, proof));
        var grant = (await verify.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!.ResetGrant;

        var resets = await Task.WhenAll(
            fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset", new RecoveryResetRequest(userName, grant, NewPassword, NewPassword)),
            fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset", new RecoveryResetRequest(userName, grant, NewPassword, NewPassword)));

        Assert.Single(resets, r => r.StatusCode == HttpStatusCode.OK);
        await AssertMessageAsync(Assert.Single(resets, r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.BadRequest, "MSG108");
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            """SELECT count(*) FROM "RECOVERY_TOKEN" WHERE "AccountId" = @id AND "UsedAt" IS NOT NULL AND "ResetGrantHash" <> '' """, connection);
        command.Parameters.AddWithValue("id", accountId);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task C4_ResetDoesNotUnlockALockedAccount()
    {
        const string userName = "dual.demo";
        var (admin, _) = await Login("admin.demo");
        var accountId = await AccountIdAsync(userName);
        await LockAccountAsync(admin, accountId);

        var (reset, _) = await CompleteRecoveryAsync(userName);
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");

        // Still locked: the new credential alone does not restore access.
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword)), HttpStatusCode.Unauthorized, "MSG09");

        await UnlockAccountAsync(admin, accountId);
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword))).StatusCode);
    }

    [Fact]
    public async Task C5_OldSessionsAreRevokedAfterReset()
    {
        const string userName = "dentist.demo";
        var (first, _) = await Login(userName);
        var (second, _) = await Login(userName);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/auth/me")).StatusCode);

        await CompleteRecoveryAsync(userName);

        await AssertMessageAsync(await first.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        await AssertMessageAsync(await second.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
    }

    [Fact]
    public async Task C6_RecoveryDoesNotVerifyAPatientLinkOrRaisePermissions()
    {
        const string userName = "dentist.demo";
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var rolesBefore = await RolesAsync(connection, await AccountIdAsync(userName));

        var (client, session) = await Login(userName);
        Assert.False(session.User.HasVerifiedPatientLink);

        await CompleteRecoveryAsync(userName);
        await fixture.AssertPatientUnchangedAsync();

        var rolesAfter = await RolesAsync(connection, await AccountIdAsync(userName));
        Assert.Equal(rolesBefore, rolesAfter);

        var signIn = await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(userName, NewPassword));
        var refreshed = (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.False(refreshed.User.HasVerifiedPatientLink);
        Assert.DoesNotContain("Admin", refreshed.User.Roles);
        Assert.DoesNotContain("Patient", refreshed.User.Roles);
    }

    [Fact]
    public async Task C7_NoSecretIsStoredAnywhereByTheRecoveryProcess()
    {
        const string userName = "guest.demo";
        var (reset, grant) = await CompleteRecoveryAsync(userName);
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");
        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId)
            ?? throw new InvalidOperationException("No proof was delivered.");

        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        // Scan every recovery table for a plaintext proof, grant or password.
        var tables = new (string Table, string[] Columns)[]
        {
            ("RECOVERY_TOKEN", ["TokenHash", "ResetGrantHash", "ChannelType", "NormalizedDestination"]),
            ("RECOVERY_AUDIT", ["Reason", "ChannelType", "Result"]),
            ("RECOVERY_OUTBOX", ["Destination", "Provider", "Status", "ProviderMessageId"]),
            ("ACCOUNT_AUDIT", ["Reason", "BeforeJson", "AfterJson"])
        };
        foreach (var (table, columns) in tables)
        {
            var projection = string.Join(" || '|' || ", columns.Select(c => $"COALESCE(\"{c}\", '')"));
            await using var command = new Npgsql.NpgsqlCommand($"""SELECT {projection} FROM "{table}" """, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = reader.GetString(0);
                Assert.DoesNotContain(proof, row, StringComparison.Ordinal);
                Assert.DoesNotContain(grant, row, StringComparison.Ordinal);
                Assert.DoesNotContain(NewPassword, row, StringComparison.Ordinal);
            }
        }

        // Only the hash forms are persisted, so a leaked database cannot be replayed.
        await using var hashCommand = new Npgsql.NpgsqlCommand("""
            SELECT count(*) FROM "RECOVERY_TOKEN" WHERE "AccountId" = @id
            AND "TokenHash" <> @proof AND "ResetGrantHash" <> @grant
            """, connection);
        hashCommand.Parameters.AddWithValue("id", accountId);
        hashCommand.Parameters.AddWithValue("proof", proof);
        hashCommand.Parameters.AddWithValue("grant", grant);
        Assert.Equal(1L, (long)(await hashCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task TheDevelopmentProofAidIsAbsentOutsideDevelopment()
    {
        // The Development contact adapter is registered only in Development, and so is the aid that
        // hands its delivered proof back. Production must not expose either.
        var production = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services => services.AddSingleton<IContactDeliveryAdapter>(new RefusedAdapter()));
        });
        var accountId = await AccountIdAsync("dentist.demo");
        var anonymous = production.CreateClient();
        var response = await anonymous.GetAsync($"/api/dev/recovery/proof/{accountId}");
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,
            $"The development aid is reachable outside Development with status {response.StatusCode}.");
        Assert.Empty(RefusedAdapter.Attempts);
        production.Dispose();
    }

    [Fact]
    public async Task TheDevelopmentProofAidRequiresASignedInSession()
    {
        var accountId = await AccountIdAsync("dentist.demo");
        await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest("dentist.demo"));

        // Anonymous access is refused even in Development.
        await AssertMessageAsync(await fixture.CreateClient().GetAsync($"/api/dev/recovery/proof/{accountId}"),
            HttpStatusCode.Unauthorized, "MSG07");

        var (admin, _) = await Login("admin.demo");
        var granted = await admin.GetAsync($"/api/dev/recovery/proof/{accountId}");
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var body = (await granted.Content.ReadFromJsonAsync<DevelopmentProofResponse>())!;
        Assert.Equal(accountId, body.AccountId);
        Assert.True(body.Proof.Length >= 4, "The development adapter must hand back the delivered proof.");
    }

    [Fact]
    public async Task AdminAssistedVerificationRecordsTheAdminAsActor()
    {
        const string userName = "guest.demo";
        var accountId = await AccountIdAsync(userName);
        var (admin, _) = await Login("admin.demo");
        var adminId = await AccountIdAsync("admin.demo");

        var started = await admin.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}", new { });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var proof = fixture.Delivery.ProofFor(accountId)!;

        // A verifier given to the owner: the Admin path only starts the process, it cannot skip it.
        var verified = await admin.PostAsJsonAsync($"/api/admin/recovery/verify/{accountId}",
            new RecoveryVerifyRequest(userName, proof));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var grant = (await verified.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!.ResetGrant;

        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT "ActorId", "TargetId", "Action" FROM "RECOVERY_AUDIT"
            WHERE "TargetId" = @id AND "Action" IN ('RecoveryRequested', 'RecoveryVerified')
            """, connection);
        command.Parameters.AddWithValue("id", accountId);
        await using var reader = await command.ExecuteReaderAsync();
        var requested = new List<(int? Actor, string Action)>();
        while (await reader.ReadAsync())
            requested.Add((reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.GetString(2)));

        // GBR-028/GBR-081: the Admin who drove the action is recorded as the actor, not null.
        Assert.Contains(requested, entry => entry.Action == "RecoveryRequested" && entry.Actor == adminId);
        Assert.Contains(requested, entry => entry.Action == "RecoveryVerified" && entry.Actor == adminId);

        var reset = await admin.PostAsJsonAsync($"/api/admin/recovery/reset/{accountId}",
            new { NewPassword, ConfirmPassword = NewPassword });
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, fixture.Password)), HttpStatusCode.Unauthorized, "MSG09");
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(userName, NewPassword))).StatusCode);

        // The grant the Admin received is single-use: replaying it fails.
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset",
            new RecoveryResetRequest(userName, grant, NewPassword + "x", NewPassword + "x")),
            HttpStatusCode.BadRequest, "MSG108");
    }

    [Fact]
    public async Task AdminRecoveryKeepsTheSharedCooldown()
    {
        const string userName = "assistant.demo";
        var accountId = await AccountIdAsync(userName);
        var (admin, _) = await Login("admin.demo");
        await admin.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}", new { });
        var first = fixture.Delivery.ProofFor(accountId)!;

        // A second Admin request inside the cooldown must not issue a new proof or invalidate the
        // first one: the Admin path reuses the same process instead of bypassing it.
        var second = await admin.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}", new { });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first, fixture.Delivery.ProofFor(accountId));
        var verify = await admin.PostAsJsonAsync($"/api/admin/recovery/verify/{accountId}",
            new RecoveryVerifyRequest(userName, first));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task AuditRecordsTheRecoveryEventsWithoutStoringSecrets()
    {
        const string userName = "dentist.demo";
        await CompleteRecoveryAsync(userName);
        var accountId = await AccountIdAsync(userName);

        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT "Action", "Reason", "ChannelType", "Result" FROM "RECOVERY_AUDIT" WHERE "TargetId" = @id
            """, connection);
        command.Parameters.AddWithValue("id", accountId);
        var actions = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                actions.Add(reader.GetString(0));
                // No proof, grant, link or password may appear in any audit field.
                foreach (var field in new[] { "Reason", "ChannelType", "Result" })
                {
                    var value = reader.GetString(reader.GetOrdinal(field));
                    Assert.DoesNotContain(fixture.Delivery.ProofFor(accountId) ?? "\0", value, StringComparison.Ordinal);
                    Assert.DoesNotContain(NewPassword, value, StringComparison.Ordinal);
                }
            }

        Assert.Contains("RecoveryRequested", actions);
        Assert.Contains("RecoveryVerified", actions);
        Assert.Contains("PasswordReset", actions);

        // The account audit also carries the credential change with actor, time and reason.
        await using var accountCommand = new Npgsql.NpgsqlCommand("""
            SELECT "ActorId", "TargetId", "Action", "Reason", "OccurredAt", "BeforeJson"
            FROM "ACCOUNT_AUDIT" WHERE "TargetId" = @id AND "Action" = 'PasswordReset'
            """, connection);
        accountCommand.Parameters.AddWithValue("id", accountId);
        await using var accountReader = await accountCommand.ExecuteReaderAsync();
        Assert.True(await accountReader.ReadAsync());
        Assert.True(accountReader.GetInt32(0) > 0);
        Assert.Equal(accountId, accountReader.GetInt32(1));
        Assert.NotEqual(default, accountReader.GetFieldValue<DateTimeOffset>(accountReader.GetOrdinal("OccurredAt")));
        var before = accountReader.GetString(accountReader.GetOrdinal("BeforeJson"));
        Assert.DoesNotContain("PasswordHash", before, StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, before, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResendStaysNeutralWhileCooldownAndCapAreStillEnforced()
    {
        const string userName = "guest.demo";
        var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        var accountId = await AccountIdAsync(userName);
        var proof = fixture.Delivery.ProofFor(accountId)!;

        // Immediate resend is refused on the server but the answer is byte-for-byte the same as a
        // successful one, and the already delivered proof is still the only live one.
        var immediate = await client.PostAsJsonAsync("/api/auth/recovery/resend", new RecoveryRequest(userName));
        var neutral = SystemMessages.Get("MSG12");
        Assert.Equal(HttpStatusCode.OK, immediate.StatusCode);
        Assert.Equal(neutral, (await immediate.Content.ReadFromJsonAsync<SystemMessage>())!);
        Assert.Equal(neutral, (await (await client.PostAsJsonAsync("/api/auth/recovery/resend",
            new RecoveryRequest("no.such.account." + Guid.NewGuid().ToString("N")[..8])))
            .Content.ReadFromJsonAsync<SystemMessage>())!);
        var verify = await client.PostAsJsonAsync("/api/auth/recovery/verify", new RecoveryVerifyRequest(userName, proof));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        // Past the cooldown the resend goes through, invalidating the earlier proof.
        await ForceLastResendAsync(accountId, DateTimeOffset.UtcNow.AddHours(-1));
        await client.PostAsJsonAsync("/api/auth/recovery/resend", new RecoveryRequest(userName));
        var replaced = fixture.Delivery.ProofFor(accountId)!;
        Assert.NotEqual(proof, replaced);
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, proof)), HttpStatusCode.BadRequest, "MSG108");
        var verifyReplaced = await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest(userName, replaced));
        Assert.Equal(HttpStatusCode.OK, verifyReplaced.StatusCode);
        Assert.NotNull((await verifyReplaced.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!.ResetGrant);

        // Past the cap no new proof is issued, so the last one stays live.
        await ForceResendCountAsync(accountId, 99);
        await client.PostAsJsonAsync("/api/auth/recovery/resend", new RecoveryRequest(userName));
        Assert.Equal(replaced, fixture.Delivery.ProofFor(accountId));
    }

    [Fact]
    public async Task ValidationUsesTheSpecifiedMessages()
    {
        var client = fixture.CreateClient();
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/request",
            new RecoveryRequest("")), HttpStatusCode.BadRequest, "MSG02");
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/request",
            new RecoveryRequest(new string('x', 100))), HttpStatusCode.BadRequest, "MSG03");
        await AssertMessageAsync(await client.PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest("dentist.demo", "")), HttpStatusCode.BadRequest, "MSG02");

        var (reset, grant) = await CompleteRecoveryAsync("dentist.demo");
        await AssertMessageAsync(reset, HttpStatusCode.OK, "MSG13");

        // A weak new password is refused by the shared password policy.
        await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest("dentist.demo"));
        var accountId = await AccountIdAsync("dentist.demo");
        var proof = fixture.Delivery.ProofFor(accountId)!;
        var verify = await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/verify",
            new RecoveryVerifyRequest("dentist.demo", proof));
        var newGrant = (await verify.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!.ResetGrant;
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset",
            new RecoveryResetRequest("dentist.demo", newGrant, "short", "short")), HttpStatusCode.BadRequest, "MSG109");
        // A mismatched confirmation is refused before any change.
        await AssertMessageAsync(await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/reset",
            new RecoveryResetRequest("dentist.demo", newGrant, NewPassword, "different")), HttpStatusCode.BadRequest, "MSG04");
    }

    [Fact]
    public async Task AdminAssistedRecoveryRequiresAdminPermission()
    {
        var accountId = await AccountIdAsync("dentist.demo");

        var anonymous = fixture.CreateClient();
        await AssertMessageAsync(await anonymous.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}",
            new { }), HttpStatusCode.Unauthorized, "MSG07");

        var (dentist, _) = await Login("dentist.demo");
        await AssertMessageAsync(await dentist.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}",
            new { }), HttpStatusCode.Forbidden, "MSG06");
        await AssertMessageAsync(await dentist.PostAsJsonAsync($"/api/admin/recovery/reset/{accountId}",
            new { NewPassword, ConfirmPassword = NewPassword }), HttpStatusCode.Forbidden, "MSG06");
        await AssertMessageAsync(await dentist.PostAsJsonAsync($"/api/admin/recovery/verify/{accountId}",
            new RecoveryVerifyRequest("dentist.demo", "123456")), HttpStatusCode.Forbidden, "MSG06");

        var (admin, _) = await Login("admin.demo");
        var started = await admin.PostAsJsonAsync($"/api/admin/recovery/request/{accountId}", new { });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var startedBody = (await started.Content.ReadFromJsonAsync<AdminRecoveryResponse>())!;
        Assert.Equal(accountId, startedBody.AccountId);

        // Without verification the Admin reset is refused.
        await AssertMessageAsync(await admin.PostAsJsonAsync($"/api/admin/recovery/reset/{accountId}",
            new { NewPassword, ConfirmPassword = NewPassword }), HttpStatusCode.BadRequest, "MSG108");
    }

    [Fact]
    public async Task RecoveryStatusIsAdminOnlySoItCannotBeUsedToEnumerateAccounts()
    {
        const string userName = "guest.demo";
        var accountId = await AccountIdAsync(userName);

        // There is deliberately no anonymous status endpoint: once a request had been made for an
        // account it would confirm that the account exists.
        var anonymous = fixture.CreateClient();
        var missingEndpoint = await anonymous.GetAsync("/api/auth/recovery/status?identifier=" + userName);
        Assert.True(missingEndpoint.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            $"Anonymous status unexpectedly returned {missingEndpoint.StatusCode}.");

        await fixture.CreateClient().PostAsJsonAsync("/api/auth/recovery/request", new RecoveryRequest(userName));
        var (dentist, _) = await Login("dentist.demo");
        await AssertMessageAsync(await dentist.GetAsync($"/api/admin/recovery/status/{accountId}"),
            HttpStatusCode.Forbidden, "MSG06");

        var (admin, _) = await Login("admin.demo");
        var status = await admin.GetFromJsonAsync<RecoveryStatusSnapshot>($"/api/admin/recovery/status/{accountId}");
        Assert.True(status!.HasActiveRequest);
        Assert.Equal("Email", status.ChannelType);
        Assert.True(status.SecondsUntilExpiry > 0);
    }

    private async Task LockAccountAsync(HttpClient admin, int accountId)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var version = await VersionAsync(connection, accountId);
        var response = await admin.PutAsJsonAsync($"/api/accounts/{accountId}/state",
            new DCMS.Application.Features.Accounts.AccountStateRequest(version, true, "Kiểm thử khôi phục"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task UnlockAccountAsync(HttpClient admin, int accountId)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var version = await VersionAsync(connection, accountId);
        var response = await admin.PutAsJsonAsync($"/api/accounts/{accountId}/state",
            new DCMS.Application.Features.Accounts.AccountStateRequest(version, false, "Kiểm thử khôi phục"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<int> VersionAsync(Npgsql.NpgsqlConnection connection, int accountId)
    {
        await using var command = new Npgsql.NpgsqlCommand("""SELECT "Version" FROM "USER_ACCOUNT" WHERE "Id" = @id""", connection);
        command.Parameters.AddWithValue("id", accountId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string[]> RolesAsync(Npgsql.NpgsqlConnection connection, int accountId)
    {
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT r."Name" FROM "USER_ACCOUNT_ROLE" ur JOIN "ROLE" r ON r."Id" = ur."RoleId"
            WHERE ur."UserId" = @id ORDER BY r."Name"
            """, connection);
        command.Parameters.AddWithValue("id", accountId);
        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) roles.Add(reader.GetString(0));
        return [.. roles];
    }

    private async Task ExpireActiveTokensAsync(int accountId)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            UPDATE "RECOVERY_TOKEN" SET "ExpiresAt" = now() - interval '1 hour'
            WHERE "AccountId" = @id AND "UsedAt" IS NULL
            """, connection);
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ForceLastResendAsync(int accountId, DateTimeOffset when)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            UPDATE "RECOVERY_TOKEN" SET "LastResentAt" = @when
            WHERE "AccountId" = @id AND "UsedAt" IS NULL
            """, connection);
        command.Parameters.AddWithValue("when", when);
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ForceResendCountAsync(int accountId, int count)
    {
        await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("""
            UPDATE "RECOVERY_TOKEN" SET "ResendCount" = @count
            WHERE "AccountId" = @id AND "UsedAt" IS NULL
            """, connection);
        command.Parameters.AddWithValue("count", count);
        command.Parameters.AddWithValue("id", accountId);
        await command.ExecuteNonQueryAsync();
    }
}
