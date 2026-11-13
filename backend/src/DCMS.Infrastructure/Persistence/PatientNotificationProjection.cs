using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DCMS.Infrastructure.Persistence;

// Patient-visible events are projected in the same SaveChanges transaction as the clinical change.
// The existing notification worker sends FCM only after that transaction commits.
public sealed partial class DcmsDbContext
{
    private sealed record PatientEvent(int PatientId, int? BranchId, NotificationKind Kind,
        string Title, string Body, string Key, string? DeepLink);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await AddPatientNotificationsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task AddPatientNotificationsAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        var events = new List<PatientEvent>();
        static bool Became<TEntity>(EntityEntry<TEntity> entry, string property, string value) where TEntity : class =>
            entry.CurrentValues[property] as string == value &&
            (entry.State == EntityState.Added || entry.State == EntityState.Modified &&
                entry.OriginalValues[property] as string != value);

        foreach (var entry in ChangeTracker.Entries<ExternalPrescription>())
            if (Became(entry, nameof(ExternalPrescription.Status), PrescriptionStatuses.Finalized))
            {
                var p = entry.Entity;
                events.Add(new(p.PatientId, p.BranchId, NotificationKind.PrescriptionReady,
                    "Đơn thuốc đã sẵn sàng", "Bác sĩ đã hoàn tất đơn thuốc của bạn. Mở ứng dụng để xem.",
                    $"rx:{p.Id}:{p.Revision}", "/prescriptions"));
            }

