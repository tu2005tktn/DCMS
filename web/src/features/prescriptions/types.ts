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

export type PrescriptionLine = {
  id: string;
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
  status: 'Draft' | 'Finalized';
  finalizedAt?: string;
  version: number;
};

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
  unit: string;
  route: string;
  dosageInstruction: string;
  fullInstruction: string;
};

export type PrescriptionPrintDto = {
  visitId: string;
  clinicName: string;
  branchName: string;
  branchAddress: string;
  branchPhone: string;
  logoUrl?: string;
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
  verificationQrData: string;
};
