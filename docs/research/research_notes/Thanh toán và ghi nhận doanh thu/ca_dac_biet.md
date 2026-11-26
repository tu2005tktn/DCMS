# Ca đặc biệt và edge case của thanh toán bệnh nhân tại phòng khám nha khoa nhỏ/vừa

Ký hiệu dùng trong mọi ca: **P** = tiền đã thu (cash collected, ròng sau hoàn); **R** = doanh thu thực hiện (performed, giá sau giảm); **Nợ** = max(R − P, 0) (công nợ/receivable); **Dư có** = max(P − R, 0) (số dư trả trước/credit, khoản phải trả cho BN, chưa là doanh thu). Mọi con số VND là ví dụ minh họa do người viết tự dựng, không phải số liệu nguồn.

Lưu ý phương pháp: web search hầu như không trả về nguồn tiếng Việt chuyên ngành về hoàn tiền nha khoa; các thông lệ phòng khám VN bên dưới là suy luận, không có nguồn trích dẫn trực tiếp. Phần có nguồn chủ yếu là Open Dental, ADA và bài kế toán TK 521.

## 1. Nguyên tắc nền (cơ chế phần mềm quản lý nha khoa và kế toán)

### Takeaway
Phần mềm nha khoa chuẩn tách "tiền trả trước/unearned" khỏi doanh thu cho đến khi thủ thuật hoàn thành, cho phép phân bổ về sau, và cảnh báo số dư âm; kế toán VN coi chiết khấu/trả lại/giảm giá là giảm trừ doanh thu (TK 521).