        foreach (var entry in ChangeTracker.Entries<PatientPrescription>())
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified &&
                entry.Property(nameof(PatientPrescription.Version)).IsModified)
            {
                var prescription = entry.Entity;
                events.Add(new(prescription.PatientId, prescription.BranchId, NotificationKind.PrescriptionReady,
                    "Đơn thuốc mới của bạn", "Bác sĩ đã kê hoặc cập nhật đơn thuốc. Mở ứng dụng để xem chi tiết.",
                    $"patient-rx:{prescription.Id}:{prescription.Version}", $"/prescriptions/{prescription.Id}"));
            }

        var examinations = ChangeTracker.Entries<VisitExamination>().Where(e => e.Entity.Confirmed &&
            (e.State == EntityState.Added || e.State == EntityState.Modified &&
                (e.OriginalValues[nameof(VisitExamination.Confirmed)] as bool? != true ||
                 e.OriginalValues[nameof(VisitExamination.ContentJson)] as string != e.Entity.ContentJson))).ToArray();
        if (examinations.Length > 0)
        {
            var visitIds = examinations.Select(x => x.Entity.VisitId).ToArray();
            var visits = await Visits.AsNoTracking().Where(v => visitIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct);
            foreach (var entry in examinations)
                if (visits.TryGetValue(entry.Entity.VisitId, out var visit))
                    events.Add(new(visit.PatientId, visit.BranchId, NotificationKind.MedicalRecordUpdated,
                        "Kết quả khám đã cập nhật", "Hồ sơ khám của bạn có thông tin mới. Mở ứng dụng để xem.",
                        $"exam:{visit.Id}:{entry.Entity.UpdatedAt.UtcDateTime.Ticks}", $"/visit-results/{visit.Id}"));
        }

        foreach (var entry in ChangeTracker.Entries<Visit>())
            if (Became(entry, nameof(Visit.Status), VisitStatuses.Completed))
            {
                var v = entry.Entity;
                events.Add(new(v.PatientId, v.BranchId, NotificationKind.MedicalRecordUpdated,
                    "Lượt khám đã hoàn tất", "Kết quả khám của bạn đã được cập nhật. Mở ứng dụng để xem.",
                    $"visit-complete:{v.Id}:{v.Version}", $"/visit-results/{v.Id}"));
            }

        foreach (var entry in ChangeTracker.Entries<FollowUpInstruction>())
            if (Became(entry, nameof(FollowUpInstruction.Status), "NeedsBooking"))
            {
                var followUp = entry.Entity;
                events.Add(new(followUp.PatientId, followUp.BranchId, NotificationKind.FollowUpBookingRequested,
                    "Bác sĩ đề nghị đặt lịch", "Bạn có lịch tái khám cần đặt. Mở ứng dụng để xem hướng dẫn.",
                    $"follow-up:{followUp.Id}:{followUp.Version}", "/book-appointment"));
            }

        foreach (var entry in ChangeTracker.Entries<TreatmentDossier>())
            if (Became(entry, nameof(TreatmentDossier.PlanState), "Committed"))
            {
                var dossier = entry.Entity;
                events.Add(new(dossier.PatientId, dossier.BranchId, NotificationKind.TreatmentUpdated,
                    "Kế hoạch điều trị đã cập nhật", "Phòng khám đã cập nhật kế hoạch điều trị của bạn.",
                    $"plan:{dossier.Id}:{dossier.PlanVersion}", $"/medical-records/{dossier.Id}"));
            }

        foreach (var entry in ChangeTracker.Entries<ClinicalActivity>())
            if (Became(entry, nameof(ClinicalActivity.State), "Posted"))
            {
                var activity = entry.Entity;
                events.Add(new(activity.PatientId, activity.BranchId, NotificationKind.TreatmentUpdated,
                    "Hồ sơ điều trị đã cập nhật", "Phòng khám đã ghi nhận một buổi điều trị của bạn.",
                    $"activity:{activity.Id}:{activity.Version}",
                    activity.DossierId is { } id ? $"/medical-records/{id}" : null));
            }

        foreach (var entry in ChangeTracker.Entries<ClinicalDocument>())
            if (Became(entry, nameof(ClinicalDocument.FileState), "Ready"))
            {
                var document = entry.Entity;
                events.Add(new(document.PatientId, null, NotificationKind.MedicalRecordUpdated,
                    "Tài liệu khám đã sẵn sàng", "Bạn có tài liệu khám mới trong ứng dụng.",
                    $"document:{document.Id}:{document.Version}",
                    document.VisitId is { } id ? $"/visit-results/{id}" : null));
            }

        // Payment notifications (NotificationKind.PaymentRecorded) return with the finance module.

        if (events.Count == 0) return;
        var patientIds = events.Select(x => x.PatientId).Distinct().ToArray();
        var links = await PatientAccountLinks.AsNoTracking().Where(x => x.Status == PatientLinkStatus.Verified
                && x.PatientId.HasValue && patientIds.Contains(x.PatientId.Value))
            .Select(x => new { PatientId = x.PatientId!.Value, x.AccountId }).ToListAsync(ct);
        if (links.Count == 0) return;
        var accountIds = links.Select(x => x.AccountId).Distinct().ToArray();
        var pushEnabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Firebase__ServiceAccountPath"));
        var devices = pushEnabled
            ? (await PushDevices.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
                .Select(x => x.AccountId).Distinct().ToListAsync(ct)).ToHashSet()
            : [];
        var now = DateTimeOffset.UtcNow;
        var queued = new HashSet<string>();
        foreach (var item in events)
            foreach (var accountId in links.Where(x => x.PatientId == item.PatientId).Select(x => x.AccountId).Distinct())
            {
                var key = $"patient:{item.Key}:account:{accountId}";
                if (!queued.Add(key) || await InAppNotifications.AnyAsync(x => x.DedupeKey == key, ct)) continue;
                var notification = new DCMS.Domain.Features.Appointments.InAppNotification
                {
                    AccountId = accountId, BranchId = item.BranchId, Kind = item.Kind.ToString(),
                    PublicId = $"NT-{Guid.NewGuid():N}", Title = item.Title, Body = item.Body,
                    CreatedAt = now, DedupeKey = key, DeepLink = item.DeepLink
                };
                notification.Deliveries.Add(new() { Channel = "InApp", Status = "Delivered",
                    AttemptCount = 1, LastAttemptAt = now });
                if (devices.Contains(accountId))
                    notification.Deliveries.Add(new() { Channel = "Push", Status = "Pending",
                        NextRetryAt = now });
                InAppNotifications.Add(notification);
            }
    }
}
