# AI hỗ trợ phát hiện sâu răng trên phim X-quang

Cập nhật 17/11/2026. Mở rộng S20 (tài liệu lâm sàng, [S19-S20-DCMS-112](S19-S20-DCMS-112.md)) và S19 (cận lâm sàng, [S19-INVESTIGATIONS](S19-INVESTIGATIONS.vi.md)). Thay phần "Mobile/AI là future work" trong AGENTS.md cho riêng tính năng phát hiện sâu răng.

## 1. Mục tiêu và phạm vi

Bác sĩ mở một phim X-quang đã tải lên DCMS (tài liệu `XRay`, JPEG/PNG, trạng thái `Ready`) và bấm **Phân tích AI**. Hệ thống gửi ảnh tới dịch vụ AI nội bộ, nhận các vùng nghi sâu răng (khung chữ nhật + độ tin cậy), lưu kết quả vào tài liệu và vẽ khung lên ảnh.

- Kết quả AI chỉ là **gợi ý hỗ trợ**, không phải chẩn đoán. Màn hình luôn hiển thị cảnh báo này; bác sĩ tự đánh giá và ghi chẩn đoán trong phiếu khám như hiện tại. AI không tự sửa phiếu khám, sơ đồ răng hay chẩn đoán.
- Phạm vi: sâu răng (`caries`) trên phim toàn cảnh (Panorama/OPG). Phim cận chóp/cánh cắn vẫn chạy được nhưng mô hình không được huấn luyện cho các loại phim này. Màn hình ghi rõ điều đó.
- Mô hình răng khôn ngầm (`impacted_molar_v2_best.pt`) **không bật mặc định**. Dịch vụ AI hỗ trợ bật bằng cấu hình (`AI_IMPACTED_WEIGHTS`) và trả cùng schema với `kind = impacted_third_molar`. DCMS lưu và hiển thị loại này nếu dịch vụ trả về.
- Kết quả đánh giá mô hình (Validation nội bộ, recall khoảng 85–88% tại điểm vận hành nghiên cứu) **không phải** kiểm chứng lâm sàng độc lập.

## 2. Thành phần

```text
Web (S19/S20 viewer) ──► DCMS API ──HTTP──► ai-service (FastAPI + YOLO) ──► weights ngoài Git
                            │
                            └─► PostgreSQL: CLINICAL_DOCUMENT.AiAnalyses (JSONB)
```

- `ai-service/` (Python 3.10+, FastAPI, Ultralytics). Không public ra Internet; chỉ DCMS API gọi. Weights đọc từ đường dẫn tuyệt đối trong biến môi trường, không commit vào Git. Ví dụ máy dev: `C:\Xray-AI\artifacts\detector_weights\recommended_caries_best.pt`.
- DCMS API đọc file X-quang từ kho tài liệu riêng (đã kiểm SHA-256), gửi sang ai-service, lưu kết quả và ghi audit.
- Web vẽ khung lên ảnh bằng SVG phủ theo tọa độ pixel gốc của ảnh.

## 3. Pipeline suy luận (ai-service)

Mô hình `recommended_caries_best.pt`: YOLO detect, 1 lớp `caries_lesion`, `imgsz` 1280. Pipeline gọn, không phụ thuộc ROI răng tính sẵn (`tooth_rois.json` chỉ tồn tại cho ảnh dataset):

