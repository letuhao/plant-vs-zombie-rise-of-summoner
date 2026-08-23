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

const boundDemon = {
  instanceId: "d1",
  bound: true,
  deployable: true,
  loyalty: 800,
  rank: "trusted",
  personality: "stoic",
  upkeepPerDay: 5
};

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
}

async function mockSanctum(page: Page, opts?: { contracts?: unknown[]; expeditions?: unknown[] }) {
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => fulfillJson(route, null, 404));
  await page.route("**/api/unique/actors**", (route) => fulfillJson(route, { playerId: 1, items: [] }));
  await page.route("**/api/relics", (route) => fulfillJson(route, { items: [] }));
  await page.route("**/api/runs", (route) => fulfillJson(route, { items: [] }));
  await page.route("**/api/souls/**", (route) =>
    fulfillJson(route, { playerId: 1, balance: 500, earnedTotal: 500, spentTotal: 0, revision: 1, updatedUtc: "2026-01-01T00:00:00Z" })
  );
  await page.route("**/api/contracts/**", (route) =>
    fulfillJson(route, {
      contracts: opts?.contracts ?? [],
      capacity: { used: 1, total: 4, purchasedSlots: 0, nextSlotPrice: 500, canBuy: true, maxSlots: 8 },
      dailyTribute: 5,
      deployFloor: 200,
      loyaltyMax: 1000
    })
  );
  await page.route("**/api/demons/catalog", (route) => fulfillJson(route, { species: [] }));
  await page.route("**/api/demons/*/codex", (route) => fulfillJson(route, { entries: [] }));
  await page.route("**/api/demons/*/summon-state", (route) => fulfillJson(route, { pity: 0 }));
  await page.route("**/api/demons/*", (route) =>
    fulfillJson(route, {
      playerId: 1,
      items: [
        {
          profile: { instanceId: "d1", speciesId: "sp-imp", rarity: "epic", star: 1, elementPrimary: "fire", nickname: null },
          actor: { level: 5 }
        }
      ]
    })
  );
  await page.route("**/api/patron/**", (route) => fulfillJson(route, { patron: null, switchCostSouls: 100 }));
  await page.route("**/api/expeditions/*/materials", (route) => fulfillJson(route, { items: [] }));
  await page.route("**/api/expeditions/*", (route) =>
    fulfillJson(route, { serverUtc: "2026-01-01T12:00:00Z", tiers: [], items: opts?.expeditions ?? [] })
  );
}

test.describe("Expeditions layer (T17)", () => {
  test("locked with no bound demon", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("rail-expeditions")).toBeDisabled();
  });

  test("unlocks and opens with a bound demon, Esc closes without unmounting the Sanctum", async ({ page }) => {
    await mockSanctum(page, { contracts: [boundDemon] });
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("rail-expeditions")).not.toBeDisabled();

    await page.keyboard.press("e");
    await expect(page.getByTestId("expeditions-layer")).toBeVisible();
    await expect(page.getByTestId("sanctum-hud")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.getByTestId("expeditions-layer")).not.toBeVisible();
    await expect(page.getByTestId("sanctum-hud")).toBeVisible();
  });

  test("a returned-but-uncollected expedition badges the rail on load, never opening a dialog on its own", async ({ page }) => {
    // The toast fires only for a return the watcher observes *after* its first poll (unit-tested
    // in expeditionReturnWatcher.test.tsx, where timing is controllable) — a return already due
    // when the player opens the app is old news, not a toast-worthy one; this is the badge half.
    await mockSanctum(page, {
      contracts: [boundDemon],
      expeditions: [
        {
          id: 1,
          state: "Dispatched",
          tierId: "scout-30m",
          squadInstanceIds: ["d1"],
          dispatchedUtc: "2026-01-01T10:00:00Z",
          dueUtc: "2026-01-01T11:00:00Z" // before serverUtc (12:00) — already due
        }
      ]
    });
    await page.goto("/#/sanctum");

    await expect(page.getByTestId("rail-expeditions")).toHaveAttribute("data-state", "badged");
    await expect(page.getByTestId("rail-expeditions-badge")).toHaveText("1");
    await expect(page.getByTestId("expeditions-layer")).not.toBeVisible();
  });

  test("#/expeditions redirects into the layer, and Expeditions is absent from AuditNav", async ({ page }) => {
    await mockSanctum(page, { contracts: [boundDemon] });
    await page.goto("/#/expeditions");
    await expect(page).toHaveURL(/#\/sanctum\?panel=expeditions/);
    await expect(page.getByTestId("expeditions-layer")).toBeVisible();

    await page.keyboard.press("Escape");
    const nav = page.getByTestId("audit-nav");
    await expect(nav.getByText("Expeditions", { exact: true })).toHaveCount(0);
  });
});

test.describe("Pacts layer (T17)", () => {
  test("locked with no contract", async ({ page }) => {
    await mockSanctum(page);
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("rail-pacts")).toBeDisabled();
  });

  test("P opens it once bound, an overdue pact disables Renegotiate with its reason inline, Esc closes without unmounting the Sanctum", async ({ page }) => {
    await mockSanctum(page, {
      contracts: [{ instanceId: "d1", bound: true, deployable: false, loyalty: 300, rank: "insubordinate", personality: "cruel", upkeepPerDay: 8 }]
    });
    await page.goto("/#/sanctum");
    await expect(page.getByTestId("rail-pacts")).not.toBeDisabled();

    await page.keyboard.press("p");
    await expect(page.getByTestId("pacts-layer")).toBeVisible();
    await expect(page.getByTestId("pact-renegotiate-d1")).toBeDisabled();
    await expect(page.getByTestId("pact-renegotiate-reason-d1")).toContainText("Insubordinate");
    await expect(page.getByTestId("sanctum-hud")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.getByTestId("pacts-layer")).not.toBeVisible();
    await expect(page.getByTestId("sanctum-hud")).toBeVisible();
  });
});
