# Chiến lược quản lý token & phiên — UC-01 mở rộng

> Phạn vi: tài liệu này là phần bổ sung cho [UC01-UC31.md](UC01-UC31.md) sau khi thống nhất
> mở rộng UC-01 với **refresh token rotation có thể thu hồi**, **đăng xuất mọi thiết bị**
> và **chính sách lưu trữ token trên Web / Mobile**. Áp dụng cho tất cả client
> (web React, mobile Expo iOS/Android/Web). Mọi quyết định dưới đây đã được rà soát
> với [ERD 37 bảng](../architecture/DCMS_Logical_ERD_37_Tables.mmd), đặc tả S01–S04
> ([specs](../specs/)) và repo hiện tại.

## 1. Nguyên tắc chung

1. **Access token là bearer JWT ngắn hạn (15 phút).** Tự mang `sub`, `sid`, `jti`, `iat`, `exp`.
   Không mang role/permission. Mỗi request vẫn đọc lại DB để lấy role/permission hiện tại
   (giữ nguyên thiết kế trong UC01-UC31.md mục "Thiết kế xác thực và quyền").
2. **Refresh token là opaque string dài hạn (30 ngày).** Lưu hash SHA-256 trong DB cùng
   `account_id`, `session_id`, `device_label`, `issued_at`, `expires_at`, `revoked_at`,
   `replaced_by_id`. Không phải JWT, không tự mang thông tin; chỉ dùng để đổi access token mới.
3. **Refresh rotation: mỗi lần dùng refresh để cấp access mới, refresh cũ bị thu hồi và
   thay bằng refresh mới.** Nếu refresh cũ được tái sử dụng (reuse detection), TOÀN BỘ
   chuỗi refresh của session đó bị thu hồi — phòng token bị đánh cắp và bị dùng lại.
4. **Sign-out: thu hồi session tương ứng** (xóa refresh + đánh dấu session revoked). Token
   cũ trả 401/MSG07. Đăng xuất một thiết bị cụ thể = thu hồi session đó.
5. **Đăng xuất mọi thiết bị: thu hồi tất cả session còn active của account.** Access token
   hiện tại hết hạn sau ≤ 15 phút. Refresh token hiện tại bị thu hồi ngay.
6. **Khóa account (UC-31): thu hồi toàn bộ session + refresh** (đã có sẵn trong UC-31).
7. **Thu hồi role (UC-31): KHÔNG thu hồi session.** Phiên còn hiệu lực, request tiếp theo
   thấy role mới trong DB → 403/MSG06 cho endpoint cần role đó (giữ nguyên UC-31).
8. **Không log token, refresh token, hash, lý do có chứa thông tin nhạy cảm.** Audit
   chỉ ghi `session_id`, `device_label`, `event_type` (REFRESH / SIGN_OUT / LOCK).
9. **CSRF/cookie: KHÔNG áp dụng trong đợt này.** JWT Bearer + CORS đã whitelist
   `localhost:5173` và `127.0.0.1:5173`. Web không dùng cookie. Mục 7 bên dưới ghi
   rõ điều kiện để chuyển sang cookie httpOnly nếu triển khai subdomain chung trong tương lai.

## 2. Lưu trữ token — Web (React/Vite)

| Khoá | Loại | Mã hoá tại rest? | Tự xoá khi? | Lý do |
| --- | --- | --- | --- | --- |
| Access token | `sessionStorage` (in-memory only) | Không (vì < 15 phút, web không tái sinh sau reload) | Đóng tab / reload | Theo quyết định đã ghi trong [RUN-ACCOUNTS.vi.md](RUN-ACCOUNTS.vi.md) "JWT được giữ trong bộ nhớ" và UC01-UC31.md. **KHÔNG dùng `localStorage`** vì access token có thể được JS đọc bởi extension/malicious script. |
| Refresh token | `sessionStorage` với key riêng, nhưng **KHÔNG commit, KHÔNG log, KHÔNG share origin khác** | Không | Đóng tab / reload | Refresh token dài hạn nên có rủi ro hơn. Nếu reload tab làm mất refresh, user phải đăng nhập lại — chấp nhận được. |
| Session id | in-memory React state | — | reload | Dùng để hiển thị "đăng xuất thiết bị này". |

