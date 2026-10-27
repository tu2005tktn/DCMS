# S58 — Nối danh mục vào hồ sơ bệnh nhân (UC-51/UC-38, đợt 3)

Nguồn: DCMS_Screen_Specifications_v2.5 S05 (Ngày lập hồ sơ, nguồn, nghề nghiệp, nhóm khách hàng, tiền sử), S58 chi tiết §5.2 (Hồ sơ bắt buộc), §6 (SET-01); UC-38 bước 2; GBR-176; SET-AT-05/09. Đợt trước: [Thiết lập chung](S58-GENERAL-SETTINGS.md), [Danh mục tham chiếu](S58-REFERENCE-CATALOGS.md).

## Phạm vi

Thêm vào hồ sơ bệnh nhân (PATIENT_PROFILE) các trường hành chính gắn danh mục S58:

| Trường | API | Ràng buộc |
|---|---|---|
| Ngày lập hồ sơ | `profileDate` (date) | Không ở tương lai theo UTC+07; không phải ngày Visit đầu. Server không tự điền; form tạo mới mặc định hôm nay và có nút "Hôm nay". |
| Nguồn | `sourceId` | Mục `patient-sources` |
| Phân nhóm khách hàng | `customerGroupId` | Mục `customer-groups`; chỉ phân loại, không tạo ưu đãi/giá |
| Nghề nghiệp | `occupationId` | Mục `occupations` |
| Tiền sử (lời khai) | `intakeHistoryIds[]` + `intakeHistoryOther` (≤1000) | Mục `intake-history`; không trùng ID. Lưu thời điểm/người ghi nhận lần thay đổi gần nhất. Không thay cho ghi nhận chuyên môn S19. |

Chưa có trong đợt này: Phone 2, email, Facebook, người giới thiệu, lý do đến lần đầu (S05 khác, không thuộc S58).

## Quy tắc

- Chọn mới phải là mục **đang dùng** đúng kind; sai kind/không tồn tại/đã ngừng → 400 MSG04 kèm `field`. Giá trị đã gắn từ trước được giữ nguyên khi sửa trường khác dù mục đã ngừng dùng (SET-01).
- Hồ sơ bắt buộc: bật được cả `profileDate`, `source`, `customerGroup` (bỏ lỗi `SETTINGS_FIELD_UNAVAILABLE` của đợt 1). Thiếu → 400 MSG02 với `field` = `profileDate`/`sourceId`/`customerGroupId`. Áp dụng ở lần tạo/sửa hành chính tiếp theo của nhân viên; không backfill.
- "Hồ sơ của tôi" (bệnh nhân tự sửa) không có các trường này: giữ nguyên giá trị, và chỉ kiểm bắt buộc với các trường có trong form của bệnh nhân (ngày sinh, giới tính, SĐT, địa chỉ) để không khóa vĩnh viễn việc tự sửa.
- Preview Thiết lập chung đếm hồ sơ chưa ngừng dùng đang thiếu cho cả ba trường mới khi vừa bật.
- Audit hành chính ghi tên mục trước/sau (ví dụ "Nguồn: — → Khác"); tiền sử ghi danh sách tên.

## API

- `CreatePatientRequest`/`UpdatePatientRequest` thêm `profileDate?`, `sourceId?`, `customerGroupId?`, `occupationId?`, `intakeHistoryIds?` (thiếu = rỗng), `intakeHistoryOther?`. Client cũ không gửi các trường này sẽ xóa giá trị khi sửa; form web luôn gửi đủ.
- `PatientDetail` thêm `profileDate`, `source`, `customerGroup`, `occupation` dạng `{ id, name, active }` hoặc null, `intakeHistory: [{ id, name, active }]`, `intakeHistoryOther`, `intakeRecordedAt`, `intakeRecordedByName`.
- Form lấy lựa chọn qua `GET /api/reference-options?kind=…` (đợt 2).

## Dữ liệu

Cột mới nullable trong PATIENT_PROFILE: `ProfileDate` date, `SourceId`/`CustomerGroupId`/`OccupationId` uuid FK Restrict → REFERENCE_ENTRY, `IntakeHistoryIds` uuid[] (mặc định rỗng), `IntakeHistoryOther` varchar(1000), `IntakeRecordedAt` timestamptz, `IntakeRecordedBy` int. Migration không suy hay điền giá trị cho hồ sơ cũ.

## UI web

Form S05/S06 thêm nhóm "Thông tin bổ sung": Ngày lập hồ sơ (mặc định hôm nay khi tạo, nút Hôm nay), Nguồn, Phân nhóm khách hàng, Nghề nghiệp (Select có tìm kiếm từ reference-options; mục đã gắn nhưng ngừng dùng vẫn hiện với nhãn "(ngừng dùng)"), Tiền sử (checkbox) và Tiền sử khác. Dấu bắt buộc theo `form-policy`. Hồ sơ S06 hiển thị các trường này. Màn Thiết lập chung bỏ khóa ba ô bắt buộc.

## Nghiệm thu

Tạo/sửa có đủ trường; chọn mục ngừng dùng bị chặn nhưng giữ được mục đã gắn; bật bắt buộc nguồn/nhóm/ngày lập hồ sơ thì lưu thiếu bị MSG02, bệnh nhân tự sửa không bị chặn; ngày lập ở tương lai bị MSG04; preview đếm hồ sơ thiếu. Integration test trên schema tạm; web build.
