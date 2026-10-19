export type BranchOption = { id: number; name: string };
export type DentistOption = { id: number; branchId: number; name: string; canDentist: boolean };
export type ServiceOption = { id: number; branchId: number; name: string; publicDurationMinutes: number };
export type BookingOptions = { branches: BranchOption[]; employees: DentistOption[]; services: ServiceOption[] };
export type AvailabilitySlot = { start: string; end: string; chairId: number; dentistId: number; scheduleVersion: number; assistantScheduleVersion: number | null; bookingSettingsVersion?: number };
export type AvailabilityResult = { timeZone: string; date: string; slots: AvailabilitySlot[] };
export type BookingResult = { appointmentId: string; code: string; version: number; status: string; startAt: string; endAt: string; notification: { code: string; message: string } };
export type BookingRequest = {
  branchId: number;
  booker: { name: string; phone: string | null; email: string | null };
  attendee: { patientId: number | null; name: string; birthDate: string | null };
  attendeeRelation: 'Self' | 'Relative';
  kind: 'Standard'; source: 'App'; serviceId: number; reason: string | null;
  dentistId: number; assistantId: null; chairId: null; startAt: string;
  durationMinutes: number; followUpId: null; exceptionReason: null;
  requestKey: string; scheduleVersion: number; assistantScheduleVersion: null; bookingSettingsVersion?: number;
};
