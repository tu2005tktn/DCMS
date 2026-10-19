/**
 * S26 — Notification DTOs (contract-first).
 *
 * Theo "Dùng mock DTO chung để làm song song, không đợi task khác hoàn thành mới bắt
 * đầu UI": file này định nghĩa DTO mà UI và backend (TV5) sẽ cùng dùng.
 *
 * Khi backend triển khai xong, UI chỉ cần bật `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=true`
 * để chuyển từ mock sang HTTP thật — shape response đã được chốt tại đây.
 *
 * Traceability:
 *   - S26: notification list của account hiện tại, deep link có kiểm quyền lại.
 *   - UC-05/06; GBR-007/012-015/031/073/075/077/078/081/123; MSG31-38/94/111/144; AT16.
 */

// ============================================================================
// Domain enums
// ============================================================================

/** Loại notification — KHÔNG chứa chẩn đoán (GBR-081). */
export type NotificationKind =
  | 'AppointmentBooked'
  | 'AppointmentRescheduled'
  | 'AppointmentCancelled'
  | 'ReminderUpcoming'
  | 'SupportRequestCreated'
  | 'SupportRequestUpdate'
  | 'NoShow'
  | 'ScheduleReduced';

/** Kênh dispatch. In-app authoritative; external tuỳ adapter/cấu hình. */
export type NotificationChannel = 'InApp' | 'Email' | 'Push' | 'Sms';

/** Trạng thái dispatch (theo dõi retry max 3 lần, backoff 1/5/15p). */
export type DispatchStatus =
  | 'Pending'
  | 'Sent'
  | 'Delivered'
  | 'Failed'
  | 'Retrying';

// ============================================================================
// DTOs
// ============================================================================

/** Một attempt gửi qua một channel. */
export interface NotificationAttemptDto {
  channel: NotificationChannel;
  status: DispatchStatus;
  attemptCount: number;
  lastAttemptAt: string | null;
  nextRetryAt: string | null;
  errorMessage: string | null;
}

/** Notification DTO (response shape chốt giữa mobile ↔ backend TV5). */
export interface NotificationDto {
  id: string;
  accountId: number | null;
  kind: NotificationKind;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
  appointmentId: string | null;
  requestId: string | null;
  deepLink: string | null;
  attempts: NotificationAttemptDto[];
  hasExternalFailure: boolean;
  dedupeKey: string;
}

// ============================================================================
// Query parameters / response envelopes
// ============================================================================

/** Query cho listForAccount. */
export interface ListNotificationsQuery {
  unreadOnly?: boolean;
  limit?: number;     // server clamp 1..100
  cursor?: string;    // pagination cursor (optional, dự phòng)
}

/** Page response — server trả về items + nextCursor. */
export interface ListNotificationsResult {
  items: NotificationDto[];
  nextCursor: string | null;
}

/** Mark-as-read request. */
export interface MarkReadRequest {
  notificationIds: string[];   // tối đa 100 theo tiêu chí nghiệm thu
  expectedAccountId: number;   // chống đánh dấu nhầm
}

/** Mark-as-read response. */
export interface MarkReadResult {
  marked: string[];            // ids đã mark thành công
  skipped: string[];           // ids không thuộc account / không tồn tại
}

/** Deep link permission check. */
export interface CheckDeepLinkRequest {
  deepLink: string;
}
export interface CheckDeepLinkResult {
  allowed: boolean;
  reason?: string;
}

/** Count unread cho badge. */
export interface UnreadCountResult {
  count: number;
}

// ============================================================================
// Error code mapping (S26)
// ============================================================================

/** Mã lỗi từ backend, dùng để UI quyết định retry / hiển thị. */
export type NotificationErrorCode =
  | 'UNAUTHORIZED'   // 401 — phiên hết hạn → sign out
  | 'FORBIDDEN'      // 403 — không thuộc account
  | 'NOT_FOUND'      // 404 — notification không tồn tại
  | 'STALE_VERSION'  // 409 — expectedVersion không khớp (MSG111)
  | 'BAD_REQUEST'    // 400 — payload sai
  | 'NETWORK'        // fetch fail / timeout
  | 'SERVER';        // 5xx — MSG144
