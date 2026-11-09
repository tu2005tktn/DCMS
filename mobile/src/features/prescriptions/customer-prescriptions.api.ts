import { authedRequest } from '@/services/api/http-client';

export type PrescriptionSummary = { id: string; prescribedOn: string; branchName: string; diagnosis: string; prescriberName: string; requiresExternalPurchase: boolean };
export type PrescriptionPage = { items: PrescriptionSummary[]; total: number; page: number; pageSize: number };
export type PrescriptionItem = { medicineName: string; strength: string | null; dosageForm: string | null; unit: string; quantity: number; instructions: string; isExternalPurchase: boolean };
export type PrescriptionDetail = PrescriptionSummary & { otherDiagnosis: string | null; advice: string | null; items: PrescriptionItem[] };

export const customerPrescriptionsApi = {
  async list(token: string, page: number): Promise<PrescriptionPage> {
    const response = await authedRequest(`/api/me/prescriptions?page=${page}`, token);
    return response.json() as Promise<PrescriptionPage>;
  },
  async detail(token: string, id: string): Promise<PrescriptionDetail> {
    const response = await authedRequest(`/api/me/prescriptions/${encodeURIComponent(id)}`, token);
    return response.json() as Promise<PrescriptionDetail>;
  },
};
