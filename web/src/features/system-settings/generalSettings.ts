// S58 Thiết lập chung. Contract: docs/specs/S58-GENERAL-SETTINGS.md.
export type RequiredFields = { profileDate: boolean; gender: boolean; birthDate: boolean; address: boolean; phone: boolean; customerGroup: boolean; source: boolean };
export type PatientCodeMode = 'AutoWithManualOverride' | 'AutoOnly' | 'ManualOnly';
export type PatientCodeSettings = { mode: PatientCodeMode; prefix: string };
export type ReceiptPrint = Record<typeof receiptFields[number][0], boolean>;
export type GeneralSettings = { staffIdleTimeoutMinutes: number; requiredFields: RequiredFields; patientCode: PatientCodeSettings; receiptPrint: ReceiptPrint };
export type GeneralSettingsDto = { version: number; effectiveAt: string | null; actorName: string | null; reason: string | null; settings: GeneralSettings };
export type SettingsChange = { field: string; label: string; before: string | null; after: string | null };
export type GeneralPreview = { currentVersion: number; stale: boolean; changes: SettingsChange[]; warnings: string[]; missingPatients: { field: string; label: string; count: number }[] };
export type GeneralHistoryItem = { version: number; effectiveAt: string; actorName: string; reason: string; restoredFromVersion: number | null; settings: GeneralSettings };
export type GeneralHistoryPage = { items: GeneralHistoryItem[]; total: number; page: number; pageSize: number };
export type SettingsCapabilities = { sections: { key: string; canManage: boolean }[] };
export type PatientFormPolicy = { version: number; requiredFields: RequiredFields; patientCode: PatientCodeSettings };
export type PatientCodeSequence = { prefix: string; nextNumber: number; nextCode: string; lastIssued: number | null; highestExisting: number | null };
export type PatientCodeSequencePreview = { stale: boolean; allowed: boolean; nextCode: string; issues: string[]; skippedCodes: number };

export const idleChoices = [5, 10, 20, 30, 60, 90, 120];
// [field, label, available on the patient master today] — all available since S58 đợt 3.
export const requiredFieldOptions: [keyof RequiredFields, string, boolean][] = [
  ['profileDate', 'Ngày lập hồ sơ', true], ['gender', 'Giới tính', true], ['birthDate', 'Ngày sinh', true], ['address', 'Địa chỉ', true],
  ['phone', 'Số điện thoại', true], ['customerGroup', 'Phân nhóm khách hàng', true], ['source', 'Nguồn', true],
];
export const codeModes: { value: PatientCodeMode; label: string; help: string }[] = [
  { value: 'AutoWithManualOverride', label: 'Tự động, cho nhập tay', help: 'Để trống thì hệ thống cấp mã; nhập tay thì kiểm tra trùng.' },
  { value: 'AutoOnly', label: 'Chỉ tự động', help: 'Ô mã bị khóa; hệ thống luôn cấp mã mới.' },
  { value: 'ManualOnly', label: 'Chỉ nhập tay', help: 'Bắt buộc nhập mã khi tạo hồ sơ; trùng mã bị từ chối.' },
];
export const receiptFields = [
  ['internalUseNote', 'Ghi chú dùng nội bộ'], ['showReceiptNumber', 'Hiện mã phiếu thu'], ['patientAddress', 'Địa chỉ bệnh nhân'],
  ['patientPhone', 'Số điện thoại bệnh nhân'], ['paymentMethod', 'Phương thức thanh toán'], ['dentistAssistant', 'Bác sĩ, trợ thủ'],
  ['totalFee', 'Tổng phí'], ['discount', 'Giảm giá'], ['previouslyPaid', 'Đã thanh toán'], ['remaining', 'Còn lại'],
  ['patientTotalOutstanding', 'Tổng công nợ của bệnh nhân'], ['signatureLines', 'Dòng ký tên'],
  ['defaultPayerToPatient', 'Mặc định người nộp là bệnh nhân'], ['defaultCollectorToActor', 'Mặc định người thu là người thao tác'],
  ['paymentNote', 'Ghi chú thanh toán'],
] as const;

export const codeExample = (prefix: string) => `${prefix.trim().toUpperCase()}000001`;
