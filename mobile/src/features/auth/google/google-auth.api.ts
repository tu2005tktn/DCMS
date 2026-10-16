import { request } from '@/services/api/http-client';
import type { SignInResponse } from '@/features/auth/sign-in/sign-in.types';

export interface GoogleConfig {
  enabled: boolean;
  clientId: string | null;
}

export const googleAuthApi = {
  async config(): Promise<GoogleConfig> {
    const response = await request('/api/auth/google/config');
    return response.json() as Promise<GoogleConfig>;
  },
  async signIn(idToken: string): Promise<SignInResponse> {
    const response = await request('/api/auth/google/sign-in', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ idToken }),
    });
    return response.json() as Promise<SignInResponse>;
  },
};
