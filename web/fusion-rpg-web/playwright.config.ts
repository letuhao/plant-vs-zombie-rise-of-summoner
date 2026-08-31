import { defineConfig, devices } from "@playwright/test";
import { isLiveActorHudE2e } from "./e2e/helpers/live-gate";

const isLiveE2e = isLiveActorHudE2e();
if (isLiveE2e) {
  process.env.ACTOR_HUD_LIVE_E2E = "1";
}

export default defineConfig({
  testDir: "./e2e",
  testIgnore: /\/helpers\/.*\.test\.ts$/,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report" }]],
  use: {
    baseURL: isLiveE2e ? "http://127.0.0.1:5173" : "http://127.0.0.1:4173",
    trace: "on-first-retry"
  },
  webServer: isLiveE2e
    ? {
        command: "npm run dev",
        url: "http://127.0.0.1:5173",
        reuseExistingServer: true,
        timeout: 120_000
      }
    : {
        command: "npm run preview -- --host 127.0.0.1 --port 4173",
        url: "http://127.0.0.1:4173",
        reuseExistingServer: !process.env.CI,
        timeout: 120_000
      },
  projects: [
    {
      name: "chromium",
      testIgnore: /actor-hud-live\.spec\.ts$/,
      use: { ...devices["Desktop Chrome"] }
    },
    {
      name: "live-chromium",
      testMatch: /actor-hud-live\.spec\.ts$/,
      use: { ...devices["Desktop Chrome"] }
    }
  ]
});
