# Chạy DCMS mobile qua mạng ngoài bằng ngrok

## Mục tiêu

Khi máy phát triển có Internet, điện thoại dùng 4G hoặc mạng khác vẫn gọi được DCMS API qua một HTTPS endpoint ngrok cố định. Một lệnh khởi động sẽ chạy PostgreSQL, API, ngrok và Expo ở chế độ tunnel. URL công khai của API được nhúng vào bundle Expo dưới dạng `EXPO_PUBLIC_API_URL`; ngrok authtoken không xuất hiện trong bundle, Git hoặc log.

## Cấu hình

Đặt `KEY_NGROK` và `LINK_NGROK` trong `.env` ở thư mục gốc. `LINK_NGROK` là URL HTTPS cố định đã đặt trước trong tài khoản ngrok. Script dùng Docker image ngrok để không cần cài CLI riêng. Điện thoại chạy Expo Go hoặc bản development build cần Expo tunnel để tải bundle; APK release đã build trước với URL localhost cần build lại một lần với URL ngrok cố định. Không dùng URL ngrok ngẫu nhiên cho APK release vì URL được nhúng lúc build.

## Khởi động và kiểm tra

Chạy `powershell -ExecutionPolicy Bypass -File infrastructure/scripts/start-mobile-remote.ps1`. Script dừng ngay nếu thiếu biến hoặc Docker, chỉ ghi `EXPO_PUBLIC_API_URL=https://<domain>` vào `mobile/.env.local` (Git bỏ qua), rồi kiểm tra API nội bộ, endpoint ngrok và khởi động Expo tunnel. Ctrl+C dừng các tiến trình do script mở. PostgreSQL chạy bằng Docker Compose. Khi đổi mạng của PC, cần khởi động lại lệnh nếu ngrok bị ngắt.

Kiểm tra trên điện thoại: mở app qua Expo tunnel, đăng nhập và mở hồ sơ bệnh án. API `/health` qua HTTPS ngrok phải trả 200. Backend và dữ liệu PostgreSQL trên PC phải tiếp tục chạy; ngrok không biến PC thành dịch vụ luôn trực tuyến. Web trên PC tiếp tục dùng `http://localhost:5173` nếu chạy riêng; không công khai giao diện nhân viên qua tunnel này.

Để chạy APK release độc lập với Expo, cài lại `mobile/android/app/build/outputs/apk/release/app-release.apk` được build với domain mới. Sau khi đổi `LINK_NGROK`, build lại từ `mobile/android` bằng `gradlew.bat assembleRelease --rerun-tasks` với `EXPO_PUBLIC_API_URL` đặt bằng URL đó trong môi trường PowerShell. Dùng `--rerun-tasks` để Gradle không giữ lại bundle có URL cũ. Sau khi cài APK, những lần sau chỉ cần chạy script ở trên để mở API/ngrok; APK release không cần Expo tunnel hoặc cáp USB.

Nếu không có domain cố định, cần cấp một domain trong ngrok trước khi build APK. Không công khai môi trường Development chứa tài khoản demo hoặc dữ liệu bệnh nhân thật. Chỉ dùng dữ liệu thử và tắt tunnel sau khi test.
