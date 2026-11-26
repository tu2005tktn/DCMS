# DCMS chỉ thu tiền sau khi làm xong, chưa có tiền trả trước

DCMS hiện chỉ xử lý được một kiểu thanh toán: **làm xong việc rồi mới thu tiền cho việc đó**. Hệ thống không lưu được tiền trả trước, tiền đặt cọc theo %, trả góp có lịch, hoàn tiền hay tín dụng chưa phân bổ. Vì vậy các ca phổ biến ở nha khoa Việt Nam (niềng răng trả trước khoảng 30–50%, implant chia đợt 70/30, bệnh nhân bỏ dở cần hoàn tiền) chưa có chỗ ghi trong hệ thống. Màn "Doanh thu" thực chất là **tiền đã thu** (cash-basis), không phải doanh thu thực hiện; ba cơ chế ghi tiền chạy song song và tài liệu về "Đã trả" mâu thuẫn nhau. Mô hình đáng theo, rút từ phần mềm nha khoa quốc tế, là tách ba thứ: tiền vào, phân bổ vào việc đã làm, và hoàn tiền. Tiền chưa phân bổ là nghĩa vụ phải trả, chưa phải doanh thu ([Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)). Với năm chỉ số tách bạch (tiền thu, doanh thu thực hiện, giá trị kế hoạch đã chốt, tiền nhận trước, công nợ), mọi kịch bản chính đều tính được bằng phép trừ đơn giản. Về thuế và hóa đơn, mọi kết luận trong báo cáo này chỉ đến từ **bài tổng hợp của bên thứ ba**. Không đọc được văn bản gốc nào, nên mọi điểm về hạch toán, hóa đơn khi nhận cọc và hộ kinh doanh đều phải được kế toán đối chiếu. Bằng chứng từ Việt Nam chủ yếu là trang quảng cáo của phòng khám, mọi con số VND là minh họa, và những chỗ là suy luận đều được đánh dấu **[SUY]**.

## Cách đọc độ tin cậy của báo cáo này

Báo cáo tổng hợp bốn bộ ghi chú: hiện trạng DCMS (đọc repo, 08/11/2026), mô hình thu tiền, ghi nhận doanh thu và thuế, và danh mục ca đặc biệt. Chất lượng bằng chứng không đồng đều.

| Phần | Chất lượng bằng chứng | Hệ quả khi dùng |
|---|---|---|
| Hiện trạng DCMS | Cao: đọc trực tiếp spec và code backend. Chưa chạy hệ thống, chưa đọc frontend và `FinanceService` đầy đủ | Có thể tin trừ các dòng đánh dấu [SUY] |
| Mô hình thu tiền ở Việt Nam | Thấp đến trung bình: nguồn chủ yếu là bài quảng cáo, listicle (Medpro, Toplist...) | Tỷ lệ cọc 30–50%, 70/30 chỉ là ví dụ, không phải chuẩn thị trường |
| Cách phần mềm quốc tế xử lý | Trung bình đến cao cho Open Dental (có manual); thấp cho Dentrix và các phần mềm khác | Dùng làm tham chiếu thiết kế |
| Thuế, hóa đơn, kế toán Việt Nam | Thấp đến trung bình: thuvienphapluat.vn, vbpl.vn, mof.gov.vn chặn truy cập (HTTP 403), không đọc được văn bản gốc | Mọi điểm phải được kế toán đối chiếu (xem phần "Thuế, hóa đơn, kế toán") |
| Số tiền VND trong ví dụ | Do người ghi chú tự dựng | Chỉ minh họa cơ chế |

## Năm chỉ số tách bạch và công thức

