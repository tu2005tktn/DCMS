using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class PrescriptionModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ExternalPrescription>(b =>
        {


            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.GeneralAdvice).HasMaxLength(1000);
            b.Property(x => x.Diagnosis).HasMaxLength(1000);
            b.Property(x => x.CorrectionReason).HasMaxLength(500);
            b.Property(x => x.VerificationCode).HasMaxLength(16);
            b.HasIndex(x => new { x.VisitId, x.Revision }).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.Status });
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExternalPrescription>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PrescribedByDoctorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.FinalizedBy).OnDelete(DeleteBehavior.Restrict);
        });



        model.Entity<PrescriptionTemplate>(b =>
        {

            b.Property(x => x.Code).HasMaxLength(40);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.DiagnosisKind).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });



        model.Entity<PrescriptionAudit>(b =>
        {

            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PrescriptionCommandReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_PrescriptionCommandReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PatientPrescription>(b =>
        {

            b.Property(x => x.Diagnosis).HasMaxLength(1000); b.Property(x => x.OtherDiagnosis).HasMaxLength(1000);
            b.Property(x => x.Advice).HasMaxLength(4000); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.PatientId, x.PrescribedOn });

            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.PrescriberEmployeeId).OnDelete(DeleteBehavior.Restrict);
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
