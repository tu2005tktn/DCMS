import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'./tests',testMatch:['finance.integration.spec.ts','examination.integration.spec.ts'],workers:1,timeout:90000,use:{baseURL:process.env.E2E_WEB_URL??'http://localhost:8086',headless:true,trace:'off',screenshot:'only-on-failure'},outputDir:'../.artifacts/finance-browser'});
