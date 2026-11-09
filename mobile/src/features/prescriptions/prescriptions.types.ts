// Contract: docs/specs/PRESCRIPTION-MANAGEMENT.md §5.3 — chỉ đơn Finalized của hồ sơ đã liên kết Verified.

export type PatientPrescriptionMedicine = {
  medicineName: string;
  strength: string | null;
  dosageForm: string | null;
  quantity: number;
  unit: string;
  route: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: 'BeforeMeal' | 'AfterMeal' | 'WithMeal' | string;
  days: number;
  instructions: string;
};

export type PatientPrescription = {
  visitId: string;
  prescriptionId: string;
  revision: number;
  isCorrection: boolean;
  branchName: string;
  doctorName: string;
  diagnosis: string;
  prescribedAt: string;
  generalAdvice: string;
  externalPurchaseNotice: string;
  medicines: PatientPrescriptionMedicine[];
};

export type PrescriptionPrintItem = {
  index: number;
  medicineName: string;
  activeIngredient: string | null;
  strength: string | null;
  dosageForm: string | null;
  quantity: number;
  quantityInWords: string;
  unit: string;
  route: string;
  dosageInstruction: string;
  instructions: string;
};

export type PrescriptionPrint = {
  prescriptionId: string;
  visitId: string;
  revision: number;
  isDraft: boolean;
  branchName: string;
  branchAddress: string | null;
  branchPhone: string | null;
  logoDataUrl: string | null;
  doctorName: string;
  doctorLicenseNumber: string | null;
  patientCode: string;
  patientName: string;
  birthYear: number | null;
  gender: string | null;
  address: string | null;
  diagnosis: string;
  prescribedAt: string;
  items: PrescriptionPrintItem[];
  generalAdvice: string;
  verificationCode: string | null;
  verificationQrData: string | null;
};

export type PatientPrescriptionDetail = { prescription: PatientPrescription; print: PrescriptionPrint };

export type PatientProfilePrescriptionItem = {
  id: string; medicineId: string; medicineName: string; medicineCode: string;
  strength: string | null; dosageForm: string | null; unit: string; quantity: number; instructions: string;
};
export type PatientProfilePrescription = {
  id: string; patientId: number; branchId: number; branchName: string; prescribedOn: string;
  diagnosis: string; otherDiagnosis: string | null; advice: string | null; prescriberName: string; version: number;
  items: PatientProfilePrescriptionItem[];
  // docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md: thông tin phòng khám và bệnh nhân để hiển thị bố cục tờ đơn.
  prescriberEmployeeId: number; branchAddress: string | null; branchPhone: string | null; branchEmail: string | null;
  patientCode: string; patientName: string; patientBirthDate: string | null; patientGender: string | null;
};

export const genderLabel = (value: string | null) =>
  value === 'MALE' ? 'Nam' : value === 'FEMALE' ? 'Nữ' : value === 'OTHER' ? 'Khác' : value || '—';

export const mealLabel = (value: string) => (value === 'BeforeMeal' ? 'Trước ăn' : value === 'WithMeal' ? 'Trong bữa ăn' : 'Sau ăn');

export const doseSlots = (m: Pick<PatientPrescriptionMedicine, 'morningDose' | 'noonDose' | 'afternoonDose' | 'eveningDose'>) => [
  { key: 'morning', label: 'Sáng', value: m.morningDose },
  { key: 'noon', label: 'Trưa', value: m.noonDose },
  { key: 'afternoon', label: 'Chiều', value: m.afternoonDose },
  { key: 'evening', label: 'Tối', value: m.eveningDose },
];

export const formatDate = (value: string) =>
  new Date(value).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' });
