import { request } from '@/services/api/http-client';
import { normalizeVietnameseMobile } from '@/utils/contact';
import type { CredentialPolicy, RegisterResponse, RegistrationForm } from './registration.types';

export async function getCredentialPolicy(): Promise<CredentialPolicy> {
  const response = await request('/api/auth/policy');
  return response.json() as Promise<CredentialPolicy>;
}

export async function registerAccount(form: RegistrationForm): Promise<RegisterResponse> {
  const response = await request('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fullName: form.fullName.trim(),
      phoneNumber: normalizeVietnameseMobile(form.phoneNumber),
      email: form.email.trim(),
      password: form.password,
      confirmPassword: form.confirmPassword,
    }),
  });
  return response.json() as Promise<RegisterResponse>;
}
