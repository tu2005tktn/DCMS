using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class PrescriptionModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Ignore<PatientPrescriptionItem>();

        model.Entity<PrescriptionAudit>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PatientPrescription>(b =>
        {
            b.ToTable("PRESCRIPTION");
            b.HasKey(x => x.Id);
            b.Property(x => x.Diagnosis).HasMaxLength(1000);
            b.Property(x => x.OtherDiagnosis).HasMaxLength(1000);
            b.Property(x => x.Advice).HasMaxLength(4000);
            b.Property(x => x.Version).HasDefaultValue(1).ValueGeneratedNever().IsConcurrencyToken();
            b.HasIndex(x => new { x.PatientId, x.PrescribedOn });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.PrescriberEmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.ComplexCollection(x => x.Items, c => c.ToJson("Items"));
        });

        model.Entity<PatientPrescriptionReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_PatientPrescriptionReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientPrescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