**Lý do KHÔNG dùng `localStorage`:**

- `localStorage` tồn tại qua nhiều tab, qua browser restart, đọc được bởi bất kỳ script
  nào chạy cùng origin (XSS amplification). Access token < 15 phút và chỉ dùng Bearer
  header — không cần persist. Refresh token dài hạn nhưng rủi ro tương tự.
- Yêu cầu của dự án ghi rõ "Web không lưu token trong localStorage" — đặc tả
  S01 mục "Ghi tài liệu quản lý token". Tài liệu này làm rõ: cả access và refresh đều
  không dùng `localStorage`.

**Lý do KHÔNG dùng cookie httpOnly trong đợt này:**

- Web hiện chỉ chạy ở `localhost:5173`, CORS whitelist. Không có subdomain chung
  (`*.dcms.local`) để cần `SameSite=Strict` + `Domain=.dcms.local`.
- Cookie httpOnly chỉ thật sự cần khi có nhiều origin cùng chia sẻ auth — đợt này
  chỉ có 1 web origin.
- Nếu tương lai mở rộng thành `web.dcms.vn` + `api.dcms.vn`, chuyển sang:
  - Backend set `Set-Cookie: refresh=...; HttpOnly; Secure; SameSite=Strict; Path=/api/auth`
  - Web không đọc refresh; browser tự gửi kèm fetch.
  - Access token vẫn giữ in-memory (Bearer header) để tránh CSRF cho endpoint business.
  - Endpoint `/api/auth/refresh` cần CSRF token (cookie `XSRF-TOKEN` riêng, JS đọc được).

**XSS mitigation bổ sung:**

- Ant Design đã sanitize content mặc định. Không dùng `dangerouslySetInnerHTML`.
- CSP: hiện đợt này chưa bật header CSP. Khi deploy production cần thêm
  `Content-Security-Policy: default-src 'self'; connect-src 'self' http://localhost:5080`.
  Ghi nhận trong backlog triển khai sau đợt này.

## 3. Lưu trữ token — Mobile (Expo iOS/Android/Web)

| Khoá | Native (iOS/Android) | Expo Web | Lý do |
| --- | --- | --- | --- |
| Access token | `expo-secure-store` (Keychain/Keystore) | `sessionStorage` (giống web) | SecureStore dùng Keychain iOS / Keystore Android, có encrypt + biometric option. Cùng vì lý do "đóng tab cần đăng nhập lại" trên web. |
| Refresh token | `expo-secure-store` với key riêng, dùng option `requireAuthentication: false` (để không ép Face ID mỗi lần refresh trong background) | `sessionStorage` với key riêng | Tách key với access để có thể rotate từng cái độc lập. |
| Session id | in-memory React state | in-memory React state | — |

**Lý do KHÔNG dùng `AsyncStorage`:**

- `AsyncStorage` lưu plaintext dưới dạng file trong app sandbox, không mã hoá. Mobile
  có thể bị root/jailbreak — kẻ tấn công truy cập được token plaintext.
- Expo đã cung cấp `expo-secure-store` chính thức cho lưu trữ nhạy cảm. Đặc tả
  MOBILE-SCAFFOLD.md đã ghi: "Native token storage uses Expo SecureStore".

**Lý do KHÔNG dùng cookie httpOnly trên native:**

- Native không dùng cookie cho API backend (backend trả JSON, không set-cookie).
- Nếu sau này dùng WebView nhúng `web.dcms.vn` thì cần xét lại; đợt này không có.

**Lý do KHÔNG dùng cookie httpOnly trên Expo Web:**

