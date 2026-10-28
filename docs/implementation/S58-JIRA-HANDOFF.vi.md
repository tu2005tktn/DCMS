# S58 — Phân công triển khai trên Jira

Ngày giao việc: 27/10/2026. Epic [DCMS-118 — Thiết lập hệ thống](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-118).

Đã tạo 14 ticket, gán người nhận, ưu tiên và 39 liên kết Blocks theo phụ thuộc. Mỗi ticket chứa yêu cầu chi tiết, API, quyền, giới hạn dữ liệu và checklist SET-AT; đây là giao việc code, chưa phải kết quả nghiệm thu. Các issue lâm sàng/tài chính có sẵn được liên kết Relates để phối hợp, giữ nguyên owner/trạng thái.

| Mã kế hoạch | Jira | Người nhận | Phạm vi | Chờ |
| --- | --- | --- | --- | --- |
| SET-W01 | [DCMS-119](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-119) | Tư Nguyễn Văn | Nền tảng cấu hình version, audit, idempotency và contract dùng chung | Bắt đầu ngay |
| SET-W02 | [DCMS-120](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-120) | K17 HL Tống Nguyên Tuấn | Dựng giao diện Thiết lập hệ thống với 11 nhóm và deep link | DCMS-119 |
| SET-W03 | [DCMS-121](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-121) | Tư Nguyễn Văn | Danh mục chẩn đoán và danh mục phụ dùng chung cho hồ sơ và Khám | DCMS-119, DCMS-120 |
| SET-W04 | [DCMS-122](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-122) | Ngo Minh Tri (K15 HL) | Thiết lập trường hồ sơ bắt buộc và ba chế độ mã bệnh nhân | DCMS-121 |
| SET-W05 | [DCMS-123](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-123) | Nguyễn Thành Vinh | Cây danh mục dịch vụ, giá và thời lượng dùng chung với đặt lịch | DCMS-119, DCMS-121 |
| SET-W06 | [DCMS-124](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-124) | K17 HL Tống Nguyên Tuấn | Thông tin chi nhánh, logo và ghế với kiểm tra tác động | DCMS-119, DCMS-120 |
| SET-W07 | [DCMS-125](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-125) | Nguyễn Thành Vinh | Danh mục nhà cung cấp, vật liệu và loại công việc labo | DCMS-121, DCMS-124 |
| SET-W08 | [DCMS-126](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-126) | trientvhe181820 | Danh mục thuốc và lựa chọn cho đơn thuốc mua ngoài | DCMS-121 |
| SET-W09 | [DCMS-127](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-127) | Ngo Minh Tri (K15 HL) | Nhóm khách hàng và liên kết vào hồ sơ bệnh nhân | DCMS-121, DCMS-122 |
| SET-W10 | [DCMS-128](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-128) | Tư Nguyễn Văn | Nhóm quyền, gán nhân viên và kiểm quyền có hiệu lực ở backend | DCMS-119, DCMS-120 |
| SET-W11 | [DCMS-129](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-129) | trientvhe181820 | Tự đăng xuất staff web theo hoạt động thật và kiểm refresh | DCMS-119, DCMS-128 |
| SET-W12 | [DCMS-130](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-130) | trientvhe181820 | Tùy chọn in phiếu thu và cấp số liên tục theo chi nhánh | DCMS-119, DCMS-124 |
| SET-W13 | [DCMS-131](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-131) | Nguyễn Thành Vinh | Seed danh mục có phiên bản và migration bảo toàn dữ liệu hiện có | DCMS-121, DCMS-122, DCMS-123, DCMS-124, DCMS-125, DCMS-126, DCMS-127, DCMS-128 |
| SET-W14 | [DCMS-132](https://capstone-fpt-fa26.atlassian.net/browse/DCMS-132) | Ngo Minh Tri (K15 HL) | Excel theo quyền, lịch sử cấu hình, restore và kiểm thử tích hợp S58 | DCMS-120, DCMS-121, DCMS-122, DCMS-123, DCMS-124, DCMS-125, DCMS-126, DCMS-127, DCMS-128, DCMS-129, DCMS-130, DCMS-131 |

## Bắt đầu triển khai

- DCMS-119 triển khai schema/contract/permission hook trước, bàn giao DTO sớm để các nhánh tích hợp; không chờ toàn bộ Epic.
- DCMS-120 có thể dựng shell/component theo contract ngay. DCMS-122/123/124/126/128 chuẩn bị form/DTO/consumer cùng lúc; commit tích hợp theo phụ thuộc.
- Quyền backend cơ bản phải có từ đợt đầu; DCMS-128 mở rộng group editor/resolver. Không trì hoãn kiểm quyền đến cuối.
- DCMS-130 nối receipt/ledger của DCMS-102; DCMS-125 nối labo DCMS-100; DCMS-126 nối thuốc DCMS-117; DCMS-121 phối hợp Khám DCMS-112; DCMS-123 dùng service/price sources với DCMS-113/114/116.
- Từng ticket phải có test nghiệp vụ trong phạm vi. DCMS-132 kiểm luồng liên module và báo riêng mock/API/PostgreSQL/browser thật. Không tự chuyển In Progress/Done thay người thực hiện và không tự đưa vào sprint chưa được chọn.

## Đặc tả để code

- [Đặc tả Markdown đầy đủ](../specs/S58-SYSTEM-SETTINGS.vi.md).
- [Screen Specifications v2.5](../specs/DCMS_Screen_Specifications_v2.5.docx): phần S58 Thiết lập hệ thống chi tiết.
- [Use Cases Business Rules Messages v2.5](../specs/DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx): phần UC51 Quản lý thiết lập hệ thống chi tiết, API và 32 SET-AT.
- [Kế hoạch và 32 ca nghiệm thu](S58-SYSTEM-SETTINGS-PLAN.vi.md); [bằng chứng khảo sát](../research/ODENTAL-SYSTEM-SETTINGS-20261027.vi.md).

Tài liệu được đính kèm Epic để người nhận Jira không cần truy cập đường dẫn máy cá nhân. Không commit/push code hoặc triển khai phần mềm trong yêu cầu giao việc này.



Cập nhật local cho phần việc Tư ngày 27/10: [DCMS-119/121/128 — kết quả code, API/UI và điểm bàn giao](S58-119-121-128.vi.md). Giữ nguyên assignee/trạng thái Jira và phần việc của các thành viên khác.
