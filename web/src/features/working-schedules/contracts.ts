// Shared HTTP contract for S09, TV2/S11 and TV4/S15. Dates are clinic-local ISO dates.
export type Period = { day: number; startMinute: number; endMinute: number; kind: 'Work' | 'Break' | 'Block'; reason: string };
export type Schedule = { id: string | null; branchId: number; dentistId: number; weekStart: string; version: number; status: string; intervals: Period[]; publishedBy: number | null; publishedAt: string | null };
export type ScheduleCommand = Pick<Schedule, 'branchId' | 'dentistId' | 'weekStart' | 'intervals'> & { expectedVersion: number; requestKey: string };
export type Employee = { id: number; branchId: number; name: string; canDentist: boolean; canEdit: boolean; self: boolean };
export type Options = { branches: { id: number; name: string }[]; employees: Employee[]; services: { id: number; branchId: number; name: string; publicDurationMinutes: number }[]; chairs: { id: number; branchId: number; name: string }[] };
export type Affected = { id: string; code: string; start: string; end: string; attendee: string; status: string };
export type AvailabilityQuery = { branchId: number; dentistId: number; serviceId: number; date: string; durationMinutes: number; assistantId?: number };
export type Slot = { start: string; end: string; chairId: number; dentistId: number; assistantId: number | null; scheduleVersion: number; assistantScheduleVersion: number | null };
export type Availability = { timeZone: string; date: string; slots: Slot[] };
