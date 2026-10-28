using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class FinanceModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SchedulingService>(b => { b.Property(x => x.InvestigationKind).HasMaxLength(16); b.Property(x => x.BillingUnit).HasMaxLength(40); });
        model.Entity<InvestigationOrder>(b =>
        {
            b.Property(x => x.BillingMode).HasMaxLength(24); b.Property(x => x.ServiceNameSnapshot).HasMaxLength(160); b.Property(x => x.UnitSnapshot).HasMaxLength(40);
            b.Property(x => x.QuoteEvidence).HasMaxLength(1000); b.Property(x => x.ProviderNameSnapshot).HasMaxLength(160);
            b.HasOne<SchedulingService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Provider>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.QuotedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PerformedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Provider>(b => { b.ToTable("PROVIDER"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedCode).HasMaxLength(40); b.Property(x => x.NormalizedName).HasMaxLength(160); b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => x.NormalizedCode).IsUnique(); b.HasIndex(x => x.NormalizedName).IsUnique(); });
        model.Entity<ExaminationFee>(b=>{
            b.ToTable("EXAMINATION_FEE");b.HasKey(x=>x.VisitId);b.Property(x=>x.State).HasMaxLength(16);b.Property(x=>x.Version).IsConcurrencyToken();
            b.Property(x=>x.ServiceName).HasMaxLength(160);b.Property(x=>x.Unit).HasMaxLength(40);b.Property(x=>x.Reason).HasMaxLength(1000);b.Property(x=>x.PriceSource).HasMaxLength(24);
            b.HasOne<Visit>().WithOne().HasForeignKey<ExaminationFee>(x=>x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingService>().WithMany().HasForeignKey(x=>x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FinanceCharge>().WithOne().HasForeignKey<ExaminationFee>(x=>x.ChargeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.CreatedBy).OnDelete(DeleteBehavior.Restrict);b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.PostedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x=>x.State);
        });
        model.Entity<FinancialAccount>(b => { b.ToTable("FINANCIAL_ACCOUNT"); b.HasKey(x => new { x.PatientId, x.BranchId }); b.Property(x => x.Version).IsConcurrencyToken(); b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<FinanceCharge>(b =>
        {
            b.ToTable("FINANCE_CHARGE", t => t.HasCheckConstraint("CK_FINANCE_CHARGE_AMOUNT", "\"AmountVnd\" >= 0 AND \"Quantity\" > 0 AND \"UnitPriceVnd\" >= 0")); b.HasKey(x => x.Id);
            b.Property(x => x.SourceKind).HasMaxLength(24); b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.Unit).HasMaxLength(40); b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.HasIndex(x => new { x.SourceKind, x.SourceId }).IsUnique(); b.HasIndex(x => new { x.PatientId, x.BranchId }); b.HasIndex(x => x.VisitId);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SchedulingService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<InvestigationOrder>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict); b.HasOne<ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PostedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ProviderPayable>(b => { b.ToTable("PROVIDER_PAYABLE"); b.HasKey(x => x.Id); b.Property(x => x.ProviderName).HasMaxLength(160); b.HasIndex(x => x.ChargeId).IsUnique(); b.HasOne<FinanceCharge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Provider>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<PaymentReceipt>(b =>
        {
            b.ToTable("PAYMENT_RECEIPT", t => t.HasCheckConstraint("CK_PAYMENT_RECEIPT", "\"AmountVnd\" > 0 AND \"Method\" IN ('Cash','Transfer') AND \"State\" IN ('Pending','Posted','Cancelled')")); b.HasKey(x => x.Id);
            b.Property(x => x.PatientName).HasMaxLength(160); b.Property(x => x.BranchName).HasMaxLength(160); b.Property(x => x.Payer).HasMaxLength(160); b.Property(x => x.Method).HasMaxLength(16); b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.Number).HasMaxLength(64); b.Property(x => x.Note).HasMaxLength(1000); b.Property(x => x.ConfirmationNote).HasMaxLength(1000); b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.DeclaredReference).HasMaxLength(100); b.Property(x => x.BankReference).HasMaxLength(100); b.Property(x => x.NormalizedBankReference).HasMaxLength(100); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.Number }).IsUnique(); b.HasIndex(x => new { x.BranchId, x.NormalizedBankReference }).IsUnique(); b.HasIndex(x => new { x.PatientId, x.BranchId });
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PaymentAllocation>(b => { b.ToTable("PAYMENT_ALLOCATION", t => t.HasCheckConstraint("CK_PAYMENT_ALLOCATION_AMOUNT", "\"AmountVnd\" > 0")); b.HasKey(x => new { x.PaymentId, x.ChargeId }); b.HasOne<FinanceCharge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<BranchReceiptSequence>(b => { b.ToTable("BRANCH_RECEIPT_SEQUENCE"); b.HasKey(x => x.BranchId); b.HasOne<SchedulingBranch>().WithOne().HasForeignKey<BranchReceiptSequence>(x => x.BranchId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<FinanceAudit>(b => { b.ToTable("FINANCE_AUDIT"); b.HasKey(x => x.Id); b.Property(x => x.Action).HasMaxLength(64); b.Property(x => x.Target).HasMaxLength(100); b.Property(x => x.PayloadJson).HasColumnType("jsonb"); b.HasIndex(x => new { x.BranchId, x.At }); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<FinanceCommandReceipt>(b => { b.ToTable("FINANCE_COMMAND_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey }); b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb"); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
    }
}
