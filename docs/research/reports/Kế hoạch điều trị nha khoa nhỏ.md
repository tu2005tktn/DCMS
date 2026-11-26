# Xây dữ liệu mẫu kế hoạch điều trị cho phòng khám nhỏ

**Kết luận chính:** DCMS có thể seed ngay một danh mục khoảng 45 dịch vụ chia thành 8 nhóm cùng 16 kế hoạch điều trị mẫu, nhưng chỉ phần giá và phần ranh giới chuyên môn là có nguồn. Toàn bộ thời lượng phút, số buổi, thời gian chờ labo, thời gian lành thương và tiêu chí chuyển tuyến đều là suy luận chuyên môn chung, chưa có nguồn trích dẫn, nên phải được bác sĩ phòng khám xác nhận trước khi nạp vào hệ thống. Về giá, các nguồn thu được gần như đều là bảng giá niêm yết của chuỗi, bệnh viện hoặc trang quảng bá, không có bảng giá gốc của phòng khám nhỏ/vừa. Chúng lệch nhau lớn: điều trị tủy hàm lớn từ 1,8 đến 4 triệu đồng, nhổ răng khôn hàm dưới độ 1 từ 1,35 đến 3 triệu đồng, trụ implant từ 8,4 đến 45 triệu đồng. Vì vậy mọi giá trong báo cáo được đặt dưới dạng khoảng tham khảo kèm mức tin cậy, và DCMS nên cho phòng khám tự chốt giá. Khung 5 giai đoạn (toàn thân, cấp cứu, kiểm soát bệnh, điều trị xác định, duy trì) là cấu trúc hợp lý để sắp xếp bước trong kế hoạch ([Wake Tech](https://www.waketech.edu/node/988351)). Phạm vi seed nên dừng ở ca đơn giản; các ca phức tạp được đánh dấu chuyển tuyến chứ không tạo kế hoạch mẫu.

Quy ước mức tin cậy giá dùng xuyên suốt: **Cao** là có từ 3 nguồn trở lên cùng khoảng giá, có phần từ phòng khám tư; **Trung bình** là 2 nguồn, hoặc nguồn là bài tổng hợp nhưng khá nhất quán; **Thấp** là một nguồn, nguồn quảng bá hoặc các nguồn mâu thuẫn; **Không có** là chưa tìm được giá. Mọi cột "phút" và "buổi" là ước lượng cần bác sĩ duyệt, trừ khi có ghi chú khác.

## Danh mục dịch vụ: giá có nguồn, thời lượng còn là ước lượng

Phần lớn giá niêm yết đến từ trang quảng bá, nên các khoảng dưới đây là khoảng tham khảo chứ không phải mức chuẩn. Giá cạo vôi ổn định nhất: 200.000-500.000 đồng/lần ([Long Châu](https://nhathuoclongchau.com.vn/bai-viet/cao-voi-rang-la-gi-gia-cao-voi-rang-hien-nay-la-bao-nhieu-49145.html)), 200.000-300.000 đồng tại một nha khoa Đà Nẵng ([Coda](https://coda.io/@richdental/lay-cao-rang-tai-da-nang-gia-bao-nhieu)), và 300.000-900.000 đồng/2 hàm trong ba bảng giá phòng khám và bệnh viện ([Cường Nhân](https://nhakhoacuongnhan.com/bang-gia/), [I-Dent](https://nhakhoaident.com/bang-gia/dich-vu-khac/), [BV RHM SG](https://benhvienranghammatsg.vn/bang-gia-nha-khoa-tong-quat/)). Trám răng có hai nguồn khớp nhau: GIC 150.000-300.000 và composite 300.000-500.000 đồng/răng ([Medpro, 06/07/2026](https://medpro.vn/tin-tuc/gia-tram-rang-bao-nhieu-tien)), còn nguồn Shark niêm yết composite 250.000-450.000 theo xuất xứ vật liệu và 700.000 cho thẩm mỹ ([Nha khoa Shark](https://nhakhoashark.vn/en/?p=73417)).

Các bảng dưới đây gộp ba mức: giá tham khảo, đơn vị tính, và thời lượng ước lượng. Cột "Tin cậy" chỉ đánh giá phần giá.

### Nhóm 1 đến 4: khám, phòng ngừa, phục hồi, nội nha

| Mã gợi ý | Dịch vụ | Đơn vị | Phút (ước lượng) | Giá tham khảo (VND) | Tin cậy |
|---|---|---|---|---|---|
| KHA-01 | Khám và tư vấn | lần | 15-30 | chưa có nguồn (nhiều nơi miễn phí) | Không có |
| KHA-02 | Phim quanh chóp | phim | 5 | chưa có nguồn (rẻ hơn panorex) | Không có |
| KHA-03 | Phim panorama | phim | 10 | 150.000-350.000 ([Long Châu](https://nhathuoclongchau.com.vn/bai-viet/chup-panorama-rang-la-gi-vai-tro-cua-chup-x-quang-panorama-rang.html)) | Thấp |
| KHA-04 | Phim sọ nghiêng, CT Cone Beam | phim | 10-15 | chưa có nguồn | Không có |
| PHN-01 | Cạo vôi, đánh bóng (nhẹ-vừa) | 2 hàm/lần | 30-45 | 150.000-500.000 | Cao |
| PHN-02 | Cạo vôi nhiều (có thể 2 buổi) | 2 hàm/lần | 45-60 | 500.000-900.000 | Trung bình |
| PHN-03 | Bôi fluor | lần | 15 | 200.000-400.000 | Trung bình |
| PHN-04 | Trám bít hố rãnh | răng | 15-20 | 150.000-200.000 (BV RHM SG ghi 2.000.000, bất thường) | Thấp |
| PHN-05 | Điều trị ê buốt (vecni, chất khử nhạy cảm) | lần | 15-30 | chưa có nguồn | Không có |
| PHC-01 | Trám GIC | răng | 20-30 | 150.000-300.000 | Trung bình |
| PHC-02 | Trám composite | răng | 30-40 | 250.000-500.000 | Trung bình |
| PHC-03 | Trám composite thẩm mỹ răng trước | răng | 40-60 | 600.000-800.000 | Trung bình |
| PHC-04 | Hàn cổ răng | răng | 20-30 | chưa có giá riêng, dùng khung trám 150.000-500.000 | Thấp |
| PHC-05 | Đóng khe thưa bằng composite | 2 răng | 60 | 1.500.000-2.000.000 | Thấp |
| NOI-01 | Mở tủy giảm đau (cấp cứu) | răng | 30 | chưa có nguồn | Không có |
| NOI-02 | Điều trị tủy răng 1 chân | răng | 45-60/buổi, 1-2 buổi | 500.000-1.500.000 | Trung bình |
| NOI-03 | Điều trị tủy răng hàm nhỏ (2 ống) | răng | 60-75/buổi, 2 buổi | 1.100.000-2.000.000 | Trung bình |
| NOI-04 | Điều trị tủy răng hàm lớn (3-4 ống) | răng | 60-90/buổi, 2-3 buổi | 1.800.000-4.000.000 | Thấp (chênh lớn) |
| NOI-05 | Chốt sợi, cùi giả | răng | 30 | 1.500.000 chốt sợi tại một phòng khám phân khúc cao | Thấp |

Giá điều trị tủy minh họa rõ mức lệch giữa các nguồn. Nha khoa Kim niêm yết 890.000 (1 chân), 1.100.000 (2 chân), 1.800.000 (3 chân), Việt Pháp niêm yết 1,5/2/3 triệu theo nhóm răng ([Toplist.vn](https://toplist.vn/top-list/nha-khoa-dieu-tri-tuy-rang-uy-tin-nhat-ha-noi-61518.htm)), trong khi Shark niêm yết 1,2 triệu cho 1 chân, 3,5 triệu cho 2 chân và 4 triệu cho nhiều chân ([Nha khoa Shark](https://nhakhoashark.vn/en/?p=73417)). Hạng mục nội nha còn thiếu một mảng quan trọng: không có giá riêng cho mở tủy giảm đau, chốt sợi tại phòng khám nhỏ, hay thuốc đặt giữa các buổi, nên các dòng đó phải để phòng khám tự nhập. Độ khó nội nha cũng không nên nằm trong seed mặc định; tủy lại được niêm yết 2,5-5 triệu đồng ([Toplist.vn](https://toplist.vn/top-list/nha-khoa-dieu-tri-tuy-rang-uy-tin-nhat-ha-noi-61518.htm)) nhưng thuộc nhóm ca khó, xếp vào danh sách chuyển tuyến ở mục sau.

### Nhóm 5 và 6: tiểu phẫu, nha chu, nha nhi

| Mã gợi ý | Dịch vụ | Đơn vị | Phút (ước lượng) | Giá tham khảo (VND) | Tin cậy |
|---|---|---|---|---|---|
| NHO-01 | Nhổ răng đơn giản | răng | 15-30 | 100.000-600.000 ([Hello Bacsi](https://hellobacsi.com/care/benh-vien/nha-khoa-dong-kinh-3131/)) | Trung bình |
| NHO-02 | Nhổ răng khôn hàm trên độ 1-2 | răng | 30-45 | 900.000-2.000.000 | Trung bình |
| NHO-03 | Nhổ răng khôn hàm dưới độ 1-2 | răng | 40-60 | 1.350.000-3.000.000 | Thấp-Trung bình (chênh lớn) |
| NHO-04 | Cắt chỉ, tái khám sau nhổ | lần | 15 | 100.000 | Thấp |
| NHO-05 | Rạch dẫn lưu áp xe lợi | lần | 30 | 800.000-1.000.000 (giá bệnh viện đa khoa) | Thấp |
| NHA-01 | Viêm lợi: cạo vôi và hướng dẫn vệ sinh | lần | 30-45 | dùng giá PHN-01 | Trung bình |
| NHA-02 | Nạo túi nha chu (túi ≤ 5 mm) | túi hoặc răng | 30-60/buổi | 300.000-500.000/túi; 550.000/răng ([Hello Bacsi](https://hellobacsi.com/suc-khoe-rang-mieng/viem-nuou-nha-chu/nao-tui-nha-chu/)) | Trung bình |
| NHA-03 | Cắt lợi trùm, tạo hình nhỏ | răng | 30-45 | 1.000.000-1.500.000 | Thấp |
| NHI-01 | Trám răng sữa | răng | 20-30 | 200.000-350.000 (BV RHM SG: 600.000) | Trung bình |
| NHI-02 | Nhổ răng sữa | răng | 10-15 | 50.000-100.000 | Trung bình |
| NHI-03 | Lấy tủy răng sữa và trám | răng | 30-45 | 500.000-1.000.000 | Trung bình |

Bảng giá nhổ răng khôn phân độ 1-4 của Shark (hàm trên độ 1 là 900.000, hàm dưới độ 1 là 1.350.000, độ 3 là 3.000.000, ngầm 4.500.000) thấp hơn BV RHM SG (hàm dưới cấp 1-3 là 2-3 triệu) và I-Dent (hàm dưới 3-4 triệu), nên khoảng đã gộp rộng. Hạng mục viêm lợi không có dòng giá riêng trong các bảng giá đã đọc, thường gộp cạo vôi và thuốc. Với nha nhi, AAPD khuyến cáo cắt tủy buồng cho răng sữa còn sống sâu sâu thay vì lấy tủy toàn phần, và dùng MTA hoặc Biodentine cho răng cối sữa ([AAPD](https://www.aapd.org/research/oral-health-policies--recommendations/vital_pulp_therapies_in_primary_teeth_with_deep_caries_lesions/)); dòng NHI-03 vì thế nên cho phép chọn "cắt tủy buồng" hoặc "lấy tủy toàn phần".

### Nhóm 7 và 8: phục hình, thẩm mỹ, chỉnh nha, implant

| Mã gợi ý | Dịch vụ | Đơn vị | Phút (ước lượng) | Giá tham khảo (VND) | Tin cậy |
|---|---|---|---|---|---|
| PHH-01 | Mão sứ kim loại | răng | mài 45-90; gắn 20-30 | 1.500.000-4.000.000 | Trung bình |
| PHH-02 | Mão sứ Titanium | răng | như trên | khoảng 2.500.000 | Thấp |
| PHH-03 | Mão toàn sứ Zirconia | răng | như trên | 3.500.000-9.000.000 | Thấp (chênh lớn) |
| PHH-04 | Mão Emax, Lisi (răng trước) | răng | như trên | 6.000.000-12.000.000 | Thấp |
| PHH-05 | Cầu răng | đơn vị răng (trụ và răng thay đều tính) | mài 60-90; thử khung 30; gắn 30-45 | = giá mão x số đơn vị | Thấp |
| PHH-06 | Inlay/onlay | răng | 45-60 | 1.500.000-5.000.000 ([Medpro](https://medpro.vn/tin-tuc/gia-tram-rang-bao-nhieu-tien)) | Thấp |
| PHH-07 | Veneer sứ (răng trước) | răng | 60-90 | 6.000.000-12.000.000 ([My Auris](https://en.myauris.vn/?p=87077)) | Thấp |
| PHH-08 | Hàm tháo lắp nhựa | hàm | 5-6 buổi, 20-45 | 2.000.000-4.000.000 ([Long Châu](https://nhathuoclongchau.com.vn/bai-viet/gia-rang-gia-thao-lap-la-bao-nhieu-thong-tin-can-biet-ve-rang-gia-thao-lap.html)) | Thấp |
| PHH-09 | Hàm khung kim loại/Titan | hàm | như trên | 5.000.000-10.000.000; riêng khung titan bán hàm 2.200.000 theo Shark | Thấp |
| PHH-10 | Phục hình tạm, tháo mão/cầu | răng | 20-30 | tháo mão 300.000 tại một phòng khám | Thấp |
| THM-01 | Tẩy trắng tại nhà (khay) | 2 hàm | 2 buổi, 20-30 | 750.000-2.000.000 | Trung bình |
| THM-02 | Tẩy trắng tại ghế | 2 hàm | 60-90 | 1.500.000-3.000.000 (BV RHM SG: 3,5-5 triệu) | Trung bình |
| CHN-01 | Chỉnh nha mắc cài kim loại (ca đơn giản) | ca 2 hàm | gắn 90; tái khám 20-30 | 20.000.000-45.000.000 ([Medpro](https://medpro.vn/tin-tuc/gia-nieng-rang-mac-cai-kim-loai)) | Trung bình |
| CHN-02 | Mắc cài tự buộc | ca 2 hàm | như trên | 30.000.000-55.000.000 | Trung bình |
| CHN-03 | Hàm duy trì | hàm | 20 | 1.000.000-2.000.000 ([Long Châu](https://nhathuoclongchau.com.vn/bai-viet/ham-duy-tri-co-may-loai-ham-duy-tri-sau-nieng-rang-gia-bao-nhieu-60815.html)) | Thấp |
| IMP-01 | Trụ implant (hãng Hàn) | trụ | 45-60 | 8.400.000-19.600.000 | Trung bình |
| IMP-02 | Abutment | cái | 30 | 3.000.000-10.000.000 | Thấp |
| IMP-03 | Mão trên implant | răng | 45 | 1.500.000-12.000.000 | Thấp |
| IMP-04 | Ghép xương nhỏ | vị trí | 30-45 | 8.000.000-15.000.000 | Thấp |

Giá mão cho thấy rõ vì sao phải cẩn trọng. Một phòng khám niêm yết sứ kim loại 1,5 triệu, Titanium 2,5 triệu, Zirconia 4,5 triệu và bảo hành 7-15 năm ([Peace Dentistry](https://sites.google.com/view/peacedentistry/trong-rang-su/chi-phi-trong-rang-su-gia-bao-nhieu)), trong khi Platinum Dental (phân khúc cao) niêm yết Cercon HT 8 triệu, Emax 9 triệu, Lava 12 triệu ([Platinum Dental](https://platinumdentalvietnam.com/?p=53288)). Hai nguồn này không cùng phân khúc, nên khoảng "Zirconia 3,5-9 triệu" ở trên là phần gộp, không phải mức giá của một phòng khám duy nhất. Tương tự, chỉnh nha mắc cài kim loại dao động từ 20 triệu (Aura Dental, ca nhẹ) đến 45 triệu (ca nặng) và 25-45 triệu theo tổng hợp Medpro ([Medpro, 26/09/2026](https://medpro.vn/tin-tuc/gia-nieng-rang-mac-cai-kim-loai)), còn khay trong suốt 40-130 triệu thuộc nhóm ca phức tạp hơn nên không đưa vào seed mặc định. Trụ implant hãng Hàn khoảng 8,4-14,9 triệu tại Lạc Việt Intech, 12 triệu tại Sante, và 15-20 triệu tại BV RHM TPHCM ([Medpro implant](https://medpro.vn/tin-tuc/gia-trong-rang-implant)); các hãng Mỹ-Đức-Thụy Sĩ lên đến 45 triệu nên phòng khám nhỏ nên chọn một hoặc hai hãng cho seed. Dữ liệu bảo hành đáng lưu thành trường riêng: Peace nêu 7 năm cho kim loại/Titanium, 10 năm cho Zirconia, 15 năm cho Lava Plus, còn chốt, cùi và tháo mão không bảo hành tại Platinum ([Platinum Dental](https://platinumdentalvietnam.com/?p=53288)).

## 16 kế hoạch mẫu: thứ tự bước dựa trên khung lâm sàng, thời lượng là suy luận

Thứ tự bước trong các kế hoạch dưới đây dựa trên khung 5 giai đoạn đã nêu: toàn thân, cấp cứu, kiểm soát bệnh, điều trị xác định, duy trì, với điểm tái đánh giá giữa kiểm soát bệnh và điều trị xác định ([Temple Univ. Dental](https://www.dental.temple.edu/Storage/DentalSyllabus/D2%20Exam%20Archive/D2%20Summer%20Exam%20Archive/Tx%20Planning/TxPlan-Exam-2010.doc)). Số buổi, phút, thời gian chờ và điều kiện loại trừ là suy luận của nhóm nghiên cứu, chưa có nguồn trích dẫn. Mọi kế hoạch bắt đầu bằng bước 0 là "khám + X-quang" (KHA-01 và KHA-02 hoặc KHA-03). Các mã dịch vụ tham chiếu bảng ở mục trước. Tổng giá chỉ cộng các dòng đã có khoảng giá; mục "chưa có nguồn" cần phòng khám nhập.

| # | Kế hoạch | Các bước theo thứ tự (dịch vụ, phút, buổi) | Giá tham khảo tổng (VND) | Điều kiện loại trừ/chuyển |
|---|---|---|---|---|
| 1 | Sâu răng đơn giản 1-2 mặt | PHC-02 trám composite (30-40 phút, 1 buổi) | 250.000-500.000 | Đau tự phát, vào tủy: chuyển kế hoạch 2 |
| 2 | Sâu vào tủy răng 1 chân kèm mão | Khám/phim; NOI-01 mở tủy (30, buổi 1); NOI-02 sửa soạn, trám bít ống tủy (45-60, buổi 2-3); NOI-05 chốt/cùi (30); lấy dấu mão (45-60); gắn mão (30); tái khám 1-2 tuần | tủy 500.000-1.500.000 + mão 1.500.000-4.000.000 + chốt chưa rõ | Ống tủy canxi hóa, nứt dọc chân |
| 3 | Điều trị tủy răng hàm nhiều chân kèm mão | Như kế hoạch 2; NOI-04 (60-90 phút, 2-3 buổi) có trám tạm giữa các buổi | tủy 1.800.000-4.000.000 + mão 1.500.000-9.000.000 | Ống tủy cong nặng, tiêu chân, dụng cụ gãy, tiêu xương quanh chóp lớn |
| 4 | Cạo vôi, viêm lợi nhẹ-vừa | PHN-01 hoặc NHA-01 (30-45, 1 buổi); hướng dẫn vệ sinh; tái khám 2-4 tuần; recall 6 tháng | 150.000-500.000 | Túi sâu >5 mm, răng lung lay độ 2-3, bệnh toàn thân chưa kiểm soát |
| 5 | Nhổ răng khôn lệch nhẹ (độ 1-2) | Khám + panorama; NHO-02 hoặc NHO-03 (30-45, 1 buổi); NHO-04 cắt chỉ sau 5-7 ngày (15) | 900.000-3.000.000/răng | Ngầm, sát ống thần kinh hoặc xoang, độ 4, nang |
| 6 | Mất 1 răng: implant đơn lẻ | Cạo vôi; CT (KHA-04); IMP-01 đặt trụ (45-60, buổi 1); cắt chỉ (15); chờ tích hợp 3-6 tháng; IMP-02 abutment (30); lấy dấu (45); IMP-03 gắn mão (30-45); tái khám | trụ 8.400.000-19.600.000 + abutment 3.000.000-10.000.000 + mão 1.500.000-12.000.000 | Cần ghép xương lớn/nâng xoang, hút thuốc nặng, đái tháo đường chưa kiểm soát, xạ trị đầu cổ |
| 7 | Mất 1 răng: cầu răng sứ | PHH-05: mài 2 trụ (60-90, buổi 1); lấy dấu + cầu tạm (45); thử khung (30); gắn (30-45) | giá mão x số đơn vị (thường 3) | Trụ yếu, nha chu chưa ổn, nhịp cầu dài |
| 8 | Mất nhiều răng: hàm tháo lắp | PHH-08/09: dấu sơ khởi (30); dấu cá nhân + khớp cắn (45); thử răng sáp (30); giao hàm (30); chỉnh đau 1-2 lần (15-20) | 2.000.000-10.000.000/hàm | Sống hàm tiêu quá mức, tổn thương niêm mạc nghi ác tính |
| 9 | Tẩy trắng | Cạo vôi, trám sâu trước (30); THM-02 tại ghế (60) hoặc THM-01 giao khay; tái khám (20) | 750.000-3.000.000 | Nhiễm tetracycline nặng, thai kỳ, men nhạy cảm nặng, răng trước có trám/mão |
| 10 | Trẻ em: sâu răng sữa | NHI-01 trám GIC/composite (20-30, 1-2 buổi); NHI-03 nếu viêm tủy; PHN-03 bôi fluor; tái khám 3-6 tháng | 200.000-350.000/răng + tủy 500.000-1.000.000 | Trẻ không hợp tác cần gây mê |
| 11 | Niềng răng ca đơn giản | Khám, phim, dấu (60, 1-2 buổi); cạo vôi, trám sâu; CHN-01 gắn mắc cài (90); tái khám siết mỗi 4-6 tuần (20-30, khoảng 18-24 lần); tháo mắc cài + CHN-03 hàm duy trì (60, 1-2 buổi) | 20.000.000-45.000.000 | Lệch hàm xương nặng, phẫu thuật chỉnh hàm, nhổ nhiều răng phức tạp, nha chu |
| 12 | Răng vỡ lớn đã chữa tủy | Phần sau tủy của kế hoạch 2: chốt/cùi, dấu, gắn mão | mão 1.500.000-9.000.000 + chốt | Răng không đủ cùi và không phục hồi được |
| 13 | Sâu nhiều răng | Ưu tiên răng đau/gần tủy; trám 2-4 răng/buổi (30-40/răng, 3-5 buổi); cạo vôi; bôi fluor; tư vấn chế độ ăn; tái khám 3 tháng | 250.000-700.000/răng | Răng đã viêm tủy chuyển kế hoạch 2/3 |
| 14 | Đau cấp: viêm tủy/áp xe | Khám, phim; NOI-01 hoặc NHO-05 (30, 1 buổi) + thuốc; tái khám 24-48 giờ; sau đó vào kế hoạch 2/3 hoặc nhổ | giá mở tủy chưa có nguồn; rạch áp xe 800.000-1.000.000 | Sưng lan sàn miệng/cổ, sốt cao, khó nuốt hoặc thở: chuyển viện ngay |
| 15 | Nhổ răng không giữ được kèm phục hình sau | NHO-01 (20-30, 1 buổi); tái khám 7 ngày; chờ lành 4-8 tuần (ước lượng); chuyển kế hoạch 6/7/8 | 100.000-600.000 + phục hình | Bệnh toàn thân (chống đông, đái tháo đường chưa ổn) |
| 16 | Duy trì định kỳ (recall) | Khám + PHN-01 (30-45), mỗi 6 tháng (3-4 tháng nếu có nha chu) | 150.000-500.000/lần | Không có |

Về cách sắp xếp, các quy tắc tiên quyết đều là suy luận từ quy trình lâm sàng, không có nguồn Việt Nam: cạo vôi và kiểm soát viêm lợi đi trước phục hình và niềng; điều trị tủy đi trước chốt và mão; nhổ răng và chờ lành thương đi trước implant, cầu, hàm giả; tẩy trắng đi sau cạo vôi, trám sâu và trước khi làm mão hoặc veneer để chọn màu. Quy trình implant của BV RHM TPHCM gồm tư vấn, CT Cone Beam, đặt trụ, gắn abutment và lấy dấu, gắn mão, tái khám, với tổng thời gian 2-6 tháng ([Medpro implant](https://medpro.vn/tin-tuc/gia-trong-rang-implant)), khớp với mô hình kế hoạch 6. Thời gian tích hợp truyền thống là 3 tháng hàm dưới và 6 tháng hàm trên, nhưng đây là nguồn thứ cấp ([OsseoNews](https://osseonews.com/short-wait-for-osseointegration)). Với mão cầu labo, quy trình 5 bước của Vinmec (khám, làm sạch, mài và lấy dấu, thử, gắn và tái khám 6 tháng) xác nhận hai giai đoạn chính ([Vinmec](https://www.vinmec.com/vie/bai-viet/quy-trinh-lam-rang-su-dien-ra-nhu-nao-vi)), còn số ngày labo (mão 3-7 ngày, cầu/veneer 5-10, hàm tháo lắp 5-10, hàm khung 7-14) là ước lượng chưa kiểm chứng và nên hỏi trực tiếp 2-3 labo. Tái khám chỉnh nha mắc cài kim loại thường 4-6 tuần và tự buộc 6-8 tuần ([Medpro](https://medpro.vn/tin-tuc/gia-nieng-rang-mac-cai-kim-loai)); thời gian niềng từ dưới 1 năm đến hơn 3 năm tùy độ khó, nên con số 18-24 tháng ở kế hoạch 11 là mức trung bình hợp lý chứ không phải chuẩn.

## Ca ngoài phạm vi: ranh giới có nguồn hẹp, phần còn lại cần hội đồng chuyên môn

Ranh giới chuyển tuyến có nguồn rất hẹp. Với nha chu, túi sâu trên 5 mm, tiêu xương ổ răng nặng hoặc viêm túi dưới xương cần bác sĩ nha chu ([Tâm Anh](https://tamanhhospital.vn/nao-tui-nha-chu/)), và nạo túi kín chỉ thực hiện khi túi không quá 5 mm. Với cắt chóp, chỉ định gồm dụng cụ gãy kẹt, gãy chân răng, nội nha thất bại có nhiễm khuẩn quanh chóp, u hạt hoặc nang chân răng, còn chân răng quá ngắn là chống chỉ định tương đối ([Vinmec](https://www.vinmec.com/vie/bai-viet/cat-va-nao-quanh-chop-rang-vi), [Long Châu](https://nhathuoclongchau.com.vn/bai-viet/khi-nao-can-thuc-hien-cat-chop-rang.html)). Với implant, khung ITI SAC chia ca thành Straightforward, Advanced và Complex, giúp bác sĩ tự đánh giá năng lực ([ITI Congress](https://events.iti.org/congressnorthamerica/treatment-planning-for-dental-implants-using-the-iti-sac-classification-system)); phòng khám nhỏ nên giới hạn ở Straightforward. Với chỉnh nha, hướng dẫn chuyển tuyến của NHS chuyển ca răng ngầm, lệch khớp cắn xương cần phối hợp phẫu thuật hàm và ca IOTN 4-5 ([NHS Grampian](https://www.hi-netgrampian.scot.nhs.uk/wp-content/uploads/2026/02/Orthodontics.pdf)). Độ khó nhổ răng khôn tăng dần theo cấp 1-3 và ngầm trong các bảng giá, nhưng các bảng này không nêu tiêu chí. Với tủy răng sữa, AAPD tách cắt tủy buồng khỏi lấy tủy toàn phần theo tình trạng tủy.

Danh sách chuyển tuyến sau đây là suy luận chuyên môn chung, không có tiêu chí chính thức của Bộ Y tế hay Hội RHM Việt Nam, và phải được hội đồng chuyên môn của phòng khám xác nhận. Nên đưa vào danh mục "ngoài phạm vi" với cờ chuyển tuyến kèm lý do và ngày, chứ không tạo kế hoạch mẫu cho chúng.

| Nhóm | Ca/dịch vụ chuyển tuyến |
|---|---|
| Phẫu thuật | Răng khôn ngầm sâu sát ống thần kinh hàm dưới hoặc xoang, độ 4; nang/u; cắt chóp răng hàm lớn; phẫu thuật xương phức tạp |
| Nội nha | Ống tủy canxi hóa hoặc cong nặng; dụng cụ gãy; thủng sàn/chân; tiêu chân; tủy lại phức tạp; nang lớn |
| Nha chu | Túi >5 mm cần lật vạt, ghép xương hoặc nướu; răng lung lay độ 3 |
| Implant | Ghép xương lớn, nâng xoang, All-on-4/6, vùng thẩm mỹ cao, đái tháo đường hoặc chống đông chưa kiểm soát, xạ trị đầu cổ, dùng bisphosphonate |
| Chỉnh nha | Lệch xương Class II/III nặng cần phẫu thuật hàm, răng ngầm, chen chúc nặng/nhổ nhiều, khớp cắn hở; khay trong suốt phức tạp |
| Phục hình | Phục hình toàn hàm phức tạp, tăng kích thước dọc, rối loạn khớp thái dương hàm, cầu dài trên 4 đơn vị, nha chu chưa kiểm soát, nghiến nặng chưa có máng, dị ứng kim loại, trụ yếu |
| Nha nhi | Trẻ không hợp tác cần gây mê |
| Cấp cứu/toàn thân | Nhiễm trùng lan (sưng mặt-cổ, sốt, khó nuốt, há hạn chế, khó thở); nghi tổn thương ác tính; bệnh toàn thân chưa kiểm soát (đái tháo đường, rối loạn đông máu, thuốc chống đông, tim mạch) |

Một chi tiết đáng lưu ý: một nguồn khuyên cạo vôi trước các bước khác, nhưng cũng xếp "đánh giá sức khỏe toàn thân và chuyển bác sĩ khi nghi bệnh chưa chẩn đoán" thành giai đoạn đầu tiên ([Wake Tech](https://www.waketech.edu/node/988351)). Điều này hỗ trợ việc DCMS nên có bước 0 là khám và hỏi bệnh sử, kèm cờ "bệnh toàn thân" trên hồ sơ bệnh nhân.

## Cấu trúc dữ liệu và quy tắc nghiệp vụ để nạp vào DCMS

Khung trường dữ liệu dưới đây là đề xuất của nhóm nghiên cứu; không có tài liệu phần mềm quản lý phòng khám nào để đối chiếu. Mỗi dòng hạng mục kế hoạch cần đủ các thành phần người dùng yêu cầu: ngày, răng, điều trị/dịch vụ, thời gian dự kiến (phút), ghi chú, bác sĩ và phụ tá, chi phí.

| Trường | Kiểu/giá trị gợi ý | Ghi chú nghiệp vụ |
|---|---|---|
| planItemId, planId | khóa | Một kế hoạch có nhiều dòng |
| phase | 0 Khám/phim; 1 Cấp cứu; 2 Kiểm soát bệnh; 3 Tái đánh giá; 4 Phục hồi và nội nha; 5 Phục hình/chỉnh nha/thẩm mỹ; 6 Duy trì | Suy luận từ khung 5 giai đoạn |
| sequence, visitNo | số nguyên | Thứ tự bước và buổi hẹn |
| plannedDate | ngày | Ngày dự kiến thực hiện |
| toothCodes | mã FDI hoặc vùng/hàm/toàn hàm | Cho phép nhiều răng, một dòng có thể là "toàn hàm" |
| serviceId, serviceName | khóa/chuỗi | Liên kết danh mục dịch vụ |
| unit | răng, hàm, 2 hàm, túi, lần, phim, ca, đơn vị cầu, cấp độ | Đơn vị tính thay đổi theo dịch vụ; nạo túi tính theo túi hoặc răng, nhổ khôn theo cấp |
| plannedMinutes | số nguyên | Lấy mặc định từ dịch vụ, cho sửa trên dòng |
| status | dự kiến, đang thực hiện, hoàn tất, hủy | |
| prerequisiteItemId | khóa | Ví dụ: mão sau tủy, abutment sau tích hợp xương |
| dentistId, assistantId | khóa nhân sự | Bác sĩ và phụ tá cho từng dòng |
| unitPrice, quantity, discountAmount, discountReason, totalPrice | số/chuỗi | Chi phí; lưu người duyệt giảm giá |
| notes | chuỗi | Ghi chú |
| warrantyMonths | số nguyên | Bảo hành theo dịch vụ (ví dụ mão 7-15 năm tùy vật liệu), điều kiện tái khám định kỳ |
| followUpDate | ngày | Tái khám |
| exclusionNotes | chuỗi (cấp kế hoạch) | Điều kiện loại trừ và chuyển tuyến |
| referralFlag, referralReason, referralDate | cờ/chuỗi/ngày | Cho ca ngoài phạm vi |
| deposit, approvalStatus, createdBy | cấp kế hoạch | Đặt cọc cho gói dài ngày |

Một số quy tắc nghiệp vụ đáng đưa vào. Giá nên lưu dưới dạng khoảng min-max cùng giá áp dụng cụ thể, vì nguồn đều là khoảng. Đơn vị tính phải là thuộc tính của dịch vụ chứ không cố định "răng". Implant nên tách thành ba dịch vụ độc lập (trụ, abutment, mão) vì giá và thời điểm khác nhau; chỉnh nha tính theo ca kèm gói tái khám. Cầu răng tính theo số đơn vị, trong đó răng trụ và răng thay đều là một đơn vị. Với mão và hàm có labo, nên có trạng thái đơn labo (đã gửi, nhận về, thử lại, gắn), phiếu labo gồm bệnh nhân, răng, vật liệu, màu, hạn giao. Vật liệu tiêu hao theo dịch vụ (đầu siêu âm, composite, gutta-percha, fluor varnish) và bảo hành theo vật liệu là các thuộc tính nên có. Đặt cọc và giảm giá là chính sách của phòng khám, không có nguồn, nên để cấu hình.

## Phần nào có nguồn, phần nào cần bác sĩ xác nhận, và các khoảng trống

Phần có nguồn gồm: khung 5 giai đoạn và nguyên tắc tái đánh giá ([Wake Tech](https://www.waketech.edu/node/988351), [Temple](https://www.dental.temple.edu/Storage/DentalSyllabus/D2%20Exam%20Archive/D2%20Summer%20Exam%20Archive/Tx%20Planning/TxPlan-Exam-2010.doc)); quy trình tủy và phục hình ở mức bước, không có phút ([Vinmec](https://www.vinmec.com/vie/bai-viet/nguyen-tac-dieu-tri-va-quy-trinh-lay-tuy-rang-vi)); các ngưỡng nha chu và cắt chóp; ITI SAC; tần suất tái khám chỉnh nha; và các khoảng giá niêm yết đã trích. Phần suy luận cần bác sĩ xác nhận gồm: toàn bộ cột phút và số buổi, thời gian labo, thời gian chờ lành thương sau nhổ (4-8 tuần), quy tắc tiên quyết giữa các bước, toàn bộ danh sách chuyển tuyến, quy tắc đặt cọc và giảm giá, và các trường dữ liệu. Nhóm nghiên cứu ghi rõ đã không truy cập được hướng dẫn ADA, FDI, EFP hay quy trình chi tiết của Bộ Y tế; danh mục 347 quy trình kỹ thuật RHM chỉ nêu tên kỹ thuật và số hiệu quyết định bị ghi không nhất quán trên trang (3027 so với 3207/QĐ-BYT) ([Thư viện pháp luật](https://thuvienphapluat.vn/van-ban/The-thao-Y-te/Quyet-dinh-3027-QD-BYT-Quy-trinh-ky-thuat-kham-chua-benh-chuyen-nganh-Rang-Ham-Mat-290450.aspx?tab=3)).

Về giá, có ba vấn đề cần nói thẳng. Thứ nhất, **không có bảng giá gốc của phòng khám nhỏ/vừa**: nguồn là chuỗi, bệnh viện (BV RHM SG, BV RHM TPHCM, BV RHM TW), trang xếp hạng quảng bá (Toplist.vn), bài tổng hợp SEO (Medpro, Long Châu) và diễn đàn (Tinhte). Thứ hai, **giá lệch nhau giữa các nguồn**: điều trị tủy hàm lớn 1,8-4 triệu; nhổ khôn hàm dưới độ 1 từ 1,35 triệu (Shark) đến 3 triệu (BV RHM SG, I-Dent); sealant 150.000-200.000 so với 2.000.000; nguồn My Auris tự mâu thuẫn về khoảng giá implant đơn lẻ (12-28,5 triệu và 13-35 triệu); veneer, Emax và Lisi 6-15 triệu tại các phòng khám phân khúc cao. Thứ ba, **nhiều nguồn không ghi năm hoặc đơn vị tính**: Shark không ghi đơn vị, Peace và Toplist.vn không ghi năm; chỉ Medpro (06/07/2026, 26/09/2026) và một số bài khác có ngày. Do đó khoảng giá trong báo cáo nên được xem là mốc tham chiếu để phòng khám điều chỉnh, và đề xuất hợp lý là thu thập thêm 5-10 bảng giá phòng khám nhỏ có ngày để thu hẹp khoảng.

Các khoảng trống không có giá nguồn nào: khám và tư vấn, phim quanh chóp, CT Cone Beam, phim sọ nghiêng, lấy dấu/scan, mở tủy giảm đau, chốt sợi tại phòng khám nhỏ, trám/lấy tủy răng sữa tại BV RHM SG, gói điều trị nha chu, viêm lợi riêng, máng nhai, cầu răng, hàm tạm, cùi đúc kim loại, chốt đúc, hàm tháo lắp toàn phần, mắc cài sứ, hàm duy trì cố định, thuốc. Thiếu nguồn cho thời lượng phút, số ngày labo, quy trình riêng cho veneer, inlay/onlay, hàm tháo lắp, và tần suất tái khám sau hàm duy trì. Khi seed, các dòng này nên để giá trống hoặc đánh dấu "chờ phòng khám nhập".

## Kết luận

Điểm đáng chú ý nhất là độ bất đối xứng giữa hai loại dữ liệu: cấu trúc (khung giai đoạn, thứ tự tiên quyết, đơn vị tính, các trường dữ liệu) đã đủ chắc để thiết kế DCMS và seed, còn nội dung số (giá, phút, buổi) chưa đủ chắc để dùng như sự thật. Vì vậy seed nên nạp giá dưới dạng khoảng min-max và đánh dấu mức tin cậy ngay trong dữ liệu, để phòng khám dễ nhận ra giá nào cần xác nhận. Những dịch vụ hay đi theo gói (niềng, implant, phục hình) thì giá lệch xa nhất, nên cũng là nơi cần cấu hình giá riêng đầu tiên.

Hướng tiếp theo thực tế nhất là chuyển báo cáo này cho một bác sĩ nha khoa (và hai hoặc ba labo trong nước) duyệt riêng ba bảng: thời lượng và số buổi, danh sách chuyển tuyến, và khoảng giá của 10 dịch vụ dùng nhiều nhất (cạo vôi, trám, tủy, nhổ, mão, implant, niềng). Khi ba bảng này được xác nhận, kế hoạch seed có thể nạp với độ rủi ro thấp; trước đó, hệ thống nên hiển thị rõ đây là dữ liệu mẫu, không phải khuyến cáo y khoa.
