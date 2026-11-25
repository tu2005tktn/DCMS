import { ApiError } from '@/services/api/api-error';

// Carries the specified MSG code so screens can tell stale (MSG41), slot taken (MSG29) and access errors apart.
export class AppointmentApiError extends ApiError {
  constructor(message: string, status: number, public readonly code: string) { super(message, status); }
}
