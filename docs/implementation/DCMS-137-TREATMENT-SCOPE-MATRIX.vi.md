# DCMS-137 — Ma trận phạm vi và nghiệm thu sáu tình huống điều trị

Người thực hiện: Ngô Minh Trí (tổng hợp, chạy nghiệm thu, lưu bằng chứng). Tư chốt cách đo và phạm vi với nhóm. Vinh giải thích nghiệp vụ và sửa backend. Task này không xây engine điều trị.

**Trạng thái: In Progress.** Mục tiêu ≥70% là điều cần chứng minh, chưa phải kết quả.

| Mục | Nội dung |
| --- | --- |
| Nền code chạy | `main` `6d62c0d` (PR #47). `main` mới nhất `319430f` không build được backend (D-01) |
| Luồng tái sử dụng | Môi trường, fixture và script browser thật của [DCMS-109](INTEGRATION-UI-CHECKIN-TO-NEXT-VISIT.vi.md). Không tạo bộ test trùng: sáu ca dưới đây chỉ bổ sung phần mẫu/cá thể hóa |
| Dữ liệu | Schema PostgreSQL riêng, nạp `--seed-demo` và `--seed-clinic-catalog` (49 dịch vụ, 17 kế hoạch mẫu). Bệnh nhân giả, không dùng dữ liệu oDental |
| Nguồn yêu cầu | Người dùng ngày 30/10/2026: số hóa ít nhất 70% quy trình điều trị, phần còn lại nha sĩ tùy chỉnh theo bệnh nhân |

Nội dung dịch vụ, giá, thời lượng và các bước trong kế hoạch mẫu lấy từ dữ liệu mẫu phát triển ([TREATMENT-CATALOG-SEED.md](../specs/TREATMENT-CATALOG-SEED.md)). Đây **không phải phác đồ đã được nha sĩ duyệt**: mọi nội dung chuyên môn ghi **Chờ nha sĩ xác minh**.

## 1. Cách đo (đề xuất, chờ Tư chốt trước khi tính chính thức)

**Công thức:** Tỷ lệ số hóa = số thao tác nghiệp vụ trong phạm vi được thực hiện và lưu xuyên suốt trên DCMS ÷ tổng thao tác nghiệp vụ trong cùng phạm vi × 100%.

**Quy tắc đếm** (khóa trước khi chạy):

1. **Mẫu số** là danh sách thao tác ở mục 2 (50 thao tác). Không thêm hay bớt thao tác sau khi chạy để đổi tỷ lệ. Muốn đổi phạm vi thì phải chốt lại trước lần chạy sau, và ghi rõ là lần đo mới.
2. **Một thao tác được đếm vào tử số** khi đủ cả bốn điều kiện sau:
   - làm trên giao diện thật gọi API thật;
   - dữ liệu lưu vào PostgreSQL và còn sau khi tải lại;
   - đúng bệnh nhân, đúng phiếu;
   - bước sau đọc lại được mà không phải nhập lại.
3. **Đạt một phần, Không đạt và Bị chặn đều đếm 0** ở tử số và được liệt kê riêng. Mock không được đếm. Bấm "Hoàn tất" trên giao diện chỉ là thao tác, không phải bằng chứng.
4. **Không dùng trọng số tần suất,** vì chưa có số liệu tần suất của phòng khám. Khi có số liệu, công thức có trọng số phải được ghi riêng.
5. **Không coi số bước điều trị đã hoàn tất là tỷ lệ số hóa.**
6. **Kết quả của sáu ca không suy ra cho toàn danh mục.** Ma trận ở mục 4 ghi riêng trạng thái từng kế hoạch: chưa xác minh / đã xác minh / đã nghiệm thu.
7. **Ngoài phạm vi:** khám, chụp phim, xét nghiệm máu (module riêng); quản lý lô thuốc.

## 2. Danh sách thao tác (mẫu số đề xuất: 50)

### Chuỗi chung — chạy cho cả sáu ca (6 × 6 = 36)

| Mã | Thao tác cần số hóa |
| --- | --- |
| C1 | Chọn kế hoạch mẫu phù hợp và áp vào phiếu điều trị của bệnh nhân |
| C2 | Cá thể hóa: răng, bỏ hoặc thêm dòng, chi phí, ngày dự kiến |
| C3 | Ghi nhận bệnh nhân đồng ý: phần nào, ai đồng ý, khi nào |
| C4 | Ghi việc thực tế (actual) và tiến độ hạng mục |
| C5 | Phí và thu tiền theo quy tắc hiện hành: phí sinh từ việc đã làm, thu một phần, số còn nợ |
| C6 | Nhu cầu hẹn và buổi sau: hạng mục kế tiếp → đặt lịch điều trị |

### Thao tác riêng từng ca (11)

| Mã | Ca | Thao tác |
| --- | --- | --- |
| T1 | Trám | Một buổi: ghi việc đã làm, kết quả, đánh dấu hạng mục xong |
| U1 | Tủy | Một hạng mục qua nhiều buổi: làm một phần, hạng mục chưa xong, buổi sau làm tiếp trên cùng hạng mục |
| N1 | Chỉnh nha | Bước tái khám lặp, số buổi chưa cố định: ghi nhiều lần và hẹn tiếp mà không cần sửa mẫu |
| I1 | Implant | Chuyển giai đoạn do bác sĩ xác nhận, có khoảng chờ (tích hợp xương) |
| I2 | Implant | Phần tùy chọn (ví dụ CT) chỉ dùng khi chỉ định: bỏ được khỏi ca không chỉ định |
| M1 | Mão sứ | Gửi Labo: phiếu labo gắn việc đã làm, có ngày hẹn |
| M2 | Mão sứ | Nhận Labo: ghi ngày giao |
| M3 | Mão sứ | Điều chỉnh rồi hoàn tất (thử, chỉnh, gắn) |
| M4 | Mão sứ | Chờ Labo không chiếm ghế: không tạo giữ chỗ hay lịch |
| P1 | Nha chu | Tái đánh giá được ghi lại |
| P2 | Nha chu | Thêm hoặc lặp bước tương lai theo quyết định bác sĩ |

### Thao tác kiểm chéo (3)

| Mã | Thao tác |
| --- | --- |
| X1 | Sửa kế hoạch mẫu không làm đổi các ca đã áp dụng |
| X2 | Hủy lịch hoặc vắng mặt không xóa phần chưa làm; hạng mục lại cần hẹn |
| X3 | Xung đột ghế hoặc nhân sự bị chặn khi đặt lịch |

## 3. Sáu ca chạy

| Ca | Kế hoạch mẫu dùng | Cá thể hóa dự kiến |
| --- | --- | --- |
| Trám | Sâu răng đơn giản 1-2 mặt | Răng 46 |
| Tủy | Điều trị tủy răng 1 chân và bọc mão | Răng 21. Hạng mục "Điều trị tủy" làm qua 2 buổi |
| Chỉnh nha | Niềng răng ca đơn giản | Tái khám siết niềng lặp, chưa cố định số buổi |
| Implant | Mất 1 răng: làm implant đơn lẻ | Răng 36. Bỏ CT khi không chỉ định. Abutment sau khoảng chờ |
| Mão sứ | Răng vỡ lớn đã chữa tủy: bọc mão | Răng 16. Phiếu labo gửi/nhận, thử và chỉnh rồi gắn |
| Nha chu | Cạo vôi răng và viêm lợi nhẹ | Tái đánh giá, thêm "Nạo túi nha chu" theo quyết định bác sĩ |

Mỗi ca đi theo chuỗi C1→C6, cộng thêm thao tác riêng. Kết quả ghi ở mục 5.

## 4. Ma trận phạm vi

Ma trận gồm 17 kế hoạch mẫu trong dữ liệu mẫu và các nhóm không phải dịch vụ điều trị. Không suy ra toàn danh mục từ sáu ca.

**Các cột chung cho mọi dòng điều trị** (ghi ở đây để bảng gọn):
- **Tần suất:** chưa có số liệu.
- **Màn/API:** Hồ sơ bệnh nhân → Điều trị & Thanh toán. API `sheet-items/from-item-plan`, `sheet-items/*`, `payments`, `next-visit-candidates`.
- **Thao tác cần số hóa:** C1–C6 và thao tác riêng của ca, nếu có.

**Trạng thái:**
- **Đã nghiệm thu (DCMS-137):** đã chạy sáu ca ở mục 5. Nội dung chuyên môn của mẫu vẫn ghi **Chờ nha sĩ xác minh**.
- **Chưa xác minh:** chưa chạy ca nào; chỉ biết mẫu áp được vào phiếu vì cùng cơ chế.

| Dịch vụ / nhóm | Kế hoạch mẫu | Phần bác sĩ phải tùy chỉnh | Trạng thái | Bằng chứng | Task xử lý thiếu sót |
| --- | --- | --- | --- | --- | --- |
| Trám (phục hồi) | Sâu răng đơn giản 1-2 mặt | Răng, mặt; bỏ dòng Khám | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Trám | D-02 |
| Nội nha | Điều trị tủy răng 1 chân và bọc mão | Răng, số buổi tủy, vật liệu mão | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Tủy | D-02, D-03 |
| Nội nha | Điều trị tủy răng hàm và bọc mão | Như trên | Chưa xác minh | — | D-03 (cùng cơ chế) |
| Chỉnh nha | Niềng răng ca đơn giản | Số lần siết, ngày; giá tái khám (mẫu 0 ₫) | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Chỉnh nha | D-02, D-08 |
| Implant | Mất 1 răng: làm implant đơn lẻ | Răng, thời gian chờ tích hợp, bỏ CT khi không chỉ định | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Implant | D-02 |
| Phục hình cố định | Răng vỡ lớn đã chữa tủy: bọc mão | Răng, vật liệu, phiếu labo | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Mão sứ | D-02 |
| Phục hình cố định | Mất 1 răng: làm cầu răng sứ | Răng trụ, số đơn vị cầu | Chưa xác minh | — | — |
| Phục hình thẩm mỹ | Răng cửa thẩm mỹ: mặt dán sứ | Số răng, màu | Chưa xác minh | — | — |
| Phục hình tháo lắp | Mất nhiều răng: hàm tháo lắp | Loại hàm, số lần thử | Chưa xác minh | — | — |
| Nha chu / vệ sinh | Cạo vôi răng và viêm lợi nhẹ | Thêm nạo túi theo tái đánh giá | Đã nghiệm thu, chờ nha sĩ xác minh | Ca Nha chu | D-02 |
| Nha chu / vệ sinh | Duy trì định kỳ 6 tháng | Chu kỳ 3–6 tháng | Chưa xác minh | — | — |
| Tiểu phẫu | Nhổ răng khôn mọc lệch nhẹ | Hàm trên/dưới, mức độ | Chưa xác minh | — | — |
| Tiểu phẫu → phục hình | Nhổ răng không giữ được rồi phục hình | Chọn kế hoạch phục hình sau | Chưa xác minh | — | — |
| Cấp cứu | Đau cấp: viêm tủy hoặc áp xe | Có/không rạch dẫn lưu | Chưa xác minh | — | — |
| Trám nhiều răng | Sâu nhiều răng | Số răng mỗi buổi | Chưa xác minh | — | — |
| Nha nhi | Trẻ em: sâu răng sữa | Lấy tủy sữa khi viêm tủy | Chưa xác minh | — | — |
| Thẩm mỹ | Tẩy trắng răng | Tại ghế/tại nhà | Chưa xác minh | — | — |

**Không phải dịch vụ điều trị** (không tính vào tỷ lệ):

| Nhóm | Loại | Màn/API | Trạng thái |
| --- | --- | --- | --- |
| Dụng cụ / vật liệu (46 mục mẫu) | Vật tư, có tồn kho | Hộp "Vật liệu dùng" trên dòng quá trình; `MATERIAL-STOCK.md` | Chưa xác minh trong đợt này (chưa nhập kho mẫu) |
| Công việc labo (7 loại, 2 công ty) | Dịch vụ bên ngoài, không chiếm ghế | Phiếu labo trên dòng quá trình | Đã kiểm gửi/nhận ở ca Mão sứ |
| Thuốc | Hàng hóa, kê đơn mua ngoài | Module đơn thuốc riêng | Ngoài phạm vi task |
| Khám, chụp phim (Panorama, quanh chóp, CT) | Module khám/cận lâm sàng riêng | Phiếu khám, chỉ định cận lâm sàng | Ngoài phạm vi task. Kế hoạch mẫu có dòng "Khám" và chụp phim, bác sĩ phải bỏ (D-08) |

## 5. Kết quả

Chạy ngày 07/11/2026 trên browser thật (Playwright Chromium), web `127.0.0.1:8081` gọi API `localhost:5082`. Code `main` `6d62c0d`, PostgreSQL schema `dcms_test_tx137`, không giả lập API.

Mỗi kết quả được đọc lại bằng hai cách:
- tải lại trang (F5);
- API thật: đọc phiếu, danh sách labo, hạng mục cần hẹn. Số giữ chỗ ghế và số lịch hẹn đọc thẳng từ PostgreSQL.

**Tài khoản:**

| Tài khoản | Vai trò | Dùng cho |
| --- | --- | --- |
| `dentist.demo` | Nha sĩ | Thao tác lâm sàng |
| `dual.demo` | Trợ lý + Nha sĩ | Thu tiền (xem D-04) |
| `assistant.demo` | Trợ lý | Hủy lịch, đặt lịch |

### Tỷ lệ

| Cách tính | Kết quả |
| --- | --- |
| Theo quy tắc đã khóa ở mục 1–2 (mẫu số 50, chỉ đếm Đạt) | **42 / 50 = 84%** (tạm tính, chờ Tư chốt mẫu số) |
| Nếu nhóm **không** chấp nhận các dòng Đạt có ghi chú: C5 × 6 (D-04), I1, P1, M3 | 33 / 50 = 66% |
| Không đạt | 7 (C3 × 6, U1) |
| Đạt một phần | 1 (X2) |

Tỷ lệ này chỉ là của sáu ca trên mẫu số đề xuất, **chưa chứng minh được** toàn danh mục đạt 70%. Danh mục còn 11/17 kế hoạch "Chưa xác minh".

### Kết quả từng ca

Ký hiệu: Đ = Đạt, K = Không đạt, M = Đạt một phần, — = không áp dụng cho ca.

| Thao tác | Trám | Tủy | Chỉnh nha | Implant | Mão sứ | Nha chu |
| --- | --- | --- | --- | --- | --- | --- |
| C1 Chọn mẫu, áp vào phiếu | Đ | Đ | Đ | Đ | Đ | Đ |
| C2 Cá thể hóa | Đ | Đ | Đ | Đ | Đ | Đ |
| C3 Ghi đồng ý | K | K | K | K | K | K |
| C4 Actual và tiến độ | Đ | Đ | Đ | Đ | Đ | Đ |
| C5 Phí và thu tiền | Đ* | Đ* | Đ* | Đ* | Đ* | Đ* |
| C6 Hẹn buổi sau | Đ | Đ | Đ | Đ | Đ | Đ |
| Thao tác riêng | T1 Đ | U1 K | N1 Đ | I1 Đ*, I2 Đ | M1 Đ, M2 Đ, M3 Đ*, M4 Đ | P1 Đ*, P2 Đ |

\* Đạt có ghi chú, xem chi tiết bên dưới.

Kiểm chéo: X1 Đạt, X2 Đạt một phần, X3 Đạt.

### Chi tiết và bằng chứng

**Trám**
- C1: áp "Sâu răng đơn giản 1-2 mặt" được 2 dòng (Khám, Trám composite).
- C2: bỏ dòng Khám (đã khám ở module khám); răng 46, ngày 07/11; giữ nguyên sau F5.
- C4, T1: dòng quá trình ghi kết quả, phí 400.000 ₫ "Đã ghi nhận phí", hàng kế hoạch gạch ngang.
- C5: thu 200.000 ₫ tiền mặt; sau F5 "Đã trả 200.000 ₫, Còn nợ 200.000 ₫".
- C6: "Chưa có hạng mục nào trong kế hoạch cần hẹn". Đúng, vì ca chỉ một buổi.
- Ghi chú: việc đã làm không gắn Visit, vì phiếu nhập trực tiếp không cần lượt khám.

**Tủy**
- C1–C2: áp 8 dòng; bỏ Khám, răng 21, ngày cho từng bước.
- C4: buổi 1 mở tủy, buổi 2 sửa soạn ống tủy.
- U1 **Không đạt:**
  - "Thực hiện" tự gạch ngang hạng mục. "Bỏ hoàn tất" mở lại được.
  - Nhưng "Thực hiện" lần hai mở lại chính dòng cũ để sửa, nên không ghi được buổi thứ hai cho cùng hạng mục.
  - Sau buổi đầu, hạng mục đang làm dở biến khỏi danh sách cần hẹn.
- C5: thu 500.000 ₫ / 1.000.000 ₫.
- C6: đặt được "Chốt sợi, cùi giả" ngày 16/11.

**Chỉnh nha**
- C1–C2: áp 5 dòng; bỏ Khám, đặt ngày.
- C4: cạo vôi và gắn mắc cài.
- N1: tái khám lần 1, rồi "Sao chép" thêm lần kế tiếp mà không sửa mẫu. Chi phí tái khám 0 ₫ vì mẫu chưa có giá.
- C5: thu 10.000.000 ₫, còn nợ 22.800.000 ₫.
- C6: đặt tái khám siết niềng ngày 14/11.

**Implant**
- C1–C2: áp 8 dòng; bỏ Khám và CT. Abutment đặt 08/02/2027 (chờ tích hợp xương 3 tháng).
- I2: bước CT tùy chọn được bỏ khỏi ca không chỉ định; mẫu gốc không đổi.
- C4: đặt trụ răng 36; 5 giai đoạn sau giữ nguyên.
- I1 Đạt\*:
  - Hệ thống không tự chuyển giai đoạn. Hạng mục kế tiếp là Abutment, khoảng đặt được 04–12/02/2027.
  - Bác sĩ quyết định thời điểm bằng ngày dự kiến và nút Thực hiện.
  - Không có bản ghi riêng "đã xác nhận tích hợp xương".
- C5: thu 7.000.000 ₫ / 14.000.000 ₫.
- C6: đặt được sau khi thêm lịch làm việc giả tháng 2/2027 cho bác sĩ.

**Mão sứ**
- C1–C2: áp 6 dòng; bỏ Khám, răng 16.
- C4: chốt cùi, lấy dấu.
- M1: phiếu labo gắn dòng "Lấy dấu", hẹn 13/11; cột Labo hiện ngày hẹn. Loại công việc do script chọn mục đầu danh sách ("Cầu răng sứ kim loại"), không ảnh hưởng việc kiểm gửi/nhận.
- M2: ngày giao 13/11 lưu và đọc lại được. Theo LAB-ORDER.md, chưa có trạng thái theo dõi xưởng.
- M4: `RESOURCE_RESERVATION` 5→5, lịch hẹn của bệnh nhân 0→0.
- M3 Đạt\*: lần thử/chỉnh ghi bằng "Thêm việc đã làm", không gắn hạng mục; sau đó mão và gắn mão. Kế hoạch xong 4/5.
- C5: thu 1.000.000 ₫.
- C6: đặt tái khám ngày 28/11.

**Nha chu**
- C1–C2: áp 3 dòng; bỏ Khám.
- C4: cạo vôi.
- P1 Đạt\*: kết quả tái đánh giá ghi bằng văn bản ở dòng "Tái khám"; chưa có trường đo túi có cấu trúc.
- P2: thêm "Nạo túi nha chu" răng 46, 47 và lặp "Tái khám".
- C5: thu 100.000 ₫.
- C6: đặt đúng hạng mục có ngày dự kiến sớm nhất (Tái khám 21/11 trước Nạo túi 24/11).
- Script đã sửa nhầm ngày dự kiến của dòng Tái khám đã làm. Phần việc đã làm không bị ảnh hưởng.

**Kiểm chéo**
- X1: sửa mẫu "Sâu răng đơn giản" (giá 450.000 ₫, ghi chú mới). Phiếu ca Trám vẫn 400.000 ₫, ghi chú cũ.
- X2 Đạt một phần:
  - Hủy lịch "Chốt sợi" của ca Tủy: hạng mục Booked→NeedsBooking, số hạng mục chưa làm 6→6, ngày dự kiến giữ nguyên, giao diện hiện lại nút Đặt lịch.
  - Lệnh hủy phải gọi qua API, vì màn chi tiết lịch hẹn của nhân viên không có nút hủy hay vắng mặt (D-05).
  - Chưa kiểm vắng mặt (cần lịch đã qua giờ).
- X3: lịch 08:00 ngày 14/11, Ghế 1.
  - Cùng bác sĩ không còn giờ đó; bác sĩ khác không được dùng ghế đó.
  - Cố đặt trùng: 409 MSG27 (trùng bác sĩ) và 409 MSG28 (trùng ghế).
  - Form đặt lịch của lễ tân không hiện 08:00.

Không có lỗi JS. Không có API 4xx/5xx ngoài các lỗi chủ động thử ở X3.

## 6. Defect và việc cần chốt

| Mã | Mô tả | Tái hiện | Thuộc |
| --- | --- | --- | --- |
| D-01 | `main` `319430f` không build được backend: `PatientPrescriptionDto` khai báo hai lần trong `PrescriptionContracts.cs` (CS0101), do merge PR #45 | `dotnet build backend/DCMS.sln` trên `origin/main` | Tuấn, Triển (PR #45) |
| D-02 | Phiếu điều trị không ghi đồng ý của bệnh nhân. Hạng mục áp từ mẫu là `ClinicianEntry/Proposed`, `decision`/`counseling`/`decisionMaker` trống, nhưng vẫn được coi là hiệu lực khi hẹn và tính tiền | Áp mẫu → xem hạng mục | Vinh (nghiệp vụ), Tư (chốt có bắt buộc hay không) |
| D-03 | Một hạng mục nhiều buổi: "Thực hiện" lần hai mở lại dòng cũ; hạng mục làm dở biến khỏi danh sách cần hẹn | Ca Tủy: Thực hiện → Bỏ hoàn tất → Thực hiện | Vinh |
| D-04 | Không vai trò đơn nào vừa mở được phiếu điều trị vừa lập phiếu thu: trợ lý 403 ở tab Điều trị; quyền `finance.payments.*` chỉ cấp được cho vai trò Trợ lý (`RoleAllows`); Owner không thấy phiếu của bác sĩ khác. Hiện chỉ `dual.demo` thu được | `assistant.demo` mở tab Điều trị; `dentist.demo` bấm TRẢ TIỀN | Tư (phân quyền), Vinh (màn phiếu) |
| D-05 | Màn nhân viên không có nút hủy hay đánh vắng mặt lịch hẹn; chỉ có API và luồng hộp thư hỗ trợ | Mở chi tiết lịch hẹn | Chủ DCMS-106/S12 (PR #45 có thể đã thêm, chưa kiểm được vì D-01) |
| D-06 | Sau "Thực hiện", hạng mục vẫn `progress=NotStarted` (phiếu dùng `displayCompleted`) | Đọc `/api/dossiers/{id}` | Vinh (mức thấp) |
| D-07 | Thứ tự dòng kế hoạch thay đổi sau khi sửa nhiều dòng, không theo ngày hay thứ tự mẫu | Ca Tủy: sửa 7 dòng rồi F5 | Vinh (mức thấp) |
| D-08 | Kế hoạch mẫu có dòng "Khám" và chụp phim, vốn thuộc module khám riêng; quên bỏ thì có nguy cơ tính phí khám hai lần. Nhiều dịch vụ mẫu chưa có giá (mở tủy, lấy dấu, gắn phục hình, tái khám siết niềng → 0 ₫ "Chưa yêu cầu phí") | Áp bất kỳ mẫu nào | Tư/Vinh (dữ liệu mẫu), phòng khám (giá) |

**Cần Tư chốt trước khi tính chính thức:**
1. Mẫu số 50 thao tác và quy tắc đếm ở mục 1–2.
2. Các dòng Đạt có ghi chú (C5, I1, P1, M3) có được đếm hay không.
3. Có cần chạy thêm các kế hoạch "Chưa xác minh" để kết luận cho toàn danh mục.

**Cần nha sĩ xác minh:** nội dung, thời lượng, giá và số buổi của 17 kế hoạch mẫu. Hiện tất cả đều **chờ xác minh**; không tự ghi đã duyệt.

## 7. Chạy lại

1. Tạo schema `dcms_test_<tên>`, chạy API với `--migrate --seed-demo`, rồi `--seed-clinic-catalog`.
2. Thêm lịch làm việc giả cho Nha sĩ demo, chỉ trong schema thử. Ca Implant cần thêm lịch tới tháng 2/2027.
3. Tạo 6 bệnh nhân giả.
4. Làm theo mục 3 và 5. Mọi thao tác đi qua nút trên màn hình, không nhập ID:
   - **Kế hoạch điều trị** để áp mẫu.
   - Menu **⋯** của hàng: Sửa, Xóa, Thực hiện, Sao chép.
   - **Phiếu labo** trên dòng quá trình.
   - **TRẢ TIỀN…** để thu tiền.
   - Tab **Lịch hẹn** → **Đặt lịch** để hẹn buổi sau.

Script Playwright và ảnh chụp của lần chạy này để ở thư mục tạm của phiên làm việc, không commit (theo quy ước test để local).
