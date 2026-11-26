using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Treatment;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>Method-level tests of the pure domain rules: AppointmentRules, TreatmentPlanRules, TreatmentSheetRules, TreatmentItemRules.</summary>
public sealed class RulesMethodTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);

    // ---------- AppointmentRules.PublicLeadTimeValid ----------
    [Theory]
    [InlineData("UT-RUL-01 Cách 59 phút (dưới 60)", 59, false)]
    [InlineData("UT-RUL-02 Đúng 60 phút (biên)", 60, true)]
    [InlineData("UT-RUL-03 Cách 61 phút", 61, true)]
    [InlineData("UT-RUL-04 Quá khứ", -5, false)]
    public void PublicLeadTimeValid(string caseName, int minutes, bool expected) =>
        Assert.Equal(expected, AppointmentRules.PublicLeadTimeValid(Now, Now.AddMinutes(minutes)));

    // ---------- AppointmentRules.CanSelfService ----------
    [Theory]
    [InlineData("UT-RUL-05 Confirmed, còn 11h59", "Confirmed", 719, false)]
    [InlineData("UT-RUL-06 Confirmed, đúng 12h (biên)", "Confirmed", 720, true)]
    [InlineData("UT-RUL-07 Confirmed, còn 48h", "Confirmed", 2880, true)]
    [InlineData("UT-RUL-08 Cancelled, còn 48h", "Cancelled", 2880, false)]
    [InlineData("UT-RUL-09 CheckedIn, còn 48h", "CheckedIn", 2880, false)]
    [InlineData("UT-RUL-10 NoShow, còn 48h", "NoShow", 2880, false)]
    public void CanSelfService(string caseName, string status, int minutes, bool expected) =>
        Assert.Equal(expected, AppointmentRules.CanSelfService(status, Now.AddMinutes(minutes), Now));

    // ---------- AppointmentBookingInfo.Phone / Email ----------
    [Theory]
    [InlineData("UT-RUL-11 Liên hệ là số điện thoại", "0987654321", "0987654321", null)]
    [InlineData("UT-RUL-12 Liên hệ là email", "a@b.vn", null, "a@b.vn")]
    [InlineData("UT-RUL-13 Liên hệ rỗng", "", null, null)]
    [InlineData("UT-RUL-14 Liên hệ null", null, null, null)]
    public void BookingInfo_splits_contact_into_phone_or_email(string caseName, string? contact, string? phone, string? email)
    {
        var info = new AppointmentBookingInfo { BookerContact = contact };
        Assert.Equal(phone, info.Phone());
        Assert.Equal(email, info.Email());
    }

    // ---------- TreatmentPlanRules.Gross ----------
    [Theory]
    [InlineData("UT-RUL-15 1 x 600.000", "1", 600000, 600000)]
    [InlineData("UT-RUL-16 0,5 x 1.001 = 500,5 làm tròn lên", "0.5", 1001, 501)]
    [InlineData("UT-RUL-17 0,5 x 1.000 = 500", "0.5", 1000, 500)]
    [InlineData("UT-RUL-18 Số lượng 0", "0", 500000, 0)]
    public void Gross_rounds_half_away_from_zero(string caseName, string quantity, long price, long expected) =>
        Assert.Equal(expected, TreatmentPlanRules.Gross(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), price));

    [Fact]
    public void Gross_UT_RUL_19_overflow_throws() =>
        Assert.Throws<OverflowException>(() => TreatmentPlanRules.Gross(decimal.MaxValue, long.MaxValue));

    // ---------- TreatmentPlanRules.Discount ----------
    [Theory]
    [InlineData("UT-RUL-20 Fixed 300.000 trên 1.000.000", 1000000, "Fixed", "300000", 300000)]
    [InlineData("UT-RUL-21 Fixed bằng đúng gross (biên)", 1000000, "Fixed", "1000000", 1000000)]
    [InlineData("UT-RUL-22 Percent 10% trên 1.000.000", 1000000, "Percent", "10", 100000)]
    [InlineData("UT-RUL-23 Percent 100% (biên)", 1000000, "Percent", "100", 1000000)]
    [InlineData("UT-RUL-24 Percent 33,3% làm tròn", 1000, "Percent", "33.3", 333)]
    [InlineData("UT-RUL-25 Percent 0,05% của 1.000 = 0,5 làm tròn lên", 1000, "Percent", "0.05", 1)]
    public void Discount_valid(string caseName, long gross, string type, string value, long expected) =>
        Assert.Equal(expected, TreatmentPlanRules.Discount(gross, type, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("UT-RUL-26 gross âm", -1L, "Fixed", "1")]
    [InlineData("UT-RUL-27 Loại giảm giá lạ", 1000L, "Other", "1")]
    [InlineData("UT-RUL-28 Giá trị 0", 1000L, "Fixed", "0")]
    [InlineData("UT-RUL-29 Giá trị âm", 1000L, "Percent", "-5")]
    [InlineData("UT-RUL-30 Fixed có phần thập phân", 1000L, "Fixed", "10.5")]
    [InlineData("UT-RUL-31 Percent trên 100", 1000L, "Percent", "100.01")]
    [InlineData("UT-RUL-32 Fixed lớn hơn gross", 1000L, "Fixed", "1001")]
    public void Discount_invalid_throws(string caseName, long gross, string type, string value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TreatmentPlanRules.Discount(gross, type, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    // ---------- TreatmentSheetRules.IsFdi ----------
    [Theory]
    [InlineData("UT-RUL-33 Răng vĩnh viễn 11", 11, true)]
    [InlineData("UT-RUL-34 Răng vĩnh viễn 48 (biên trên)", 48, true)]
    [InlineData("UT-RUL-35 Răng 49 (quá 8)", 49, false)]
    [InlineData("UT-RUL-36 Răng 10 (số cuối 0)", 10, false)]
    [InlineData("UT-RUL-37 Răng sữa 51", 51, true)]
    [InlineData("UT-RUL-38 Răng sữa 85 (biên trên)", 85, true)]
    [InlineData("UT-RUL-39 Răng sữa 86 (quá 5)", 86, false)]
    [InlineData("UT-RUL-40 Nhóm 9x", 91, false)]
    [InlineData("UT-RUL-41 Số 0", 0, false)]
    [InlineData("UT-RUL-42 Số một chữ số", 5, false)]
    public void IsFdi(string caseName, int tooth, bool expected) => Assert.Equal(expected, TreatmentSheetRules.IsFdi(tooth));

    // ---------- TreatmentSheetRules.ValidQuantity ----------
    [Theory]
    [InlineData("UT-RUL-43 Số lượng 0", "0", "răng", false)]
    [InlineData("UT-RUL-44 Số lượng âm", "-1", "răng", false)]
    [InlineData("UT-RUL-45 4 chữ số thập phân", "1.0001", "ml", false)]
    [InlineData("UT-RUL-46 3 chữ số thập phân, đơn vị ml", "1.125", "ml", true)]
    [InlineData("UT-RUL-47 Đơn vị răng, số lẻ", "1.5", "răng", false)]
    [InlineData("UT-RUL-48 Đơn vị hàm, số lẻ", "0.5", "hàm", false)]
    [InlineData("UT-RUL-49 Đơn vị lần, số lẻ", "2.5", "lần", false)]
    [InlineData("UT-RUL-50 Đơn vị gói, số lẻ", "2.5", "gói", false)]
    [InlineData("UT-RUL-51 Đơn vị răng, số nguyên", "3", "răng", true)]
    [InlineData("UT-RUL-52 Đơn vị khác, số lẻ", "2.5", "ống", true)]
    public void ValidQuantity(string caseName, string quantity, string unit, bool expected) =>
        Assert.Equal(expected, TreatmentSheetRules.ValidQuantity(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), unit));

    // ---------- TreatmentSheetRules.RegionWithinScope ----------
    [Theory]
    [InlineData("UT-RUL-53 Giống hệt nhau (kể cả chữ)", "Hàm trên", "Hàm trên", true)]
    [InlineData("UT-RUL-54 Giống nhau sau khi cắt khoảng trắng", " Hàm trên ", "Hàm trên", true)]
    [InlineData("UT-RUL-55 Cả hai đều null", null, null, true)]
    [InlineData("UT-RUL-56 Thực hiện là tập con của kế hoạch", "11,12,13", "12", true)]
    [InlineData("UT-RUL-57 Thực hiện nằm ngoài kế hoạch", "11,12", "13", false)]
    [InlineData("UT-RUL-58 Thực hiện thêm răng ngoài kế hoạch", "11", "11 12", false)]
    [InlineData("UT-RUL-59 Kế hoạch không phải mã răng", "Hàm trên", "11", false)]
    [InlineData("UT-RUL-60 Kế hoạch rỗng, thực hiện có răng", null, "11", false)]
    [InlineData("UT-RUL-61 Kế hoạch có răng, thực hiện rỗng", "11", "", false)]
    [InlineData("UT-RUL-62 Mã răng không hợp lệ (19)", "19", "19 ", true)]
    [InlineData("UT-RUL-63 Mã răng không hợp lệ trong tập con", "19,11", "19", false)]
    public void RegionWithinScope(string caseName, string? agreed, string? actual, bool expected) =>
        Assert.Equal(expected, TreatmentSheetRules.RegionWithinScope(agreed, actual));

    // ---------- TreatmentItemRules ----------
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Theory]
    [InlineData("UT-RUL-64 Hạng mục chưa hoàn thành", false, true)]
    [InlineData("UT-RUL-65 Hạng mục đã hoàn thành", true, false)]
    public void IsPendingForBooking(string caseName, bool completed, bool expected) =>
        Assert.Equal(expected, TreatmentItemRules.IsPendingForBooking(new TreatmentItem { DisplayCompleted = completed }));

    [Fact]
    public void PlannedTarget_UT_RUL_66_returns_estimated_start_or_null()
    {
        Assert.Equal(Today, TreatmentItemRules.PlannedTarget(new TreatmentItem { EstimatedStart = Today }));
        Assert.Null(TreatmentItemRules.PlannedTarget(new TreatmentItem()));
    }

    [Theory]
    [InlineData("UT-RUL-67 Kế hoạch trong tương lai xa: giữ nguyên cửa sổ", 30, 26, 30, 34)]
    [InlineData("UT-RUL-68 Đầu cửa sổ trùng hôm nay (biên)", 4, 0, 4, 8)]
    [InlineData("UT-RUL-69 Trong cửa sổ: cắt phần quá khứ, target giữ nguyên", 2, 0, 2, 6)]
    [InlineData("UT-RUL-70 Target đã qua nhưng chưa quá hạn: target = hôm nay", -2, 0, 0, 2)]
    [InlineData("UT-RUL-71 Quá hạn đúng 4 ngày (biên): vẫn còn hôm nay", -4, 0, 0, 0)]
    [InlineData("UT-RUL-72 Quá hạn 5 ngày: coi như dời sang hôm nay", -5, 0, 0, 4)]
    public void BookingWindow(string caseName, int plannedOffset, int fromOffset, int targetOffset, int toOffset)
    {
        var window = TreatmentItemRules.BookingWindow(new TreatmentItem { EstimatedStart = Today.AddDays(plannedOffset) }, Today);
        Assert.Equal((Today.AddDays(fromOffset), Today.AddDays(targetOffset), Today.AddDays(toOffset)), window);
    }
}
