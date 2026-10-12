import { test, expect } from '@playwright/test';

/**
 * UC-02 account recovery (S03) end-to-end coverage of the four steps and their error / loading
 * states. The API and Vite are started explicitly to reuse the configured development environment,
 * and the tests talk to the real endpoints with no mocked data.
 */
const password = process.env.E2E_DEMO_PASSWORD;
test.beforeAll(() => {
  if (!password) throw new Error('Set E2E_DEMO_PASSWORD from ignored .env before running browser tests.');
});

const MSG12 =
  'Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.';
const MSG13 = 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.';
const MSG108 =
  'Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.';
const MSG144 = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

const newPassword = () => 'Zx9!q' + Math.random().toString(36).slice(2, 20);

test('sign-in page links to recovery instead of claiming it is unavailable', async ({ page }) => {
  await page.goto('/sign-in');
  await expect(page.getByRole('link', { name: 'Khôi phục tài khoản' })).toBeVisible();
  await expect(page.getByText(/chưa khả dụng/)).toHaveCount(0);
  await page.getByRole('link', { name: 'Khôi phục tài khoản' }).click();
  await expect(page.getByRole('heading', { name: 'Khôi phục tài khoản', exact: true })).toBeVisible();
});

test('request, verify, reset and landing on sign-in for an existing account', async ({ page }) => {
  const updated = newPassword();
  await page.goto('/recover');

  // Step 1 - the neutral answer is identical for a real account.
  await page.getByLabel('Tài khoản', { exact: true }).fill('dentist.demo');
  const request = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/request') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  expect((await request).status()).toBe(200);
  await expect(page.getByRole('status').filter({ hasText: MSG12 })).toBeVisible();

  // Step 2 - the delivered proof is supplied by the development adapter's outbox.
  const proof = await readOutboxProof(page.request, 'dentist.demo');
  await page.getByLabel('Mã xác minh', { exact: true }).fill(proof);
  const verify = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/verify') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  expect((await verify).status()).toBe(200);

  // Step 3 - shared S02 password policy hints are rendered from the API.
  await expect(page.locator('.recovery-policy')).toContainText('Tối thiểu');
  await expect(page.getByLabel('Mật khẩu mới', { exact: true })).toBeVisible();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(updated);
  await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill(updated);
  const reset = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/reset') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  expect((await reset).status()).toBe(200);

  // Step 4 - success message, then navigation to S01.
  await expect(page.getByText(MSG13).first()).toBeVisible();
  await page.getByRole('button', { name: 'Về trang đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/sign-in/);
  // The old credential no longer works and the new one does.
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('dentist.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password!);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByText('Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng. Vui lòng kiểm tra và thử lại.')).toBeVisible();
  await page.getByLabel('Mật khẩu', { exact: true }).fill(updated);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible();
});

test('unknown account produces the same neutral request answer', async ({ page }) => {
  await page.goto('/recover');
  await page.getByLabel('Tài khoản', { exact: true }).fill('no.such.account.' + Date.now().toString(36));
  const request = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/request') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  const response = await request;
  expect(response.status()).toBe(200);
  expect((await response.json())).toEqual({ code: 'MSG12', message: MSG12 });

  // The follow-up steps behave identically and reveal nothing about the account.
  await expect(page.getByRole('status').filter({ hasText: MSG12 })).toBeVisible();
  await page.getByLabel('Mã xác minh', { exact: true }).fill('000000');
  const verify = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/verify') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  expect((await verify).status()).toBe(400);
  await expect(page.getByText(MSG108, { exact: true })).toBeVisible();
});

test('wrong proof, weak password and mismatch show inline errors', async ({ page }) => {
  await page.goto('/recover');
  await page.getByLabel('Tài khoản', { exact: true }).fill('guest.demo');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  await page.getByLabel('Mã xác minh', { exact: true }).fill('000000');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByText(MSG108, { exact: true })).toBeVisible();

  const proof = await readOutboxProof(page.request, 'guest.demo');
  await page.getByLabel('Mã xác minh', { exact: true }).fill(proof);
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByLabel('Mật khẩu mới', { exact: true })).toBeVisible();

  // Weak password is refused by the shared policy (MSG109) with the rule hints visible.
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('short');
  await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill('short');
  const rejected = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/reset') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  expect((await rejected).status()).toBe(400);
  await expect(page.getByText('Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.')).toBeVisible();

  // Mismatched confirmation is refused before the request is sent.
  const updated = newPassword();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(updated);
  await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill(updated + 'typo');
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await expect(page.getByText('Xác nhận mật khẩu không đúng định dạng.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true })).toBeEnabled();
});

test('validation, resend cooldown and service failure are handled', async ({ page }) => {
  await page.goto('/recover');
  // Empty identifier uses the required-field message.
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  await expect(page.getByText('Vui lòng nhập Tài khoản.', { exact: true })).toBeVisible();

  await page.getByLabel('Tài khoản', { exact: true }).fill('assistant.demo');
  const request = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/request') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  expect((await request).status()).toBe(200);

  // Resend starts a countdown and the button stays disabled while it runs.
  const resend = page.getByRole('button', { name: /Gửi lại sau|Gửi lại mã/ });
  await expect(resend).toBeDisabled();
  await expect(resend).toContainText('Gửi lại sau');

  // A service failure on the verify step surfaces MSG144.
  await page.route('**/api/auth/recovery/verify', async route => {
    await route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'MSG144', message: MSG144 }),
    });
  });
  await page.getByLabel('Mã xác minh', { exact: true }).fill('123456');
  const failed = page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/verify') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  expect((await failed).status()).toBe(503);
  await expect(page.getByText(MSG144, { exact: true })).toBeVisible();

  // The request is in flight while the button stays disabled (double-submit guard).
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.unroute('**/api/auth/recovery/verify');
  await page.route('**/api/auth/recovery/verify', async route => {
    await pending;
    await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ code: 'MSG108', message: MSG108 }) });
  });
  await page.getByLabel('Mã xác minh', { exact: true }).fill('123456');
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Xác minh', exact: true })).toBeDisabled();
  release();
  await expect(page.getByText(MSG108, { exact: true })).toBeVisible();
});

