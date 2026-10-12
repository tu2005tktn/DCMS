import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests', fullyParallel: false, workers: 1, timeout: 45_000,
  use: { baseURL: 'http://localhost:5173', headless: true, trace: 'off', screenshot: 'off', video: 'off' },
  // API and Vite are started explicitly to reuse the configured development environment.
});