### Cited Findings
- Open Dental: thanh toán có paysplit loại "unearned" là prepayment, thường là tiền cho khoản phí tương lai hoặc khoản trả thừa cần hoàn; tổng unearned hiển thị ở Account Module. Prepay không phân bổ tạo paysplit unallocated; dùng "Allocate Unearned Income" để chuyển vào khoản phí sau. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)
- Open Dental cho phép phân bổ prepayment vào từng thủ thuật trong treatment plan (cần bật tùy chọn trong Allocations Setup), mỗi thủ thuật có paysplit ẩn; khi thủ thuật hoàn thành, hệ thống tự chuyển thu nhập vào tài khoản BN. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)
- Open Dental có loại unearned "ẩn" không ảnh hưởng số dư BN và báo cáo, có báo cáo riêng cho unearned và hidden splits. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)
- Open Dental: "never advisable to create a negative unearned balance"; sửa sai bằng Income Transfer (payment $0, tách positive/negative split, tổng bằng 0) rồi chạy Income Transfer Manager kiểm tra. Hoàn tiền phải tạo paysplit đúng, nếu không gây unearned âm. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)
- Open Dental blog: nếu phòng khám cho phép hoàn prepayment thì chuột phải vào payment gốc ở Account Module chọn Refund và chọn loại thanh toán; hoàn thẻ có quy trình riêng (theo tóm tắt kết quả tìm kiếm, chưa fetch trang). — [Open Dental blog: prepayments](https://opendental.blog/best-practices-for-accepting-prepayments)
- ADA: chính sách tài chính cần quyết định hoàn đầy đủ/một phần/không hoàn khi BN đã trả một phần nhưng bỏ dở; số dư tín dụng chưa dùng theo chính sách hoàn tiền; phí hủy hẹn (mức phí, thời hạn báo trước, áp dụng mỗi lần hay sau nhiều lần); cọc tối thiểu theo phần BN phải trả ước tính; BN có thể trả trước toàn bộ; giảm giá/chiết khấu cho trả trước và có áp dụng cho thẻ không; trả góp nội bộ/2-3 đợt hoặc tài chính bên thứ ba; mức tối thiểu để đủ điều kiện trả góp. — [ADA](https://www.ada.org/resources/practice/practice-management/considerations-in-developing-a-financial-policy)
- TK 521 (TT 200): gồm chiết khấu thương mại, hàng bán bị trả lại, giảm giá hàng bán; không có số dư cuối kỳ, cuối kỳ kết chuyển Nợ 511 / Có 521. Hàng bán bị trả lại: Nợ 521x, Nợ 3331, Có 111/112/131. Một nguồn cho biết TT 99/2025/TT-BTC đã thay đổi cách hạch toán; các blog bất nhất về số hiệu TK cấp 2 (5211/5212/5213), cần đối chiếu văn bản gốc. — [AZTAX](https://aztax.com.vn/tai-khoan-521), [Ketoananpha](https://ketoananpha.vn/tai-khoan-521.html), [MISA SME](https://sme.misa.vn/63854/huong-dan-hach-toan-tai-khoan-521-ke-toan-cac-khoan-giam-tru-doanh-thu/), [Kế toán Lê Ánh về TT 99](https://ketoanleanh.edu.vn/kinh-nghiem-ke-toan/tai-khoan-521-cac-khoan-giam-tru-doanh-thu-theo-thong-tu-99-2025-tt-btc.html)
- Hóa đơn điện tử VN: điều chỉnh/thay thế/hủy theo Nghị định 123/2020/NĐ-CP — chỉ là kiến thức ghi nhớ của người tra cứu, chưa kiểm chứng bằng nguồn trong phiên này.

### Inferences
- Mô hình dữ liệu đề xuất: sổ cái BN gồm các dòng (a) Charge/Performed (theo thủ thuật, giá snapshot), (b) Adjustment/Discount (gắn thủ thuật hoặc cấp kế hoạch), (c) Payment (tiền vào, có kênh), (d) Allocation (liên kết payment-thủ thuật, có thể trống = unallocated), (e) Refund (tiền ra, gắn payment gốc). Dư có = tổng payment − tổng allocation đã dùng cho thủ thuật hoàn thành. Số dư không bao giờ âm ở phía "unearned".
- Dư có của BN là nghĩa vụ phải trả (liability), không phải doanh thu; về kế toán VN thường thể hiện bằng dư Có TK 131 hoặc TK 3387/"người mua trả tiền trước" tùy chính sách (chưa có nguồn xác nhận trong phiên này).
- Báo cáo bắt buộc: thu theo kỳ (cash basis), doanh thu thực hiện theo kỳ (accrual), công nợ BN, dư có BN, chênh lệch "thu chưa làm" và "làm chưa thu" cuối tháng.

### Gaps
- Không tìm thấy văn bản VN nói thẳng về hoàn tiền dịch vụ nha khoa bỏ dở; cần tư vấn pháp lý/Luật Bảo vệ quyền lợi người tiêu dùng.
- Chưa xác minh cách hạch toán tiền nhận trước (TK 131 dư có vs 3387) và ảnh hưởng của TT 99/2025.

## 2. Danh mục ca (catalog)

### Takeaway
Dưới đây là 42 ca, nhóm A đến I. Mỗi ca ghi: mô tả; cách xử lý thường gặp; tác động (Thu / R / Nợ / Dư có); dữ liệu cần ghi; biện pháp kiểm soát. Ví dụ dùng kế hoạch mẫu: trám 2 răng 600.000 mỗi răng, nhổ khôn 1.500.000, 1 implant 18.000.000 trọn gói, v.v.

### Cited Findings
- Các thực hành nền (prepay, allocate, refund, income transfer, chính sách hủy/hoàn/chiết khấu trả trước/trả góp tài chính bên thứ ba) có nguồn ở mục 1; các ca bên dưới dựa trên đó và được suy luận cho bối cảnh VN. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html), [ADA](https://www.ada.org/resources/practice/practice-management/considerations-in-developing-a-financial-policy)

### Inferences (catalog)

**A. Thời điểm thanh toán**

1. **Trả trước toàn bộ kế hoạch.** BN kế hoạch 30.000.000, trả đủ ngày 1, làm 0 đồng. Xử lý: ghi nhận tiền trả trước, phát hành phiếu thu, doanh thu ghi dần theo thủ thuật hoàn thành. Thu +30.000.000; R 0; Nợ 0; Dư có 30.000.000. Sau khi hoàn thành 12.000.000: R 12.000.000, Dư có 18.000.000. Ghi: kế hoạch, số tiền, kênh, người thu, điều khoản hoàn. Kiểm soát: phiếu thu có mã, xác nhận BN đã đọc chính sách hoàn, báo cáo dư có hằng tháng.
2. **Đặt cọc số tiền cố định.** Cọc 2.000.000 cho implant 18.000.000. Thu +2.000.000; R 0; Dư có 2.000.000. Ghi: loại "cọc", cọc hoàn lại hay không, hạn hiệu lực. Kiểm soát: điều khoản cọc ký/xác nhận, không dùng cọc cho kế hoạch khác nếu không có duyệt.
3. **Đặt cọc theo %.** Cọc 30% của 18.000.000 = 5.400.000; hệ thống tính từ giá đã duyệt (làm tròn theo quy ước ví dụ 1.000đ). Sau đó kế hoạch đổi giá thì cọc không tự đổi, chỉ so sánh. Ghi: % và cơ sở tính. Kiểm soát: cảnh báo khi tổng đã thu < % yêu cầu trước khi bắt đầu.
4. **Trả nhiều hơn phần đã làm (tín dụng).** Đã làm 4.000.000, BN trả 6.000.000 dịp khác. Thu +6.000.000; R 4.000.000; Dư có 2.000.000. Dư có tự trừ vào lần làm kế tiếp. Ghi: không để âm công nợ; hiển thị "còn dư". Kiểm soát: báo cáo dư có theo tuổi.
5. **Trả ít hơn phần đã làm (công nợ).** Đã làm 4.000.000, trả 3.000.000. Thu +3.000.000; R 4.000.000; Nợ 1.000.000. Ghi: ngày hẹn trả, người cam kết. Kiểm soát: trần nợ theo BN, cảnh báo trước khi làm tiếp khi nợ vượt ngưỡng, duyệt của quản lý.
6. **Thu tiền trước khi kế hoạch được xác nhận.** BN đưa 1.000.000 khi mới tư vấn, chưa có kế hoạch duyệt. Ghi như "tiền chờ" (unallocated) gắn BN, không gắn kế hoạch. Thu +1.000.000; R 0; Dư có 1.000.000. Kiểm soát: không cho gắn vào thủ thuật cho đến khi kế hoạch được duyệt; hết hạn thì nhắc.
7. **Thu tiền không phân bổ.** Khoản thu chưa gắn thủ thuật cụ thể. Xử lý: để unallocated, phân bổ khi làm (tham chiếu Open Dental). Tác động như ca 4 (dư có). Ghi: người phân bổ, thời điểm. Kiểm soát: báo cáo unallocated cuối ngày/tháng; không để tồn không rõ.
8. **Phân bổ theo thủ thuật cụ thể so với FIFO.** BN trả 2.000.000, có hai thủ thuật chưa trả 1.500.000 (nhổ) và 1.200.000 (trám). Chọn: BN chỉ định trả nhổ; hoặc FIFO theo ngày thực hiện. Tổng Thu +2.000.000, Nợ còn 700.000 ở dù cách nào, nhưng thủ thuật nào còn nợ khác nhau (ảnh hưởng hoa hồng, bảo hành, hoàn). Ghi: quy tắc mặc định (FIFO) + ghi đè có lý do. Kiểm soát: log thay đổi phân bổ.

**B. Giá và điều chỉnh kế hoạch**

9. **Giảm giá khi trả đủ.** Kế hoạch 30.000.000, giảm 5% nếu trả đủ trước = 1.500.000. R tổng 28.500.000. Thu +28.500.000. Ghi: lý do giảm, phân bổ giảm theo thủ thuật. Kiểm soát: mức giảm tối đa theo vai trò; nếu BN sau đó hủy một phần, giảm có thể phải tính lại (ca 16, 17).
10. **Khuyến mãi/voucher/giới thiệu/điểm tích lũy.** Voucher 500.000, điểm 200.000. Ghi như chiết khấu có mã nguồn (voucher, referral, loyalty) để theo dõi chi phí marketing; không phải tiền thu. R giảm 700.000 so với giá niêm yết. Kiểm soát: voucher dùng một lần, hạn, chống dùng trùng; điểm có sổ riêng.
11. **Phê duyệt giảm giá vượt ngưỡng.** Bác sĩ tự giảm 20% (6.000.000 trên 30.000.000) trong khi ngưỡng lễ tân 5%, quản lý 10%. Chờ duyệt; chưa duyệt thì chưa áp dụng. Kiểm soát: workflow duyệt, log người đề xuất/người duyệt/lý do.
12. **Sửa kế hoạch sau khi đã thu: thêm hạng mục.** Thêm 1 trám 600.000 vào kế hoạch đã trả 30.000.000. Giá kế hoạch 30.600.000; Thu không đổi; thêm 600.000 phải thu (Nợ tiềm năng). Ghi: phiên bản kế hoạch, BN xác nhận. Kiểm soát: kế hoạch có version; thay đổi cần BN xác nhận (chữ ký/OTP).
13. **Bớt hạng mục chưa làm.** Bỏ 1 răng chưa trám (600.000). Kế hoạch giảm 600.000; nếu đã thu đủ thì phát sinh Dư có 600.000, hoàn hoặc giữ làm tín dụng. Kiểm soát: không xóa cứng, đánh dấu hủy kèm lý do.
14. **Đổi số lượng răng.** Bọc sứ 6 răng chuyển 4 răng, đơn giá 3.000.000: giảm 6.000.000. Xử lý như ca 13. Nếu đã làm 2 răng, R chỉ tính 2 răng. Ghi lại giá snapshot theo răng.
15. **Nâng/hạ vật liệu (sứ kim loại sang zirconia, hãng implant).** Răng sứ 2.500.000 sang zirconia 4.000.000 trước khi làm: chênh +1.500.000 phải thu; hạ cấp thì chênh âm = dư có/hoàn. Nếu đã gắn răng tạm hoặc đã đặt hàng labo, ghi chi phí labo đã phát sinh (ca 22). Ghi: giá cũ/mới, lý do, bác sĩ. Kiểm soát: duyệt thay đổi, BN xác nhận.
16. **Hoàn lại phần chiết khấu khi rút hạng mục.** KH có giảm 10% cho gói trả đủ 30.000.000 (nhận 27.000.000), sau đó hủy hạng mục giá gốc 6.000.000. Quy tắc phân bổ tỷ lệ: giá hoàn = 6.000.000 × 90% = 5.400.000 (không hoàn 6.000.000 gốc). Kiểm soát: lưu tỷ lệ giảm phân bổ trên từng dòng.
17. **Giảm giá có điều kiện bị mất điều kiện.** Giảm vì trả đủ nhưng BN hủy giữa chừng: có thể tính lại theo bảng giá thường cho phần đã làm. Đã làm 10.000.000 theo giá gốc, đã trả 27.000.000 giá giảm ... phải thống nhất quy tắc trước. Ghi quy tắc trong chính sách; kiểm soát: cần duyệt ngoại lệ.
18. **Bảng giá đổi sau khi đã đồng ý.** Giá trám tăng 600.000 lên 700.000 sau khi kế hoạch duyệt. Dùng giá snapshot lúc duyệt (khuyến nghị có hạn hiệu lực, ví dụ 90 ngày); thủ thuật mới thêm dùng giá mới. Kiểm soát: lưu giá tại thời điểm duyệt trên từng dòng, báo cáo kế hoạch quá hạn giá.

**C. Dừng điều trị**

19. **BN bỏ dở, hoàn phần chưa thực hiện.** Trả 30.000.000, đã làm 12.000.000, dư có 18.000.000; hoàn 18.000.000 theo chính sách. Thu ròng 12.000.000; R 12.000.000; Dư có 0. Ghi: yêu cầu hoàn, người duyệt, kênh. Kiểm soát: duyệt quản lý/chủ phòng khám, phiếu chi có chữ ký BN (ADA nêu cần quy định rõ hoàn đầy đủ/một phần/không, xem mục 1).
20. **Cọc không hoàn.** Cọc 2.000.000 không hoàn lại theo điều khoản, BN hủy. Chuyển từ dư có sang doanh thu khác/thu nhập từ phạt cọc 2.000.000 (hoặc bù chi phí đã phát sinh). Ghi: điều khoản BN đã chấp nhận, ngày chuyển. Kiểm soát: chỉ quản lý được chuyển cọc thành thu nhập, log đầy đủ; sai điều khoản có thể gây tranh chấp người tiêu dùng.
21. **Trừ chi phí phát sinh khi hoàn (vật tư/labo/phí hủy).** Dư có 18.000.000, trừ vật tư implant đã mở 3.000.000 và phí labo 1.500.000 thì hoàn 13.500.000. Ghi các khoản trừ có chứng từ. Tác động: R tăng 4.500.000 (phí) hoặc ghi vào thu nhập khác theo chính sách. Kiểm soát: chứng từ chi phí, BN ký xác nhận.
22. **Phòng khám hủy điều trị.** Phòng khám không thể tiếp tục (bác sĩ nghỉ, thiết bị): hoàn toàn bộ dư có, không tính phí. Dư có 0 sau hoàn. Kiểm soát: lý do "phòng khám hủy" riêng để báo cáo.
23. **Bảo hành, làm lại.** Mão sứ bị bể trong thời hạn bảo hành, làm lại miễn phí: R không đổi, tạo thủ thuật "bảo hành" giá 0 nhưng ghi chi phí labo/vật tư (chi phí không có doanh thu). Nếu hết hạn bảo hành, tính phí. Ghi: thủ thuật gốc, ngày, điều kiện bảo hành, lỗi do ai. Kiểm soát: bảo hành cần bác sĩ/quản lý xác nhận; chặn khi quá hạn trừ ngoại lệ.
24. **Biến chứng điều trị.** Nhiễm trùng sau nhổ cần thuốc và tái khám: miễn phí hay tính phí tùy chính sách; nếu miễn thì giá 0 và gắn thủ thuật gốc. Kiểm soát: mã lý do, thống kê tỷ lệ biến chứng.
25. **BN chuyển sang phòng khám khác.** Giống ca 19, kèm xuất hồ sơ/phim; có thể tính phí sao hồ sơ. Kiểm soát: chứng từ hoàn, xuất hồ sơ có log.
26. **BN mất hoặc vắng lâu.** BN mất: hoàn cho người thừa kế theo giấy tờ hợp lệ; vắng lâu: giữ dư có, nhắc theo mốc (ví dụ 6, 12 tháng). Kiểm soát: không tự ghi thu nhập khi hết hạn nếu chưa có điều khoản rõ; tham vấn pháp lý.
27. **Dư có hết hạn.** Chính sách nêu hạn 12 tháng; đến hạn chuyển thu nhập khác sau khi đã thông báo bằng văn bản. Ví dụ dư có 1.000.000 hết hạn. Kiểm soát: điều khoản được BN chấp thuận trước, duyệt chủ phòng khám, thông báo trước. Rủi ro pháp lý, chưa có nguồn VN.

**D. Hoàn tiền và đảo ngược**

28. **Nhập sai thanh toán.** Nhập 5.000.000 thay vì 500.000. Không sửa trực tiếp/xóa: hủy phiếu có lý do và lập phiếu mới, hoặc bút toán điều chỉnh (Open Dental dùng income transfer). Thu ròng +500.000. Kiểm soát: chỉ vai trò được phép hủy trong kỳ chưa khóa; phiếu hủy giữ số, không tái sử dụng; log.
29. **Chuyển khoản bị đảo ngược/chargeback.** Giao dịch QR bị hoàn do lỗi hệ thống: payment chuyển trạng thái "reversed", Thu giảm, Nợ tăng lại nếu đã phân bổ. Kiểm soát: đối soát sao kê hằng ngày, không xác nhận chỉ bằng ảnh chụp màn hình.
30. **Hoàn một phần.** Hoàn 2.000.000 trong dư có 5.000.000. Dư có còn 3.000.000. Ghi gắn payment gốc, tổng hoàn không vượt tổng đã thu của payment đó (tránh unearned âm theo Open Dental).
31. **Phương thức hoàn.** Tiền mặt hay chuyển khoản; thông lệ hoàn về đúng kênh đã thu (thẻ về thẻ). Kiểm soát: chuyển khoản hoàn về tài khoản đứng tên BN hoặc người trả; tiền mặt cần chữ ký nhận; hạn mức tiền mặt cần duyệt.
32. **Hoàn gói đã giảm giá.** Xem ca 16: hoàn theo giá sau giảm phân bổ tỷ lệ.
33. **Hoàn sau khi đã xuất hóa đơn.** Hóa đơn GTGT/điện tử đã phát hành: lập hóa đơn điều chỉnh hoặc thay thế (Nghị định 123/2020, chưa kiểm chứng) và ghi giảm trừ doanh thu qua TK 521 nếu áp dụng ([MISA SME](https://sme.misa.vn/63854/huong-dan-hach-toan-tai-khoan-521-ke-toan-cac-khoan-giam-tru-doanh-thu/)). Ghi: số hóa đơn gốc, số hóa đơn điều chỉnh. Kiểm soát: không hoàn nếu chưa liên kết chứng từ; kế toán duyệt. Lưu ý dịch vụ khám chữa bệnh có thể thuộc diện không chịu thuế GTGT, cần kế toán xác nhận.

**E. Bên thứ ba**

34. **Người trả khác BN (cha mẹ, vợ chồng, công ty).** Phụ huynh trả cho con. Ghi payer tách khỏi patient; dư có gắn BN được điều trị (hoặc "hộ gia đình" nếu chính sách cho phép dùng chung, Open Dental có số dư theo gia đình). Hoàn về đúng người trả. Hóa đơn xuất theo tên/MST người yêu cầu. Kiểm soát: lưu CCCD/quan hệ khi số tiền lớn.
35. **Bảo hiểm hoặc công ty đài thọ.** Công ty trả 5.000.000 hạn mức, BN đồng chi trả 1.000.000 trên 6.000.000. R 6.000.000; Thu từ BN 1.000.000; Phải thu công ty 5.000.000. Ghi: hạn mức, hợp đồng, thời hạn thanh toán. Kiểm soát: phải thu bên thứ ba tách khỏi công nợ BN; đối chiếu định kỳ. (Ca này nhỏ tại VN; mức độ phức tạp thấp.)
36. **Công ty trả góp (tài chính tiêu dùng) trả ròng sau phí.** Trả góp 24.000.000, tổ chức tài chính chuyển 22.800.000 (phí 5% = 1.200.000, sau vài ngày). R 24.000.000; Thu 22.800.000; phí 1.200.000 ghi chi phí tài chính (hoặc giảm trừ tùy chính sách). Trong lúc chờ: phải thu tổ chức 24.000.000. Nếu BN hủy: hoàn qua tổ chức, phí có thể không được hoàn. Kiểm soát: đối soát với đối tác, theo dõi hồ sơ trả góp bị từ chối.
37. **Thẻ/QR (PayOS, VNPay, MoMo) có phí và thời gian về tiền.** Thu 10.000.000 qua cổng, phí 0,7% = 70.000, tiền về T+1. Thu ghi theo ngày BN thanh toán, phí ghi chi phí, đối soát khoản "đang về". Kiểm soát: đối soát hằng ngày với bảng kê cổng; mã giao dịch chống ghi trùng; xử lý webhook lặp.
38. **Ngoại tệ.** Khách Việt kiều trả tiền mặt 500 USD, tỷ giá nội bộ 25.000 = 12.500.000. Ghi nguyên tệ, tỷ giá, quy đổi VND; không có chênh lệch nếu quy đổi chốt tại thời điểm. Kiểm soát: tỷ giá do quản lý đặt theo ngày. (Quy định pháp lý về thu ngoại tệ tại VN chưa tra cứu; cần kiểm tra.)

**F. Nhiều chi nhánh, nhiều lần khám**

39. **Thu ở chi nhánh A, điều trị ở chi nhánh B.** Dư có thuộc BN toàn hệ thống; doanh thu ghi tại chi nhánh thực hiện; chi nhánh thu tiền ghi nhận công nợ nội bộ với chi nhánh B (chuyển nội bộ). Kiểm soát: báo cáo đối soát liên chi nhánh, không tính hai lần.
40. **Hoa hồng bác sĩ/phụ tá: tính theo thu hay theo thực hiện.** Ví dụ tỷ lệ 30%. Theo thực hiện: thủ thuật R 10.000.000 → 3.000.000, kể cả khi BN còn nợ; theo thực thu: chỉ trên số đã thu. Chọn một cách nhất quán, ghi rõ: nếu hoàn tiền sau khi đã chi hoa hồng, thu hồi (clawback). Open Dental có bài về trả bác sĩ theo thu/collection ([Open Dental blog](https://opendental.blog/best-practices-when-paying-providers-on-collections/); chưa fetch nội dung). Kiểm soát: khóa kỳ lương, điều chỉnh kỳ sau.
41. **Chi phí labo và vật tư so với doanh thu.** Răng sứ 3.000.000, labo 900.000, vật tư 200.000: lợi nhuận gộp 1.900.000. Khi làm lại bảo hành chi phí tăng mà R không đổi. Ghi: chi phí gắn thủ thuật. Kiểm soát: báo cáo biên lợi nhuận theo dịch vụ, cảnh báo giá vốn bất thường.
42. **Chỉnh nha: phí điều chỉnh hàng tháng và bảo hành.** Gói niềng 30.000.000: 10.000.000 khởi đầu, 20 tháng × 1.000.000 phí điều chỉnh. Doanh thu có thể ghi theo mốc (khởi đầu, từng lần tái khám) hay chia đều theo thời gian; chọn một cách. Bỏ giữa chừng: tính theo số tháng đã thực hiện. Khám tái miễn phí trong gói. Kiểm soát: lịch thanh toán, nhắc nợ, hợp đồng chỉnh nha.
43. **Chốt doanh thu cuối tháng.** Có hai nhóm: đã làm chưa thu (Nợ, ví dụ 8.000.000) và đã thu chưa làm (dư có, ví dụ 25.000.000). Báo cáo tháng phải hiện cả thu (cash), R, Nợ, dư có để không nhầm "doanh thu". Kiểm soát: khóa kỳ; thay đổi sau khóa phải qua điều chỉnh kỳ sau, không sửa số cũ.

**G. Kiểm soát và đối soát**

44. **Đánh số phiếu thu.** Số liên tục theo chi nhánh/năm, không tái sử dụng, phiếu hủy giữ lại kèm lý do. Kiểm soát: kiểm tra hụt số.
45. **Hủy phiếu có lý do và quyền sửa thanh toán quá khứ.** Hạn chế: lễ tân chỉ hủy trong ngày; quản lý hủy trong kỳ mở; sau khóa chỉ đảo bút toán. Ghi: người, thời gian, giá trị trước/sau. Tham chiếu: kiến nghị của Open Dental dùng income transfer thay vì chỉnh trực tiếp ([Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)).
46. **Chốt két cuối ngày.** Tiền mặt thực kiểm so với tổng phiếu thu tiền mặt − phiếu chi; chênh lệch ghi nhận có lý do, không tự san bằng; kênh chuyển khoản/POS đối soát riêng. Kiểm soát: hai người ký, báo cáo chênh lệch theo ngày/thu ngân.

### Gaps
- Không có nguồn VN xác nhận quy tắc hoàn cọc, hết hạn dư có, thu ngoại tệ, thuế GTGT cho dịch vụ nha khoa, hay quy trình hóa đơn điều chỉnh; các điểm này cần tư vấn pháp lý/kế toán.
- Chưa fetch nội dung bài Dentrix/Curve, Open Dental refund page, PlanetDDS về unallocated; chưa kiểm chứng cách các phần mềm đó xử lý clawback hoa hồng.
- Bản liệt kê trên đánh số 46 mục (một số là ca kiểm soát chung, không phải ca riêng).

## 3. Quy tắc hệ thống đề xuất rút ra

### Takeaway
Ba nguyên tắc: không xóa cứng (đảo bút toán), tách tiền thu khỏi doanh thu bằng phân bổ, mọi ngoại lệ tiền cần lý do + người duyệt.

### Cited Findings
- Open Dental cảnh báo unearned âm và yêu cầu income transfer cân bằng tổng 0 khi sửa. — [Open Dental Manual](https://opendental.com/manual/unearnedprepayment.html)

### Inferences
- Công thức bất biến: Dư có = Tổng thu ròng − Tổng phân bổ cho thủ thuật hoàn thành; Nợ = R − phân bổ; không bao giờ để Dư có < 0.
- Bảng quyền đề xuất: lễ tân (thu, hủy trong ngày), quản lý (hoàn, giảm vượt ngưỡng, hủy trong kỳ), chủ phòng khám (cọc không hoàn, hết hạn dư có, ngoại lệ bảo hành).
- Log kiểm toán chung: người, thời điểm, giá trị cũ/mới, lý do, chứng từ liên kết.

### Gaps
- Ngưỡng giảm giá và hạn dư có là tham số cấu hình do phòng khám quyết định, không có chuẩn ngành tìm được.
