using System.Text.Json;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Treatment;

// Development-only sample data: services and reusable treatment plans for a small/medium dental clinic.
// Rules and sources: docs/specs/TREATMENT-CATALOG-SEED.md. Prices are reference values inside the researched ranges, not an official price list.
public sealed class DevelopmentTreatmentCatalogSeeder(DcmsDbContext db, TimeProvider clock)
{
    private sealed record Svc(string Name, string Unit, int Minutes, long? Price);
    private sealed record Step(string Service, string Note, int Quantity = 1, int? Minutes = null);
    private sealed record Plan(string Name, Step[] Steps);

    private const string Exam = "Khám";
    private static readonly Svc[] Services =
    [
        new("Chụp phim panorama", "phim", 10, 250_000), new("Chụp phim quanh chóp", "phim", 5, null), new("Chụp CT Cone Beam", "phim", 15, null),
        new("Cạo vôi răng, đánh bóng", "2 hàm", 40, 300_000), new("Cạo vôi răng nhiều", "2 hàm", 60, 700_000), new("Bôi fluor", "lần", 15, 300_000),
        new("Trám bít hố rãnh", "răng", 20, 200_000), new("Điều trị ê buốt răng", "lần", 20, null),
        new("Trám răng GIC", "răng", 25, 200_000), new("Trám răng composite", "răng", 35, 400_000), new("Trám composite thẩm mỹ răng trước", "răng", 50, 700_000),
        new("Hàn cổ răng", "răng", 25, 300_000), new("Đóng khe thưa bằng composite", "2 răng", 60, 1_750_000),
        new("Mở tủy giảm đau", "răng", 30, null), new("Điều trị tủy răng 1 chân", "răng", 60, 1_000_000), new("Điều trị tủy răng hàm nhỏ", "răng", 75, 1_550_000),
        new("Điều trị tủy răng hàm lớn", "răng", 90, 2_900_000), new("Chốt sợi, cùi giả", "răng", 30, 1_500_000),
        new("Nhổ răng đơn giản", "răng", 25, 350_000), new("Nhổ răng khôn hàm trên độ 1-2", "răng", 45, 1_450_000), new("Nhổ răng khôn hàm dưới độ 1-2", "răng", 60, 2_200_000),
        new("Cắt chỉ, tái khám sau nhổ", "lần", 15, 100_000), new("Rạch dẫn lưu áp xe lợi", "lần", 30, 900_000),
        new("Nạo túi nha chu (túi không quá 5 mm)", "túi", 45, 400_000), new("Cắt lợi trùm, tạo hình lợi nhỏ", "răng", 40, 1_250_000),
        new("Trám răng sữa", "răng", 25, 300_000), new("Nhổ răng sữa", "răng", 15, 100_000), new("Lấy tủy răng sữa và trám", "răng", 40, 750_000),
        new("Mão răng sứ kim loại", "răng", 60, 2_750_000), new("Mão răng sứ Titanium", "răng", 60, 2_500_000), new("Mão răng toàn sứ Zirconia", "răng", 60, 6_250_000),
        new("Mão răng Emax", "răng", 60, 9_000_000), new("Cầu răng sứ kim loại", "đơn vị", 60, 2_750_000), new("Inlay/Onlay sứ", "răng", 50, 3_250_000),
        new("Mặt dán sứ Veneer", "răng", 75, 9_000_000), new("Hàm tháo lắp nhựa", "hàm", 30, 3_000_000), new("Hàm tháo lắp khung kim loại", "hàm", 30, 7_500_000),
        new("Tháo mão, phục hình tạm", "răng", 25, 300_000), new("Lấy dấu, thử khung phục hình", "lần", 45, null), new("Gắn phục hình, giao hàm", "lần", 30, null),
        new("Tẩy trắng răng tại nhà", "2 hàm", 25, 1_400_000), new("Tẩy trắng răng tại ghế", "2 hàm", 75, 2_250_000),
        new("Niềng răng mắc cài kim loại (ca đơn giản)", "ca", 90, 32_500_000), new("Tái khám siết niềng răng", "lần", 25, null), new("Hàm duy trì sau niềng", "hàm", 20, 1_500_000),
        new("Cắm trụ Implant (ca đơn giản)", "trụ", 60, 14_000_000), new("Abutment Implant", "cái", 30, 6_500_000), new("Mão răng trên Implant", "răng", 45, 6_750_000),
        new("Tái khám", "lần", 20, null),
    ];

