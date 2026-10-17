/**
 * useSignIn — Hook submit form đăng nhập S01.
 *
 * Đặc tính:
 *  - Validate client trước khi gọi API (giảm round-trip khi rỗng).
 *  - Submit có retry: lỗi mạng (status 0 / fetch error) cho phép retry tối đa 2 lần với backoff
 *    500ms / 1000ms. Lỗi 4xx KHÔNG retry vì backend đã chốt kết quả.
 *  - Khi thành công: lưu session qua onSuccess callback (SessionProvider sẽ xử lý token storage).
 *  - Không log payload, không log token, không log password.
 */

import { useCallback, useState } from 'react';
import { ApiError } from '@/services/api/api-error';
import { signInApi } from '../sign-in.api';
import { validateSignInForm } from '../sign-in.schema';
import type { SignInFeedback, SignInRequest, SessionSnapshot } from '../sign-in.types';

interface UseSignInOptions {
  onSuccess: (snapshot: SessionSnapshot) => void | Promise<void>;
}

interface UseSignInResult {
  feedback: SignInFeedback;
  submit: (input: SignInRequest) => Promise<SignInFeedback>;
  reset: () => void;
}

const MAX_NETWORK_RETRIES = 2;
const RETRY_DELAYS_MS = [500, 1000];

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isNetworkError(error: unknown): boolean {
  if (error instanceof ApiError) return error.status === 0;
  return error instanceof TypeError;
}

export function useSignIn({ onSuccess }: UseSignInOptions): UseSignInResult {
  const [feedback, setFeedback] = useState<SignInFeedback>({ kind: 'idle' });

  const submit = useCallback(
    async (input: SignInRequest): Promise<SignInFeedback> => {
      const validation = validateSignInForm(input);
      if (validation) {
        const next: SignInFeedback = {
          kind: 'error',
          code: validation.code,
          message: validation.message,
        };
        setFeedback(next);
        return next;
      }

      setFeedback({ kind: 'submitting' });

      let lastError: unknown;
      for (let attempt = 0; attempt <= MAX_NETWORK_RETRIES; attempt += 1) {
        try {
          const response = await signInApi.signIn({
            userName: input.userName.trim(),
            password: input.password,
          });
          const snapshot = signInApi.buildSnapshot(response);
          await onSuccess(snapshot);
          const success: SignInFeedback = { kind: 'success' };
          setFeedback(success);
          return success;
        } catch (error) {
          lastError = error;
          if (error instanceof ApiError) {
            // 4xx: backend đã chốt, không retry. 5xx: thử lại 1 lần.
            if (error.status >= 400 && error.status < 500) {
              const next: SignInFeedback = {
                kind: 'error',
                code: (signInApi.translateStatusToCode(error.status) as SignInFeedback extends { kind: 'error'; code: infer C } ? C : never) ?? 'MSG09',
                message: await extractServerMessage(error),
              };
              setFeedback(next);
              return next;
            }
          }
          if (attempt < MAX_NETWORK_RETRIES && isNetworkError(error)) {
            await sleep(RETRY_DELAYS_MS[attempt] ?? 1000);
            continue;
          }
          break;
        }
      }

      const fallback: SignInFeedback = {
        kind: 'error',
        code: 'MSG144',
        message: 'Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.',
      };
      if (lastError instanceof ApiError && lastError.status >= 500) {
        fallback.code = 'MSG144';
        fallback.message = 'Máy chủ đang bận. Vui lòng thử lại sau ít phút.';
      } else if (lastError instanceof ApiError && (lastError.status === 401 || lastError.status === 403)) {
        fallback.code = signInApi.translateStatusToCode(lastError.status) as typeof fallback.code;
        fallback.message = await extractServerMessage(lastError);
      }
      setFeedback(fallback);
      return fallback;
    },
    [onSuccess],
  );

  const reset = useCallback(() => {
    setFeedback({ kind: 'idle' });
  }, []);

  return { feedback, submit, reset };
}

/** Đọc { code, message } từ body lỗi; nếu không parse được thì dùng thông báo mặc định. */
async function extractServerMessage(error: ApiError): Promise<string> {
  // ApiError không giữ body; dùng status để dịch. Một số mã có thông báo mặc định.
  switch (error.status) {
    case 400:
      return 'Thông tin đăng nhập chưa đúng định dạng.';
    case 401:
      return 'Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng.';
    case 403:
      return 'Tài khoản không có quyền truy cập.';
    case 409:
      return 'Tài khoản đang được cập nhật. Vui lòng thử lại.';
    case 503:
      return 'Máy chủ đang bảo trì. Vui lòng thử lại sau.';
    default:
      return 'Đã có lỗi xảy ra. Vui lòng thử lại.';
  }
}
