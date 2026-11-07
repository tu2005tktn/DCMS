export type MedicineOption = {
  id: string;
  code: string;
  name: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  baseUnit: string;
  route: string;
  packaging?: string;
  contraindications?: string;
  allergyWarnings?: string;
  defaultInstructions?: string;
  referencePrice?: number;
};

export type PrescriptionStatus = 'Draft' | 'Finalized' | 'Superseded';

export type PrescriptionLine = {
  id: string;
  prescriptionId: string;
  visitId: string;
  patientId: number;
  branchId: number;
  prescribedByDoctorId: number;
  doctorName?: string;
  medicineId: string;
  medicineCode: string;
  medicineName: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  unit: string;
  route: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: string;
  days: number;
  quantity: number;
  instructions: string;
  status: PrescriptionStatus;
  finalizedAt?: string;
  version: number;
};

export type Prescription = {
  id: string;
  visitId: string;
  patientId: number;
  branchId: number;
  prescribedByDoctorId: number;
  doctorName?: string;
  revision: number;
  status: PrescriptionStatus;
  generalAdvice: string;
  diagnosis?: string;
  correctionReason?: string;
  supersedesId?: string;
  finalizedAt?: string;
  finalizedByName?: string;
  verificationCode?: string;
  version: number;
  lines: PrescriptionLine[];
};

export type PrescriptionSummary = {
  id: string;
  revision: number;
  status: PrescriptionStatus;
  doctorName?: string;
  createdAt: string;
  finalizedAt?: string;
  correctionReason?: string;
  lineCount: number;
};

export type VisitPrescription = { current: Prescription | null; history: PrescriptionSummary[] };

export type ApplyTemplateResult = { prescription: Prescription; skippedLines: number };

export type PrescriptionTemplateLine = {
  id: string;
  templateId: string;
  medicineId: string;
  medicineCode: string;
  medicineName: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  unit: string;
  route: string;
  morningDose: string;
  noonDose: string;
  afternoonDose: string;
  eveningDose: string;
  mealTiming: string;
  days: number;
  quantity: number;
  instructions: string;
  sortOrder: number;
};

export type PrescriptionTemplate = {
  id: string;
  code: string;
  name: string;
  diagnosisKind?: string;
  description?: string;
  isActive: boolean;
  version: number;
  lines: PrescriptionTemplateLine[];
};

export type ClinicalAllergyWarning = {
  medicineName: string;
  activeIngredient?: string;
  warningType: 'Allergy' | 'DuplicateIngredient' | 'Contraindication';
  message: string;
};

export type PrescriptionPrintItem = {
  index: number;
  medicineName: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  quantity: number;
  quantityInWords: string;
  unit: string;
  route: string;
  dosageInstruction: string;
  instructions: string;
};

export type PrescriptionPrintDto = {
  prescriptionId: string;
  visitId: string;
  revision: number;
  isDraft: boolean;
  branchName: string;
  branchAddress?: string;
  branchPhone?: string;
  logoDataUrl?: string;
  doctorName: string;
  doctorLicenseNumber?: string;
  patientCode: string;
  patientName: string;
  birthYear?: number;
  gender?: string;
  address?: string;
  diagnosis: string;
  prescribedAt: string;
  items: PrescriptionPrintItem[];
  generalAdvice: string;
  verificationCode?: string;
  verificationQrData?: string;
};

export const mealLabel = (value: string) => (value === 'BeforeMeal' ? 'Trước ăn' : value === 'WithMeal' ? 'Trong bữa ăn' : 'Sau ăn');

export const doseSummary = (x: { morningDose: string; noonDose: string; afternoonDose: string; eveningDose: string }) =>
  [
    x.morningDose !== '0' ? `Sáng ${x.morningDose}` : null,
    x.noonDose !== '0' ? `Trưa ${x.noonDose}` : null,
    x.afternoonDose !== '0' ? `Chiều ${x.afternoonDose}` : null,
    x.eveningDose !== '0' ? `Tối ${x.eveningDose}` : null,
  ].filter(Boolean).join(', ') || 'Theo chỉ dẫn';