    private static readonly Plan[] Plans =
    [
        new("Sâu răng đơn giản 1-2 mặt", [new(Exam, "Khám và chụp phim quanh chóp nếu cần."), new("Trám răng composite", "1 buổi. Nếu đau tự phát hoặc sâu vào tủy thì chuyển sang kế hoạch điều trị tủy.")]),
        new("Điều trị tủy răng 1 chân và bọc mão", [new(Exam, "Khám, hỏi bệnh sử, chụp phim."), new("Mở tủy giảm đau", "Buổi 1, kê thuốc giảm đau nếu cần."),
            new("Điều trị tủy răng 1 chân", "Buổi 2-3: sửa soạn và trám bít ống tủy."),
            new("Chốt sợi, cùi giả", "Làm cùi nếu răng mất nhiều mô."), new("Lấy dấu, thử khung phục hình", "Lấy dấu để gửi labo."), new("Mão răng sứ kim loại", "Giá có thể đổi theo vật liệu mão."),
            new("Gắn phục hình, giao hàm", "Gắn mão, chỉnh khớp cắn."), new("Tái khám", "Tái khám sau 1-2 tuần.")]),
        new("Điều trị tủy răng hàm và bọc mão", [new(Exam, "Khám, chụp phim quanh chóp."), new("Mở tủy giảm đau", "Buổi 1."),
            new("Điều trị tủy răng hàm lớn", "2-3 buổi, trám tạm giữa các buổi."),
            new("Chốt sợi, cùi giả", "Phục hồi cùi sau tủy."), new("Lấy dấu, thử khung phục hình", "Lấy dấu gửi labo."), new("Mão răng toàn sứ Zirconia", "Có thể thay bằng sứ kim loại tùy nhu cầu."),
            new("Gắn phục hình, giao hàm", "Gắn mão."), new("Tái khám", "Tái khám sau 1-2 tuần.")]),
        new("Cạo vôi răng và viêm lợi nhẹ", [new(Exam, "Khám, đánh giá túi nha chu."),
            new("Cạo vôi răng, đánh bóng", "Kèm hướng dẫn vệ sinh răng miệng."), new("Tái khám", "Tái khám sau 2-4 tuần, sau đó 6 tháng/lần.")]),
        new("Nhổ răng khôn mọc lệch nhẹ", [new(Exam, "Khám và chụp phim panorama."), new("Chụp phim panorama", "Đánh giá hướng mọc của răng khôn."),
            new("Nhổ răng khôn hàm dưới độ 1-2", "1 buổi. Hàm trên dùng dịch vụ nhổ răng khôn hàm trên."), new("Cắt chỉ, tái khám sau nhổ", "Sau 5-7 ngày.")]),
        new("Mất 1 răng: làm implant đơn lẻ", [new(Exam, "Khám, cạo vôi trước nếu cần."), new("Chụp CT Cone Beam", "Đánh giá xương."),
            new("Cắm trụ Implant (ca đơn giản)", "Buổi đặt trụ, cắt chỉ sau 7 ngày. Chờ tích hợp xương khoảng 3 tháng hàm dưới, 6 tháng hàm trên."),
            new("Abutment Implant", "Sau khi trụ tích hợp xương."), new("Lấy dấu, thử khung phục hình", "Lấy dấu gửi labo."), new("Mão răng trên Implant", "Giá phụ thuộc vật liệu mão."),
            new("Gắn phục hình, giao hàm", "Gắn mão, chỉnh khớp cắn."), new("Tái khám", "Tái khám định kỳ.")]),
        new("Mất 1 răng: làm cầu răng sứ", [new(Exam, "Khám, cạo vôi, điều trị bệnh nha chu trước nếu có."), new("Cầu răng sứ kim loại", "Mài 2 răng trụ (buổi 1). Số đơn vị: 3 (2 trụ và 1 răng thay), giá là 3 lần giá một đơn vị.", 3),
            new("Lấy dấu, thử khung phục hình", "Lấy dấu và gắn cầu tạm, thử khung."), new("Gắn phục hình, giao hàm", "Gắn cầu sứ."), new("Tái khám", "Tái khám sau 1-2 tuần.")]),
        new("Mất nhiều răng: hàm tháo lắp", [new(Exam, "Khám, đánh giá sống hàm, niêm mạc."), new("Lấy dấu, thử khung phục hình", "Lấy dấu sơ khởi, dấu cá nhân và ghi khớp cắn, thử răng sáp (khoảng 3 buổi).", 3),
            new("Hàm tháo lắp nhựa", "Có thể chọn hàm khung kim loại."), new("Gắn phục hình, giao hàm", "Giao hàm và hướng dẫn sử dụng."), new("Tái khám", "Chỉnh đau 1-2 lần.", 2)]),
        new("Tẩy trắng răng", [new(Exam, "Khám, cạo vôi, trám sâu trước khi tẩy."), new("Cạo vôi răng, đánh bóng", "Làm sạch trước tẩy trắng."), new("Tẩy trắng răng tại ghế", "Hoặc tẩy trắng tại nhà (giao khay)."),
            new("Tái khám", "Tái khám sau 1-2 tuần. Không nên tẩy khi có thai, men nhạy cảm nặng hoặc răng trước có trám/mão.")]),
        new("Trẻ em: sâu răng sữa", [new(Exam, "Khám, hướng dẫn phụ huynh."), new("Trám răng sữa", "20-30 phút/răng, 1-2 buổi."), new("Lấy tủy răng sữa và trám", "Chỉ khi viêm tủy: cắt tủy buồng hoặc lấy tủy toàn phần tùy tình trạng."),
            new("Bôi fluor", "Phòng sâu răng."), new("Tái khám", "Mỗi 3-6 tháng.")]),
        new("Niềng răng ca đơn giản", [new(Exam, "Khám, chụp phim, lấy dấu, tư vấn."), new("Cạo vôi răng, đánh bóng", "Cạo vôi, trám sâu trước khi gắn mắc cài."),
            new("Niềng răng mắc cài kim loại (ca đơn giản)", "Gắn mắc cài (khoảng 90 phút). Thời gian điều trị trung bình 18-24 tháng."),
            new("Tái khám siết niềng răng", "Tái khám 4-6 tuần/lần, khoảng 18-24 lần.", 20), new("Hàm duy trì sau niềng", "Tháo mắc cài và đeo hàm duy trì.")]),
        new("Răng vỡ lớn đã chữa tủy: bọc mão", [new(Exam, "Khám, chụp phim."), new("Chốt sợi, cùi giả", "Dựng cùi."), new("Lấy dấu, thử khung phục hình", "Lấy dấu gửi labo."),
            new("Mão răng sứ kim loại", "Hoặc toàn sứ theo nhu cầu."), new("Gắn phục hình, giao hàm", "Gắn mão."), new("Tái khám", "Tái khám sau 1-2 tuần.")]),
        new("Sâu nhiều răng", [new(Exam, "Khám, ưu tiên răng đau hoặc gần tủy."), new("Trám răng composite", "Trám 2-4 răng mỗi buổi, 3-5 buổi. Răng đã viêm tủy thì chuyển kế hoạch điều trị tủy.", 4),
            new("Cạo vôi răng, đánh bóng", "Làm sạch."), new("Bôi fluor", "Phòng sâu răng, tư vấn chế độ ăn."), new("Tái khám", "Tái khám sau 3 tháng.")]),
        new("Đau cấp: viêm tủy hoặc áp xe", [new(Exam, "Khám, chụp phim quanh chóp."), new("Mở tủy giảm đau", "Mở tủy, kê thuốc."),
            new("Rạch dẫn lưu áp xe lợi", "Chỉ khi có ổ áp xe lợi khu trú."), new("Tái khám", "Tái khám sau 24-48 giờ, rồi vào kế hoạch điều trị tủy hoặc nhổ răng.")]),
        new("Nhổ răng không giữ được rồi phục hình", [new(Exam, "Khám, hỏi bệnh sử (chống đông, đái tháo đường)."), new("Nhổ răng đơn giản", "1 buổi."), new("Cắt chỉ, tái khám sau nhổ", "Sau 7 ngày. Chờ lành thương 4-8 tuần tùy vị trí (ước lượng)."),
            new("Tái khám", "Sau đó chuyển sang kế hoạch implant, cầu răng hoặc hàm tháo lắp.")]),
        new("Duy trì định kỳ 6 tháng", [new(Exam, "Khám định kỳ."), new("Cạo vôi răng, đánh bóng", "Mỗi 6 tháng (3-4 tháng nếu có bệnh nha chu)."), new("Tái khám", "Hẹn lần sau.")]),
        new("Răng cửa thẩm mỹ: mặt dán sứ", [new(Exam, "Khám, chụp phim, chọn màu."), new("Cạo vôi răng, đánh bóng", "Làm sạch trước."), new("Mặt dán sứ Veneer", "2 răng trước, mài và lấy dấu. Giá phụ thuộc vật liệu.", 2),
            new("Lấy dấu, thử khung phục hình", "Thử veneer."), new("Gắn phục hình, giao hàm", "Gắn veneer."), new("Tái khám", "Tái khám sau 2 tuần.")]),
    ];