- Expo Web thực chất là React chạy trong trình duyệt. Cùng ràng buộc với Web ở mục 2.

**Expo SecureStore — giới hạn & fallback:**

- iOS 11+ trở lên; Android 5+ trở lên. Đợt này chấp nhận nếu thiết bị cũ thì refresh
  rotation sẽ lỗi → user đăng nhập lại, không crash app.
- SecureStore có giới hạn kích thước ~2 KB/entry — đủ cho JWT 256 byte và refresh 64 byte.
- Khi SecureStore lỗi (private mode trên web, SecureStore exception), SessionProvider
  coi như "chưa có phiên" và hiển thị nút "Đăng nhập" — không crash, không loop.

## 4. Luồng refresh — Web

```text
1. User đăng nhập
   POST /api/auth/sign-in { userName, password }
     -> 200 { accessToken (15 phút), refreshToken (30 ngày), sessionId, user }

2. Web lưu: accessToken + refreshToken vào sessionStorage, sessionId vào state.

3. Trước khi access hết hạn (timer chạy mỗi 30s kiểm tra expiresAt - now < 60s):
   POST /api/auth/refresh { refreshToken }
     -> 200 { accessToken (mới), refreshToken (mới, rotation), sessionId }
   Web xoá token cũ, lưu token mới.

4. Nếu /refresh trả 401 (refresh cũ bị reuse → toàn bộ chain bị thu hồi):
   Web xoá cả 2 token, đăng xuất, hiển thị MSG07 "Phiên hết hạn, vui lòng đăng nhập lại".
```

**Lưu ý: KHÔNG để 2 tab cùng refresh đồng thời.** Dùng mutex trong AuthContext (in-flight
promise) để tránh rotation xảy ra hai lần trong cùng một tick.

## 5. Luồng refresh — Mobile

```text
1. User đăng nhập
   POST /api/auth/sign-in { userName, password }
     -> 200 { accessToken, refreshToken, sessionId, user }

2. Mobile lưu: accessToken + refreshToken vào SecureStore (tách key), sessionId vào state.

3. App foreground / 60s trước khi access hết hạn / 401 từ API:
   POST /api/auth/refresh { refreshToken }
     -> 200 { accessToken, refreshToken (mới), sessionId }
   Mobile xoá token cũ trong SecureStore, lưu token mới.

4. Nếu /refresh trả 401: xoá cả 2, set state = anonymous, navigate về /sign-in.
```

**Khác biệt với Web:**

- Mobile chỉ có 1 instance app nên không cần mutex giữa các tab. Nhưng vẫn cần
  tránh 2 request refresh chồng nhau (foreground + 401 trigger) — dùng `inFlightRef`
  trong SessionProvider (đã có sẵn pattern cho /me, tái sử dụng cho /refresh).
- Khi app bị kill và mở lại, SecureStore vẫn giữ refresh → hydrate gọi /me với access
  hết hạn → 401 → tự /refresh. Nếu /refresh cũng 401 → anonymous.

## 6. Đăng xuất mọi thiết bị

| Hành động | API | Kết quả |
| --- | --- | --- |
| Đăng xuất thiết bị hiện tại | `POST /api/auth/sign-out` (đã có) | Session hiện tại bị revoke. |
| Đăng xuất mọi thiết bị | `POST /api/auth/sign-out-all` (mới) | Tất cả session active + refresh bị revoke. Access token hiện tại hết hạn tự nhiên sau ≤ 15 phút. Web/Mobile client hiện tại cũng bị 401 ở request tiếp theo vì refresh bị thu hồi. |
| Liệt kê phiên đang hoạt động | `GET /api/auth/sessions` (mới, tương lai) | Trả về list `{ sessionId, deviceLabel, lastSeenAt, currentSession }` — dùng để user quản lý. **Hoãn UI** ở đợt này, chỉ cần endpoint. |

