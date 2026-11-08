using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class TreatmentModel
{
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConv<T>() where T : class =>
        new(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? (T)(object)(typeof(T).IsArray ? Array.CreateInstance(typeof(T).GetElementType()!, 0) : Activator.CreateInstance(typeof(T))!));
    private static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<T> JsonCompare<T>() where T : class =>
        new((a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null), (System.Text.Json.JsonSerializerOptions?)null)!);

    public static void Configure(ModelBuilder model)
    {
        model.Entity<TreatmentDossier>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER");
            b.Property(x=>x.SourceExaminationJson).HasColumnType("jsonb");
            b.HasOne<DCMS.Domain.Features.Visits.Visit>().WithMany().HasForeignKey(x=>x.SourceExaminationVisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.UpdatedAt });
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Diagnosis).HasMaxLength(4000);
            b.Property(x => x.Note).HasMaxLength(4000);
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.DisplayCompleted).HasDefaultValue(false);
            b.Property(x => x.ClosureOutcome).HasMaxLength(32);
            b.Property(x => x.ClosureReason).HasMaxLength(1000);
            b.Property(x => x.PlanState).HasMaxLength(16);
            b.Property(x => x.Goals).HasMaxLength(4000);
            b.Property(x => x.PaymentTermsJson).HasColumnType("jsonb");
            b.Property(x => x.DepositExceptionReason).HasMaxLength(1000);
            b.HasIndex(x => x.SourceTemplateId);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingMember>().WithMany().HasForeignKey(x => new { x.BranchId, EmployeeId = x.DentistId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        ClinicalWorkModel.Configure(model);
        model.Entity<TreatmentItem>(b =>
        {
            b.ToTable("TREATMENT_PLAN_ITEM");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlannedOrder });
            b.Property(x => x.ServiceName).HasMaxLength(160);
            b.Property(x => x.EntrySource).HasMaxLength(24);
            b.Property(x => x.Material).HasMaxLength(160);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.DecisionPlanVersion).HasColumnType("integer");
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Quantity).HasPrecision(12, 3);
            b.Property(x => x.Decision).HasMaxLength(16);
            b.Property(x => x.Progress).HasMaxLength(16);
            b.Property(x => x.Counseling).HasMaxLength(4000);
            b.Property(x => x.DecisionMaker).HasMaxLength(160);
            b.Property(x => x.RepresentativeBasis).HasMaxLength(1000);
            b.Property(x => x.DiscountType).HasMaxLength(16);
            b.Property(x => x.DiscountReason).HasMaxLength(1000);
            b.Property(x => x.DiscountStatus).HasMaxLength(16);
            b.Property(x => x.DiscountBasisHash).HasMaxLength(64);
            b.Property(x => x.DiscountValue).HasPrecision(18, 3);
            b.Property(x => x.WarrantyPolicy).HasMaxLength(160);
            b.Property(x => x.WarrantyPolicyVersion).HasMaxLength(40);
            b.Property(x => x.WarrantyScope).HasMaxLength(1000);
            b.Property(x => x.WarrantyComponent).HasMaxLength(500);
            b.Property(x => x.WarrantyExclusions).HasMaxLength(1000);
            b.Property(x => x.WarrantyExplanation).HasMaxLength(2000);
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_ITEM_VALUES", "\"Quantity\" > 0 AND \"AgreedUnitPriceVnd\" >= 0"));
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_ITEM_DURATION_V23", "(\"EstimatedMinMinutes\" IS NULL AND \"EstimatedMaxMinutes\" IS NULL OR \"EstimatedMinMinutes\" > 0 AND \"EstimatedMinMinutes\" <= \"EstimatedMinutes\" AND \"EstimatedMinutes\" <= \"EstimatedMaxMinutes\" AND \"EstimatedMaxMinutes\" <= 480) AND (\"ReservedMinutes\" IS NULL OR \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\")"));
        });
        model.Entity<TreatmentEvent>(b =>
        {
            b.ToTable("TREATMENT_EVENT");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentReceipt>(b =>
        {
            b.ToTable("TREATMENT_RECEIPT");
            b.HasKey(x => x.RequestKey);
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentSheetDraft>(b =>
        {
            b.ToTable("TREATMENT_SHEET_DRAFT");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.Kind, x.UpdatedAt });
            b.Property(x => x.Kind).HasMaxLength(12);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.LastFingerprint).HasMaxLength(64);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentRevisionDraft>(b =>
        {
            b.ToTable("TREATMENT_REVISION_DRAFT");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.ItemId, x.ActorId }).IsUnique();
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentTemplate>(b =>
        {
            b.Property(x => x.DefinitionKind).HasMaxLength(24).HasDefaultValue("LegacySteps");
            b.ToTable("TREATMENT_TEMPLATE"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BranchId, x.Name });
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentTemplateItem>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE_ITEM"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TemplateId, x.Position });
            b.HasOne<TreatmentTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DCMS.Domain.Features.Accounts.EmployeeProfile>().WithMany().HasForeignKey(x => x.DentistId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DCMS.Domain.Features.Accounts.EmployeeProfile>().WithMany().HasForeignKey(x => x.AssistantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.TeethJson).HasColumnType("jsonb");
            b.Property(x => x.ServiceName).HasMaxLength(200);
            b.Property(x => x.DentistName).HasMaxLength(200);
            b.Property(x => x.AssistantName).HasMaxLength(200);
            b.Property(x => x.Note).HasMaxLength(1000);
        });
        model.Entity<TreatmentTemplateVersion>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE_VERSION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
            b.HasIndex(x => x.TemplateId).IsUnique().HasDatabaseName("IX_TREATMENT_TEMPLATE_VERSION_Active")
                .HasFilter("\"State\" = 'Active'");
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.StepsJson).HasColumnType("jsonb");
        });
        model.Entity<TreatmentStep>(b =>
        {
            b.ToTable("TREATMENT_STEP"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlanVersion });
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Capability).HasMaxLength(160);
            b.Property(x => x.LabRequirement).HasMaxLength(160);
            b.Property(x => x.ChargingCoverage).HasMaxLength(40);
            b.Property(x => x.Outcome).HasMaxLength(16);
            b.Property(x => x.HoldReason).HasMaxLength(1000);
            b.Property(x => x.ReviewCondition).HasMaxLength(1000);
            b.Property(x => x.PredecessorsJson).HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_STEP_DURATION", "\"EstimatedMinutes\" > 0 AND \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\"");
                t.HasCheckConstraint("CK_STEP_ESTIMATE_RANGE", "(\"EstimatedMinMinutes\" IS NULL AND \"EstimatedMaxMinutes\" IS NULL) OR (\"EstimatedMinMinutes\" IS NOT NULL AND \"EstimatedMaxMinutes\" IS NOT NULL AND \"EstimatedMinMinutes\" > 0 AND \"EstimatedMinMinutes\" <= \"EstimatedMinutes\" AND \"EstimatedMinutes\" <= \"EstimatedMaxMinutes\" AND \"EstimatedMaxMinutes\" <= 480)");
            });
        });
        model.Entity<FollowUpInstruction>(b =>
        {
            b.ToTable("FOLLOW_UP_INSTRUCTION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PatientId, x.Status });
            b.HasIndex(x => x.StepId).IsUnique().HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_ActiveStep")
                .HasFilter("\"StepId\" IS NOT NULL AND \"Status\" IN ('NeedsBooking','Booked')");
            b.HasIndex(x => new { x.DossierId, x.ItemId }).IsUnique()
                .HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly")
                .HasFilter("\"DossierId\" IS NOT NULL AND \"ItemId\" IS NOT NULL AND \"StepId\" IS NULL AND \"Status\" IN ('NeedsBooking','Booked')");
            b.HasIndex(x => new { x.StepId, x.SourceCompletionEventId }).IsUnique()
                .HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_Step_Completion")
                .HasFilter("\"StepId\" IS NOT NULL AND \"SourceCompletionEventId\" IS NOT NULL");
            b.HasIndex(x => x.ActiveAppointmentId).IsUnique().HasFilter("\"ActiveAppointmentId\" IS NOT NULL");
            b.Property(x => x.Purpose).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.Guidance).HasMaxLength(4000);
            b.Property(x => x.Capability).HasMaxLength(160);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FOLLOW_UP_WINDOW", "\"EarliestOn\" <= \"TargetOn\" AND \"TargetOn\" <= \"LatestOn\"");
                t.HasCheckConstraint("CK_FOLLOW_UP_DURATION", "\"DurationMinutes\" BETWEEN 15 AND 480 AND \"DurationMinutes\" % 15 = 0");
            });
        });
        model.Entity<DossierPayment>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER_PAYMENT"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PaidOn });
            b.Property(x => x.Method).HasMaxLength(20);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_DOSSIER_PAYMENT_TOTAL", "\"TotalVnd\" > 0");
                t.HasCheckConstraint("CK_DOSSIER_PAYMENT_METHOD", "\"Method\" IN ('Cash','Transfer')");
            });
        });
        model.Entity<DossierPaymentLine>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER_PAYMENT_LINE"); b.HasKey(x => new { x.PaymentId, x.ActivityId });
            b.HasIndex(x => x.ActivityId);
            b.HasOne<ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_DOSSIER_PAYMENT_LINE_AMOUNT", "\"AmountVnd\" > 0"));
        });
        model.Entity<DossierPayOsOrder>(b =>
        {
            b.ToTable("TREATMENT_PAYOS_ORDER"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrderCode).IsUnique();
            b.HasIndex(x => x.RequestKey).IsUnique();
            b.HasIndex(x => x.PaymentLinkId).IsUnique().HasFilter("\"PaymentLinkId\" IS NOT NULL");
            b.HasIndex(x => x.DossierId).IsUnique().HasDatabaseName("IX_TREATMENT_PAYOS_ORDER_ActiveDossier")
                .HasFilter("\"State\" IN ('Creating','Pending','Review')");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.State).HasMaxLength(20);
            b.Property(x => x.PaymentLinkId).HasMaxLength(100);
            b.Property(x => x.BankBin).HasMaxLength(12);
            b.Property(x => x.AccountNumber).HasMaxLength(40);
            b.Property(x => x.AccountName).HasMaxLength(160);
            b.Property(x => x.BankReference).HasMaxLength(100);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<DossierPayment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_PAYOS_ORDER_AMOUNT", "\"AmountVnd\" > 0"));
        });
        model.Entity<DossierPayOsOrderLine>(b =>
        {
            b.ToTable("TREATMENT_PAYOS_ORDER_LINE"); b.HasKey(x => new { x.OrderId, x.ActivityId });
            b.HasOne<ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_PAYOS_ORDER_LINE_AMOUNT", "\"AmountVnd\" > 0"));
        });
        model.Entity<LabOrder>(b =>
        {
            b.ToTable("LAB_ORDER"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.WorkName).HasMaxLength(160);
            b.Property(x => x.MarginOuter).HasMaxLength(20); b.Property(x => x.MarginInner).HasMaxLength(20); b.Property(x => x.MarginSide).HasMaxLength(20);
            b.Property(x => x.BridgeShapes).HasColumnType("jsonb").HasConversion(JsonConv<string[]>(), JsonCompare<string[]>());
            b.Property(x => x.Teeth).HasColumnType("jsonb").HasConversion(JsonConv<string[]>(), JsonCompare<string[]>());
            b.Property(x => x.Shade).HasColumnType("jsonb").HasConversion(JsonConv<Dictionary<string, string>>(), JsonCompare<Dictionary<string, string>>());
            b.Property(x => x.Note).HasMaxLength(1000); b.Property(x => x.Material).HasMaxLength(100); b.Property(x => x.Warranty).HasMaxLength(100);
            b.HasIndex(x => x.ActivityId).IsUnique(); b.HasIndex(x => x.DossierId);
            b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
            b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(LabOrderStatus.NotSent);
            b.HasIndex(x => new { x.BranchId, x.Status, x.DueOn });
            b.HasOne<ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LAB_ORDER_STATUS", "\"Status\" IN ('NotSent','Sent','Received','Delivered','Rework')");
                t.HasCheckConstraint("CK_LAB_ORDER_FEES", "\"TotalFeeVnd\" >= 0 AND \"PaidVnd\" >= 0 AND \"PaidVnd\" <= \"TotalFeeVnd\"");
                t.HasCheckConstraint("CK_LAB_ORDER_QUANTITY", "\"Quantity\" BETWEEN 1 AND 999");
            });
        });
        model.Entity<LabOrderStatusLog>(b =>
        {
            b.ToTable("LAB_ORDER_STATUS_LOG"); b.HasKey(x => x.Id);
            b.Property(x => x.FromStatus).HasMaxLength(20); b.Property(x => x.ToStatus).HasMaxLength(20); b.Property(x => x.Note).HasMaxLength(300);
            b.HasIndex(x => new { x.LabOrderId, x.ChangedAt });
            b.HasOne<LabOrder>().WithMany().HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TreatmentProgressionReceipt>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_RECEIPT"); b.HasKey(x => x.RequestKey);
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
        model.Entity<TreatmentProgressionEvent>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_EVENT"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.StableKey).IsUnique().HasFilter("\"StableKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AggregateId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(80);
            b.Property(x => x.StableKey).HasMaxLength(160);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
    }
}
