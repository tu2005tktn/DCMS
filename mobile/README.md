# DCMS Mobile

Bộ khung React Native + Expo SDK 55 + TypeScript + Expo Router để team phát triển chung. S02 triển khai đăng ký Guest với họ tên, số di động Việt Nam, email và mật khẩu theo [đặc tả UC-33 hiện hành](../docs/specs/UC33-S02-MOBILE.md). Email được lưu nhưng chưa xác minh; số điện thoại vẫn dùng để đăng nhập. S01/S03/S07 vẫn là placeholder; đăng nhập trên Mobile, khôi phục và liên kết bệnh nhân chưa triển khai.

## Chạy trên Windows / PowerShell

Yêu cầu Node.js 22.13 trở lên (hoặc bản LTS mới tương thích), npm và Expo Go tương thích SDK 55 hoặc development build. Từ thư mục gốc repository:

```powershell
cd mobile
npm.cmd ci
Copy-Item .env.example .env # Chỉ chạy nếu chưa có .env
npm.cmd start
```

Quét QR bằng thiết bị hoặc nhấn `a` với Android Emulator đã cài. `npm.cmd run web` mở bản web để xem khung màn hình; iOS Simulator cần macOS. Nếu Expo Go trên thiết bị không hỗ trợ SDK 55, dùng development build theo hướng dẫn Expo. Máy tính và điện thoại cần kết nối mạng phù hợp.

```powershell
npm.cmd run typecheck
npx.cmd expo install --check
npm.cmd run export:web
```

`export:web` kiểm tra bundle và tạo `dist/` (không commit). Khi đổi dependencies, dùng `npx.cmd expo install <package>` để chọn phiên bản phù hợp SDK và commit cả `package.json` lẫn `package-lock.json`.

## Phân chia code

| Thư mục | Trách nhiệm |
| --- | --- |
| `assets/{images,icons,fonts}` | Tài nguyên dùng chung |
| `src/app` | Route/layout; route chỉ trỏ tới feature screen |
| `src/features/auth/registration` | S02 đăng ký Guest; không có bước mã xác minh |
| `src/features/auth/sign-in` | S01 |
| `src/features/auth/recovery` | S03 và đặt lại mật khẩu |
| `src/features/auth/session` | Provider, hook và vòng đời phiên |
| `src/features/patient-link` | S07 |
| `src/components/{ui,forms,feedback}` | Component dùng chung |
| `src/services/{api,storage}` | HTTP, lỗi API, SecureStore |
| `src/config`, `src/theme`, `src/utils` | Cấu hình, giao diện, tiện ích |

Trong mỗi feature, dùng `screens` cho màn hình, `components` cho UI riêng, `hooks` cho logic, `.api.ts` cho kết nối API, `.schema.ts` cho validation và `.types.ts` cho kiểu dữ liệu. Các file TODO chưa có contract; team bổ sung theo đặc tả. `.gitkeep` giữ thư mục rỗng trên GitHub; có thể xóa khi thư mục đã có code.

## Quy trình cho team

1. Đọc [đặc tả màn hình](../docs/specs/DCMS_Screen_Specifications_v2.4.docx) và [phạm vi bộ khung](../docs/specs/MOBILE-SCAFFOLD.md).
2. Tạo branch riêng cho task, ví dụ `mobile/s01-sign-in`. Cập nhật tài liệu trong `docs/` trước khi triển khai theo `AGENTS.md`.
3. Viết code trong feature phụ trách; thống nhất thay đổi shared components, session và API với team.
4. Chạy các lệnh kiểm tra ở trên, kiểm tra trên thiết bị khi có UI/logic, rồi tạo pull request ghi rõ phần đã làm và phần chưa làm.

## API và môi trường

`EXPO_PUBLIC_API_URL` là cấu hình công khai được nhúng vào app, không chứa secret. Khi dùng điện thoại thật, thay `localhost` bằng địa chỉ LAN của máy chạy API; cần cấu hình API listen/firewall phù hợp. S02 gọi `POST /api/auth/register` thật để tạo Guest. S01 Mobile vẫn là placeholder; đăng nhập bằng số vừa đăng ký đã hoạt động qua `POST /api/auth/sign-in`. Recovery và liên kết bệnh nhân phải có contract riêng trước khi tích hợp.

Session hiện luôn anonymous. Layout account chưa có guard vì chưa có luồng xác thực. `token-storage.ts` cung cấp SecureStore trên native nhưng chưa gắn vào phiên; bản web báo chưa hỗ trợ lưu token lâu dài. Không commit `.env`, token hoặc dữ liệu bệnh nhân.

## EAS

`eas.json` là mẫu profile development/preview/production. Trước khi build, người phụ trách cần đăng nhập Expo, cấu hình EAS project, thống nhất Android package/iOS bundle identifier và credentials. Development profile cần cài `expo-dev-client` bằng `npx.cmd expo install expo-dev-client`. Chưa cấu hình tài khoản EAS hoặc phát hành app trong bước này.

Tham khảo: [Expo Router setup](https://docs.expo.dev/router/installation/), [src directory](https://docs.expo.dev/router/reference/src-directory/).

## Kiểm tra bộ khung (2026-10-12)

TypeScript, kiểm tra phiên bản Expo và export bundle web/Android đã đạt. Export bundle không thay thế kiểm tra trên thiết bị; chưa chạy trên điện thoại hoặc build/phát hành EAS. `npm audit` còn 35 cảnh báo dependency (14 moderate, 21 high) sau khi áp dụng bản vá tương thích; các đề xuất `--force` đổi phiên bản Expo/React Native nên cần xử lý trong task nâng cấp dependency riêng.
