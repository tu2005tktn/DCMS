# Phí khám và thu ngân sau khám

## Làm rõ bàn giao thu ngân — 28/10/2026

Đã giới hạn link Chuyển thu ngân trên phiếu khám cho Visit Completed. Màn thanh toán ghi rõ phí Posted đã chốt không cần nhập lại; hộp thu tiền nêu số tiền thu lần này, tự điền dư từng khoản và chỉ chỉnh khi thu một phần. Không đổi quy tắc/API thu tiền. Kiểm tra read-only ca DEMO2810-11 xác nhận phí 100.000đ đã Posted từ bước kết thúc; browser API thật kiểm link ẩn khi đang khám/hiện khi Completed, không có nút xác nhận lại phí Posted và số thực thu điền sẵn đúng dư. Không ghi chứng từ trong kiểm tra. Web build đạt; cập nhật locator của luồng browser phí khám theo nhãn link mới.

Đặc tả: [EXAMINATION-BILLING](../specs/EXAMINATION-BILLING.vi.md). Áp dụng trên nhánh làm việc hiện tại, chưa commit/push.

## Luồng sử dụng

1. Người có quyền quản lý dịch vụ vào `/clinical-services`, chọn chi nhánh và sửa **Dịch vụ và giá khám**. Để trống giá là chưa cấu hình; nhập 0 là miễn phí theo bảng giá. Không tự đặt giá cho dữ liệu thật.
2. Bác sĩ ghi và xác nhận phiếu khám. Khi **Kết thúc lượt khám**, kiểm tra phí lấy từ dịch vụ Khám đã đặt. Đổi phí/miễn khác bảng giá cần quyền và lý do. Chưa có giá hoặc chủ động để trống thì kết thúc lâm sàng với phí chờ xác định.
3. Việc kết thúc ghi phí và đóng lượt trong cùng transaction. Không cần có điều trị đã thực hiện, không tự thu tiền, không cần thanh toán hết trước khi lập kế hoạch điều trị.
4. Chọn **Thanh toán khám**, hoặc thu ngân mở `/payments`. Hàng chờ có cả lượt chỉ khám và lượt đang chờ xác định phí. Thu ngân áp dụng giá bảng cho phí đang chờ; người có quyền mới được đổi giá/miễn khác bảng.
5. Sổ phí hiển thị khám cùng các cận lâm sàng đã thực hiện. Thu tiền mặt toàn bộ/một phần hoặc lưu chuyển khoản chờ. Chuyển khoản chỉ tăng số đã thu sau xác nhận tiền vào, với mã giao dịch và nội dung đối chiếu. Có phiếu thu và phân bổ vào từng khoản.
6. Phí 0 được ghi rõ miễn phí, không tạo phiếu thu 0. Chưa có giá không được coi là hết nợ. Phí chờ xác định khác với chuyển khoản chờ xác minh.

## Tính toàn vẹn và giới hạn

- Một phí khám cho một Visit; unique source, request key, expected version và advisory lock ngăn ghi trùng hoặc dùng bảng giá cũ.
- Snapshot giá/tên dịch vụ không đổi khi sửa danh mục hoặc đính chính bệnh án. Khoản đã ghi không sửa trực tiếp.
- Không tự tính lại lượt khám lịch sử. Bổ sung phí lượt cũ cần thao tác ghi nhận riêng và căn cứ.
- Kiểm tra quyền và chi nhánh trên server; thu ngân không tự miễn/đổi giá khác bảng.
- Migration `20261027164034_ExaminationFees` chỉ bổ sung bảng và chỉ mục. Bản chạy manual dùng migrate-only, không seed; số lượt khám, phiếu khám, tài liệu trước/sau được đối chiếu và giữ nguyên.
- Phạm vi nối thanh toán là phí khám + CLS. Luồng ghi phí điều trị mới của Vinh vẫn dùng outbox riêng; không coi kế hoạch điều trị là khoản thu của lượt khám.
- Chưa mở rộng hoàn tiền, đảo phiếu thu đã ghi, đặt cọc hoặc trả tiền cho đơn vị thuê ngoài.

## Bằng chứng

- Kiểm thử API/PostgreSQL thật: chỉ khám, khám + CLS đã thực hiện/đọc kết quả, giá trống/0, miễn/giảm có lý do, dữ liệu sai rollback, stale price, chạy đồng thời/replay, lịch sử, scope/quyền, thu một phần và xác nhận chuyển khoản. Log `.artifacts/examination-billing-verified-tests.log`.
- Playwright chạy API và PostgreSQL riêng: cấu hình giá 100.000 → kết thúc khám bình thường không răng bệnh → hàng chờ thu ngân → tiền mặt 30.000 → chuyển khoản chờ 70.000 → xác nhận → còn phải thu 0. **1/1 passed**, không mock. Log `.artifacts/examination-billing-browser.log`, ảnh `.artifacts/examination-billing-paid.png` đã kiểm tra trực quan.
- `npm run build` thành công; build API Release thành công. Có cảnh báo bundle lớn và cảnh báo Pkcs đã tồn tại.
- Manual API/database healthy trên 5084 và web HTTP 200 trên 8084. Log `.artifacts/examination-billing-manual-runtime.log` ghi `MANUAL_RECORD_COUNTS_PRESERVED` và `MANUAL_API_AND_DATABASE_HEALTHY`.

Kết quả API: **16/16 ca phí khám mới đạt**. Đợt regression 114 ca đạt 112, hai ca fixture buổi điều trị bị phụ thuộc giờ chạy gần nửa đêm. Đã sửa fixture dùng lại khung giờ hợp lệ sau khi giải phóng reservation synthetic của buổi đã xong; chạy lại cả nhóm tách khám/điều trị đạt **3/3** (`.artifacts/examination-billing-separation-final.log`). Thay đổi này chỉ ở dữ liệu kiểm thử, không nới điều kiện tiếp nhận production.