Mỗi con số trên dashboard nên thuộc đúng một trong năm khái niệm dưới đây. Các định nghĩa là **đề xuất thiết kế của người ghi chú (suy luận), chưa được kế toán kiểm chứng**, dựa trên cách quản lý nha khoa Mỹ tách "production" (giá trị dịch vụ đã thực hiện) khỏi "collections" (tiền thu). Tỷ lệ thu chuẩn là tiền thu chia net production ([Curve Dental](https://www.curvedental.com/dental-blog/dental-collections-ratio-what-the-number-reveals-about-your-practices-billing-health), [Pearly](https://pearly.co/academy/dental-practice-benchmarking-for-accounts-receivable)). Các nguồn dùng mẫu số khác nhau (gross hay adjusted) nên cần chốt mẫu số ([Rework](https://resources.rework.com/libraries/dental-clinic-growth/key-financial-metrics-dental-practices)). Ký hiệu: P là tiền đã thu ròng sau hoàn, R là doanh thu thực hiện theo giá sau giảm.

| Chỉ số | Công thức | Lưu ý |
|---|---|---|
| Tiền thu trong kỳ (P kỳ) | Σ tiền thực nhận (cọc + trả trước + trả đợt) − hoàn tiền trong kỳ | Theo ngày thực nhận. Cash-basis |
| Doanh thu thực hiện (R) | Σ giá trị hạng mục/giai đoạn đã hoàn thành trong kỳ, sau chiết khấu, trừ điều chỉnh/hoàn liên quan phần đã làm | Tính theo giá sau giảm, không ghi 30 triệu rồi chiết khấu sau |
| Giá trị kế hoạch đã chốt | Σ hạng mục đã chấp nhận, sau chiết khấu hợp đồng | Không phải doanh thu. Là cam kết (booked) |
| Tiền nhận trước chưa thực hiện | Σ theo từng bệnh nhân/kế hoạch của max(0, P lũy kế − R lũy kế) | Là nghĩa vụ (contract liability), không phải doanh thu |
| Công nợ phải thu | Σ theo từng bệnh nhân/kế hoạch của max(0, R lũy kế − P lũy kế) | Chỉ phát sinh từ việc đã làm |
| Tỷ lệ thu | P / R (theo năm hoặc cuốn chiếu) | Biến động mạnh theo tháng, mẫu số cần chốt |

Hai hệ thức kiểm tra dùng chung cho cả bốn bộ ghi chú. Thứ nhất, **đối soát toàn phòng khám**: Nhận trước − Công nợ = Tổng thu lũy kế − Tổng doanh thu thực hiện lũy kế. Thứ hai, **bất biến cấp bệnh nhân** (từ danh mục ca): Dư có = Tổng thu ròng − Tổng phân bổ cho việc đã hoàn thành, không bao giờ âm. Giá trị kế hoạch đã chốt hoàn toàn độc lập với hai hệ thức này: sửa kế hoạch chỉ đổi nó và đổi R tương lai, không đổi P.

## Các kịch bản thanh toán với số liệu từng bước

Tất cả dùng kế hoạch mẫu **30.000.000 VND** chia ba giai đoạn 10 triệu (giả định để minh họa, theo ví dụ của ghi chú về doanh thu). Các cột là số **lũy kế** sau mỗi bước, đơn vị triệu VND.

| Kịch bản | Bước | P lũy kế | R lũy kế | Nhận trước | Công nợ | Ghi chú |
|---|---|---|---|---|---|---|
| 1. Trả đủ một lần | Ngày 1: thu 30 | 30 | 0 | 30 | 0 | Hóa đơn phát sinh khi thu (nguồn thứ cấp, xem phần thuế) |
| | Xong giai đoạn 1 | 30 | 10 | 20 | 0 | |
| | Hoàn thành | 30 | 30 | 0 | 0 | |
| 2. Cọc 30% rồi trả theo giai đoạn | Ngày 1: cọc 9 | 9 | 0 | 9 | 0 | Cọc trừ vào tổng chi phí |
| | Xong GĐ1 (R=10), thu thêm 1 | 10 | 10 | 0 | 0 | Cọc cấn trừ vào GĐ1. Trước khi thu: công nợ 1 |
| | Xong GĐ2, thu 10 | 20 | 20 | 0 | 0 | |
| | Xong GĐ3, thu 10 | 30 | 30 | 0 | 0 | |
| 3a. Trả góp 12 tháng × 2,5 (điều trị nhanh) | Tháng 6: điều trị xong 30, mới thu 15 | 15 | 30 | 0 | 15 | Làm trước, thu sau |
| 3b. Trả góp (điều trị chậm) | Tháng 6: thu 15, mới làm 10 | 15 | 10 | 5 | 0 | Thu trước, làm sau |
| 4. Giảm 10% khi trả đủ (30 xuống 27) | Ngày 1: thu 27 (giá chốt 27) | 27 | 0 | 27 | 0 | Phân bổ 9 mỗi giai đoạn (**[SUY]**: chia đều theo giá trị) |
| | Xong GĐ1 | 27 | 9 | 18 | 0 | |
| | Hoàn thành | 27 | 27 | 0 | 0 | R tối đa 27 |
| 5a. Hủy giữa chừng, hoàn không phạt | Đã thu 30, đã làm 12, hoàn 18 | 12 | 12 | 0 | 0 | P ròng sau hoàn |
| 5b. Hủy, giữ phí phạt 2 | Hoàn 16 | 14 | 12 | 2 | 0 | 2 triệu giữ lại là thu nhập khác (**cần kế toán xác nhận**) |
| 6a. Sửa kế hoạch: thêm 0,6 sau khi đã trả 30 | Kế hoạch thành 30,6 | 30 | 0 | 30 | 0 | Giá trị kế hoạch đã chốt 30,6. Phải thu thêm 0,6 khi làm |
| 6b. Bớt hạng mục 0,6 chưa làm | Kế hoạch thành 29,4 | 30 | 0 | 30 | 0 | Phần dư 0,6 là chênh so với kế hoạch: hoàn hoặc giữ làm tín dụng theo chính sách |

Các điểm cần đọc kỹ. Ở kịch bản 2, nếu cọc được cấn trừ vào **giai đoạn đầu** như trên, số dư nhận trước về 0 ngay sau giai đoạn 1. Nếu chính sách cấn trừ vào **giai đoạn cuối** (hoặc chia đều), khách vẫn trả đủ 10 triệu mỗi giai đoạn đầu và cọc 9 triệu nằm ở nhận trước đến cuối. Hai cách cho cùng tổng nhưng khác dòng tiền; đây là một quyết định chính sách (xem phần quyết định của chủ phòng khám). Ở kịch bản 4, ghi chú nhấn mạnh doanh thu thực hiện tính theo giá sau giảm, phân bổ cho các giai đoạn. Ở kịch bản 5b, việc giữ phí phạt là suy luận của người ghi chú, **không có nguồn** về xử lý kế toán. Ở kịch bản 6a, "phải thu thêm 0,6" là công nợ tiềm năng; theo công thức, nó chỉ thành công nợ khi việc đó được làm mà chưa thu. Ngoài sáu kịch bản trên, **trả góp qua công ty tài chính** là một dòng tiền khác (ca 36): khoản 24 triệu, tổ chức tài chính chuyển ròng 22,8 triệu sau phí 5% (1,2 triệu). R là 24, P là 22,8, phí 1,2 ghi chi phí tài chính, và trong lúc chờ có khoản phải thu từ tổ chức tài chính chứ không phải công nợ của bệnh nhân. Con số 5% là ví dụ; tại Việt Nam nguồn chỉ nói có thể phát sinh phí hồ sơ, bảo hiểm khoản vay, phí chuyển đổi trả góp ([Medpro](https://medpro.vn/tin-tuc/nieng-rang-tra-gop)).

### Thực tế thị trường Việt Nam, và độ mỏng của bằng chứng

Mô hình phổ biến là trả trước một tỷ lệ rồi chia phần còn lại. Với niềng răng, một bài mô phỏng nêu trả trước khoảng 30% (mắc cài kim loại 30 triệu, trả trước 9 triệu, khoảng 1,16 triệu mỗi tháng trong 18 tháng; Invisalign 80 triệu, trả trước 24 triệu, khoảng 3,1 triệu mỗi tháng) ([Medpro](https://medpro.vn/tin-tuc/nieng-rang-tra-gop)). Một phòng khám ở Quảng Ninh được nêu trả trước 40–50% với lãi 0% ([Toplist Quảng Ninh](https://toplist.vn/top-list/dia-chi-nieng-rang-tra-gop-uy-tin-nhat-tinh-quang-ninh-56351.htm)). Với implant có ví dụ trả trước 70% khi đồng ý cấy ghép và 30% còn lại khi hoàn tất ([Nha khoa Đông Nam](https://sites.google.com/site/nhakhoadongnam/cay-ghep-rang-implant-gia-re-tron-goi-tai-tp-hcm)). Trả góp trực tiếp tại phòng khám cam kết bằng hợp đồng hai bản ([Toplist Hà Nội](https://toplist.vn/top-list/dia-chi-nieng-rang-tra-gop-uy-tin-nhat-ha-noi-46136.htm)); trả góp qua thẻ tín dụng có kỳ hạn 3/6/9/12 tháng ([Medpro](https://medpro.vn/tin-tuc/nieng-rang-tra-gop)). Trả muộn vài ngày thường không phạt, nợ kéo dài có thể bị ngừng điều trị (cùng nguồn Medpro). Phần mềm MEDi bản nha khoa có chức năng tạm ứng rồi trừ dần theo dịch vụ đã dùng ([BrandsVietnam](https://www.brandsvietnam.com/congdong/topic/336468-Phan-mem-quan-ly-phong-kham-chuan-Nhat-MEDi-ra-mat-phien-ban-danh-rieng-cho-phong-kham-Nha-khoa)), nhưng thiếu chi tiết công khai. Đây gần như toàn bộ bằng chứng Việt Nam; **tất cả đều là nội dung quảng cáo hoặc listicle, không độc lập**. Không tìm được nguồn đáng tin về tỷ lệ cọc cố định, thời hạn hiệu lực cọc, phạt trả muộn, ai duyệt chiết khấu, hoàn tiền trả trước ở Việt Nam, hay tranh chấp thực tế về cọc niềng/implant. Quốc tế, ADA liệt kê các lựa chọn: trả đủ trước (có thể chiết khấu), đặt cọc tối thiểu, chia 2–3 đợt, kế hoạch nội bộ, tài chính bên thứ ba, và khuyên chính sách tài chính phải nêu rõ hoàn đủ, một phần hay không hoàn khi bệnh nhân bỏ dở ([ADA](https://www.ada.org/resources/practice/practice-management/considerations-in-developing-a-financial-policy)).

## Danh mục ca đặc biệt, cô đọng

Danh mục gốc là **suy luận của người ghi chú cho bối cảnh Việt Nam**; chỉ các nguyên tắc nền (prepay, allocate, refund, income transfer) có nguồn ([Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html), [ADA](https://www.ada.org/resources/practice/practice-management/considerations-in-developing-a-financial-policy)). Số ca ghi trong tài liệu gốc là "42 ca, nhóm A đến I", nhưng thực tế liệt kê 46 mục trong các nhóm A đến G, trong đó một số là kiểm soát chung (xem phần mâu thuẫn). Bảng dưới gom thành chín nhóm. Số trong ngoặc là số thứ tự ca gốc.

| Nhóm | Ca (số gốc) | Tác động chính lên P / R / Nợ / Dư có |
|---|---|---|
| A. Thời điểm thanh toán | Trả trước toàn bộ (1); cọc cố định (2); cọc theo % (3); trả nhiều hơn phần đã làm (4); trả ít hơn (5); thu trước khi kế hoạch duyệt (6); thu chưa phân bổ (7); phân bổ theo thủ thuật chỉ định so với FIFO (8) | Tiền vào tăng P, R không đổi, tạo Dư có. Trả thiếu tạo Nợ. Cách phân bổ không đổi tổng nhưng đổi thủ thuật nào còn nợ (ảnh hưởng hoa hồng, bảo hành, hoàn). Cọc theo % tính từ giá đã duyệt, đổi giá thì cọc không tự đổi |
| B. Giá và điều chỉnh kế hoạch | Giảm khi trả đủ (9); voucher, giới thiệu, điểm (10); duyệt giảm vượt ngưỡng (11); thêm hạng mục (12); bớt hạng mục (13); đổi số lượng răng (14); đổi vật liệu (15); hoàn phần chiết khấu khi rút hạng mục (16); giảm có điều kiện bị mất điều kiện (17); bảng giá đổi sau khi đồng ý (18) | Đổi giá trị kế hoạch và R tương lai, không đổi P. Rút hạng mục khi đã trả đủ tạo Dư có. Hoàn hạng mục đã giảm theo tỷ lệ phân bổ (rút hạng mục gốc 6 triệu trong gói giảm 10% thì hoàn 5,4 triệu). Dùng giá snapshot lúc duyệt |
| C. Dừng điều trị | Bỏ dở, hoàn phần chưa làm (19); cọc không hoàn (20); trừ chi phí vật tư, labo, phí hủy khi hoàn (21); phòng khám hủy (22); bảo hành, làm lại (23); biến chứng (24); chuyển phòng khám khác (25); bệnh nhân mất hoặc vắng lâu (26); dư có hết hạn (27) | Hoàn làm giảm P và đưa Dư có về 0. Cọc không hoàn, phí trừ, dư có hết hạn là chuyển sang thu nhập khác (**rủi ro pháp lý, không có nguồn Việt Nam**). Bảo hành tạo thủ thuật giá 0 nhưng có chi phí |
| D. Hoàn tiền và đảo ngược | Nhập sai (28); chuyển khoản bị đảo hoặc chargeback (29); hoàn một phần (30); phương thức hoàn (31); hoàn gói đã giảm (32); hoàn sau khi đã xuất hóa đơn (33) | Không sửa hoặc xóa trực tiếp mà hủy kèm lý do, lập phiếu mới hoặc bút toán điều chỉnh. Tổng hoàn không vượt tổng đã thu của payment gốc (tránh unearned âm). Hoàn thường về đúng kênh và người đã trả. Hóa đơn đã xuất cần điều chỉnh hoặc thay thế (cần kế toán) |
| E. Bên thứ ba | Người trả khác bệnh nhân (34); bảo hiểm hoặc công ty đài thọ (35); công ty trả góp trả ròng sau phí (36); cổng thanh toán có phí và độ trễ T+1 (37); ngoại tệ (38) | Tách payer khỏi patient. Phải thu bên thứ ba tách khỏi công nợ bệnh nhân. Phí cổng ghi chi phí. Ngoại tệ ghi nguyên tệ, tỷ giá, quy đổi VND (quy định pháp lý chưa tra cứu) |
| F. Đa chi nhánh và đa lần khám | Thu ở chi nhánh A, điều trị ở B (39); hoa hồng theo thu hoặc theo thực hiện (40); chi phí labo, vật tư so với doanh thu (41); chỉnh nha phí điều chỉnh hàng tháng và bảo hành (42); chốt doanh thu cuối tháng (43) | Dư có thuộc bệnh nhân toàn hệ thống, doanh thu ghi ở chi nhánh thực hiện, có công nợ nội bộ giữa chi nhánh. Hoa hồng cần chọn nhất quán một cơ sở và có thu hồi khi hoàn. Chốt tháng phải hiện cả "đã làm chưa thu" và "đã thu chưa làm" |
| G. Kiểm soát và đối soát | Đánh số phiếu thu (44); hủy phiếu và quyền sửa thanh toán quá khứ (45); chốt két cuối ngày (46) | Số phiếu liên tục không tái sử dụng, phiếu hủy giữ lại. Sau khóa kỳ chỉ đảo bút toán. Két chênh lệch ghi nhận có lý do |
| H. Nguyên tắc nền | Không xóa cứng (đảo bút toán), tách tiền thu khỏi doanh thu bằng phân bổ, mọi ngoại lệ tiền cần lý do và người duyệt | Nguồn gốc: Open Dental. Bảng quyền đề xuất lễ tân, quản lý, chủ phòng khám là suy luận |
| I. Ghi chú nhóm | Tài liệu gốc nói "nhóm A đến I" nhưng chỉ có A đến G | Xem phần mâu thuẫn |

Với niềng răng, ghi chú ca 42 nhấn mạnh phải chọn một cách ghi doanh thu (theo mốc, hoặc chia đều theo thời gian) và áp dụng nhất quán; với implant, doanh thu theo bước hoàn thành, không phân bổ đều theo thời gian, và giai đoạn chờ tích hợp xương không phát sinh doanh thu mới. Hợp đồng chỉnh nha nên ghi rõ "báo giá trọn gói, không phát sinh" ([Medpro](https://medpro.vn/tin-tuc/nieng-rang-tra-gop)). Về chính sách hoàn tiền, chưa có nguồn pháp lý Việt Nam. Thực hành quốc tế gồm hoàn hoặc giữ làm tín dụng (NHS Anh, [Dental Choices](https://dentalchoices.org/deposits-for-nhs-dental-treatment)), cọc 25% không hoàn để giữ lịch ([Wyndham House](https://www.wyndhamhousedental.com/?p=606)), và chính sách mẫu hoàn trong 7 ngày làm việc ([Tetri Smile](https://tetrismile.com/refund-policy)); một đạo luật California 2009 yêu cầu hoàn trong 15 ngày làm việc nhưng chưa xác minh còn hiệu lực ([leginfo](https://www.leginfo.ca.gov/pub/09-10/bill/asm/ab_0151-0200/ab_171_bill_20090902_enrolled.html)). Rủi ro nhận tiền trả trước mà không cung cấp dịch vụ được ghi nhận có thật ở nước ngoài ([Mass.gov](https://www.mass.gov/doc/aspen-dental-faq/download), [Moneywise](https://moneywise.com/news/patients-out-thousands-after-dentist-dies-before-finishing-prepaid-treatment-what-to-know-about-claims-in-probate)), nhưng không tìm thấy báo cáo tranh chấp tương ứng ở Việt Nam.

## DCMS xử lý từng ca ra sao và thiếu gì

### Kiến trúc hiện tại: ba đường tiền không hợp nhất

Theo ghi chú đọc repo, DCMS có ba cơ chế ghi tiền chạy song song. Đường A là sổ cái chung (`FinanceCharge`, `PaymentReceipt`, `PaymentAllocation`) cho khám và cận lâm sàng theo lượt khám, kèm charge của dossier (`backend/src/DCMS.Domain/Features/Finance/FinanceModels.cs:14-116`). Đường B là form "TRẢ TIỀN" trong phiếu điều trị, ghi vào `DossierPayment` và `DossierPaymentLine`, mỗi dòng gắn một việc đã làm (`backend/src/DCMS.Domain/Features/Treatment/DossierPayment.cs:3-24`, spec `docs/specs/TREATMENT-DOSSIER-PAYMENTS.md`). Đường C là lệnh PayOS (`DossierPayOsOrder`), chỉ là bước trung gian để tạo thanh toán loại Transfer của đường B (`backend/src/DCMS.Infrastructure/Features/Treatment/PayOsOrderService.cs`). Màn "Doanh thu" cộng `PaymentReceipt` Posted (ngày `ConfirmedAt`) với `DossierPayment` (ngày `CreatedAt`, không phải `PaidOn`) trong `backend/src/DCMS.Infrastructure/Features/Finance/RevenueService.cs`; spec `docs/specs/REVENUE-DASHBOARD.vi.md` tự nhận đó là "tiền đã thu, không phải doanh thu dồn tích/kế toán". Màn Thống kê công việc/dịch vụ là màn khác: Chi phí, Đã trả, Còn lại của việc đã làm, lọc theo **ngày tạo phiếu** `StartedOn` chứ không theo ngày thực hiện hay ngày thu (`WorkStatisticsService.cs:27-28`). Hai màn không đối chiếu được trực tiếp theo cùng kỳ. Ghi chú ghi nhận thêm: nguy cơ cộng đôi nếu cùng một việc được thu qua `/collections` (ledger) và `/payments` (đường B), vì code không chặn; đây là **[SUY]** của người ghi chú.

### Bảng đối chiếu theo kịch bản

| Kịch bản | DCMS hôm nay | Thiếu gì | Nguồn trong repo |
|---|---|---|---|
| Thu sau khi làm, có thu một phần và nhiều lần | **Làm được.** Mỗi dòng: Chi phí = round(Quantity × PriceSnapshotVnd); Giảm = min(chi phí, DiscountSnapshotVnd); Còn nợ = max(0, chi phí − giảm − đã trả). UI tự chia FIFO "cũ nhất trước" (server nhận phân bổ tường minh theo activity) | Xử lý tốt ca A5, A8 ở mức việc đã làm. Không có hạn trả, nhắc nợ, hay ngưỡng nợ | `TreatmentDossierPayments.cs:30-58, 92-125`; `docs/specs/PATIENT-TREATMENT-PAYMENT-UI.md:37` |
| Trả đủ một lần cho cả kế hoạch (ca 1) | **Không thể.** POST /payments yêu cầu việc đã Posted và số tiền ≤ Còn nợ; việc chưa làm không có nợ | Thực thể "tiền nhận trước" gắn bệnh nhân hoặc kế hoạch, phân bổ về sau | `TreatmentDossierPayments.cs:111-116`; `PATIENT-TREATMENT-PAYMENT-UI.md:30` |
| Cọc cố định hoặc theo % (ca 2, 3) | **Không lưu được.** `acknowledgedAdvanceVnd` bắt buộc 0; `unusedAdvance` luôn 0. `PaymentTerms` chỉ là ghi chú, "không tự ghi charge/payment khi đến hạn". `DepositException*` chỉ ghi lý do không cọc | Loại giao dịch cọc riêng, chính sách cọc (hoàn hay không, hạn dùng), tính % từ giá đã duyệt | `docs/specs/TREATMENT-LEDGER-INTEGRATION.md:15`; `FinanceService.cs:186`; `TreatmentFinancialSummaryPort.cs:28`; `docs/specs/S21-S43-TREATMENT-DOSSIER.md:16, 82`; `TreatmentDossier.cs:32-34` |
| Trả theo giai đoạn | **Chỉ theo việc đã làm.** Phiếu là các việc ClinicalActivity; không có "giai đoạn" làm mốc thu tiền | Mốc thanh toán gắn tiến độ, tự so sánh tiền thu với tiến độ | `S21-S43-TREATMENT-DOSSIER.md:16, 82` |
| Trả góp có lịch (ca 3) | **Không có.** Không có lịch kỳ, nhắc nợ, hay trạng thái quá hạn; trả góp bên thứ ba cũng không có mô hình | Lịch trả, theo dõi quá hạn, phương thức "trả góp qua đối tác" với khoản phải thu bên thứ ba | `hien_trang_dcms.md` mục 3, điểm 3 |
| Giảm giá khi trả đủ, voucher (ca 9, 10) | **Không có.** Giảm giá hiện chỉ ở hai nơi: kế hoạch (Fixed hoặc Percent, Proposed/Approved) và dòng sheet (VND hoặc Percent, lý do bắt buộc khi > 0, làm tròn half-up, không có bước duyệt) | Giảm có điều kiện, voucher có mã nguồn, quy tắc tính lại khi mất điều kiện | `S21-S43-TREATMENT-DOSSIER.md:80`; `TreatmentSheetActivityService.cs:59-65` |
| Duyệt giảm vượt ngưỡng (ca 11) | **Không đồng bộ.** UI duyệt giảm trừ và ngoại lệ cọc đã gỡ ở màn "Dịch vụ" (06/11), backend giữ nguyên; dòng sheet giảm tự nhập không duyệt | Một quy trình duyệt xuyên suốt theo vai trò | `S21-S43-TREATMENT-DOSSIER.md:90`; `TreatmentSheetActivityService.cs:65, 77` |
| Thêm hoặc bớt hạng mục sau khi đã thu (ca 12, 13, 14) | **Một phần.** Có version kế hoạch và chốt `plan/commit`; sửa phạm vi sau chốt qua S62. Tiền gắn với activity, không gắn kế hoạch, nên xóa dòng kế hoạch không động đến tiền. Xóa activity đã thu bị chặn hoàn toàn; sửa giá thấp hơn đã trả bị chặn 409. Sửa tiền đặt `FeeStatus=NeedsAdjustment` và đẩy outbox "correction request" nhưng chưa có consumer | Cơ chế xử lý chênh lệch (phải thu thêm, hoặc dư cần hoàn), consumer cho yêu cầu điều chỉnh | `TreatmentSheetActivityService.cs:67, 91, 64, 70, 77, 110-113`; `docs/specs/TREATMENT-PAYMENT-V25.md:15, 71` |
| Dừng điều trị, hoàn tiền (ca 19, 20, 21, 22) | **Không có.** Đóng phiếu `StoppedIncomplete` không xóa actual hay nợ và không có logic hoàn hoặc cấn trừ. Payment bất biến: "Hoàn tiền/hủy là yêu cầu riêng". Không có endpoint hủy hoặc hoàn payment | Hoàn tiền có audit, người duyệt, phiếu chi, lý do (khách bỏ, phòng khám hủy), khoản trừ chi phí | `docs/specs/S21-S43-TREATMENT-DOSSIER.md:13, 86`; `TREATMENT-DOSSIER-PAYMENTS.md:16`; `TreatmentEndpoints.cs:58-74` |
| Nhập sai, hủy phiếu (ca 28, 45) | **Một nửa.** Ledger A cho hủy phiếu Pending kèm lý do; phiếu Posted không sửa hoặc xóa. Đường B không có hủy | Hủy hoặc đảo phiếu đã Posted có lý do, khóa kỳ, phân quyền | `docs/specs/INVESTIGATION-BILLING.vi.md:17-18, 30` |
| Chuyển khoản dư, thiếu, đảo (ca 29, 37) | **Một phần.** PayOS Pending không tính vào Đã trả và doanh thu, chỉ Paid khi xác nhận đủ (webhook HMAC, poll, worker). Lệnh hết hạn sau 30 phút. Nếu dư đổi trước khi tiền về, lệnh chuyển `Review` và "không ghi vượt dư" | Quy trình xử lý lệnh `Review`; ngày thu của transfer là lúc xác nhận chứ không phải lúc khách chuyển; phí cổng; xử lý chargeback | `docs/specs/TREATMENT-TRANSFER-BILL.md:33-37`; `PayOsOrderService.cs:134-138, 182-190` |
| Trả quá (ca 4, 7) | **Bị chặn** (409), không tạo tín dụng | Số dư chưa phân bổ | `TREATMENT-DOSSIER-PAYMENTS.md`; `TreatmentDossierPayments.cs` |
| Gói trọn gói / `Included` | **Không có đường thu.** Dòng `Included` có Còn nợ 0 và không nhập được; "gói chưa có Q08 giữ trạng thái phí chưa xử lý" | Phân bổ tiền gói vào các buổi | `TREATMENT-DOSSIER-PAYMENTS.md:11`; `TREATMENT-LEDGER-INTEGRATION.md:5` |
| Báo cáo thu, R, Nợ, Dư có (ca 43) | **Chỉ thu.** Màn Doanh thu là cash-basis; không có doanh thu hoãn lại; `FinancialAccount` không lưu số dư. Không có báo cáo theo `PerformedAt`; không lọc bác sĩ hoặc dịch vụ; không Excel. "Còn nợ phiếu" chỉ tính trên việc đã làm, và spec nói rõ "không gọi là công nợ kế toán" | Doanh thu thực hiện theo kỳ, dư có theo tuổi, lệch "làm chưa thu" và "thu chưa làm", đối soát theo chi nhánh | `REVENUE-DASHBOARD.vi.md:5-17`; `WORK-SERVICE-STATISTICS.md:31, 47`; `PATIENT-TREATMENT-PAYMENT-UI.md:30` |
| Hoa hồng bác sĩ (ca 40) | **Không có model.** Chỉ có `PerformerId` và `AssistantId` trên activity làm dữ liệu thô (**[SUY]**) | Quy tắc hoa hồng theo thu hay theo thực hiện, thu hồi khi hoàn | `hien_trang_dcms.md` mục 2 và 3 |
| Thuế VAT, hóa đơn điện tử | **Chưa có.** "Không hiển thị ô VAT khi chưa có chính sách thuế" | Chính sách thuế, liên kết hóa đơn với thu và hoàn | `INVESTIGATION-BILLING.vi.md:30`; `PATIENT-TREATMENT-PAYMENT-UI.md:47` |
| Phân quyền thu | **Có thể hẹp.** Tạo payment dossier đòi policy `TreatmentDentist` cộng quyền tài chính; thu ngân hoặc lễ tân có thể không dùng được form (**[SUY]**, cần kiểm vai trò thực) | Bảng quyền: lễ tân thu và hủy trong ngày, quản lý hoàn và giảm vượt ngưỡng, chủ phòng khám xử lý cọc không hoàn | `TREATMENT-DOSSIER-PAYMENTS.md:21` |

Các giới hạn kỹ thuật phụ: Màn Doanh thu tải toàn bộ chứng từ rồi cắt trang trong bộ nhớ (`RevenueService.cs:30-47, 62`) và WorkStatistics gom trong bộ nhớ, đủ cho quy mô nhỏ nhưng là điểm cần lưu ý nếu lượng dữ liệu tăng. Dùng `CreatedAt` thay `PaidOn` làm ngày doanh thu có thể lệch kỳ khi nhập ngày thu quá khứ (`PaidOn` cho phép ≤ hôm nay); đây là **[SUY]** từ code. Ledger A thì không cho ngày nhận cách server hơn 5 phút.

### Mâu thuẫn giữa các tài liệu

Quá trình tổng hợp tìm ra các mâu thuẫn sau, cần xử lý trước khi viết spec.

| # | Mâu thuẫn | Các phía | Ý nghĩa |
|---|---|---|---|
| 1 | "Đã trả" của phiếu lấy từ đâu | `PATIENT-TREATMENT-PAYMENT-UI.md:29` nói `financialSummary.allocatedVnd` (ledger A); nhưng form Trả tiền ghi vào `DossierPayment` (B) và cột "Đã trả" từng việc tính từ B (`TreatmentDossierPayments.cs:30-58`); `TREATMENT-PAYMENT-V25.md:53` ghi "Đã trả chỉ lấy ConfirmedAllocated từ ledger, chưa có nguồn" (lỗi thời, V25.md:3 có chú thích thay thế) | Con số "Đã trả" ở đầu phiếu và ở từng dòng có thể lệch nhau; chưa đọc frontend nên chưa biết web đang gọi route nào |
| 2 | Ledger còn dùng hay không | `WORK-SERVICE-STATISTICS.md:62-67` nói FinanceCharge/PaymentAllocation "không còn áp dụng", nhưng `TreatmentFinancialSummaryPort` vẫn đọc ledger | Đường A và B cùng tồn tại, có nguy cơ cộng đôi trên màn Doanh thu |
| 3 | Ngày doanh thu | Màn Doanh thu dùng `CreatedAt` (đường B) và `ConfirmedAt` (đường A), không `PaidOn`; Thống kê công việc dùng ngày tạo phiếu | Hai màn không đối chiếu được theo cùng kỳ |
| 4 | Số ca trong danh mục | Tài liệu nói 42 ca nhóm A đến I, thực tế liệt kê 46 mục, nhóm A đến G; ca 17 có số liệu trộn (đã làm 10 triệu theo giá gốc, đã trả 27 triệu giá giảm) không kết luận được | Số liệu ca 17 không dùng được làm ví dụ chuẩn |
| 5 | Cọc là nghĩa vụ phải trả, hay thanh toán trước | `ca_dac_biet`: cọc luôn là Dư có (liability), cọc không hoàn chuyển sang thu nhập khác. `ghi_nhan_doanh_thu_thue`: cọc đảm bảo hợp đồng (BLDS) khác tiền thu trước/tạm ứng, khác về hóa đơn | Hai nguồn xem cọc là một loại; thực ra nên có hai loại giao dịch (xem quyết định O3) |
| 6 | Tài khoản cho tiền nhận trước | MISA: ghi TK 131 dư Có ([MISA](https://sme.misa.vn/215650/phan-biet-doanh-thu-chua-thuc-hien-va-khach-hang-tra-truoc-ma-ke-toan-can-biet/), chỉ qua snippet). Luật Minh Khuê, Anpha: TK 3387 cho khoản nhiều kỳ ([Minh Khuê](https://luatminhkhue.vn/hach-toan-tien-hoc-phi-thu-cho-nhieu-ky.aspx), [Anpha](https://ketoananpha.vn/hach-toan-doanh-thu-chua-thuc-hien.html)) | Chọn tài khoản là quyết định của kế toán; phần mềm chỉ cần lưu ba giá trị |
| 7 | Hiệu lực thông tư | Lạc Việt nói TT 99/2025 thay TT 200 và đổi tên TK 3387 nhưng trang tự mâu thuẫn, vẫn viện dẫn TT 200 và TT 133 mà không nêu TT 133 còn hiệu lực ([Lạc Việt](https://lacviet.vn/en/hach-toan-doanh-thu-chua-thuc-hien/)); danh mục ca còn viện dẫn TK 521 theo TT 200 trong khi có nguồn nói TT 99/2025 đổi cách hạch toán | Tài khoản cụ thể trong danh mục ca có thể đã lỗi thời |
| 8 | Văn bản hóa đơn | Danh mục ca viện dẫn ND 123/2020 cho điều chỉnh hóa đơn; ghi chú thuế nói ND 254/2026 thay thế ND 123/2020 và ND 70/2025 ([MISA AMIS](https://amis.misa.vn/279369/nghi-dinh-254-2026/)). Thông tư đi kèm lúc là TT 91/2026, lúc có nguồn nhắc TT 89/2026 | Số hiệu văn bản cần kiểm tra |
| 9 | Ngưỡng hộ kinh doanh | Sapo: miễn thuế dưới 500 triệu mỗi năm; hóa đơn điện tử bắt buộc từ 1 tỷ ([Sapo](https://www.sapo.vn/blog/nghi-dinh-68-ve-quan-ly-thue-doi-voi-ho-ca-nhan-kinh-doanh)). einvoice.vn: ngưỡng 500 triệu cố định đã bỏ, dự thảo nêu 1 tỷ, bài tự mâu thuẫn ([einvoice.vn](https://einvoice.vn/tin-tuc/thue-ho-kinh-doanh-2026)) | Không chốt được ngưỡng |
| 10 | Chế độ kế toán áp dụng | Ghi chú thuế suy luận phòng khám hộ kinh doanh thường chỉ dùng sổ đơn giản, không cần TK 131/3387/511; danh mục ca giả định các tài khoản này | Cấu trúc dữ liệu cần trung lập với hình thức pháp lý |
| 11 | Quy tắc phân bổ mặc định | DCMS hiện: UI chia FIFO "cũ nhất trước". Danh mục ca: FIFO theo ngày thực hiện, có ghi đè kèm lý do | Hai cách "cũ nhất" khác nhau (ngày tạo hay ngày thực hiện) |

## Thuế, hóa đơn, kế toán Việt Nam: độ tin cậy và việc phải hỏi kế toán

Toàn bộ phần này dựa trên bài tổng hợp thương mại và blog (MISA, Sapo, Lạc Việt, einvoice.vn, Forvis Mazars, Luật Minh Khuê và các trang tương tự), vì **thuvienphapluat.vn, vbpl.vn, mof.gov.vn chặn truy cập (HTTP 403) nên không có văn bản gốc nào được đọc**. Vì vậy, không có điểm nào dưới đây đạt mức "cao". Mức "trung bình" nghĩa là có từ hai nguồn thứ cấp độc lập trở lên nhất quán với nhau; mức "thấp" nghĩa là chỉ một nguồn, nguồn tự mâu thuẫn, hoặc chỉ thấy tiêu đề hay snippet.

| Điểm | Nội dung trong nguồn | Độ tin cậy | Ghi chú |
|---|---|---|---|
| Dịch vụ khám chữa bệnh không chịu thuế GTGT | Luật 48/2024/QH15 (ban hành 26/11/2024, hiệu lực 01/07/2025), Điều 5 khoản 10 theo bài tổng hợp; thuốc trong gói điều trị theo quy định Bộ Y tế cũng không chịu thuế ([bản dịch Luật](https://thuvienphapluat.vn/van-ban/Thue-Phi-Le-Phi/Law-48-2024-QH15-on-Value-added-Tax-637020.aspx), [hỏi đáp](https://thuvienphapluat.vn/hoi-dap-phap-luat/dich-vu-y-te-co-chiu-thue-gtgt-khong-138090058.html)) | Trung bình | Điều 5 gốc và nghị định hướng dẫn chưa đọc. Văn bản cũ (Luật 71/2014, TT 219/2013) cũng miễn dịch vụ y tế nhưng hiệu lực sau 01/07/2025 chưa xác nhận ([einvoice.vn](https://einvoice.vn/tin-tuc/nhung-dieu-can-biet-ve-thue-gtgt-dich-vu-y-te)) |
| Dịch vụ thuần thẩm mỹ có thể không được miễn | Công văn 5224/TCT-CS nhấn mạnh phân biệt điều trị y khoa với thẩm mỹ ([hethongphapluat](https://hethongphapluat.com/cong-van-5224-tct-cs-ve-thue-gia-tri-gia-tang-doi-voi-hoat-dong-chua-benh-do-tong-cuc-thue-ban-hanh.html)) | Thấp | Chỉ bản tóm tắt, ngày ban hành chưa rõ. Răng sứ, implant, niềng có chỉ định y khoa nhiều khả năng không chịu thuế, tẩy trắng và mặt dán sứ không bệnh lý có rủi ro: đây là **[SUY]**. Bán riêng vật tư/thuốc ngoài gói điều trị có thể chịu thuế (**[SUY]**) |
| Thời điểm lập hóa đơn dịch vụ: khi hoàn thành, hoặc khi thu tiền nếu thu trước/trong quá trình cung cấp | ND 70/2025 (hiệu lực 01/06/2025) sửa Điều 9 ND 123/2020 ([Thuvienphapluat hỏi đáp](https://thuvienphapluat.vn/ma-so-thue/phap-luat-thue/thoi-diem-xuat-hoa-don-theo-nghi-dinh-70-chi-tiet-248654-215343.html), [PwC](https://www.pwc.com/vn/vn/publications/2025/250416-changes-to-invoicing-regulations.pdf), cả hai chỉ thấy tiêu đề hoặc snippet) | Trung bình | Nhất quán qua nhiều nguồn thứ cấp nhưng chưa có nguyên văn |
| ND 254/2026/NĐ-CP (ban hành 30/06/2026, hiệu lực 01/07/2026) thay thế ND 123/2020 và ND 70/2025 | [Forvis Mazars](https://www.forvismazars.com/vn/vi/n-ph-m-s-ki-n/c-p-nh-t-v-vi-t-nam/b-n-tin-thong-bao-chuyen-mon/b-n-tin-v-thu-va-phap-ly/ngh-d-nh-254-va-thong-t-91) (không nêu văn bản bị thay thế); [MISA AMIS](https://amis.misa.vn/279369/nghi-dinh-254-2026/) (kết quả tìm kiếm) | Trung bình-thấp | Thông tư đi kèm có thể là TT 91/2026 hoặc TT 89/2026: số hiệu cần kiểm tra |
| Tiền đặt cọc đảm bảo thực hiện hợp đồng dịch vụ (theo BLDS) chưa phải lập hóa đơn; chỉ áp dụng cho dịch vụ | Khoản 2 Điều 9 ND 254/2026 theo [MISA AMIS](https://amis.misa.vn/39192/thoi-diem-xuat-hoa-don/) (bài có số khoản không nhất quán); [Thời báo Tài chính VN](https://thoibaotaichinhvietnam.vn/phan-biet-tien-dat-coc-va-tien-thu-truoc-bi-quyet-tranh-rui-ro-do-bi-lap-hoa-don-sai-thoi-diem-202488.html), [Thuvienphapluat](https://thuvienphapluat.vn/ma-so-thue/phap-luat-thue/chinh-thuc-thu-tien-dat-coc-tu-dich-vu-khong-phai-lap-hoa-don-tu-01072026-227651.html) (chỉ tiêu đề) | Thấp | Nếu "cọc 30% kế hoạch" thực chất là thanh toán trước một phần giá dịch vụ thì rủi ro bị coi là tiền thu trước và phải xuất hóa đơn ngay (**[SUY]**) |
| Nghiệm thu/bàn giao từng giai đoạn thì lập hóa đơn từng lần | Khoản 3 Điều 9 theo MISA AMIS | Thấp | Số khoản trong bài không nhất quán |
| Trả góp: mỗi lần thu là thu trong quá trình cung cấp, nên cần hóa đơn khi thu; hóa đơn theo tiền thu và theo khối lượng hoàn thành phải đối soát để không xuất trùng | Chỉ là suy luận của người ghi chú từ các điểm trên | Thấp | **[SUY]**, không có nguồn trực tiếp |
| Hóa đơn điều chỉnh, thay thế, hủy khi hoàn tiền hoặc hủy kế hoạch | Chỉ là kiến thức ghi nhớ (viện dẫn ND 123/2020) | Rất thấp | Chưa kiểm chứng, chưa rõ còn đúng sau ND 254/2026 |
| Hạch toán tiền nhận trước: TK 131 dư Có, hoặc TK 3387 / "doanh thu chờ phân bổ" | Xem mâu thuẫn #6 | Thấp | MISA chỉ qua snippet. Chọn tài khoản là việc của kế toán |
| TT 99/2025/TT-BTC (27/10/2025, hiệu lực 01/01/2026) thay TT 200/2014 và đổi tên TK 3387 | [Lạc Việt](https://lacviet.vn/en/hach-toan-doanh-thu-chua-thuc-hien/) (trang tự mâu thuẫn) | Thấp | Hiệu lực của TT 133/2016 sau 2026 chưa xác nhận |
| Giảm trừ doanh thu qua TK 521 (chiết khấu thương mại, hàng bán trả lại, giảm giá) | AZTAX, Ketoananpha, MISA SME ([MISA SME](https://sme.misa.vn/63854/huong-dan-hach-toan-tai-khoan-521-ke-toan-cac-khoan-giam-tru-doanh-thu/)); các blog bất nhất về số hiệu TK cấp 2 | Trung bình-thấp | TT 99/2025 có thể đã đổi cách hạch toán |
| Hộ kinh doanh: bỏ thuế khoán và lệ phí môn bài từ 01/01/2026, tự kê khai (NQ 198/2025/QH15); ND 68/2026/NĐ-CP (05/03/2026) hướng dẫn cách tính | [einvoice.vn](https://einvoice.vn/tin-tuc/thue-ho-kinh-doanh-2026) | Trung bình-thấp | Tên "Nghị định 68/2026" khớp theo các nguồn thứ cấp |
| Ngưỡng miễn thuế hộ kinh doanh (500 triệu hay khoảng 1 tỷ) và ngưỡng hóa đơn điện tử | Xung đột giữa Sapo và einvoice.vn | Rất thấp | Phải kế toán xác nhận |
| Thời điểm ghi nhận doanh thu tính thuế của hộ kinh doanh | Các nguồn không nêu rõ | Rất thấp | Giả định thận trọng (**[SUY]**): tiền thu trước có hóa đơn nên có thể đã tính vào doanh thu kê khai |
| IFRS 15/ASC 606 coi tiền nhận trước là nghĩa vụ hợp đồng, doanh thu ghi khi thực hiện nghĩa vụ | **Không có nguồn trong phiên nghiên cứu**; là kiến thức nền của người ghi chú | Không đánh giá được | Chỉ dùng để định hướng, không trích dẫn như sự thật đã kiểm chứng |

**Danh sách việc phải xác nhận với kế toán, đối chiếu văn bản gốc, trước khi chốt spec:**
1. Hiệu lực của TT 133/2016 sau TT 99/2025, phạm vi áp dụng của TT 99/2025 với doanh nghiệp nhỏ, và tài khoản dùng cho tiền nhận trước (131 dư Có hay 3387 / "doanh thu chờ phân bổ").
2. Nguyên văn Điều 9 ND 254/2026: ranh giới "đặt cọc" và "thu trước", hóa đơn theo từng giai đoạn nghiệm thu, và số hiệu thông tư hướng dẫn (91 hay 89/2026).
3. Quy trình hóa đơn điều chỉnh, thay thế hoặc hủy khi hoàn tiền, hủy kế hoạch, hoặc khi hoàn gói đã giảm giá.
4. Xử lý kế toán cho cọc không hoàn, phí phạt, phí trừ khi hoàn và dư có hết hạn: có ghi nhận thu nhập khác không, và có bị xem là bất lợi cho người tiêu dùng không. Cần cả tư vấn pháp lý, vì không có nguồn Việt Nam.
5. Ngưỡng doanh thu miễn thuế của hộ kinh doanh (500 triệu hay khoảng 1 tỷ), ngưỡng hóa đơn điện tử, và thời điểm ghi nhận doanh thu tính thuế (thu tiền hay hoàn thành).
6. Phạm vi miễn GTGT cho từng nhóm dịch vụ trong danh mục (đặc biệt tẩy trắng và dán sứ thẩm mỹ) và vật tư, sản phẩm bán riêng.
7. Phí cổng thanh toán và phí công ty tài chính: ghi chi phí tài chính hay giảm trừ doanh thu.
8. Quy định thu ngoại tệ tại Việt Nam (chưa tra cứu).
9. Hình thức pháp lý của phòng khám (hộ kinh doanh hay công ty): quyết định hệ thống tài khoản nào phải áp dụng.

## Quyết định cần chủ phòng khám chốt, kèm phương án đề xuất

Các quyết định này là chính sách kinh doanh, không phải kỹ thuật; không có chuẩn ngành tìm được, nên đề xuất bên dưới là **ý kiến của người tổng hợp**, dựa trên thực hành quốc tế và logic nội tại của các công thức.

| # | Câu hỏi | Phương án | Đề xuất | Lý do |
|---|---|---|---|---|
| O1 | Chỉ số nào làm con số chính (headline) trên dashboard "Doanh thu" | (a) tiền thu (hiện tại); (b) doanh thu thực hiện; (c) hiển thị cả hai, gọi tên rõ | (c): đổi nhãn con số hiện tại thành "Tiền thu", thêm "Doanh thu thực hiện", "Nhận trước", "Công nợ" | Hiện nhãn "Doanh thu" dễ gây hiểu nhầm; không có cọc thì hai con số gần nhau, có cọc thì lệch rất xa |
| O2 | Cọc có hoàn lại không, và hạn hiệu lực | Hoàn đủ; hoàn có khấu trừ chi phí đã phát sinh; giữ làm tín dụng; không hoàn một phần (ví dụ 25% để giữ lịch) | Cấu hình theo từng loại dịch vụ, ghi vào văn bản đồng ý của bệnh nhân; mặc định "hoàn trừ chi phí đã phát sinh có chứng từ" | Thực hành nước ngoài đa dạng ([Eastcote Lane Dental](https://eastcotelanedental.co.uk/?p=4765), [Wyndham House](https://www.wyndhamhousedental.com/?p=606)); chưa có quy định Việt Nam. **Cần tham vấn pháp lý** |
| O3 | Tách "đặt cọc" khỏi "thanh toán trước" | Một loại; hai loại | Hai loại giao dịch riêng, thêm "thanh toán cho đợt điều trị" | Nguồn thuế phân biệt cọc đảm bảo hợp đồng với tiền thu trước về hóa đơn (độ tin cậy thấp); tách sớm rẻ hơn tách sau |
| O4 | Cọc cấn trừ vào đâu | Giai đoạn đầu; giai đoạn cuối; chia đều theo giá trị | Chia đều hoặc theo chính sách hợp đồng; nếu không rõ thì giai đoạn đầu (cho nhận trước giảm sớm) | Ảnh hưởng dòng tiền và số nhận trước (xem kịch bản 2) |
| O5 | Cọc % tính trên giá nào, có tự cập nhật khi kế hoạch đổi giá | Tính tự động; chỉ cảnh báo | Cảnh báo thiếu cọc, không tự cập nhật | Ghi chú cho rằng cọc không tự đổi, chỉ so sánh |
| O6 | Trả quá | Chặn; giữ làm tín dụng | Cho giữ làm tín dụng chưa phân bổ, có báo cáo tuổi | Hiện bị chặn nên phải tính tiền thừa ở ngoài hệ thống; nguồn quốc tế đều cho phép |
| O7 | Hoàn tiền: ai duyệt, kênh, giới hạn | Chỉ chủ phòng khám; quản lý; lễ tân | Quản lý duyệt hoàn; chủ phòng khám duyệt cọc không hoàn và dư có hết hạn; hoàn về đúng kênh và đúng người đã trả | Bảng quyền đề xuất trong danh mục ca (suy luận) |
| O8 | Chiết khấu trả đủ: khi bệnh nhân bỏ giữa chừng thì tính lại theo giá thường hay giữ giá giảm | Tính lại theo giá thường; hoàn theo tỷ lệ phân bổ | Chốt trước trong chính sách; mặc định hoàn theo tỷ lệ phân bổ (rút 6 triệu gốc trong gói giảm 10% thì hoàn 5,4 triệu) | Tránh tranh chấp; ca 16 và 17 |
| O9 | Cơ sở doanh thu cho niềng răng | Theo mốc (khởi đầu, từng lần tái khám); chia đều theo thời gian | Theo mốc, nhất quán | Ghi chú ca 42 yêu cầu chọn một cách |
| O10 | Hoa hồng bác sĩ: theo thu hay theo thực hiện, có thu hồi khi hoàn | Theo thu; theo thực hiện | Chọn một cách và áp dụng nhất quán; nếu theo thu thì có quy tắc thu hồi | Ca 40 |
| O11 | Ngày doanh thu của một khoản thu | `PaidOn` (ngày bệnh nhân trả); `CreatedAt` (ngày nhập); `ConfirmedAt` (ngày xác nhận chuyển khoản) | Một quy tắc duy nhất cho toàn hệ thống; nghiêng về ngày tiền thực về | Hiện đường A và B dùng hai mốc khác nhau |
| O12 | Dư có hết hạn, bệnh nhân vắng lâu hoặc mất | Giữ vô hạn; hết hạn sau 12 tháng và chuyển thu nhập khác; hoàn cho người thừa kế | Không tự chuyển thu nhập; chỉ nhắc theo mốc 6, 12 tháng cho đến khi có tư vấn pháp lý | Rủi ro pháp lý, không có nguồn Việt Nam |
| O13 | Trả góp bên thứ ba: ghi như phương thức thanh toán hay công nợ bệnh nhân | Phương thức thanh toán, kèm khoản phải thu bên thứ ba | Phương thức thanh toán, kèm phải thu từ đối tác và phí | Phòng khám nhận tiền từ bên thứ ba, không phải từ bệnh nhân |
| O14 | Một nguồn "Đã trả" duy nhất | Hợp nhất A và B; giữ hai nguồn và đối soát | Hợp nhất | Mâu thuẫn #1 và #2 |

## Một spec tương lai cần định nghĩa những gì, theo thứ tự ưu tiên

Phần này chỉ liệt kê nội dung spec phải quyết định, không phải thiết kế mã. Thứ tự theo mức độ chặn và rủi ro **theo đánh giá của người tổng hợp**.

**Ưu tiên 1 – nền tảng, chặn mọi thứ phía sau**
1. Từ điển chỉ số: năm chỉ số và công thức ở phần trên, nhãn trên giao diện, quy tắc làm tròn, mẫu số của tỷ lệ thu, và quy tắc kiểm tra bất biến (Dư có không âm, đối soát toàn phòng khám).
2. Một nguồn duy nhất cho "tiền đã thu" và "Đã trả": quyết định số phận của đường A và B, xử lý dữ liệu đã có và chặn thu trùng.
3. Thực thể "khoản thu chưa phân bổ" (loại: đặt cọc, thanh toán trước, thanh toán đợt, tín dụng) gắn với bệnh nhân hoặc kế hoạch, người trả khác bệnh nhân, kênh thu, người thu; quy tắc phân bổ vào việc đã làm (mặc định và ghi đè có lý do); quy tắc về cọc cấn trừ vào đâu.
4. Quy tắc ngày: một định nghĩa ngày cho tiền thu, doanh thu thực hiện, và báo cáo.

**Ưu tiên 2 – các luồng gây tổn thất nếu thiếu**
5. Hoàn tiền và đảo ngược: điều kiện, quyền duyệt, kênh hoàn, giới hạn tổng hoàn không vượt payment gốc, khấu trừ có chứng từ, lý do (khách bỏ, phòng khám hủy), hủy phiếu thay cho xóa, khóa kỳ. Kèm xử lý PayOS `Review`.
6. Đổi kế hoạch sau thu tiền: thêm, bớt, đổi vật liệu và số lượng; kết quả chênh lệch (phải thu thêm hoặc dư cần hoàn); giá snapshot và hạn hiệu lực; hoàn hạng mục đã giảm theo tỷ lệ; consumer cho yêu cầu điều chỉnh (nay chưa có).
7. Dừng điều trị: quy trình từ `StoppedIncomplete` đến hoàn hoặc giữ tín dụng, và cách xử lý nợ còn lại.
8. Chính sách và cấu hình của phòng khám: cọc hoàn hay không, hạn hiệu lực, chiết khấu trả đủ và điều kiện, ngưỡng nợ, bảng quyền theo vai trò.

**Ưu tiên 3 – nghiệp vụ mở rộng**
9. Lịch trả góp và theo dõi quá hạn; mốc thanh toán gắn tiến độ (niềng theo tháng, implant theo bước); nhắc nợ.
10. Trả góp bên thứ ba và bảo hiểm hoặc công ty đài thọ: phải thu bên thứ ba, phí, đối soát.
11. Giảm giá hợp nhất giữa kế hoạch và dòng sheet; voucher, giới thiệu, điểm tích lũy; luồng duyệt xuyên suốt.
12. Báo cáo: doanh thu thực hiện theo kỳ, theo bác sĩ và dịch vụ, dư có theo tuổi, "làm chưa thu" và "thu chưa làm" cuối tháng, đối soát chi nhánh, xuất Excel.
13. Hoa hồng và chi phí: quy tắc theo thu hoặc thực hiện, thu hồi khi hoàn; chi phí labo, vật tư gắn thủ thuật; bảo hành và biến chứng.
14. Đa chi nhánh: thu một nơi, làm một nơi; công nợ nội bộ.

**Ưu tiên 4 – tuân thủ, chờ kế toán xác nhận**
15. Hóa đơn: liên kết hóa đơn với thu và hoàn, loại giao dịch cọc so với thu trước, điều chỉnh khi hoàn, đối soát hóa đơn theo thu và theo hoàn thành.
16. Chính sách thuế: VAT theo nhóm dịch vụ, ghi nhận theo hình thức pháp lý của phòng khám. Nên để mô hình dữ liệu trung lập, chỉ lưu tiền nhận, doanh thu thực hiện, số dư nhận trước (nguyên tắc của ghi chú thuế).
17. Kiểm soát vận hành: số phiếu liên tục, chốt két cuối ngày, đối soát cổng thanh toán hàng ngày.

## Kết luận

Điểm đáng chú ý nhất không nằm ở một tính năng thiếu, mà ở một **khoảng hở khái niệm**. DCMS dùng chữ "doanh thu" cho một con số thực ra là tiền đã thu, và hiện chưa có chỗ để một đồng tiền tồn tại mà chưa gắn với việc đã làm. Cho tới khi có cọc, trả trước, trả góp có lịch và hoàn tiền, khoảng hở này vô hại vì hai con số trùng nhau. Khi các ca đó xuất hiện, hai con số sẽ lệch, và nếu dashboard giữ nguyên nhãn cũ thì chủ phòng khám sẽ đọc tiền cọc là doanh thu. Vì vậy việc rẻ nhất và nên làm đầu tiên là chốt từ điển chỉ số và nhãn hiển thị (quyết định O1) trước khi bàn đến tính năng. Hai mâu thuẫn nội bộ cũng cần xử lý sớm: "Đã trả" có hai nguồn, và hai màn báo cáo dùng hai mốc ngày khác nhau.

Về độ chắc chắn, phần lớn các khuyến nghị về mô hình dữ liệu (tiền chưa phân bổ, phân bổ, hoàn có audit) có nền tảng tốt từ cách Open Dental hoạt động, nhưng phần pháp lý và thuế thì chưa có cơ sở vững: nguồn chỉ là bài thứ cấp, nhiều điểm tự mâu thuẫn hoặc chỉ thấy tiêu đề, và không có nguồn Việt Nam nào về hoàn cọc hay dư có hết hạn. Cách xử lý hợp lý là tách hai việc: thiết kế dữ liệu trung lập về thuế (chỉ lưu tiền nhận, doanh thu thực hiện, số dư nhận trước) để triển khai được ngay, và để kế toán cùng tư vấn pháp lý chốt các điểm đã liệt kê trước khi spec đụng đến hóa đơn, thuế, hoặc việc chuyển tiền cọc thành thu nhập. Số liệu trong các ví dụ chỉ minh họa cơ chế; mọi tỷ lệ cọc, mức phí và ngưỡng thực tế phải do chủ phòng khám quyết định.