    public async Task<string> SeedAsync(bool refresh = false, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({Scheduling.WorkingScheduleService.TransactionLock})", ct);
        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Name == "S09 - Chi nhánh thử nghiệm", ct)
            ?? throw new InvalidOperationException("Chưa có chi nhánh demo: chạy --seed-demo trước.");
        var creator = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select u.Id).SingleOrDefaultAsync(ct);
        if (creator == 0) throw new InvalidOperationException("Chưa có tài khoản dentist.demo: chạy --seed-demo trước.");

        var existing = await db.Set<SchedulingService>().Where(x => x.BranchId == branch.Id).ToListAsync(ct);
        var addedServices = 0;
        foreach (var s in Services.Where(s => existing.All(x => x.Name != s.Name)))
        {
            var row = new SchedulingService { BranchId = branch.Id, Name = s.Name, Unit = s.Unit, BillingUnit = s.Unit, PublicDurationMinutes = s.Minutes,
                PublicBookable = false, ReferenceUnitPriceVnd = s.Price };
            db.Add(row); existing.Add(row); addedServices++;
        }
        await db.SaveChangesAsync(ct);
        var byName = existing.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g
            .OrderByDescending(x => x.Active).ThenByDescending(x => x.IsInitialExamination)
            .ThenByDescending(x => x.PublicBookable).ThenBy(x => x.Id).First());

        if (refresh)
        {
            var names = Plans.Select(p => p.Name).ToArray();
            foreach (var old in await db.TreatmentTemplates.Where(x => x.BranchId == branch.Id && x.DefinitionKind == "ItemPlan" && x.DeletedAt == null && names.Contains(x.Name)).ToListAsync(ct))
                old.DeletedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        var planNames = await db.TreatmentTemplates.Where(x => x.BranchId == branch.Id && x.DefinitionKind == "ItemPlan" && x.DeletedAt == null).Select(x => x.Name).ToListAsync(ct);
        var addedPlans = 0;
        foreach (var plan in Plans.Where(p => !planNames.Contains(p.Name)))
        {
            var template = new TreatmentTemplate { BranchId = branch.Id, Name = plan.Name, DefinitionKind = "ItemPlan", CreatedBy = creator, CreatedAt = clock.GetUtcNow() };
            db.Add(template); var position = 0;
            foreach (var step in plan.Steps)
            {
                if (!byName.TryGetValue(step.Service, out var service)) continue;
                await db.AddTreatmentDataAsync(new TreatmentTemplateItem { TemplateId = template.Id, Position = ++position, TeethJson = JsonSerializer.Serialize(Array.Empty<string>()), ServiceId = service.Id,
                    ServiceName = service.Name, EstimatedMinutes = step.Minutes ?? service.PublicDurationMinutes, Note = step.Note.Length > 1000 ? step.Note[..1000] : step.Note,
                    CostVnd = (service.ReferenceUnitPriceVnd ?? 0) * step.Quantity });
            }
            addedPlans++;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return $"Dịch vụ: thêm {addedServices}, đã có {Services.Length - addedServices}. Kế hoạch: thêm {addedPlans}, đã có {Plans.Length - addedPlans}.";
    }
}
