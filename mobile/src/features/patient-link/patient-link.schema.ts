import type { SubmitLinkRequest } from './patient-link.types';

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
function toIsoDate(value: string) {
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
