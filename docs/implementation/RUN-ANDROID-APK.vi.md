# Build APK Android tự động để demo

## Phạm vi

- Đóng gói ứng dụng Expo trong `mobile/` thành APK cài trực tiếp trên Android, có JavaScript bundle sẵn và không cần Expo Go hoặc Metro.
- Build theo `mobile/package-lock.json`; không dùng phiên bản khác đang còn trong `node_modules` từ lần chạy trước.
- APK chỉ chứa các màn hình đã có trong ứng dụng mobile của checkout hiện tại. Các màn web dành cho nhân viên không tự xuất hiện trong APK.
- Không đưa thông tin xác thực, dữ liệu bệnh nhân, `.env` của backend hoặc khóa ký vào Git hay gói source gửi build.

## Cấu hình demo

- `EXPO_PUBLIC_API_URL` là URL công khai được nhúng vào APK. Với demo LAN, dùng IPv4 hiện tại của máy chạy API, không dùng `localhost`.
- `EXPO_PUBLIC_APPOINTMENTS_MOCK=false` và `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=true` để dùng API thật.
- Cấu hình Expo hiện có cho phép HTTP Android khi URL API bắt đầu bằng `http://`.
- Máy chạy API và điện thoại phải cùng mạng; API chạy bằng `infrastructure/scripts/start-api.ps1 -Lan` tại cổng 5080. Không tự thay đổi firewall.
- IP máy thay đổi thì cần build lại APK hoặc dùng một URL API ổn định đã được cấu hình riêng.

## GitHub Actions

- Workflow `.github/workflows/mobile-apk.yml` chạy khi push vào bất kỳ nhánh nào có thay đổi trong `mobile/` hoặc chính workflow. Push chỉ sửa backend/web không tạo APK mới.
- Có thể chạy thủ công bằng `workflow_dispatch` với URL API tùy chọn. Nút Run workflow cần workflow tồn tại trên nhánh mặc định của repository; push vào nhánh `tư` vẫn kích hoạt tự động theo bộ lọc đường dẫn.
- URL API được chọn theo thứ tự: input `api_url` khi chạy thủ công, Repository Variable `MOBILE_API_URL`, mặc định demo hiện tại `http://192.168.0.102:5080`. Giá trị được nhúng vào APK, không phải secret. Không chấp nhận URL loopback `localhost`/`127.0.0.1` vì điện thoại sẽ gọi chính nó.
- Runner Ubuntu cài dependencies từ lockfile, kiểm tra TypeScript, tạo Android native project bằng Expo prebuild và chạy Gradle `:app:assembleRelease`. APK có sẵn bundle, dùng API lịch hẹn/thông báo thật, không cần Expo Go, Metro, tài khoản EAS hoặc token Expo.
- APK nội bộ dùng debug keystore có sẵn trong template Expo/React Native; đây không phải chữ ký phát hành Play Store. Không đưa khóa release vào workflow/source. Google Sign-In trên Android vẫn cần OAuth Android client đăng ký đúng package/chữ ký của APK.
- Các lần build cùng nhánh dùng concurrency để ưu tiên push mới nhất; build cũ đang chạy có thể bị hủy khi có push mới.
- Job kiểm tra chữ ký APK và JavaScript bundle, rồi upload APK, SHA256 và thông tin commit/API vào artifact `dcms-mobile-<short-sha>`, lưu 14 ngày. Mở GitHub → Actions → Mobile APK → lần chạy thành công → Artifacts để tải ZIP và giải nén lấy APK.
- Muốn đổi URL cho các lần build tiếp theo: Settings → Secrets and variables → Actions → Variables → `MOBILE_API_URL`. Với backend dùng được từ mọi mạng, cần triển khai backend/database dùng chung và đặt URL HTTPS công khai ổn định vào biến này.
- Máy chạy backend vẫn phải bật khi demo LAN. GitHub runner chỉ build APK; không cần và không kiểm tra kết nối vào địa chỉ LAN của người dùng.

## Build và xác minh

### Menu native nhận thao tác nhấn (07/11/2026)

- Khi kiểm tra APK trên LDPlayer, ứng dụng mở được nhưng các thẻ menu trung tâm không điều hướng trên Android: `Link asChild` đang bọc `View`, trong khi phần tử con cần xử lý `onPress`.
- Menu dùng `Pressable` cho toàn bộ thẻ, giữ route, bộ lọc theo phiên, nội dung và kích thước vùng nhấn hiện có. Chạm thẻ phải mở đúng màn hình trên Android; nút Back trở về menu.
- Sửa này không đổi API hay dữ liệu. Xác minh TypeScript, bundle Android và APK thực tế: mở màn đăng nhập và khôi phục từ menu. Kiểm tra kết nối backend bằng màn đặt lịch công khai, chỉ tải danh mục, không gửi yêu cầu đặt lịch hoặc khôi phục mật khẩu.
- Theo [hướng dẫn Expo Router](https://docs.expo.dev/router/basics/navigation/), phần tử con của `Link asChild` phải nhận `onPress`/`onClick`, ví dụ `Pressable`.

1. Kiểm tra TypeScript và dependency từ lockfile trong bản sao build riêng nếu môi trường cài đặt hiện tại khác phiên bản.
2. Expo Build: profile internal/preview với `android.buildType=apk`, cấu hình URL API trong môi trường build; lưu lại build ID để không gửi trùng khi mất kết nối.
3. Hoặc build native trên máy: Expo prebuild Android rồi Gradle `:app:assembleRelease`; cần JDK và Android SDK. Chỉ dùng chữ ký demo cho APK nội bộ, không coi đây là bản phát hành Play Store.
4. Lưu APK trong thư mục `.artifacts/` bị Git bỏ qua; kiểm tra tên package, chữ ký, SHA256 và kích thước tệp thực tế.
5. Kiểm tra API `/health` và đăng nhập tài khoản demo qua API. Chỉ khẳng định chạy trên điện thoại sau khi thực sự cài và mở app; build thành công không thay thế kiểm tra thiết bị.

Tham khảo chính thức: [Expo APK](https://docs.expo.dev/build-reference/apk/), [build Android local](https://docs.expo.dev/guides/local-app-production/).