1. **Global**: dự đoán trên toàn ảnh, `conf ≥ 0.10`.
2. **Flip**: lật ngang ảnh, dự đoán, lật tọa độ về ảnh gốc.
3. **Tile**: cắt ô 960×640 ở độ phân giải gốc, chồng lấn 25%, dự đoán từng ô với `imgsz` 960 (không phóng ô lên 1280; phóng to làm mất phần lớn tổn thương khi thử trên ảnh mẫu) rồi đổi tọa độ về ảnh gốc. Mục đích là bắt tổn thương nhỏ.
4. **Gộp**: gom ứng viên theo IoU ≥ 0.35 (weighted box fusion). Khung là trung bình có trọng số theo độ tin cậy. Độ tin cậy cuối là max của cụm, cộng 0.03 cho mỗi nguồn xác nhận thêm (global/flip/tile), tối đa 0.99.
5. **Lọc**: giữ cụm có độ tin cậy cuối ≥ `AI_CARIES_MIN_CONFIDENCE` (mặc định 0.25). Nếu chỉ một nguồn phát hiện thì cần ≥ `AI_CARIES_SINGLE_SOURCE_MIN_CONFIDENCE` (mặc định 0.35).
6. **Vị trí gợi ý**: hàm trên/dưới theo nửa trên/dưới ảnh; bên phải/trái **của bệnh nhân** theo quy ước phim toàn cảnh (nửa trái ảnh = bên phải bệnh nhân). Đây là gợi ý thô, không phải số răng FDI.

Ảnh tối đa 20 MiB (giới hạn S20), cạnh dài tối đa 8000 px. Ảnh xám/RGBA được chuyển sang RGB.

## 4. Hợp đồng ai-service

`GET /health` → `200 {"status":"ok","device":"cpu|cuda","models":[{"kind":"caries","name":"recommended_caries_best","sha256":"…","loaded":true}]}`.

`POST /v1/xray/analyze`, multipart field `file` (image/jpeg hoặc image/png). Nếu đặt `AI_SERVICE_API_KEY` thì header `X-Api-Key` bắt buộc.

```json
{
  "schemaVersion": "1.0",
  "models": [{ "kind": "caries", "name": "recommended_caries_best", "sha256": "…" }],
  "image": { "width": 2836, "height": 1536 },
  "device": "cpu",
  "elapsedMs": 2140,
  "findings": [
    { "findingId": "F01", "kind": "caries", "label": "caries_lesion", "confidence": 0.62,
      "box": { "x1": 1012.4, "y1": 640.0, "x2": 1060.1, "y2": 690.7 },
      "jaw": "upper", "side": "right", "sources": ["global", "flip", "tile"] }
  ]
}
```

Lỗi: `400` ảnh không đọc được hoặc sai định dạng, `401` sai API key, `413` quá lớn, `503` model chưa nạp được.

## 5. API DCMS

| Method | Đường dẫn | Mô tả |
| --- | --- | --- |
| POST | `/api/clinical-documents/{id}/ai-analyses` | Body `{ "requestKey": "uuid" }`. Chạy phân tích, lưu và trả `AiAnalysisDto`. |
| GET | `/api/clinical-documents/{id}` | `DocumentDto` có thêm `aiAnalyses` (mới nhất trước). |

`AiAnalysisDto`: `analysisId, kind ("Caries"), schemaVersion, modelName, modelSha256, inputSha256, requestedBy, requestedByName, requestedAt, elapsedMs, device, imageWidth, imageHeight, findings[]`. Mỗi finding: `findingId, kind, label, confidence, x1, y1, x2, y2, jaw, side`.

Quy tắc:

- Quyền: `documents.review` (mặc định vai trò Bác sĩ). Người chạy phải là bác sĩ đang hoạt động, có membership `CanDentist` tại chi nhánh nguồn của tài liệu. Không bắt buộc là bác sĩ phụ trách lượt và không cần lượt còn mở, để đọc lại phim cũ. Xem kết quả cần `documents.read` như tài liệu.
- Chỉ tài liệu `DocumentType = XRay`, `ContentType` JPEG/PNG, `FileState = Ready` và SHA-256 file khớp. Sai loại/định dạng trả `MSG04` 400; chưa Ready hoặc hash lệch trả `MSG42` 409; sai quyền/không thấy trả 403/404 như S20.
- ai-service chưa cấu hình, không phản hồi, quá thời gian (`DentalAi:TimeoutSeconds`, mặc định 120 giây) hoặc trả lỗi thì trả `MSG144` 503, không lưu kết quả, ghi audit `AiAnalysisFailed`.
- Idempotency theo `requestKey` dùng kho `DocumentReceipt` hiện có. Cùng key + cùng tài liệu trả lại đúng kết quả cũ, không gọi AI lần nữa. Cùng key khác tài liệu trả `MSG111` 409.
- Mỗi lần chạy thành công thêm một phần tử **bất biến** vào `AiAnalyses`. Chạy lại tạo phần tử mới, không ghi đè. Tối đa 20 lần phân tích/tài liệu (vượt quá trả `MSG42` 409). Tăng `CLINICAL_DOCUMENT.Version`, ghi `DocumentAudit` `AiAnalyzed` với ghi chú "N vùng nghi sâu răng · <model>".
- Gọi AI nằm ngoài transaction. Phần lưu kết quả dùng transaction + advisory lock như các lệnh S20 khác.

