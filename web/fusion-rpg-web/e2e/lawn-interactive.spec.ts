import { test, expect } from "@playwright/test";

/**
 * Lawn interactive chrome — Field CTA, dock, spawn tray, commander bar, observability.
 * Uses mocked / stubbed lawn page routes already used by lawn-hud.spec.ts patterns.
 */
test.describe("lawn interactive chrome", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/#/lawn");
    await page.waitForSelector('[data-testid="page-lawn"], [data-testid="lawn-match-hud"], [data-testid="lawn-hud"]', {
      timeout: 30_000
    });
  });

  test("match HUD exposes Field CTA and connection", async ({ page }) => {
    const hud = page.getByTestId("lawn-match-hud");
    await expect(hud).toBeVisible();
    await expect(page.getByTestId("lawn-match-hud-field")).toBeVisible();
    await expect(page.getByTestId("lawn-hud-sun")).toBeVisible();
    const conn = page.getByTestId("lawn-match-hud-connection");
    await expect(conn).toBeVisible();
  });

  test("Field opens spawn tray without typeId input", async ({ page }) => {
    await page.getByTestId("lawn-match-hud-field").click();
    await expect(page.getByTestId("spawn-tray")).toBeVisible();
    await expect(page.getByTestId("spawn-tray-hint")).toContainText(/no type id/i);
    await expect(page.locator('[data-testid="lawn-spawn-typeid"]')).toHaveCount(0);
  });

  test("commander action bar shows nine locked-visible slots", async ({ page }) => {
    await expect(page.getByTestId("commander-action-bar")).toBeVisible();
    for (let i = 1; i <= 9; i++) {
      await expect(page.getByTestId(`commander-action-slot-${i}`)).toBeVisible();
    }
    await expect(page.getByTestId("commander-action-slot-1")).toHaveAttribute("data-locked", "true");
  });

  test("observability ring records Field click", async ({ page }) => {
    await page.getByTestId("lawn-match-hud-field").click();
    const ring = await page.evaluate(() => {
      return (window as unknown as { __fusionRpgLawnInteractive?: { event: string }[] })
        .__fusionRpgLawnInteractive;
    });
    expect(ring?.some((e) => e.event === "hud.field")).toBeTruthy();
  });
});

test.describe("lawn interactive responsive", () => {
  for (const viewport of [
    { name: "desktop", width: 1440, height: 900 },
    { name: "tablet", width: 1280, height: 720 },
    { name: "mobile", width: 390, height: 844 }
  ] as const) {
    test(`layout at ${viewport.name} ${viewport.width}x${viewport.height}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto("/#/lawn");
      await expect(page.getByTestId("lawn-match-hud")).toBeVisible({ timeout: 30_000 });
      const overflow = await page.evaluate(() => {
        const el = document.documentElement;
        return el.scrollWidth <= el.clientWidth + 2;
      });
      expect(overflow).toBe(true);
      await page.screenshot({
        path: `e2e/artifacts/lawn-interactive-${viewport.name}.png`,
        fullPage: true
      });
    });
  }
});
