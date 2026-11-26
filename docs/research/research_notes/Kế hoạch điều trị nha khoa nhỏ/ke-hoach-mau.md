# Kế hoạch điều trị mẫu trong nha khoa cho phòng khám nhỏ/vừa tại Việt Nam (dữ liệu seed)

Lưu ý chung: đây là dữ liệu mẫu tham khảo, không phải khuyến cáo y khoa. Phiên nghiên cứu này chỉ dùng ~7 lần gọi công cụ; giá có nguồn chỉ phủ một phần dịch vụ. Mọi con số KHÔNG có link nguồn bên dưới là ước lượng của người nghiên cứu (thời lượng, số buổi) hoặc chưa có nguồn (xem Gaps) và phải được bác sĩ phòng khám xác nhận trước khi seed.

## Thứ tự ưu tiên và các giai đoạn của kế hoạch điều trị tổng quát

### Takeaway
Khung chuẩn gồm 5 giai đoạn: toàn thân → cấp cứu/giảm đau → kiểm soát bệnh (sâu răng, nha chu) → điều trị xác định (phục hồi, nội nha, phục hình, chỉnh nha, thẩm mỹ) → duy trì; có điểm tái đánh giá trước khi sang giai đoạn xác định.

### Cited Findings
- Một khóa học liệt kê 5 giai đoạn: systemic, acute, disease control, definitive, maintenance; giai đoạn toàn thân đánh giá sức khỏe chung và chuyển bác sĩ nếu nghi bệnh chưa chẩn đoán — [Wake Tech](https://www.waketech.edu/node/988351), tổng hợp ở [Brainscape (tài liệu sinh viên, độ tin cậy trung bình)](https://www.brainscape.com/flashcards/9-treatment-planning-phases-14262791/packs/21596689)
- Giai đoạn cấp tính xử trí đau, sưng, chấn thương trước; kiểm soát bệnh nhằm ngừng nhiễm trùng/sâu răng/bệnh nha chu và quản lý yếu tố nguy cơ — cùng các nguồn trên
- Sau kiểm soát bệnh có bước tái đánh giá; nếu bệnh chưa kiểm soát được hoặc bệnh nhân muốn hạn chế điều trị thì có thể tạm dừng, chưa sang điều trị xác định (tên/đánh số giai đoạn khác nhau tùy trường) — [Temple Univ. Dental (đề thi)](https://www.dental.temple.edu/Storage/DentalSyllabus/D2%20Exam%20Archive/D2%20Summer%20Exam%20Archive/Tx%20Planning/TxPlan-Exam-2010.doc)
- Giai đoạn xác định chia nhỏ: nha chu, phục hồi (operative), chỉnh nha, phục hình cố định và tháo lắp — cùng nguồn Brainscape ở trên
- Duy trì (recall) nhằm giữ sức khỏe răng miệng lâu dài — cùng nguồn trên

### Inferences
- Thứ tự gợi ý cho DCMS (field `phase`): 0 Khám/chụp phim/hỏi bệnh sử; 1 Cấp cứu-giảm đau; 2 Kiểm soát bệnh (cạo vôi, trám sâu, nhổ răng không giữ được, điều trị nha chu); 3 Tái đánh giá; 4 Phục hồi và nội nha; 5 Phục hình/chỉnh nha/thẩm mỹ; 6 Duy trì/tái khám.
- Quy tắc tiên quyết hay gặp (suy ra từ quy trình lâm sàng): cạo vôi/kiểm soát viêm lợi trước phục hình và niềng; điều trị tủy trước chốt/mão; nhổ răng và chờ lành thương trước implant/cầu/hàm giả; implant: đặt trụ → chờ tích hợp xương → abutment/mão; tẩy trắng sau khi cạo vôi, trám sâu, và trước khi làm mão/veneer (để chọn màu). Hạng mục có thể gán `prerequisiteItemId` hoặc `phase` + `sequence`.
- Cọc/giảm giá (không có nguồn, cần chủ phòng khám quyết định): đặt cọc thường áp cho gói dài ngày (niềng, implant, phục hình) theo % tổng; giảm giá theo hạng mục hoặc toàn kế hoạch, cần lưu người duyệt và lý do; thanh toán chia đợt theo buổi.

### Gaps
- Không tìm được hướng dẫn chính thức của ADA hay giáo trình RHM Việt Nam (chỉ có tài liệu học liệu/đề thi của các trường Mỹ). Cần đối chiếu giáo trình RHM nếu muốn trích nguồn chuẩn.

## Mẫu kế hoạch phổ biến ở phòng khám nhỏ/vừa Việt Nam (đề xuất 16 kế hoạch)

### Takeaway
Dưới đây là 16 kế hoạch đề xuất. Thời lượng/số buổi là ước lượng của người nghiên cứu (không có nguồn), giá chỉ ghi khi có nguồn; mục không có nguồn ghi "chưa có nguồn".

### Cited Findings (bảng giá tham chiếu, VND)
Nguồn giá A = [Nha khoa Shark, bảng giá trang web, hiển thị năm 2026; không ghi đơn vị tính, nên hỏi lại phòng khám](https://nhakhoashark.vn/en/?p=73417). Nguồn B = [My Auris, bảng giá tháng 10/2025](https://en.myauris.vn/?p=87362). Nguồn C = [My Auris, bài đăng 11/2025](https://en.myauris.vn/?p=88057). Nguồn D = [Medpro, niềng mắc cài kim loại](https://medpro.vn/tin-tuc/gia-nieng-rang-mac-cai-kim-loai) và [Long Châu](https://nhathuoclongchau.com.vn/bai-viet/tim-hieu-gia-nieng-rang-mac-cai-kim-loai-va-cac-yeu-to-anh-huong-den-chi-phi.html) (bài tổng hợp/quảng cáo, không phải bảng giá chính thức).
- Trám composite: Hàn Quốc 250.000; Đức 350.000; Mỹ 450.000; thẩm mỹ 700.000 — A
- Điều trị tủy: 1 chân 1.200.000; 2 chân 3.500.000; nhiều chân 4.000.000 — A. Nguồn khác: 500.000–1.500.000/răng (chưa phân chân) — C. Hai nguồn chênh lệch lớn.
- Mão sứ: từ 2.800.000 (Venus) đến 15.000.000; Zirconia 3.500.000 — A. Sứ kim loại 2.000.000–2.500.000; toàn sứ 3.000.000–4.000.000 — C. Zirconia (Đức) 1.500.000–3.000.000; Vita 2.500.000–4.500.000; Emax 6.000.000–12.000.000 — B.
- Implant đơn lẻ: trụ Biotech (Hàn) 12.250.000; Dentium Hàn 19.600.000; Hiossen 28.500.000; Straumann 45.000.000 (giá/bộ, 10/2025) — B. Nguồn A: Dentium Hàn 11.000.000; Straumann SLA 32.000.000. Abutment 3.000.000–10.000.000 (không ghi năm) — B. Ghép xương 8.000.000–15.000.000 (2025) — B. Nguồn B tự mâu thuẫn về khoảng giá implant đơn (12–28,5 triệu và 13–35 triệu).
- Thời gian lành thương/tích hợp implant theo hãng: 3 tuần (Zimmer) đến 2–6 tháng — B
- Cạo vôi: đánh bóng 50.000; độ 1 100.000; độ 2 350.000; độ 3 450.000 — A
- Tẩy trắng: tái khoáng men 350.000; khay 750.000; laser 1.500.000 — A
- Hàm tháo lắp: răng nhựa Nhật 250.000/răng; khung titan 2.200.000; đệm hàm mềm bán hàm 2.500.000 — A
- Nhổ răng khôn: cắt chỉ 100.000; hàm trên độ 1 900.000; hàm dưới độ 1 1.350.000; trên độ 2 1.500.000; dưới độ 2 2.000.000; độ 3 3.000.000; răng ngầm 4.500.000; trên độ 4 5.000.000 — A. Nguồn khác: 2.000.000–5.000.000 — [Nha khoa Việt Mỹ](https://hackmd.io/@nhakhoavietmy/B1g4R26nA)
- Niềng mắc cài kim loại 2 hàm: 25–60 triệu (Medpro), 27–35 triệu thông thường, từ 18 triệu ca đơn giản (Long Châu) — D

### Inferences (16 kế hoạch đề xuất; thời lượng là ước lượng)
Quy ước: bước = dịch vụ | vùng | phút | buổi. Mọi kế hoạch bắt đầu bằng bước khám + X-quang (khám 20–30 phút, phim quanh chóp/panorama theo chỉ định; giá chưa có nguồn).

1. **Sâu răng đơn giản (1–2 mặt)**: trám composite | 1 răng | 30–40 | 1 buổi. Giá 250.000–450.000 (A). Tổng ~1 buổi. Loại trừ/chuyển: đau tự phát, vào tủy -> kế hoạch 2.
2. **Sâu răng vào tủy 1 chân (răng cửa/tiền hàm) + mão sứ**: giảm đau/mở tủy | 1 răng | 30–45 | 1; sửa soạn và trám bít ống tủy | 45–60 | 1–2; chốt/cùi giả (chưa có nguồn giá) | 30 | 1; lấy dấu/sửa soạn mão | 45–60 | 1; gắn mão | 30 | 1 (+ tái khám sau 1–2 tuần). Giá tủy 1.200.000 (A) hoặc 500.000–1.500.000 (C); mão 2.000.000–4.000.000 (C) hoặc 3.500.000 zirconia (A). Tổng 4–6 buổi, ~2–3 tuần.
3. **Điều trị tủy răng hàm (nhiều chân) + mão**: như kế hoạch 2 nhưng tủy 3.500.000–4.000.000 (A), thời lượng tủy 60–90 phút/buổi, 2–3 buổi; thêm trám phục hồi tạm giữa các buổi. Loại trừ: ống tủy canxi hóa/cong nặng, tiêu chân, gãy dọc chân, tiêu xương quanh chóp lớn -> chuyển chuyên khoa nội nha.
4. **Cạo vôi, viêm lợi mức nhẹ–vừa**: cạo vôi + đánh bóng | toàn hàm | 30–45 | 1; hướng dẫn vệ sinh; tái khám 2–4 tuần, tái khám định kỳ 6 tháng. Giá 100.000–450.000 theo độ (A). Loại trừ: túi nha chu sâu, lung lay độ 2–3, bệnh toàn thân chưa kiểm soát (tiểu đường, đang dùng thuốc chống đông) -> chuyển nha chu/bác sĩ nội khoa.
5. **Nhổ răng khôn mọc lệch nhẹ (độ 1–2)**: khám + phim panorama; nhổ | 1–2 răng | 30–45 | 1; cắt chỉ | 15 | 1 (sau 5–7 ngày). Giá 900.000–2.000.000 (A) hoặc 2–5 triệu (Việt Mỹ). Loại trừ/chuyển: ngầm sát ống thần kinh hàm dưới, ngầm sâu/độ 4, nang, bệnh toàn thân nặng -> chuyển phẫu thuật hàm mặt. Nên đặt kế hoạch chỉ với độ 1–2.
6. **Mất 1 răng: implant đơn lẻ**: kiểm soát vệ sinh/cạo vôi; CT/CBCT (chưa có nguồn giá); đặt trụ | 1 răng | 45–60 | 1; tái khám cắt chỉ | 15 | 1; gắn abutment sau 2–6 tháng | 30 | 1; lấy dấu và gắn mão | 45 | 2. Giá trụ 11–45 triệu theo hãng (A, B), abutment 3–10 triệu (B), mão 1,5–12 triệu (B), ghép xương nếu cần 8–15 triệu (B). Tổng 5–7 buổi, 3–7 tháng. Loại trừ/chuyển: thiếu xương nhiều cần ghép lớn/nâng xoang, hút thuốc nặng, tiểu đường chưa kiểm soát, xạ trị vùng đầu cổ, loãng xương dùng bisphosphonate -> chuyển cơ sở phẫu thuật/implant chuyên sâu.
7. **Mất 1 răng: cầu răng sứ**: mài 2 răng trụ | 3 răng | 60–90 | 1; lấy dấu/cầu tạm | 45 | 1; thử khung | 30 | 1; gắn cầu | 30–45 | 1. Giá theo giá mão x số đơn vị (C, A). Tổng 3–4 buổi, ~2 tuần. Loại trừ: răng trụ yếu/nha chu chưa ổn, nhịp cầu dài.
8. **Mất nhiều răng: hàm tháo lắp bán phần/toàn phần**: lấy dấu sơ khởi | 30 | 1; dấu cá nhân/khớp cắn | 45 | 1; thử răng sáp | 30 | 1; giao hàm | 30 | 1; chỉnh đau 1–2 lần | 15–20 | 2. Giá: răng nhựa 250.000/răng, khung titan 2,2 triệu (A). Tổng 5–7 buổi, 2–4 tuần. Loại trừ: sống hàm tiêu quá mức hoặc cần cấy ghép, tổn thương niêm mạc nghi ác tính -> chuyển.
9. **Tẩy trắng răng**: cạo vôi + trám sâu trước | 30 | 1; tẩy trắng (laser hoặc khay) | toàn hàm | 60 | 1; tái khám hoặc giao khay | 20 | 1. Giá 350.000–1.500.000 (A). Loại trừ: răng nhiễm tetracycline nặng, thai kỳ, nhạy cảm men nặng, miếng trám/mão ở răng trước (không đổi màu).
10. **Trẻ em: sâu răng sữa**: trám GIC/composite | 1–2 răng | 20–30 | 1–2; (nếu viêm tủy răng sữa: lấy tủy răng sữa + trám/mão thép, giá chưa có nguồn); tái khám 3–6 tháng; bôi fluor. Giá trẻ em chưa có nguồn. Loại trừ/chuyển: trẻ không hợp tác nhiều, cần gây mê -> chuyển nha nhi.
11. **Niềng răng ca đơn giản (mắc cài kim loại, lệch nhẹ–vừa, đủ chỗ)**: khám, phim sọ/panorama, lấy dấu/scan | 60 | 1–2; cạo vôi và trám sâu trước | 1; gắn mắc cài | 90 | 1; tái khám siết 4–6 tuần/lần, 18–24 tháng (~18–24 buổi, 20–30 phút); tháo mắc cài + hàm duy trì | 60 | 1–2; tái khám duy trì. Giá 18–60 triệu 2 hàm (D, độ tin cậy trung bình). Loại trừ/chuyển: lệch hàm xương nặng, cần phẫu thuật chỉnh hàm, nhổ nhiều răng phức tạp, bệnh nha chu -> chuyển chỉnh nha.
12. **Răng vỡ lớn/nứt, không đủ cùi: phục hồi bằng mão sứ (răng đã chữa tủy)**: tương tự kế hoạch 2 phần sau tủy; giá mão 2–4 triệu (C).
13. **Sâu răng nhiều răng (miệng nhiều sâu)**: ưu tiên răng đau/nguy cơ vào tủy; trám lần lượt 2–4 răng/buổi | 30–40/răng | 3–5 buổi; cạo vôi và bôi fluor; hướng dẫn chế độ ăn/vệ sinh; tái khám 3 tháng. Giá 250.000–700.000/răng (A).
14. **Đau cấp: viêm tủy/áp xe cấp**: khám, phim; mở tủy giảm đau/rạch dẫn lưu, kê thuốc (chưa có nguồn giá) | 30 | 1; tái khám 24–48 giờ; sau đó vào kế hoạch 2/3 hoặc nhổ nếu không giữ được. Chuyển viện nếu sưng lan sàn miệng/cổ, sốt cao, khó nuốt/thở.
15. **Nhổ răng không giữ được (răng sâu vỡ chân, lung lay) + phục hình sau**: nhổ | 20–30 | 1; tái khám 7 ngày; chờ lành 4–8 tuần (khoảng này ước lượng); chuyển sang kế hoạch 6/7/8. Giá nhổ răng thường chưa có nguồn riêng (nguồn A chỉ có răng khôn).
16. **Duy trì định kỳ (recall) sau điều trị**: khám + cạo vôi + đánh bóng | 30–45 | mỗi 6 tháng (3–4 tháng nếu nha chu). Giá 100.000–450.000 (A).

Trường dữ liệu tối thiểu cho dòng hạng mục (đề xuất): `planItemId`, `planId`, `phase`, `sequence`, `visitNo` (buổi), `plannedDate`, `toothCodes` (FDI, vùng/hàm/toàn hàm), `serviceId/serviceName`, `plannedMinutes`, `status` (dự kiến/đang/hoàn tất/hủy), `prerequisiteItemId`, `dentistId`, `assistantId`, `unitPrice`, `quantity`, `discountAmount/discountReason`, `totalPrice`, `notes`, `warrantyMonths`, `followUpDate`. Cấp kế hoạch: `deposit`, tổng giá, tổng giảm, trạng thái duyệt, người tạo.

### Gaps
- Không tìm thấy nguồn giá cho: chốt/cùi giả sợi thủy tinh, trám/lấy tủy răng sữa, nhổ răng thường, gói điều trị nha chu, hàm tháo lắp toàn phần, X-quang/CBCT, thuốc. Những mục này phải do phòng khám nhập hoặc tra bổ sung.
- Giá niềng chỉ có từ bài tổng hợp/quảng cáo; không có năm rõ ràng ở một số nguồn.
- Thời lượng/số buổi/thời gian lành thương và chính sách bảo hành/cọc/giảm giá không có nguồn trích dẫn (ước lượng thực hành, cần bác sĩ xác nhận). Chưa tìm được tài liệu phần mềm quản lý phòng khám để đối chiếu cấu trúc dữ liệu.
- Giá nguồn A ghi năm 2026 theo trang, không ghi đơn vị tính; hai nguồn nhổ răng khôn và điều trị tủy chênh lệch lớn.

## Ca cần chuyển tuyến nên không đưa vào dữ liệu mẫu

### Takeaway
Nên loại khỏi seed các ca vượt năng lực phòng khám nhỏ/vừa: phẫu thuật phức tạp, nội nha khó, bệnh toàn thân chưa kiểm soát, nhi khó hợp tác và chỉnh nha phức tạp. Chỉ đưa ca đơn giản kèm điều kiện loại trừ.

### Cited Findings
- Giai đoạn toàn thân của khung chuẩn yêu cầu đánh giá sức khỏe chung và chuyển bác sĩ khi nghi bệnh chưa chẩn đoán — [Wake Tech](https://www.waketech.edu/node/988351)
- Nguồn B nêu thiếu xương cần ghép xương/nâng xoang làm tăng bước và chi phí implant — [My Auris](https://en.myauris.vn/?p=87362)
- Bảng giá nhổ răng khôn phân độ 1–4 và răng ngầm, cho thấy độ khó tăng dần — [Nha khoa Shark](https://nhakhoashark.vn/en/?p=73417)

### Inferences
- Không đưa vào seed: răng khôn ngầm sát thần kinh/độ 4; implant có ghép xương lớn, nâng xoang, toàn hàm (All-on-4/6); nội nha chữa lại/ống tủy khó; nha chu nặng; chỉnh nha lệch hàm xương, chỉnh nha trong suốt phức tạp; nha nhi cần gây mê; nhiễm trùng lan rộng; nghi tổn thương ác tính; bệnh nhân bệnh toàn thân nặng.
- Mỗi kế hoạch seed nên có trường `exclusionNotes` ghi các điều kiện trên.

### Gaps
- Danh sách chuyển tuyến là suy luận theo thực hành, không có nguồn quy chuẩn Việt Nam (ví dụ quy định phân tuyến của Bộ Y tế); cần hội đồng chuyên môn của phòng khám xác nhận.