## 6. Lưu trữ

Không thêm bảng. Thêm một cột JSONB `AiAnalyses` (mặc định `[]`) vào `CLINICAL_DOCUMENT`, cùng mẫu với `Reviews`. Bảng logic `AI_ANALYSIS` trong ERD được hiện thực vật lý bằng collection này. Tài liệu là gốc (aggregate root), phân tích là con bất biến luôn đọc cùng tài liệu, số lượng nhỏ và không truy vấn chéo tài liệu. Vì vậy không cần bảng riêng. Phần review từng finding (`review_payload` trong ERD logic) chưa làm ở bước này.

Không lưu ảnh annotate hay ảnh crop; chỉ lưu tọa độ. Ảnh gốc vẫn ở kho tài liệu riêng.

## 7. Giao diện

Trong drawer chi tiết tài liệu (S20, dùng ở S19 và trang tài liệu bệnh nhân), nếu tài liệu là X-quang dạng ảnh và Ready thì có tab **AI sâu răng**:

- Nút **Phân tích AI** (hiện khi có quyền `documents.review`). Lần sau đổi thành **Phân tích lại**. Đang chạy thì nút ở trạng thái loading (có thể vài giây đến vài chục giây trên CPU).
- Cảnh báo cố định: "Kết quả AI chỉ mang tính gợi ý hỗ trợ, không thay thế chẩn đoán của bác sĩ. Mô hình được huấn luyện trên phim toàn cảnh."
- Ảnh phim với khung đỏ đánh số (F01, F02…) theo kết quả mới nhất. Danh sách vùng nghi sâu: số, độ tin cậy %, hàm, bên; rê chuột vào dòng làm nổi khung. Ô chọn ngưỡng hiển thị độ tin cậy (mặc định 0%, chỉ lọc hiển thị, không đổi dữ liệu).
- Thông tin lần chạy: người chạy, thời điểm, model, thời gian xử lý. Chọn được lần phân tích cũ để xem.
- Không có vùng nào thì hiển thị "AI không phát hiện vùng nghi sâu răng", kèm nhắc bác sĩ vẫn tự đánh giá.

## 8. Cấu hình

| Biến (.env) | Cấu hình API | Ý nghĩa |
| --- | --- | --- |
| `DENTAL_AI_BASE_URL` | `DentalAi:BaseUrl` | Ví dụ `http://127.0.0.1:8090`. Trống thì tính năng trả 503. |
| `DENTAL_AI_API_KEY` | `DentalAi:ApiKey` | Khớp `AI_SERVICE_API_KEY` của ai-service (tùy chọn). |
| `DENTAL_AI_TIMEOUT_SECONDS` | `DentalAi:TimeoutSeconds` | Mặc định 120. |

Biến của ai-service xem `ai-service/.env.example`.

## 9. Kiểm thử

- ai-service: health, ảnh mẫu panorama trả finding hợp lệ trong biên ảnh, ảnh hỏng → 400, sai key → 401.
- API: tài liệu không phải XRay/PDF → 400; PendingScan → 409; AI tắt → 503 + audit; thành công lưu và tăng Version; replay requestKey không gọi AI lần hai; trợ lý → 403.
- UI: chạy phân tích trên X-quang thật, khung khớp vị trí ảnh khi co giãn, xem lần cũ.
