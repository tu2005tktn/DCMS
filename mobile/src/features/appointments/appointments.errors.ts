import { ApiError } from '@/services/api/api-error';

// Carries the specified MSG code so screens can tell stale (MSG111), slot taken (MSG34) and access errors apart.
export class AppointmentApiError extends ApiError {
  constructor(message: string, status: number, public readonly code: string) { super(message, status); }
}
