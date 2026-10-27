// Contract: docs/implementation/UC05-06-S14-S16.md §4 (shared with TV2/TV3/TV5).
export type SystemMessage = { code: string; message: string };
export type AppointmentStatus = 'Confirmed' | 'CheckedIn' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow';
export type AppointmentScope = 'UPCOMING' | 'HISTORY';
export type AccessSource = 'BOOKED_BY_ME' | 'MY_OWN' | 'BOTH';
export type IneligibleReason = 'WITHIN_12H' | 'NOT_CONFIRMED' | 'STARTED' | 'NOT_OWNER';

export type AppointmentCard = {
  code: string; start: string; end: string; durationMinutes: number; dentistName: string;
  attendeeName: string; attendeeKind: 'SELF' | 'RELATIVE'; status: AppointmentStatus; access: AccessSource; version: number;
};
export type AppointmentPage = { items: AppointmentCard[]; total: number; page: number; pageSize: number };

export type SupportRequestStatus = 'Pending' | 'Resolved' | 'Rejected' | 'Withdrawn';
export type SupportRequestKind = 'RESCHEDULE' | 'CANCEL' | 'OTHER';
export type SupportRequest = {
  id: number | string; appointmentCode: string; kind: SupportRequestKind; reason: string; status: SupportRequestStatus;
  createdAt: string; decidedAt: string | null; publicNote: string | null; version: number;
  proposedStartAt?: string | null;
};
export type AppointmentChange = {
  action: 'RESCHEDULE' | 'CANCEL'; actorKind: 'SELF' | 'STAFF'; oldStart: string; newStart: string | null; reason: string; occurredAt: string;
};
export type AppointmentDetail = AppointmentCard & {
  branchId: number; branchName: string; dentistId: number; assistantId?: number | null; serviceId: number; serviceName: string;
  bookerName: string; bookerContactMasked: string; followUp: { id: number; status: string } | null;
  eligibility: { canSelfService: boolean; hoursRemaining: number; reasonCode: IneligibleReason | null; canRequestSupport: boolean };
  supportRequests: SupportRequest[]; history: AppointmentChange[]; clinicContact: { phone: string; hours: string };
};

// Same shape as Tư's GET /api/availability (S09).
export type AvailabilitySlot = {
  start: string; end: string; chairId: number; dentistId: number; assistantId: number | null;
  scheduleVersion: number; assistantScheduleVersion: number | null; bookingSettingsVersion?: number;
};
export type RescheduleDentist = { id: number; branchId: number; name: string };

export type AppointmentQuery = { scope: AppointmentScope; status: AppointmentStatus | 'ALL'; from: string | null; to: string | null; page: number; pageSize: number };
export type RescheduleCommand = {
  expectedVersion: number; requestKey: string; dentistId: number; newStart: string; chairId: number; scheduleVersion: number;
  assistantScheduleVersion: number | null; reason: string; bookingSettingsVersion?: number;
};
export type CancelCommand = { expectedVersion: number; requestKey: string; reason: string };
export type SupportCommand = {
  requestKey: string; kind: SupportRequestKind; reason: string;
  newStartAt?: string | null; dentistId?: number | null; chairId?: number | null;
};
export type AppointmentChangeResult = { appointment: AppointmentDetail; notification: SystemMessage };
export type SupportResult = { request: SupportRequest; notification: SystemMessage };
