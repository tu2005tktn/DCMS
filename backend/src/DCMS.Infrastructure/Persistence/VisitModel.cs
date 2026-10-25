using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class VisitModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Visit>(b =>
        {
            b.ToTable("VISIT", t => {
                t.HasCheckConstraint("CK_VISIT_STATUS", "\"Status\" IN ('Queued','InProgress','Completed','Cancelled')");
                t.HasCheckConstraint("CK_VISIT_PURPOSE", "\"Purpose\" IN ('InitialExamination','TreatmentContinuation','PostoperativeOrWarrantyReview','PeriodicOrNewProblem')");
                t.HasCheckConstraint("CK_VISIT_ARRIVAL", "\"ActualArrivalAt\" <= \"EnteredAt\"");
            });
            b.HasIndex(x => x.AppointmentId).IsUnique();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.QueueOrder });
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Status).HasMaxLength(24);
            b.Property(x => x.Purpose).HasMaxLength(40);
            b.Property(x => x.ReviewReason).HasMaxLength(24);
            b.Property(x => x.AdditionalConcern).HasMaxLength(1000);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.IdentityNote).HasMaxLength(500);
            b.Property(x => x.ArrivalReason).HasMaxLength(500);
            b.Property(x => x.ClinicalPriority).HasMaxLength(24);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<Appointment>().WithOne().HasForeignKey<Visit>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.DentistId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.PriorityDentistId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EnteredBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ReceptionCommandReceipt>(b =>
        {
            b.ToTable("RECEPTION_COMMAND_RECEIPT");
            b.HasKey(x => new { x.ActorAccountId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<VisitAudit>(b =>
        {
            b.ToTable("VISIT_AUDIT");
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.VisitId, x.OccurredAt });
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.DecidingDentistId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ClinicalSourceReference>(b =>
        {
            b.ToTable("CLINICAL_SOURCE_REFERENCE", t => t.HasCheckConstraint("CK_CLINICAL_SOURCE_KIND", "\"Kind\" IN ('Dossier','Item','Step','Activity')"));
            b.Property(x => x.Kind).HasMaxLength(16);
            b.Property(x => x.Label).HasMaxLength(200);
            b.Property(x => x.Blocker).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.PatientId });
            b.HasAlternateKey(x => new { x.Id, x.BranchId, x.PatientId });
            b.HasOne<ClinicalSourceReference>().WithMany().HasForeignKey(x => new { x.ParentId, x.BranchId, x.PatientId })
                .HasPrincipalKey(x => new { x.Id, x.BranchId, x.PatientId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
