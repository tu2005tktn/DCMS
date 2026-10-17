import { authedRequest, request } from '@/services/api/http-client';
import type { AvailabilityResult, BookingOptions, BookingRequest, BookingResult } from './booking.types';

async function send(path: string, token?: string, options?: RequestInit): Promise<Response> {
  return token ? authedRequest(path, token, options) : request(path, options);
}

export const bookingApi = {
  async options(token?: string): Promise<BookingOptions> {
    return (await send('/api/availability/options', token)).json() as Promise<BookingOptions>;
  },
  async slots(branchId: number, dentistId: number, serviceId: number, date: string, durationMinutes: number,
    token?: string): Promise<AvailabilityResult> {
    const query = new URLSearchParams({ branchId: String(branchId), dentistId: String(dentistId),
      serviceId: String(serviceId), date, durationMinutes: String(durationMinutes) });
    return (await send(`/api/availability?${query.toString()}`, token)).json() as Promise<AvailabilityResult>;
  },
  async create(payload: BookingRequest, token?: string): Promise<BookingResult> {
    return (await send('/api/appointments', token, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload) })).json() as Promise<BookingResult>;
  },
};
