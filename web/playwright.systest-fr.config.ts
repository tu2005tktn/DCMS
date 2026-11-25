import { defineConfig } from '@playwright/test';

// System test (functional requirements) run for Report-5.3; see outputs/system-test-frs.
// Expects Vite on :5173 and the API on :5080 against the isolated dcms_systest database.
export default defineConfig({
  testDir: './systest-fr', fullyParallel: false, workers: 1, timeout: 90_000,
  reporter: [['list'], ['json', { outputFile: `../outputs/system-test-frs/results/${process.env.RUN ?? 'run'}.json` }]],
  use: { baseURL: 'http://localhost:5173', headless: true, channel: 'chrome', viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure', trace: 'off', locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh' },
  outputDir: '../outputs/system-test-frs/playwright-artifacts',
});