test('no proof, grant or password is written into the URL', async ({ page }) => {
  await page.goto('/recover');
  await page.getByLabel('Tài khoản', { exact: true }).fill('owner.demo');
  await page.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
  await page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/request') && r.request().method() === 'POST');
  const proof = await readOutboxProof(page.request, 'owner.demo');
  await page.getByLabel('Mã xác minh', { exact: true }).fill(proof);
  await page.getByRole('button', { name: 'Xác minh', exact: true }).click();
  await page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/verify') && r.request().method() === 'POST');
  const updated = newPassword();
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill(updated);
  await page.getByLabel('Xác nhận mật khẩu', { exact: true }).fill(updated);
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click();
  await page.waitForResponse(r => r.url().endsWith('/api/auth/recovery/reset') && r.request().method() => 'POST');

  const url = page.url();
  expect(url).not.toContain(proof);
  expect(url).not.toContain(updated);
  expect(url).not.toContain('grant');
  expect(url).not.toContain('token');
  expect(url).not.toContain('code');
});

/**
 * Reads the proof the Development contact adapter handed to the account.
 *
 * No real mail provider is configured, so the Development adapter keeps the delivered proof in
 * process memory and exposes it through a Development-only endpoint that requires a signed-in
 * session. The proof is never stored in the database, the outbox, the logs or an audit entry.
 */
async function readOutboxProof(request: import('@playwright/test').APIRequestContext, userName: string): Promise<string> {
  const login = await request.post('http://localhost:5080/api/auth/sign-in', {
    data: { userName: 'admin.demo', password: password! },
    failOnStatusCode: false,
  });
  if (login.status() !== 200) {
    throw new Error('Could not sign in as admin.demo to read the development proof.');
  }
  const token = (await login.json() as { accessToken: string }).accessToken;

  const accountId = await accountIdFor(request, token, userName);
  if (accountId === null) throw new Error(`Account ${userName} was not found.`);

  const proof = await request.get(`http://localhost:5080/api/dev/recovery/proof/${accountId}`, {
    headers: { Authorization: `Bearer ${token}` },
    failOnStatusCode: false,
  });
  if (proof.status() !== 200) {
    throw new Error(
      `No proof available for ${userName} (status ${proof.status()}). ` +
      'Request recovery first so the development adapter has a proof to hand back.',
    );
  }
  return (await proof.json() as { proof: string }).proof;
}

async function accountIdFor(
  request: import('@playwright/test').APIRequestContext,
  token: string,
  userName: string,
): Promise<number | null> {
  const response = await request.get(
    `http://localhost:5080/api/accounts?search=${encodeURIComponent(userName)}&page=1&pageSize=1`,
    { headers: { Authorization: `Bearer ${token}` }, failOnStatusCode: false },
  );
  if (response.status() !== 200) return null;
  const body = await response.json() as { items: { id: number; userName: string }[] };
  return body.items.find(item => item.userName === userName)?.id ?? null;
}