**Lý do có "đăng xuất mọi thiết bị":**

- S01 mục "Identity + JWT: ... logout phiên thiết bị hiện tại" — hiểu là đăng xuất trên
  thiết bị user đang dùng (đã có `sign-out`). Bổ sung `sign-out-all` cho trường hợp
  user mất máy và muốn đăng xuất khỏi tất cả thiết bị khác.
- Tương đương với "Sign out all other sessions" phổ biến trong các sản phẩm SaaS.

**Audit:**

- `ACCOUNT_AUDIT` thêm event `SIGN_OUT_ALL` với target là account_id (không phải
  session_id cụ thể), reason do user nhập.

## 7. Chính sách cookie/CSRF — quyết định tương lai

| Kịch bản | Áp dụng cookie httpOnly? | CSRF token? | Refresh token lưu ở? |
| --- | --- | --- | --- |
| Hiện tại (localhost:5173, chỉ 1 origin) | **Không** | Không | in-memory/SecureStore |
| Sau này có 2 origin cùng domain (`web.dcms.vn` + `admin.dcms.vn`) | **Có** | Có | cookie httpOnly + `XSRF-TOKEN` riêng |
| Mobile native | Không (không dùng cookie) | Không (Bearer header) | SecureStore |
| Expo Web | Không (cùng ràng buộc với web ở mục 2) | Không | sessionStorage |

**Cách chuyển sang cookie httpOnly nếu cần:**

1. Backend `POST /api/auth/sign-in` set cookie `refresh=...; HttpOnly; Secure; SameSite=Strict;
   Path=/api/auth`. Response body vẫn trả `accessToken` để Web đưa vào Bearer header.
2. Frontend không đọc cookie refresh; `POST /api/auth/refresh` tự browser gửi kèm.
3. Backend thêm middleware CSRF: cookie `XSRF-TOKEN` (không HttpOnly) chứa random, header
   `X-XSRF-TOKEN` bắt buộc cho `POST /api/auth/refresh` và mọi state-changing.
4. Mobile giữ nguyên: vẫn nhận `refreshToken` trong JSON, lưu SecureStore. Backend có thể
   set thêm cookie để tương thích với Web sau này, Mobile bỏ qua.

**Tại sao chưa áp dụng ngay:**

- Backend hiện whitelist 2 origin localhost. Mở rộng sang production cần chính sách
  domain chung. Khi đó team sẽ cập nhật tài liệu này và chuyển đổi trong 1 đợt triển
  khai riêng.
- Thêm cookie + CSRF là thay đổi lớn cần test cross-origin kỹ. Tránh làm chung với
  refresh rotation để cô lập rủi ro.

## 8. Thay đổi backend cần thêm

| Endpoint mới | Method | Mục đích | Auth | Response |
| --- | --- | --- | --- | --- |
| `/api/auth/refresh` | POST | Đổi refresh → access mới + rotation | Public (Bearer refresh) | `200 { accessToken, refreshToken, sessionId, expiresAt }` |
| `/api/auth/sign-out-all` | POST | Thu hồi tất cả session + refresh | Bearer | `204 No Content` |
| `/api/auth/sessions` | GET | Liệt kê session đang active (cho UI tương lai) | Bearer | `200 [{ sessionId, deviceLabel, lastSeenAt, currentSession }]` |

| Bảng/field mới | Mục đích |
| --- | --- |
| `AUTH_REFRESH_TOKEN` (mới) | `id, session_id FK, account_id FK, token_hash (sha256), device_label, issued_at, expires_at, revoked_at, replaced_by_id, status (ACTIVE/REVOKED)` |
| `AUTH_SESSION.device_label` (mới) | Label client gửi lên lúc sign-in (vd: "iPhone Safari", "Web Chrome") |
| `AUTH_SESSION.last_seen_at` (mới) | Cập nhật mỗi request thành công, dùng cho "đăng nhập 3 ngày trước" |
| `ACCOUNT_AUDIT` thêm event `REFRESH`, `SIGN_OUT_ALL` | Theo dõi lịch sử |

