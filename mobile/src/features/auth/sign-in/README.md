# S01 — Sign In (Đăng nhập)

## Tác giả
`trientvhe181820`

## Mô tả
Triển khai màn hình S01 trên **Mobile (Web + iOS + Android)** dùng Expo Router + React Native.

## Phạm vi triển khai

### ✅ Đã làm

| Tính năng | Chi tiết |
|---|---|
| Form đăng nhập | `username` + `password` với hiện/ẩn mật khẩu. |
| Validation client-side | Ràng buộc username (3–64 ký tự `A-Za-z0-9._-`), password (≤256 ký tự). |
| API thật | `POST /api/auth/sign-in`, `GET /api/auth/me`, `POST /api/auth/sign-out`. |
| Token storage | Native: Expo SecureStore (Keychain/Keystore). Web: `sessionStorage` (không dùng `localStorage` theo yêu cầu). |
| Session lifecycle | Hydrate từ token khi khởi động; auto refresh mỗi 30s + khi app foreground/tab focus. |
| 401 / khóa | Tự đăng xuất + chuyển về sign-in khi phiên hết hạn hoặc bị thu hồi. |
| Route guard | `(account)/` chỉ cho authenticated; `(auth)/` redirect nếu đã đăng nhập. |
| Role-based guard | `useAuthGuard({ requireRoles: ['Patient'] })` cho link patient (UC-36 chưa triển khai → redirect). |
| Error handling | Lỗi mạng có retry (tối đa 2 lần với backoff); 4xx không retry, hiển thị MSG. |
| Bảo mật | Không log password/token; không lưu vào env; `sessionStorage` trên web tự xóa khi đóng tab. |
| Components dùng chung | `TextField`, `PasswordField`, `PrimaryButton`, `FormError` có thể reuse. |

### ⚠️ Ngoài phạm vi (backend chưa hỗ trợ)

| Tính năng trong đặc tả | Lý do |
|---|---|
| "Liên hệ đã xác minh" (email/phone) | Backend API chỉ nhận `username`. Không sinh thêm contract. |
| Refresh token có thể thu hồi | Backend đợt này không có endpoint refresh token. |
| Rate-limit đăng nhập | Backend chưa có cơ chế giới hạn thử đăng nhập. |
| S02 (đăng ký) / S03 (khôi phục) | Ngoài scope S01. Chỉ là link navigation. |
| Patient link (UC-36) | Backend chưa có endpoint, `HasVerifiedPatientLink` luôn `false`. |
| CSRF / cookie | Không áp dụng: JWT Bearer token, CORS đã cấu hình backend. |

## Cấu trúc file

```
mobile/src/
├── features/auth/sign-in/
│   ├── sign-in.api.ts          # API client (sign-in, me, sign-out)
│   ├── sign-in.schema.ts       # Validation (username, password)
│   ├── sign-in.types.ts        # Types (SignInRequest, SignInResponse, CurrentUser, SessionState)
│   ├── screens/SignInScreen.tsx # Màn hình chính
│   └── hooks/
│       ├── useSignIn.ts        # Submit + retry logic
│       └── useSignOut.ts       # Đăng xuất + thu hồi
├── features/auth/session/
│   ├── SessionProvider.tsx     # Provider: hydrate, auto-refresh, focus listener
│   ├── session.types.ts        # SessionState | AuthenticatedState | ...
│   ├── useSession.ts           # Đọc state phiên
│   └── useAuthGuard.ts         # Bảo vệ route theo role
├── components/
│   ├── forms/TextField.tsx     # Input text cơ bản
│   ├── forms/PasswordField.tsx # Input password có toggle hiện/ẩn
│   ├── ui/PrimaryButton.tsx    # Nút bấm chính (loading state)
│   └── feedback/FormError.tsx  # Thanh thông báo lỗi/thành công
└── services/
    ├── api/http-client.ts       # request() + authedRequest()
    ├── api/api-error.ts         # ApiError model
    └── storage/token-storage.ts  # SecureStore (native) / sessionStorage (web)
```

## Chạy thử

```powershell
cd mobile
npm.cmd start
# Hoặc web: npm.cmd run web
```

Cần backend đang chạy tại `EXPO_PUBLIC_API_URL` (mặc định `http://localhost:5080`).
Demo accounts: `admin.demo`, `dentist.demo`, `owner.demo`, `assistant.demo`, `guest.demo` (password: lấy từ `.env` `DevSeed__Password` trên máy chạy backend).

## Kiểm thử

```powershell
npm.cmd run typecheck  # ✅ 0 lỗi TypeScript
npm.cmd run export:web # Bundle web (không thay thế kiểm tra trên thiết bị)
```

## Tài liệu tham khảo

- Đặc tả S01 hiện hành: [Screen Specifications v2.4](../../../../../docs/specs/DCMS_Screen_Specifications_v2.4.docx); các kết quả kiểm thử trên giữ mốc triển khai ban đầu.
- Đối chiếu UC: `docs/implementation/UC01-UC31.md`
- Contract backend: `backend/src/DCMS.Application/Features/Authentication/Contracts.cs`
- System Messages: `backend/src/DC..MS.Application/Common/SystemMessages.cs`
