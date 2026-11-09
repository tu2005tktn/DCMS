using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class FinanceSettlementModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FinancePaymentMethod>(b =>
        {
            b.ToTable("FINANCE_PAYMENT_METHOD"); b.HasKey(x => new { x.BranchId, x.Code });
            b.Property(x => x.Code).HasMaxLength(32); b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinanceInvoice>(b =>
        {
            b.ToTable("FINANCE_INVOICE", t => { t.HasCheckConstraint("CK_FINANCE_INVOICE_SOURCE", "(\"VisitId\" IS NOT NULL) <> (\"DossierId\" IS NOT NULL)");
                t.HasCheckConstraint("CK_FINANCE_INVOICE_DATES", "\"DueDate\" >= \"InvoiceDate\""); });
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).HasMaxLength(64);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.DueDate });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinanceInvoiceLine>(b =>
        {
            b.ToTable("FINANCE_INVOICE_LINE", t => t.HasCheckConstraint("CK_FINANCE_INVOICE_LINE_AMOUNT", "\"GrossVnd\" >= 0 AND \"DiscountVnd\" >= 0 AND \"NetVnd\" = \"GrossVnd\" - \"DiscountVnd\""));
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ChargeId).IsUnique();
            b.HasOne<FinanceCharge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinanceAdvance>(b =>
        {
            b.ToTable("FINANCE_ADVANCE", t => t.HasCheckConstraint("CK_FINANCE_ADVANCE_KIND", "\"Kind\" IN ('Deposit','ServiceAdvance')")); b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasMaxLength(24);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.PaymentId).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PaymentReceipt>().WithOne().HasForeignKey<FinanceAdvance>(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinanceAdvanceAllocation>(b =>
        {
            b.ToTable("FINANCE_ADVANCE_ALLOCATION", t => t.HasCheckConstraint("CK_FINANCE_ADVANCE_ALLOCATION_AMOUNT", "\"AmountVnd\" > 0"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.AdvanceId, x.InvoiceLineId });
            b.HasOne<FinanceAdvance>().WithMany().HasForeignKey(x => x.AdvanceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FinanceInvoiceLine>().WithMany().HasForeignKey(x => x.InvoiceLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AllocatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinanceRefund>(b =>
        {
            b.ToTable("FINANCE_REFUND", t => { t.HasCheckConstraint("CK_FINANCE_REFUND_AMOUNT", "\"AmountVnd\" > 0");
                t.HasCheckConstraint("CK_FINANCE_REFUND_STATE", "\"State\" IN ('Requested','Approved','Completed','Rejected')");
                t.HasCheckConstraint("CK_FINANCE_REFUND_SOURCE", "(\"PaymentId\" IS NOT NULL) <> (\"DossierPaymentId\" IS NOT NULL)");
                t.HasCheckConstraint("CK_FINANCE_REFUND_TARGET", "(\"AdvanceId\" IS NOT NULL AND \"InvoiceId\" IS NULL AND \"ChargeId\" IS NULL) OR (\"InvoiceId\" IS NOT NULL AND \"ChargeId\" IS NOT NULL AND \"AdvanceId\" IS NULL)"); });
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).HasMaxLength(64);
            b.Property(x => x.Method).HasMaxLength(32);
            b.Property(x => x.State).HasMaxLength(24);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.RequestedAt });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FinanceInvoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FinanceCharge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FinanceAdvance>().WithMany().HasForeignKey(x => x.AdvanceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PaymentReceipt>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DossierPayment>().WithMany().HasForeignKey(x => x.DossierPaymentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CompletedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
