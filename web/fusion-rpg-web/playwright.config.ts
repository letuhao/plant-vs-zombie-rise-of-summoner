import { defineConfig, devices } from "@playwright/test";
import { isLiveActorHudE2e } from "./e2e/helpers/live-gate";

const isLiveE2e = isLiveActorHudE2e();
if (isLiveE2e) {
  process.env.ACTOR_HUD_LIVE_E2E = "1";
}

export default defineConfig({
  testDir: "./e2e",
  // `.test.ts` anywhere under e2e/ is a vitest-only unit test (picked up separately by
  // vite.config.ts's `e2e/**/*.test.{ts,tsx}` include) — never a Playwright spec. Matched with no
  // path-separator anchor so it holds on Windows too: `/\/helpers\//`, the previous form, silently
  // matched nothing here because Playwright's own path is backslash-joined, so
  // `e2e/helpers/live-debug-api-core.test.ts` (and, once I1 added it,
  // e2e/fixtures/passive-tree-volume.test.ts) were both still collected as specs and crashed at
  // load time on the bare vitest `describe`/`it` globals they import instead of using.
  testIgnore: /\.test\.ts$/,
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
      // A project-level testIgnore replaces, rather than adds to, the top-level one above — so the
      // `.test.ts` exclusion has to be repeated here too, or this project re-collects every
      // vitest-only file the top-level pattern was meant to keep out.
      testIgnore: [/\.test\.ts$/, /actor-hud-live\.spec\.ts$/],
      use: { ...devices["Desktop Chrome"] }
    },
    {
      name: "live-chromium",
      testMatch: /actor-hud-live\.spec\.ts$/,
      use: { ...devices["Desktop Chrome"] }
    }
  ]
});
