using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence.Migrations;

// Frozen model state after 20261019143000_TreatmentProgression.
internal static class TreatmentProgressionMigrationSnapshot
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentDossier", b =>
        {
            b.Property<Guid?>("SourceTemplateId").HasColumnType("uuid");
            b.Property<int?>("SourceTemplateVersion").HasColumnType("integer");
            b.HasIndex("SourceTemplateId");
        });
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentTemplate", b =>
        {
            b.ToTable("TREATMENT_TEMPLATE"); b.HasKey("Id");
            b.HasIndex("BranchId", "Name");
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<int>("BranchId").HasColumnType("integer");
            b.Property<string>("Name").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<int>("Version").HasColumnType("integer").IsConcurrencyToken();
            b.Property<int>("CreatedBy").HasColumnType("integer");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingBranch", null).WithMany()
                .HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentTemplateVersion", b =>
        {
            b.ToTable("TREATMENT_TEMPLATE_VERSION"); b.HasKey("Id");
            b.HasIndex("TemplateId", "Number").IsUnique();
            b.HasIndex("TemplateId").IsUnique().HasDatabaseName("IX_TREATMENT_TEMPLATE_VERSION_Active")
                .HasFilter("\"State\" = 'Active'");
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<Guid>("TemplateId").HasColumnType("uuid");
            b.Property<int>("Number").HasColumnType("integer");
            b.Property<string>("State").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("StepsJson").IsRequired().HasColumnType("jsonb");
            b.Property<int>("CreatedBy").HasColumnType("integer");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset?>("ActivatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset?>("RetiredAt").HasColumnType("timestamp with time zone");
            b.HasOne("DCMS.Domain.Features.Treatment.TreatmentTemplate", null).WithMany()
                .HasForeignKey("TemplateId").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentStep", b =>
        {
            b.ToTable("TREATMENT_STEP"); b.HasKey("Id");
            b.HasIndex("DossierId", "PlanVersion");
            b.HasIndex("ItemId");
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<Guid>("DossierId").HasColumnType("uuid");
            b.Property<Guid>("PlanId").HasColumnType("uuid");
            b.Property<int>("PlanVersion").HasColumnType("integer");
            b.Property<Guid>("ItemId").HasColumnType("uuid");
            b.Property<string>("Name").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("Region").HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<int>("EstimatedMinutes").HasColumnType("integer");
            b.Property<int>("ReservedMinutes").HasColumnType("integer");
            b.Property<string>("Capability").HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("LabRequirement").HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<int?>("EarliestOffsetDays").HasColumnType("integer");
            b.Property<int?>("TargetOffsetDays").HasColumnType("integer");
            b.Property<int?>("LatestOffsetDays").HasColumnType("integer");
            b.Property<string>("ChargingCoverage").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property<string>("Outcome").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("HoldReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<string>("ReviewCondition").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<DateTimeOffset?>("HoldEffectiveAt").HasColumnType("timestamp with time zone");
            b.Property<Guid?>("CompletionEventId").HasColumnType("uuid");
            b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("PredecessorsJson").IsRequired().HasColumnType("jsonb");
            b.Property<int>("Version").HasColumnType("integer").IsConcurrencyToken();
            b.HasOne("DCMS.Domain.Features.Treatment.TreatmentDossier", null).WithMany().HasForeignKey("DossierId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Treatment.TreatmentItem", null).WithMany().HasForeignKey("ItemId").OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_STEP_DURATION", "\"EstimatedMinutes\" > 0 AND \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\""));
        });
        model.Entity<TreatmentProgressionReceipt>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_RECEIPT"); b.HasKey(x => x.RequestKey);
            b.Property(x => x.RequestKey).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.Command).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property(x => x.ResultJson).IsRequired().HasColumnType("jsonb");
        });
        model.Entity<TreatmentProgressionEvent>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_EVENT"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.StableKey).IsUnique().HasFilter("\"StableKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AggregateId, x.OccurredAt });
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.AggregateId).HasColumnType("uuid");
            b.Property(x => x.Kind).IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property(x => x.StableKey).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        });
    }
}
