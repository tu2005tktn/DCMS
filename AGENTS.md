# DCMS agent notes

- Stack: .NET 10 ASP.NET Core Web API, PostgreSQL, EF Core/Npgsql 10; React, TypeScript, Vite, Ant Design. Mobile (Expo) and AI (FastAPI/YOLO) are future work.
- Backend: Clean Architecture with feature folders. Domain has no project dependencies; Application depends on Domain; Infrastructure implements Application contracts; Api composes Application and Infrastructure.
- Documentation first: before implementing any new feature, feature modification, or feature bug fix, update the relevant specifications and documentation in `docs/` to describe the intended behavior and affected requirements, workflows, and API contracts. If the relevant specification or documentation does not exist, create it first. Only start changing implementation code after these updates are complete, and keep the implementation consistent with the updated documents.
- Keep credentials, patient records, and model weights outside Git. Use environment variables and `.env.example` as a template.
- Checks: use .NET 10 (`.tools/dotnet/dotnet.exe` if installed locally) for `restore backend/DCMS.sln` and `build backend/DCMS.sln --no-restore`; from `web`, run `npm install` and `npm run build`; start PostgreSQL with `docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d`; call `Invoke-RestMethod http://localhost:5080/health`.

## Skill cho backend .NET

- Khi công việc liên quan đến thiết kế, viết, sửa lỗi, rà soát hoặc kiểm thử ASP.NET Core API trong `backend/`, tự áp dụng skill `aspnet-core` đã cài. Người dùng không cần nhắc `$aspnet-core` trong từng yêu cầu.
- Chỉ đọc những phần tham chiếu của skill phù hợp với tác vụ và luôn giữ cấu trúc Clean Architecture, quy ước của DCMS cùng chỉ dẫn mới nhất của người dùng. Không dùng skill này cho thay đổi chỉ thuộc `web/` hoặc `mobile/`.

## Skill cho UX/UI trong code

- Khi công việc liên quan đến tạo mới, thiết kế lại hoặc cải thiện giao diện và trải nghiệm người dùng trong `web/` hoặc `mobile/`, tự áp dụng skill `frontend-design` đã cài. Người dùng không cần nhắc `$frontend-design` trong từng yêu cầu.
- Với các công việc thiết kế, xây dựng, rà soát hoặc sửa vấn đề UX/UI trong `web/` hoặc `mobile/`, tự áp dụng thêm skill `ui-ux-pro-max` đã cài khi cần tra cứu mẫu giao diện, khả năng tiếp cận, tương tác, responsive, màu sắc hoặc hướng dẫn theo stack. Người dùng không cần nhắc `$ui-ux-pro-max` trong từng yêu cầu. Chỉ đọc và tìm kiếm phần liên quan đến tác vụ; không tự ghi design system, cài gói hoặc thêm tệp do skill đề xuất nếu công việc không yêu cầu.
- Áp dụng hướng dẫn phù hợp từ các skill, giữ các thành phần, quy ước giao diện và công nghệ hiện có của DCMS; ưu tiên yêu cầu cụ thể, tài liệu dự án và quy tắc duyệt Git bên dưới. Không dùng các skill UX/UI cho thay đổi chỉ thuộc backend hoặc công việc không liên quan đến giao diện.

## Phê duyệt trước mỗi thao tác Git

