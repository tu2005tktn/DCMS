// DTOs of /api/patients/me/visits* and /api/patients/me/documents* (docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §3).

export type OwnVisitItem = {
  visitId: string;
  arrivalAt: string;
  branchName: string;
  dentistName: string;
  visitStatus: string;
  completedAt: string | null;
  examinationVersion: number;
  diagnosisSummary: string | null;
  documentCount: number;
  hasPrescription: boolean;
};
export type OwnVisitPage = { items: OwnVisitItem[]; total: number; page: number; pageSize: number };

export type OwnFinding = { tooth: number | null; region: string | null; condition: string; surfaces: string[]; diagnoses: string[] };
export type OwnExaminationResult = {
  version: number;
  symptoms: string | null;
  findings: OwnFinding[];
  diagnoses: string[];
  conclusion: string | null;
  advice: string | null;
  disposition: string | null;
  returnNeed: string | null;
  referralDestination: string | null;
};
export type OwnInvestigation = {
  id: string; kind: string; name: string; region: string | null; state: string;
  resultText: string | null; interpretation: string | null; reviewedAt: string | null;
};
export type OwnVisitDetail = { visit: OwnVisitItem; examination: OwnExaminationResult; investigations: OwnInvestigation[] };

export type OwnDocument = {
  documentId: string; fileName: string; contentType: string; sizeBytes: number; documentType: string;
  source: string; sourceDate: string; visitId: string | null; dossierId: string | null; uploadedAt: string;
};
export type OwnDocumentPage = { items: OwnDocument[]; total: number; page: number; pageSize: number };

// Labels follow the staff wording of S19/S20 (web dispositionLabels, document types).
export const dispositionLabels: Record<string, string> = {
  ConsultationOnly: 'Tư vấn, chưa điều trị', FollowUp: 'Theo dõi / cần quay lại', Referral: 'Chuyển khám',
  DeferredDecision: 'Hoãn quyết định điều trị', ProceedToTreatment: 'Tiếp tục điều trị',
};
export const documentTypeLabels: Record<string, string> = {
  XRay: 'X-quang', IntraoralPhoto: 'Ảnh trong miệng', LabResult: 'Kết quả xét nghiệm', ConsentForm: 'Phiếu đồng ý',
  Referral: 'Giấy chuyển khám', Other: 'Tài liệu khác',
};
export const sourceLabels: Record<string, string> = { Clinic: 'Phòng khám', External: 'Cơ sở khác' };
export const investigationStateLabels: Record<string, string> = {
  Ordered: 'Đã chỉ định', ResultAvailable: 'Đã có kết quả, chờ bác sĩ đọc', Reviewed: 'Bác sĩ đã đọc kết quả',
};
export const visitStatusLabels: Record<string, string> = {
  Queued: 'Chờ khám', InProgress: 'Đang khám', Completed: 'Đã kết thúc', Cancelled: 'Đã hủy',
};
export const isImage = (doc: Pick<OwnDocument, 'contentType'>) => doc.contentType.startsWith('image/');
