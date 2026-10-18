import type { Gender, OwnPatientRecord, SubmitLinkRequest, UpdateOwnRecord } from './patient-link.types';

// Mirrors server validation ([PROPOSED] limits in docs/implementation/UC36-S07-S08.md); the server re-validates.
export type LinkForm = { fullName: string; birthDate: string; patientCode: string; matchingInformation: string; confirmSelf: boolean };
export type LinkFormErrors = Partial<Record<keyof LinkForm, string>>;
export const emptyLinkForm: LinkForm = { fullName: '', birthDate: '', patientCode: '', matchingInformation: '', confirmSelf: false };
export const limits = { fullName: 160, patientCode: 40, matchingInformation: 1000 } as const;

const required = (field: string) => `Vui lòng nhập ${field}.`;
const tooLong = (field: string, max: number) => `${field} không được vượt quá ${max} ký tự.`;
const invalid = (field: string) => `${field} không đúng định dạng.`;

// Users type DD-MM-YYYY; dashes are inserted automatically while typing digits.
export function formatBirthDateInput(value: string) {
  const digits = value.replace(/\D/g, '').slice(0, 8);
  return [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4)].filter(Boolean).join('-');
}

// DD-MM-YYYY → ISO YYYY-MM-DD for the API, or null when not a real date within the allowed range.
export function toIsoDate(value: string) {
  const match = /^(\d{2})-(\d{2})-(\d{4})$/.exec(value);
  if (!match) return null;
  const iso = `${match[3]}-${match[2]}-${match[1]}`;
  const date = new Date(`${iso}T00:00:00Z`);
  if (Number.isNaN(date.getTime()) || date.toISOString().slice(0, 10) !== iso) return null;
  // Clinic dates use Asia/Ho_Chi_Minh (UTC+7).
  const today = new Date(Date.now() + 7 * 3_600_000).toISOString().slice(0, 10);
  return iso >= '1900-01-01' && iso <= today ? iso : null;
}

export function validateLinkForm(form: LinkForm): { errors: LinkFormErrors; request?: SubmitLinkRequest } {
  const errors: LinkFormErrors = {};
  const fullName = form.fullName.trim();
  const birthDate = form.birthDate.trim();
  const patientCode = form.patientCode.trim();
  const matching = form.matchingInformation.trim();
  if (!fullName) errors.fullName = required('Họ tên');
  else if (fullName.length > limits.fullName) errors.fullName = tooLong('Họ tên', limits.fullName);
  const isoBirthDate = birthDate ? toIsoDate(birthDate) : null;
  if (birthDate && !isoBirthDate) errors.birthDate = invalid('Ngày sinh');
  if (patientCode.length > limits.patientCode) errors.patientCode = tooLong('Mã hồ sơ', limits.patientCode);
  else if (patientCode && !/^[A-Za-z0-9_-]+$/.test(patientCode)) errors.patientCode = invalid('Mã hồ sơ');
  if (matching.length > limits.matchingInformation) errors.matchingInformation = tooLong('Thông tin đối chiếu', limits.matchingInformation);
  if (!form.confirmSelf) errors.confirmSelf = required('xác nhận đây là hồ sơ của chính bạn');
  if (Object.keys(errors).length) return { errors };
  return { errors, request: { fullName, birthDate: isoBirthDate, patientCode: patientCode || null, matchingInformation: matching || null, confirmSelf: true } };
}

// "Hồ sơ của tôi": same limits as the staff form (UC-38); the server re-validates and keeps the patient code read-only.
export type RecordForm = {
  fullName: string; birthDate: string; gender: Gender | null; phone: string; address: string;
  guardianName: string; guardianRelationship: string; guardianPhone: string;
};
export type RecordFormErrors = Partial<Record<keyof RecordForm, string>>;
export const isoToDisplay = (value: string | null) => value ? value.split('-').reverse().join('-') : '';

export function recordToForm(record: OwnPatientRecord): RecordForm {
  return {
    fullName: record.fullName, birthDate: isoToDisplay(record.birthDate), gender: record.gender, phone: record.phone ?? '',
    address: record.address ?? '', guardianName: record.guardianName ?? '', guardianRelationship: record.guardianRelationship ?? '',
    guardianPhone: record.guardianPhone ?? '',
  };
}

const phonePattern = /^\+?[0-9 ().-]+$/;
function checkPhone(value: string, field: string) {
  if (!value) return undefined;
  if (value.length > 20) return tooLong(field, 20);
  return phonePattern.test(value) && value.replace(/\D/g, '').length >= 8 ? undefined : invalid(field);
}

export function validateRecordForm(form: RecordForm, version: number): { errors: RecordFormErrors; request?: UpdateOwnRecord } {
  const errors: RecordFormErrors = {};
  const value = (key: Exclude<keyof RecordForm, 'gender'>) => form[key].trim();
  const fullName = value('fullName');
  if (!fullName) errors.fullName = required('Họ tên');
  else if (fullName.length > 100) errors.fullName = tooLong('Họ tên', 100);
  const birthDate = value('birthDate');
  const isoBirthDate = birthDate ? toIsoDate(birthDate) : null;
  if (birthDate && !isoBirthDate) errors.birthDate = invalid('Ngày sinh');
  errors.phone = checkPhone(value('phone'), 'Số điện thoại');
  if (value('address').length > 255) errors.address = tooLong('Địa chỉ', 255);
  const guardianName = value('guardianName');
  const relationship = value('guardianRelationship');
  const guardianPhone = value('guardianPhone');
  if (guardianName.length > 160) errors.guardianName = tooLong('Tên người giám hộ', 160);
  else if (!guardianName && (relationship || guardianPhone)) errors.guardianName = required('tên người giám hộ');
  if (relationship.length > 40) errors.guardianRelationship = tooLong('Quan hệ với bệnh nhân', 40);
  else if (guardianName && !relationship) errors.guardianRelationship = required('quan hệ với bệnh nhân');
  errors.guardianPhone = checkPhone(guardianPhone, 'Số điện thoại người giám hộ');
  for (const key of Object.keys(errors) as (keyof RecordForm)[]) if (!errors[key]) delete errors[key];
  if (Object.keys(errors).length) return { errors };
  const blank = (text: string) => text || null;
  return { errors, request: {
    version, fullName, birthDate: isoBirthDate, gender: form.gender, phone: blank(value('phone')), address: blank(value('address')),
    guardianName: blank(guardianName), guardianRelationship: blank(relationship), guardianPhone: blank(guardianPhone),
  } };
}
