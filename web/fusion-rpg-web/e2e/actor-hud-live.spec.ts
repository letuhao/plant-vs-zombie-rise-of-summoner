import { test, expect } from "@playwright/test";
import { expectCanvasHud, selectOccupant } from "./helpers/actor-hud-e2e";
import { isLiveActorHudE2e } from "./helpers/live-gate";
import { setupLiveActorHudBoard, waitForApiHealth } from "./helpers/live-debug-api";

const liveEnabled = isLiveActorHudE2e();

test.describe("Actor HUD program E2E (live injector)", () => {
  test.describe.configure({ mode: "serial" });

  test.skip(!liveEnabled, "requires live-chromium project or ACTOR_HUD_LIVE_E2E=1");

  let targetPtr = "";
  let row = 0;
  let col = 0;

  test.beforeAll(async () => {
    if (!liveEnabled) return;
    const board = await setupLiveActorHudBoard();
    targetPtr = board.targetPtr;
    row = board.row;
    col = board.col;
  });

  test("Inspector and Phaser canvas show shield + statuses from real board-stats", async ({ page }) => {
    await page.goto("/#/lawn?devmode=1");
    await waitForApiHealth();
    await expect(page.getByTestId("panel-lawn-inspector")).toBeVisible({ timeout: 30_000 });

    await expect(page.getByTestId("lawn-occupant-list")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("lawn-occupant-list")).toContainText(`${row},${col}`, {
      timeout: 30_000
    });

    await selectOccupant(page, row, col);
    await expect(page.getByTestId("lawn-occupant-sel")).toContainText(targetPtr, { ignoreCase: true });

    await expect(page.getByTestId("actor-hud-shield")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByTestId("actor-hud-status-expose")).toBeVisible();
    await expect(page.getByTestId("actor-hud-status-command")).toBeVisible();

    await expect(page.getByTestId("lawn-game-host")).toBeVisible();
    await expectCanvasHud(page, targetPtr, {
      identity: true,
      shield: true,
      status0: true,
      status1: true,
      chipRow: false
    });
  });
});
