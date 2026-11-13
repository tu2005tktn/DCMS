using System.Text.Json;
using System.Text.Json.Nodes;
using DCMS.Domain.Features.Examinations;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ExaminationStorageTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    private async Task<Guid> PrepareVisit()
    {
        var reception = new ReceptionApiTests(fixture);
        var setup = await reception.Prepare();
        using var client = await reception.Login();
        return (await reception.Check(client, setup)).VisitId;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoTablesPreserveExaminationInvestigationReviewAndLegacyReferencesAcrossRollback(bool minimalLegacySchema)
    {
        var visitId = await PrepareVisit();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var visit = await db.Visits.SingleAsync(x => x.Id == visitId);
        var actor = visit.RecordedBy;
        var now = fixture.Clock.Now;
        var document = new ClinicalDocument
        {
            PatientId = visit.PatientId, VisitId = visitId, FileName = "synthetic.pdf", ContentType = "application/pdf",
            FileState = "Ready", DocumentType = "XRay", Sha256 = new string('a', 64), SizeBytes = 100,
            SourceDate = DateOnly.FromDateTime(now.UtcDateTime), UploadedAt = now, UploaderId = actor, Version = 4
        };
        var order = new InvestigationOrder
        {
            VisitId = visitId, Name = "SYNTHETIC investigation", Reason = "SYNTHETIC reason", Region = "16",
            CreatedBy = actor, CreatedAt = now, CreateRequestKey = Guid.NewGuid(), State = "Reviewed", Version = 7,
            BillingMode = "PatientDirect", Quantity = 2, QuoteEvidence = "SYNTHETIC quote", QuotedBy = actor, QuotedAt = now,
            ResultText = "SYNTHETIC result", ResultBy = actor, ResultAt = now, ReviewedBy = actor, ReviewedAt = now,
            Interpretation = "SYNTHETIC interpretation", ResultDocumentIdsJson = JsonSerializer.Serialize(new[] { document.Id })
        };
        document.InvestigationId = order.Id;
        document.Reviews.Add(new DocumentReview { DocumentId = document.Id, VisitId = visitId, ReviewedBy = actor, ReviewedAt = now });
        visit.Investigations.Add(order);
        db.Add(document);
        db.Add(new VisitExamination { VisitId = visitId, ContentJson = "{\"symptoms\":\"SYNTHETIC\"}", Confirmed = true, UpdatedBy = actor, UpdatedAt = now });
        await db.SaveChangesAsync();
        var expectedOrder = OrderSnapshot(order);
        var expectedReviews = JsonSerializer.Serialize(document.Reviews);
        var previous = "20261112132059_ConsolidateSchedulingStorage";
        var migrator = db.GetService<IMigrator>();
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync(previous);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"CLINICAL_SOURCE_REFERENCE\"");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"CLINICAL_SOURCE_REFERENCE\" (\"Id\" uuid PRIMARY KEY, \"PatientId\" integer, \"BranchId\" integer, \"Label\" text, \"Blocker\" text)");
        if (!minimalLegacySchema)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CLINICAL_SOURCE_REFERENCE\" ADD COLUMN \"Kind\" varchar(40) NOT NULL DEFAULT 'Course', ADD COLUMN \"ParentId\" uuid, ADD COLUMN \"Version\" integer NOT NULL DEFAULT 1");
        var sourceId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CLINICAL_SOURCE_REFERENCE\" (\"Id\",\"PatientId\",\"BranchId\",\"Label\",\"Blocker\") VALUES ({sourceId},{visit.PatientId},{visit.BranchId},'SYNTHETIC legacy source','SYNTHETIC review required')");
        var sourceBefore = await db.Database.SqlQuery<string>($"SELECT to_jsonb(s)::text AS \"Value\" FROM \"CLINICAL_SOURCE_REFERENCE\" s WHERE \"Id\"={sourceId}").SingleAsync();
        await db.Database.MigrateAsync();
        var removedTables = await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ('VISIT_EXAMINATION','INVESTIGATION_ORDER','DOCUMENT_REVIEW','CLINICAL_SOURCE_REFERENCE')").SingleAsync();
        Assert.Equal(0, removedTables);
        var restoredVisit = await db.Visits.AsNoTracking().SingleAsync(x => x.Id == visitId);
        Assert.True(JsonNode.DeepEquals(expectedOrder, OrderSnapshot(Assert.Single(restoredVisit.Investigations))));
        var examination = await db.Set<VisitExamination>().AsNoTracking().SingleAsync(x => x.VisitId == visitId);
        Assert.True(examination.Confirmed);
        Assert.Equal(actor, examination.UpdatedBy);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"symptoms\":\"SYNTHETIC\"}"), JsonNode.Parse(examination.ContentJson)));
        var restoredDocument = await db.Set<ClinicalDocument>().AsNoTracking().SingleAsync(x => x.Id == document.Id);
        Assert.Equal(4, restoredDocument.Version);
        Assert.Equal(order.Id, restoredDocument.InvestigationId);
        Assert.Equal(expectedReviews, JsonSerializer.Serialize(restoredDocument.Reviews));
        Assert.Single(await db.Set<ClinicalSourceArchive>().ToArrayAsync());
        await migrator.MigrateAsync(previous);
        var sourceAfter = await db.Database.SqlQuery<string>($"SELECT to_jsonb(s)::text AS \"Value\" FROM \"CLINICAL_SOURCE_REFERENCE\" s WHERE \"Id\"={sourceId}").SingleAsync();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(sourceBefore), JsonNode.Parse(sourceAfter)));
        Assert.Equal(1, await db.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"INVESTIGATION_ORDER\" WHERE \"Id\"={order.Id}").CountAsync());
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task StaleVisitCannotOverwriteAConcurrentInvestigationChangeAtTheSameBusinessVersion()
    {
        var visitId = await PrepareVisit();
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var firstVisit = await first.Visits.SingleAsync(x => x.Id == visitId);
        var staleVisit = await second.Visits.SingleAsync(x => x.Id == visitId);
        firstVisit.Investigations.Add(new InvestigationOrder { VisitId = visitId, Name = "SYNTHETIC", Reason = "SYNTHETIC", CreatedBy = firstVisit.RecordedBy, CreatedAt = fixture.Clock.Now });
        await first.SaveChangesAsync();
        staleVisit.Note = "SYNTHETIC stale note";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        var saved = await second.Visits.SingleAsync(x => x.Id == visitId);
        Assert.Single(saved.Investigations);
        Assert.Equal(firstVisit.Version, saved.Version);
    }

    [Fact]
    public async Task StaleExaminationCannotOverwriteANewerVisitLoadedDuringSave()
    {
        var visitId = await PrepareVisit();
        using var scope = fixture.Services.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var visit = await first.Visits.SingleAsync(x => x.Id == visitId);
        first.Add(new VisitExamination { VisitId = visitId, UpdatedBy = visit.RecordedBy, UpdatedAt = fixture.Clock.Now });
        await first.SaveChangesAsync();
        using var staleScope = fixture.Services.CreateScope();
        var stale = staleScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var examination = await stale.Set<VisitExamination>().SingleAsync(x => x.VisitId == visitId);
        visit.Note = "SYNTHETIC current note";
        await first.SaveChangesAsync();
        examination.ContentJson = "{\"symptoms\":\"SYNTHETIC stale\"}";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    private static JsonNode OrderSnapshot(InvestigationOrder order)
    {
        var node = JsonSerializer.SerializeToNode(order)!;
        node[nameof(order.ResultDocumentIdsJson)] = JsonNode.Parse(order.ResultDocumentIdsJson);
        return node;
    }
}
