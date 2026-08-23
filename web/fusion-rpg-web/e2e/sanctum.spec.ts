import { test, expect, type Page, type Route } from "@playwright/test";

const health = {
  ok: true,
  injectorConnected: false,
  lastHeartbeatUtc: null,
  source: "none",
  simEnabled: false,
  ingestQueued: 0,
  lastFlushMs: 0,
  currentPlayerId: 1
};

const players = {
  items: [{ id: 1, name: "Default", createdUtc: "2026-01-01T00:00:00Z" }],
  currentPlayerId: 1
};

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
}

async function mockSanctum(page: Page) {
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => fulfillJson(route, null, 404));
  await page.route("**/api/unique/actors**", (route) => fulfillJson(route, { playerId: 1, items: [] }));
  await page.route("**/api/runs", (route) => fulfillJson(route, { items: [] }));
  await page.route("**/api/souls/**", (route) =>
    fulfillJson(route, { playerId: 1, balance: 500, earnedTotal: 500, spentTotal: 0, revision: 1, updatedUtc: "2026-01-01T00:00:00Z" })
  );
  await page.route("**/api/contracts/**", (route) =>
    fulfillJson(route, {
      contracts: [],
      capacity: { used: 0, total: 0, purchasedSlots: 0, nextSlotPrice: 0, canBuy: false, maxSlots: 0 },
      dailyTribute: 0,
      deployFloor: 0,
      loyaltyMax: 0
    })
  );
}

test.describe("Sanctum stage (T9)", () => {
  test("the bare index route redirects to the sanctum", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/");
    await expect(page).toHaveURL(/#\/sanctum/);
  });

  test("first paint contains a playable affordance, not a blank stage (GG-2)", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("sanctum-hud")).toBeVisible();
    await expect(page.getByTestId("rail")).toBeVisible();
    await expect(page.getByTestId("focus-card-cta")).toBeVisible();
  });

  test("the rail's Sanctum entry is active and locked entries say what unlocks them", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("rail-sanctum")).toHaveAttribute("data-state", "active");
    const relics = page.getByTestId("rail-relics");
    await expect(relics).toBeDisabled();
    await expect(relics).toHaveAttribute("title", /item/);
  });

  test("opening a layer from the rail keeps the stage mounted underneath, and Esc returns to it", async ({ page }) => {
    await mockSanctum(page);
    // Unlock Almanac so this test can exercise the *generic placeholder* path — Creatures has
    // its own real layer as of T10 (creatures.spec.ts covers its stage-persistence in depth).
    await page.route("**/api/runs", (route) => fulfillJson(route, { items: [{ id: 1, startedUtc: "2026-01-01T00:00:00Z" }] }));
    await page.goto("/#/sanctum");

    await page.getByTestId("rail-almanac").click();
    await expect(page.getByTestId("sanctum-layer-placeholder")).toBeVisible();
    await expect(page.getByTestId("sanctum-hud")).toBeVisible(); // stage still mounted behind it

    await page.keyboard.press("Escape");
    await expect(page.getByTestId("sanctum-layer-placeholder")).not.toBeVisible();
    await expect(page.getByTestId("rail")).toBeVisible();
  });

  test("#/status redirects into the developer tree (T12), not a bare route", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/#/status");
    await expect(page).toHaveURL(/#\/sanctum\?dev=status/);
    await expect(page.getByRole("heading", { name: "Status" })).toBeVisible();
  });
});