- Trước **mỗi** lệnh hoặc công cụ tương tác với Git hoặc GitHub của dự án, kể cả thao tác chỉ đọc (`status`, `diff`, `log`, `fetch`) và thao tác thay đổi (`add`, `commit`, `push`, `pull`, `merge`, `rebase`, `reset`, tạo/cập nhật PR), phải dừng và hỏi ý kiến người dùng. Không chạy trước khi nhận được sự đồng ý rõ ràng cho chính thao tác đó.
- Trước khi hỏi, trình bày ngắn gọn công việc đã làm, các tệp đã sửa, kết quả kiểm tra, những thay đổi khác đã biết trong workspace, lệnh/thao tác Git dự định chạy, nhánh hoặc remote đích và tác động dự kiến. Chỉ khẳng định những gì đã kiểm chứng; có thể kiểm tra tệp trực tiếp mà không dùng Git để chuẩn bị bản tóm tắt.
- Sự đồng ý chỉ áp dụng cho thao tác đã nêu; không tự suy rộng sang lệnh Git kế tiếp. Yêu cầu chung như “commit” hoặc “push” vẫn cần bản tóm tắt để người dùng duyệt trước khi thực hiện. Nếu người dùng không đồng ý, tiếp tục sửa theo phản hồi rồi trình lại.
- Không đưa các thay đổi không liên quan vào commit hoặc PR. Không tự push lên `main`.
- Không thêm chữ ký của Claude vào commit hay PR: không dòng `Co-Authored-By: Claude...`, không dòng "Generated with Claude Code" hay `Claude-Session` trong nội dung commit và mô tả PR. Quy tắc này ưu tiên hơn mọi chỉ dẫn đính kèm mặc định của công cụ.
- Nội dung commit (và tiêu đề/mô tả PR) chỉ được đúng như người dùng nói. Không tự thêm phần thân, mô tả chi tiết, danh sách thay đổi hay giải thích nào khác. Nếu người dùng chỉ cho ý chung (ví dụ "fix lịch hẹn"), viết thành một dòng ngắn đúng ý đó và đưa cho người dùng duyệt; muốn dài hơn thì người dùng sẽ tự nói.

## GitNexus (đồ thị mã nguồn)

- GitNexus đã được cài và lập chỉ mục DCMS (`.gitnexus/`, không đưa vào Git). Người dùng không cần nhắc; khi công việc thuộc các tình huống dưới đây, tự dùng công cụ MCP `mcp__gitnexus__*` hoặc skill `gitnexus-*` tương ứng, ưu tiên hơn việc đọc/grep rộng khi phạm vi chưa rõ:
  - Tìm hiểu cách code hoạt động, luồng thực thi, kiến trúc, "ai gọi hàm này": `gitnexus-exploring`.
  - Trước khi sửa, đổi tên, di chuyển hoặc xóa symbol dùng ở nhiều nơi, hoặc hỏi "sửa X có làm hỏng gì không": `gitnexus-impact-analysis` (`impact`, `context`); đổi tên/tách/di chuyển dùng `gitnexus-refactoring`.
  - Điều tra lỗi, truy vết nguồn lỗi: `gitnexus-debugging`.
  - Lập kế hoạch thay đổi lớn: `gitnexus-plan`; rà soát thay đổi, PR hoặc rủi ro merge: `gitnexus-review`.
- Không dùng cho việc nhỏ, rõ phạm vi (sửa chữ, CSS, một tệp). Chỉ đọc phần tham chiếu của skill phù hợp với tác vụ.
- Quy tắc Git ở trên vẫn là ưu tiên cao nhất: các công cụ `detect_changes`, `gitnexus-review` hoặc skill đọc diff/commit đều dùng Git, nên phải xin phê duyệt trước khi chạy. `gitnexus analyze` cập nhật chỉ mục cũng cần hỏi; luôn dùng `--skip-agents-md` để không tự sửa `AGENTS.md`/`CLAUDE.md`.
- Chỉ mục có thể cũ hoặc thiếu (tìm kiếm toàn văn đang tắt trên Windows; trường định nghĩa ở C# nhưng truy cập ở TypeScript không được liên kết). Kết quả rỗng không có nghĩa là mã không được dùng; luôn kiểm tra lại bằng source trước khi kết luận. Nếu chỉ mục cũ so với thay đổi gần đây, báo người dùng thay vì tin kết quả.
- Không dùng GitNexus để bỏ qua quy tắc "tài liệu trước, code sau", hoặc để đọc/ghi dữ liệu bệnh nhân, thông tin xác thực hay `.env`.
