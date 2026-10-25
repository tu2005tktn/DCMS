# S62 — Điều chỉnh kế hoạch điều trị đã thỏa thuận

Nguồn: `DCMS_Screen_Specifications_v2.3.docx`, `DCMS_Use_Cases_Business_Rules_Messages_v2.3.docx` (24/10/2026), UC-13; GBR-141/144/145/151/158; AT33/40/49; MSG111/144/233/240/245. S62 là màn hình ngữ cảnh của Staff Web, không có mục sidebar riêng. S21/S43 mở S62 với Patient, dossier và item đã chọn; S22 dùng cùng deep link khi module đó có thật.

## Luồng và ranh giới

Wizard có ba bước: **So sánh phạm vi → Tư vấn và xác nhận → Xem ảnh hưởng và lưu**. Chỉ Dentist thuộc chi nhánh của dossier Open được đọc hoặc sửa, kể cả khi vào URL trực tiếp. Bản nháp trên server gắn với dossier, item, actor và plan version gốc. Back giữ nháp; Cancel xóa nháp sau khi người dùng xác nhận. Rời sang S12 để xử lý lịch hoặc S38 để xem tài chính rồi quay lại phải tải lại version, giữ nháp và kiểm tra ảnh hưởng lần nữa. Giao dịch ở S12/S38 có lịch sử và quyền riêng, không bị rollback theo S62.

So sánh current/proposed service, material, răng/vùng, quantity, giá thỏa thuận, thời gian dự kiến, quyết định cũ, actual đã ghi và việc còn lại. Lý do 1–1000 ký tự. Thay đổi phạm vi item Agreed yêu cầu ghi lại tư vấn phương án, lợi ích/rủi ro, thời gian/điều khoản, quyết định `Agreed/Refused/Deferred`, người quyết định và căn cứ đại diện khi áp dụng. Bác sĩ xác nhận có cấu trúc trên Web; API lưu actor/time và `decisionPlanVersion` của **plan version mới + item**. Đây không phải file/chữ ký S20: `consentDocumentId` và `consentPlanVersion` giữ rỗng nếu chưa có file được xác minh. Consent version cũ không mở việc mới; nếu quy trình của ca đòi file/chữ ký S20 mà nguồn chưa có, chặn lưu bằng MSG240.

Preview hiển thị step, hold, dependency, follow-up instruction, Appointment Confirmed và phiên bản tương ứng. Chỉ xử lý future work; không sửa/xóa actual, charge, receipt hoặc giá lịch sử. Booking không tương thích phải xử lý bằng API S12 rồi preview lại; không tự reschedule. Phần tài chính là read-only từ DCMS-101/108: posted charges, adjustment, allocation/credit/due, giá tham chiếu, giảm giá và chênh lệch **đề xuất**. Đổi dịch vụ, vật liệu, số lượng hoặc giá đều cần nguồn này, kể cả khi giá đề xuất bằng giá cũ. Không tự tạo receivable, chia phí theo buổi, áp giảm giá hai lần, chuyển credit hoặc hoàn tiền. Giảm giá phải được nguồn có quyền xác thực lý do, loại/mức giảm, qualifying/target item và line net không âm. Ca cần nguồn actual/tài chính mà adapter chưa xác thực được, hoặc không thể giữ token nguồn hợp lệ tới lúc commit, trả trạng thái `Unavailable` và không cho Save; nháp vẫn còn. Khi nguồn chưa tích hợp, chỉ revision chưa đụng tới actual/tài chính và chưa có lịch xung đột mới được lưu.

## API contract

Các route dùng chính sách `TreatmentDentist`; server xác minh scope cho mỗi request:

| Route | Kết quả |
|---|---|
| `GET /api/dossiers/{id}/plan/revisions/context?itemId={itemId}` | Current item, `planId`, `planVersion`, `dossierVersion`, step/instruction/booking IDs và versions, dịch vụ điều trị active đúng chi nhánh; không dùng danh sách dịch vụ khám ban đầu của Appointment |
| `GET /api/dossiers/{id}/plan/revision-drafts/{itemId}` | Nháp của actor hoặc 404 |
| `PUT /api/dossiers/{id}/plan/revision-drafts/{itemId}` | `{expectedDraftVersion,basePlanVersion,proposed,reason,counseling,decision,decisionMaker,representativeBasis?,futureDispositions[],requestKey}`; lưu draft, trả `draftVersion` |
| `DELETE /api/dossiers/{id}/plan/revision-drafts/{itemId}` | Xóa nháp actor; plan chính thức không đổi |
| `POST /api/dossiers/{id}/plan/revisions/preview` | `{itemId,draftVersion}`; trả chênh lệch lâm sàng, nguồn actual/finance và các impact với expected versions, `canSave`, blockers |
| `POST /api/dossiers/{id}/plan/revisions` | `{itemId,draftVersion,expectedDossierVersion,expectedPlanVersion,steps:[{id,version}],instructions:[{id,version}],bookings:[{id,version}],actualVersionToken,financeVersionToken,requestKey}`; trả dossier/plan version mới, selected item và notification; event đọc từ History |
| `GET /api/dossiers/{id}/plan/history` | Gồm snapshot `PlanCommitted` và `PlanRevised` theo version |

Ví dụ đề xuất tối thiểu trong draft: `{"serviceId":12,"material":"Sứ","region":"25","quantity":1,"unit":"răng","agreedUnitPriceVnd":1000000,"estimatedMinutes":60}`. `futureDispositions` chỉ định step chưa làm cần `Stopped/Cancelled/Skipped` và lý do; không nhận disposition của actual đã post. `requestKey` UUID ổn định cho một lệnh; cùng payload trả cùng kết quả, payload khác trả 409 MSG111, kể cả hai request gửi đồng thời. `steps`, `instructions`, `bookings` và token nguồn lấy từ preview. Preview không phải cam kết: Save kiểm lại trong transaction, trả MSG233/245 khi ảnh hưởng chưa xử lý, MSG240 khi thiếu đồng ý, MSG111 khi stale. Lỗi dịch vụ MSG144. Không tin Patient, branch, actor, trạng thái success hoặc tài chính do client tự gửi.

## Dữ liệu, giao dịch và kiểm thử

Tận dụng `TreatmentEvent` để lưu snapshot đầy đủ cũ/mới, provenance `(planId,planVersion,itemId)` và `TreatmentReceipt` để chống lặp. Thêm một bảng draft JSONB có unique `(dossierId,itemId,actorId)`; thêm trường material/version cần thiết vào item bằng migration không xóa dữ liệu cũ. Lệnh Save khóa dossier và các nguồn liên quan, kiểm version, chỉ cập nhật phạm vi/decision tương lai, tăng `PlanVersion`, ghi snapshot/event/receipt và xóa draft trong một transaction. Nếu nguồn bên ngoài không bảo đảm token còn hiệu lực khi chốt, chặn ca phụ thuộc nguồn đó. Đọc lại ID/version/trạng thái sau commit.

Integration test PostgreSQL cách ly: quyền URL trực tiếp; partial actual và giá cũ; Refused/Deferred; consent cũ; booking không tương thích; stale/concurrent/retry; giảm giá liên dịch vụ một lần; không tạo activity, charge, payment hoặc refund. Playwright mock kiểm wizard, draft sau reload/đi S12 rồi về, lỗi giữ dữ liệu và quay đúng selected item. Không tạo dữ liệu thử trong database dự án đang dùng; chưa nhận nghiệm thu adapter thật khi các module DCMS-99/101/108 chưa có.
