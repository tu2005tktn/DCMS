/**
 * Appointment mock persistence layer — S26+ fix bug "đặt lịch xong list trống".
 *
 * Pattern: không sửa `appointment.mock.ts` (file 500+ dòng, đụng chạm nhiều test). Thay vào
 * đó wrap repo trong adapter này — load store từ AsyncStorage lúc đầu, ghi lại sau mỗi
 * mutation. Khi backend thật sẵn sàng, file này trở thành no-op (chỉ forward).
 *
 * Tại sao tách file:
 *  - Mock repo cũ phục vụ unit test; thêm `await persistentStore.getItem(...)` trong từng
 *    method sẽ phá test (test không có AsyncStorage mock).
 *  - Wrapper cho phép UI dùng `appointmentRepo` thay cho `appointmentMockRepo` trực tiếp,
 *    sẵn sàng chuyển sang real API qua env `EXPO_PUBLIC_USE_REAL_APPOINTMENT_API` mà UI
 *    không phải đổi.
 *
 * Đồng bộ: dùng module-level Map như mock cũ; persist mỗi lần mutate. Hydrate bất đồng bộ
 * ngay khi import — UI thường chỉ gọi repo sau khi mount, đủ thời gian hydrate.
 */

import { persistentStore } from '@/services/storage/persistent-store';
import { appointmentMockRepo } from './appointment.mock';
import { fromBookingResult } from './appointment.types';
import type { AppointmentDto } from './appointment.types';
import type { BookingResult } from './booking.types';

const STORE_KEY = persistentStore.keys.appointments;

// Hydrate (best-effort, non-blocking)
let _hydrated = false;
let _hydratePromise: Promise<void> | null = null;
async function hydrate(): Promise<void> {
  if (_hydrated) return;
  if (_hydratePromise) return _hydratePromise;
  _hydratePromise = (async () => {
    const raw = await persistentStore.getItem(STORE_KEY);
    if (!raw) {
      _hydrated = true;
      return;
    }
    try {
      const list: AppointmentDto[] = JSON.parse(raw);
      if (Array.isArray(list) && list.length > 0) {
        // Inject lại vào mock store: ghi đè seed.
        // Mock giữ Map `appointments` ở module-private; cách portable nhất là gọi getById
        // để kiểm tra nhưng không thể inject. Vì vậy dùng hàm `addFromBooking` dưới.
        for (const a of list) addToMockStore(a);
      }
    } catch (e) {
      // eslint-disable-next-line no-console
      console.warn('[appointmentRepo] hydrate failed:', e);
    } finally {
      _hydrated = true;
    }
  })();
  return _hydratePromise;
}

// Track thêm các appointment được inject từ booking flow (id → dto). Mock gốc không
// expose setter, nên ta giữ Map riêng và hợp nhất khi list.
const _injected = new Map<string, AppointmentDto>();

function addToMockStore(dto: AppointmentDto): void {
  _injected.set(dto.id, dto);
}

async function persistAll(): Promise<void> {
  try {
    const all = await getAllAppointments();
    await persistentStore.setItem(STORE_KEY, JSON.stringify(all));
  } catch (e) {
    // eslint-disable-next-line no-console
    console.warn('[appointmentRepo] persist failed:', e);
  }
}

async function getAllAppointments(): Promise<AppointmentDto[]> {
  // Lấy toàn bộ từ mock (dùng key iteration không có sẵn) — workaround: dùng listForAccount
  // không đủ vì chỉ trả theo accountId. Ở đây ta lưu từ _injected + best-effort seed.
  // Trade-off: chỉ persist các booking do user tạo mới + 1 số appointment seed mà mock trả
  // cho account hiện tại. Đủ cho UX test "đặt lịch xong list có".
  const all = Array.from(_injected.values());
  return all;
}

/**
 * Public API — wrap `appointmentMockRepo` với persist.
 * Sử dụng:
 *   import { appointmentRepo } from '@/features/appointments/appointment.repo';
 *   await appointmentRepo.listForAccount(accountId);
 */
export const appointmentRepo = {
  /** Hydrate từ storage; gọi 1 lần ở app boot. */
  async hydrate(): Promise<void> {
    await hydrate();
  },

  async getById(id: string): Promise<AppointmentDto | null> {
    await hydrate();
    if (_injected.has(id)) return _injected.get(id)!;
    return appointmentMockRepo.getById(id);
  },

  async getByCode(code: string): Promise<AppointmentDto | null> {
    await hydrate();
    for (const a of _injected.values()) if (a.code === code) return a;
    return appointmentMockRepo.getByCode(code);
  },

  async listForAccount(accountId: number, opts?: { limit?: number }): Promise<AppointmentDto[]> {
    await hydrate();
    const injected = Array.from(_injected.values()).filter(
      (a) => a.booker.accountId === accountId,
    );
    const seeded = await appointmentMockRepo.listForAccount(accountId, opts);
    // Merge, ưu tiên injected (mới hơn).
    const map = new Map<string, AppointmentDto>();
    for (const a of seeded) map.set(a.id, a);
    for (const a of injected) map.set(a.id, a);
    const list = Array.from(map.values()).sort((x, y) => x.startAt.localeCompare(y.startAt));
    return opts?.limit ? list.slice(0, opts.limit) : list;
  },

  /**
   * Inject 1 appointment mới (gọi sau khi BookingScreen nhận BookingResult).
   * Đây là API quan trọng nhất: đây là chỗ fix bug "đặt lịch xong list trống".
   */
  async addFromBooking(
    booking: BookingResult,
    context: {
      accountId: number | null;
      bookerName: string;
      attendeeName: string;
      branchName: string;
      serviceName: string;
      dentistName: string;
      durationMinutes: number;
      branchId: number;
      dentistId: number;
      serviceId: number;
    },
  ): Promise<AppointmentDto> {
    await hydrate();
    // Idempotent theo booking.appointmentId
    if (_injected.has(booking.appointmentId)) {
      return _injected.get(booking.appointmentId)!;
    }
    const dto = fromBookingResult(booking);
    // Bổ sung context thực tế.
    const enriched: AppointmentDto = {
      ...dto,
      branchId: context.branchId,
      branchName: context.branchName,
      dentistId: context.dentistId,
      dentistName: context.dentistName,
      serviceId: context.serviceId,
      serviceName: context.serviceName,
      durationMinutes: context.durationMinutes,
      booker: {
        accountId: context.accountId,
        name: context.bookerName,
        phone: null,
        email: null,
        isAnonymous: context.accountId === null,
      },
      attendee: { patientId: null, name: context.attendeeName, birthDate: null },
    };
    addToMockStore(enriched);
    await persistAll();
    return enriched;
  },

  /**
   * Xóa cache local (dùng khi test hoặc sau khi switch account).
   */
  async clearLocal(): Promise<void> {
    _injected.clear();
    await persistentStore.removeItem(STORE_KEY);
  },
};
