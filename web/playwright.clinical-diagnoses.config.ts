import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir:'./tests',testMatch:/(examination|clinical-diagnoses)\.integration\.spec\.ts/,workers:1,timeout:120_000,
  use:{baseURL:process.env.E2E_WEB_URL??'http://localhost:8084',headless:true,screenshot:'only-on-failure'},
  outputDir:'../.artifacts/s58-browser',
});
