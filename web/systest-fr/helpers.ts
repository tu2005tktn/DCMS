import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

// System test helpers for Report-5.3 (functional requirements). The browser drives the real web
// application; the API is used only to prepare preconditions and to read back evidence.
export const API = 'http://localhost:5080';
export const PW = process.env.E2E_DEMO_PASSWORD ?? '';
export const EVIDENCE = path.resolve(process.cwd(), '../outputs/system-test-frs/evidence');
fs.mkdirSync(EVIDENCE, { recursive: true });

test.beforeAll(() => { if (!PW) throw new Error('Set E2E_DEMO_PASSWORD (DEV_SEED_PASSWORD from .env).'); });

/** Adds a line of evidence to the current test (copied into the report's Note column). */
export function note(text: string) { test.info().annotations.push({ type: 'evidence', description: text }); }

export async function shot(page: Page, name: string) {
  const file = path.join(EVIDENCE, `${name}.png`);
  await page.screenshot({ path: file, fullPage: false });
  test.info().annotations.push({ type: 'screenshot', description: `evidence/${name}.png` });
}

async function resetSession(page: Page) {
  if (page.url().startsWith('http')) {
    for (let i = 0; i < 3 && await page.locator('.ant-modal-wrap:visible').count(); i++) { await page.keyboard.press('Escape').catch(() => {}); await page.waitForTimeout(300); }
    const out = page.getByRole('button', { name: 'Đăng xuất' });
    if (await out.count()) { await out.first().click({ timeout: 5000 }).catch(() => {}); await page.waitForTimeout(500); }
    await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); }).catch(() => {});
  }
  await page.context().clearCookies();
}

export async function staffLogin(page: Page, user: string, password = PW) {
  await resetSession(page);
  await page.goto('/sign-in');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill(user);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Đăng xuất' })).toBeVisible({ timeout: 20_000 });
}

export async function customerLogin(page: Page, identifier: string, password = PW) {
  await resetSession(page);
  await page.goto('/customer/sign-in');
  await page.getByLabel('Email hoặc tên đăng nhập', { exact: true }).fill(identifier);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/customer\/home/, { timeout: 20_000 });
}

/** Minimal API client for preconditions (signs in with the given account). */
export class Api {
  token = ''; user: any;
  constructor(private request: APIRequestContext) {}
  static async as(request: APIRequestContext, user: string, password = PW) {
    const api = new Api(request);
    const res = await request.post(`${API}/api/auth/sign-in`, { data: { userName: user, password, deviceLabel: 'systest' } });
    if (!res.ok()) throw new Error(`sign-in ${user}: ${res.status()}`);
    const body = await res.json(); api.token = body.accessToken; api.user = body.user;
    return api;
  }
  private h() { return { Authorization: `Bearer ${this.token}` }; }
  async get(url: string) { const r = await this.request.get(API + url, { headers: this.h() }); return { status: r.status(), body: await r.json().catch(() => null) }; }
  async post(url: string, data: unknown = {}) { const r = await this.request.post(API + url, { headers: this.h(), data }); return { status: r.status(), body: await r.json().catch(() => null) }; }
  async put(url: string, data: unknown = {}) { const r = await this.request.put(API + url, { headers: this.h(), data }); return { status: r.status(), body: await r.json().catch(() => null) }; }
}

export const key = () => crypto.randomUUID();
export const rnd = () => Math.floor(100000 + Math.random() * 899999);
export const todayIso = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
export const addDays = (iso: string, d: number) => new Date(Date.parse(iso + 'T00:00:00Z') + d * 86400_000).toISOString().slice(0, 10);
