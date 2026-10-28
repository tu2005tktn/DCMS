import { test, expect, type Page } from '@playwright/test';

const nearToken = () => `header.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now()/1000)+30 })).toString('base64url')}.signature`;
const freshToken = () => `header.${Buffer.from(JSON.stringify({ exp: Math.floor(Date.now()/1000)+900 })).toString('base64url')}.fresh`;
async function prepare(page: Page) {
  await page.route('**/api/auth/me', route => route.fulfill({ json: { id: 1 } }));
  await page.goto('/sign-in');
}
async function requests(page: Page, token: string) {
  return page.evaluate(async token => {
    const modulePath='/src/services/api.ts'; const client=await import(modulePath);
    client.setSessionTokens({accessToken:token,refreshToken:'refresh-old'});
    return Promise.all([client.api('/api/auth/me'),client.api('/api/auth/me')]);
  },token);
}

test('concurrent requests renew once, use rotated token and persist the pair across reload', async ({page}) => {
  let count=0;const fresh=freshToken();const authorizations:string[]=[];
  await page.route('**/api/auth/refresh',async route=>{count++;await new Promise(r=>setTimeout(r,100));await route.fulfill({json:{accessToken:fresh,refreshToken:'refresh-new'}});});
  await prepare(page);
  await page.route('**/api/auth/me',route=>{authorizations.push(route.request().headers().authorization);return route.fulfill({json:{id:1}});});
  await requests(page,nearToken());
  expect(count).toBe(1);expect(authorizations).toEqual([`Bearer ${fresh}`,`Bearer ${fresh}`]);
  await page.reload();
  expect(await page.evaluate(()=>sessionStorage.getItem('dcms.web.refresh-token'))).toBe('refresh-new');
  expect(await page.evaluate(()=>sessionStorage.getItem('dcms.web.access-token'))).toBe(fresh);
});

for(const status of [503,401])test(`refresh ${status} ${status===401?'ends':'preserves'} the session`,async({page})=>{
  await page.route('**/api/auth/refresh',route=>route.fulfill({status,json:{code:status===401?'MSG07':'MSG144',message:'Renewal unavailable'}}));
  await prepare(page);
  await requests(page,nearToken()).catch(()=>undefined);
  expect(await page.evaluate(()=>sessionStorage.getItem('dcms.web.refresh-token'))).toBe(status===401?null:'refresh-old');
});

for(const status of [200,401])test(`late refresh ${status} cannot replace or clear a newer login or send the old command`,async({page})=>{
  let release!:()=>void;let started!:()=>void;const ready=new Promise<void>(r=>started=r),gate=new Promise<void>(r=>release=r);let writes=0;
  await page.route('**/api/auth/refresh',async route=>{started();await gate;await route.fulfill({status,json:status===200?{accessToken:freshToken(),refreshToken:'late-old-refresh'}:{code:'MSG07',message:'Expired'}});});
  await page.route('**/api/test-write',route=>{writes++;return route.fulfill({json:{}});});
  await prepare(page);
  const old=page.evaluate(async token=>{const modulePath='/src/services/api.ts';const c=await import(modulePath);c.setSessionTokens({accessToken:token,refreshToken:'refresh-old'});await c.api('/api/test-write',{method:'POST',body:'{}'}).catch(()=>undefined);},nearToken());
  await ready;
  const fresh=freshToken();await page.evaluate(async token=>{const modulePath='/src/services/api.ts';const c=await import(modulePath);c.setSessionTokens({accessToken:token,refreshToken:'new-login-refresh'});},fresh);
  release();await old;
  expect(writes).toBe(0);
  expect(await page.evaluate(()=>sessionStorage.getItem('dcms.web.refresh-token'))).toBe('new-login-refresh');
  expect(await page.evaluate(()=>sessionStorage.getItem('dcms.web.access-token'))).toBe(fresh);
});
