using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence.Migrations;

internal static class TreatmentMigrationSnapshot
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentDossier", b =>
        {
            b.ToTable("TREATMENT_DOSSIER");
            b.HasKey("Id");
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<int>("PatientId").HasColumnType("integer");
            b.Property<int>("BranchId").HasColumnType("integer");
            b.Property<int>("DentistId").HasColumnType("integer");
            b.Property<DateOnly>("StartedOn").HasColumnType("date");
            b.Property<DateTimeOffset?>("ClosedAt").HasColumnType("timestamp with time zone");
            b.Property<int?>("ClosedBy").HasColumnType("integer");
            b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
            b.Property<Guid>("PlanId").HasColumnType("uuid");
            b.Property<int>("PlanVersion").HasColumnType("integer");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasIndex("Code").IsUnique();
            b.HasIndex("PatientId", "BranchId", "UpdatedAt");
            b.HasIndex("BranchId", "DentistId");
            b.Property<string>("Code").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property<string>("Diagnosis").IsRequired().HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property<string>("Note").HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property<string>("State").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("ClosureOutcome").HasMaxLength(32).HasColumnType("character varying(32)");
            b.Property<string>("ClosureReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<string>("PlanState").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("Goals").HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property<string>("PaymentTermsJson").IsRequired().HasColumnType("jsonb");
            b.HasOne("DCMS.Domain.Features.Patients.PatientProfile", null).WithMany().HasForeignKey("PatientId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingBranch", null).WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingMember", null).WithMany()
                .HasForeignKey("BranchId", "DentistId")
                .OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity("DCMS.Domain.Features.Treatment.TreatmentItem", b =>
        {
            b.ToTable("TREATMENT_PLAN_ITEM");
            b.HasKey("Id");
            b.Property<Guid>("Id").HasColumnType("uuid");
            b.Property<Guid>("DossierId").HasColumnType("uuid");
            b.Property<int?>("SourceDiagnosisId").HasColumnType("integer");
            b.Property<int>("ServiceId").HasColumnType("integer");
            b.Property<int?>("EstimatedMinutes").HasColumnType("integer");
            b.Property<DateOnly?>("EstimatedStart").HasColumnType("date");
            b.Property<DateOnly?>("EstimatedEnd").HasColumnType("date");
            b.Property<long>("AgreedUnitPriceVnd").HasColumnType("bigint");
            b.Property<int>("PlannedOrder").HasColumnType("integer");
            b.Property<DateTimeOffset?>("DecidedAt").HasColumnType("timestamp with time zone");
            b.Property<int?>("DecidedBy").HasColumnType("integer");
            b.Property<Guid?>("ConsentDocumentId").HasColumnType("uuid");
            b.Property<int?>("ConsentPlanVersion").HasColumnType("integer");
            b.Property<DateTimeOffset?>("WarrantyConfirmedAt").HasColumnType("timestamp with time zone");
            b.Property<DateOnly?>("WarrantyEndsOn").HasColumnType("date");
            b.Property<int?>("WarrantyConfirmedBy").HasColumnType("integer");
            b.HasIndex("DossierId", "PlannedOrder");
            b.Property<string>("ServiceName").IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("Region").HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<string>("Unit").IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property<decimal>("Quantity").HasPrecision(12, 3).HasColumnType("numeric(12,3)");
            b.Property<string>("Decision").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("Progress").IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property<string>("Counseling").HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property<string>("DecisionMaker").HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("RepresentativeBasis").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property<string>("WarrantyPolicy").HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property<string>("WarrantyPolicyVersion").HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property<string>("WarrantyScope").HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.HasOne("DCMS.Domain.Features.Treatment.TreatmentDossier", null).WithMany("Items").HasForeignKey("DossierId").OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_ITEM_VALUES", "\"Quantity\" > 0 AND \"AgreedUnitPriceVnd\" >= 0"));
        });
        model.Entity<TreatmentEvent>(b =>
        {
            b.ToTable("TREATMENT_EVENT");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.DossierVersion).HasColumnType("integer");
            b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
            b.HasIndex(x => new { x.DossierId, x.OccurredAt });
            b.Property(x => x.Kind).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentReceipt>(b =>
        {
            b.ToTable("TREATMENT_RECEIPT");
            b.HasKey(x => x.RequestKey);
            b.HasIndex(x => x.DossierId);
            b.Property(x => x.RequestKey).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.Command).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property(x => x.ResultJson).IsRequired().HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