**Migration mới: `RefreshTokensAndSessions`** áp dụng sau migration
`AccountsAndSessions` hiện có. Phải review trước khi apply, không recreate migration cũ.

## 9. Thay đổi client

**Web (`web/src/features/authentication/`):**

- `AuthContext.tsx` thêm `refresh()` tự động trước khi access hết hạn, dùng
  `expiresAt - now < 60s` làm ngưỡng. Mutex với `inFlightRef` để tránh double refresh.
- `AuthContext.tsx` thêm `signOutAll()` gọi `POST /api/auth/sign-out-all`.
- `App.tsx` thêm nút "Đăng xuất khỏi mọi thiết bị" trong menu account.
- `services/api.ts` thêm `api.refresh()` không gắn Authorization cũ (vì có thể đang
  hết hạn) — chỉ gắn `Authorization: Bearer <refreshToken>`.

**Mobile (`mobile/src/features/auth/session/`):**

- `SessionProvider.tsx` thêm `refreshSession()` tự động dựa trên `expiresAt`.
- `token-storage.ts` thêm `getRefresh`/`setRefresh`/`clearRefresh` (key riêng).
- Thêm `useUnsavedChanges()` cho mỗi form có dirty state (AccountEditor, PatientLink,
  v.v.) — khi sign-out được kích hoạt, confirm dialog hỏi "Có thay đổi chưa lưu".
- `useSignOut.ts` không tự chặn — chính sách là form tự phát hiện dirty.

**Cảnh báo thay đổi chưa lưu khi đăng xuất:**

- Web: `App.tsx` `signOut()` kiểm tra `formRegistry.getDirty()`. Nếu có, hỏi xác nhận
  qua Ant Design `Modal.confirm`.
- Mobile: `(account)` layout đăng ký một `UnsavedChangesContext`; mỗi form đánh dấu dirty
  khi thay đổi input. Khi user nhấn "Đăng xuất", layout gọi `Alert.alert` với 2 lựa chọn.

## 10. Tổng kết quyết định

| Quyết định | Lý do |
| --- | --- |
| Access token: JWT 15 phút, in-memory (web) / SecureStore (mobile) | Ngắn hạn, hết hạn tự nhiên giảm rủi ro leak. |
| Refresh token: opaque, 30 ngày, lưu hash trong DB | Dài hạn, có thể thu hồi, không JWT để tránh trust claim. |
| Refresh rotation + reuse detection | Phát hiện token bị đánh cắp. |
| Web: `sessionStorage` cho cả 2 token | Đóng tab = đăng nhập lại; không `localStorage` theo yêu cầu. |
| Mobile: SecureStore cho cả 2 token | Mã hoá tại rest, keychain/keystore. |
| Không cookie/CSRF trong đợt này | Chỉ có 1 origin; CORS whitelist. Tương lai có thể chuyển. |
| Endpoint `/sign-out-all` | Cho phép user đăng xuất mọi thiết bị khi mất máy. |
| Audit thêm `REFRESH`, `SIGN_OUT_ALL` | Truy vết lịch sử phiên. |

## Tham chiếu

- [UC01-UC31.md](UC01-UC31.md) — triển khai UC-01 + UC-31 hiện tại.
- [RUN-ACCOUNTS.vi.md](RUN-ACCOUNTS.vi.md) — hướng dẫn chạy thử.
- [ERD 37 bảng](../architecture/DCMS_Logical_ERD_37_Tables.mmd) — `AUTH_SESSION` được tham chiếu; phần mở rộng `AUTH_REFRESH_TOKEN` là chi tiết giao thức xác thực.
- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- [Refresh token rotation best practices (OWASP)](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#token-binding-to-the-client).
- [Expo SecureStore](https://docs.expo.dev/versions/latest/sdk/securestore/).
