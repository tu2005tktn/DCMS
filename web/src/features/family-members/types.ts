import { useCallback } from 'react';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { genderLabels, type Gender } from '../patients/types';
export { genderLabels };
export type { Gender };
export const familyGender = (gender: Gender | null | undefined) => gender ? genderLabels[gender] : 'Chưa cung cấp';
export type FamilyMember = { id: string; fullName: string; relationship: string; birthDate: string | null; version: number; gender: Gender | null; contactPhone: string | null };
export type FamilyPage = { items: FamilyMember[]; total: number; page: number; pageSize: number };
export const relationships = [
  { value: 'Father', label: 'Bố' }, { value: 'Mother', label: 'Mẹ' }, { value: 'Spouse', label: 'Vợ/Chồng' },
  { value: 'Child', label: 'Con' }, { value: 'Sibling', label: 'Anh/Chị/Em' }, { value: 'Grandparent', label: 'Ông/Bà' }, { value: 'Other', label: 'Khác' },
];
export const relationshipLabel = (value: string) => relationships.find(x => x.value === value)?.label ?? value;
export const familyDate = (value: string | null) => value ? value.split('-').reverse().join('/') : 'Chưa cung cấp';
export const clinicToday = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
export const validBirthDate = (value: string) => !value || (/^\d{4}-\d{2}-\d{2}$/.test(value) && Number(value.slice(0, 4)) >= 1
  && !Number.isNaN(Date.parse(value)) && new Date(value).toISOString().slice(0, 10) === value && value <= clinicToday());
export function useFamilyApi() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : undefined;
  return useCallback(async <T,>(path = '', init: RequestInit = {}) => {
    if (!token) throw new ApiError(401, 'MSG07', 'Vui lòng đăng nhập lại.');
    try { return await customerApi<T>(`/api/me/family-members${path}`, init, token); }
    catch (failure) { if (failure instanceof ApiError && failure.status === 401) expire(); throw failure; }
  }, [token, expire]);
}
