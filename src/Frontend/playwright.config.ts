import { defineConfig } from "@playwright/test";

const baseURL = "http://127.0.0.1:3000";

export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: "list",
  use: {
    baseURL,
    browserName: "chromium",
    trace: "retain-on-failure",
  },
  webServer: [
    {
      command: "node tests/e2e/mock-api.mjs",
      url: "http://127.0.0.1:5100/health",
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
<<<<<<< HEAD
      command: "node node_modules/next/dist/bin/next dev --hostname 127.0.0.1 --port 3100",
      url: `${baseURL}/robots.txt`,
=======
      command: "npm run dev -- --hostname 127.0.0.1 --port 3100",
      url: baseURL,
>>>>>>> da0a4a211ab920f98d12313e75d414fb7b9ab35a
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      stdout: "pipe",
      env: {
        API_INTERNAL_URL: "http://127.0.0.1:5100",
        NEXT_DIST_DIR: ".next-e2e",
        NEXT_PUBLIC_SITE_URL: baseURL,
      },
    },
  ],
});
