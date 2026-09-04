import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { test, expect, type Page, type Route } from "@playwright/test";

// Read rather than import: Playwright runs this file as ESM, where a JSON import needs an import
// attribute Node's loader will not accept from a .ts source. Reading it keeps the fixture the single
// source of truth without fighting the module system.
const fixture = JSON.parse(
  readFileSync(fileURLToPath(new URL("../src/features/world/fixtures/first-light.json", import.meta.url)), "utf8")
) as {
  sectors: {
    sectorId: string;
    intel: string;
    intelAge: number;
    lifeline: boolean;
    lifelineCost: number;
    slots: { slotIndex: number; slotTypeId: string }[];
  }[];
};

// world-stage W20: the turn-report counterpart to the state fixture above — a real six-turn opening
// (turns 0-5), byte-pinned by `WorldTurnFixtureTests.cs`. Indexed by its own `turn` field so the mock
// below can answer `/turn/{n}` for whichever turn a test asks about, the same way the live server does.
const turnFixture = JSON.parse(
  readFileSync(fileURLToPath(new URL("../src/features/world/fixtures/first-light-turn.json", import.meta.url)), "utf8")
) as { turn: number }[];
const turnReportsByTurn = new Map(turnFixture.map((report) => [report.turn, report]));

/**
 * `#/world` in a real browser.
 *
 * Almost everything Checkpoint 7 asks a human to look at is mechanical: does an unseen sector render
 * without a name, does a remembered one say how old it is, does a force nobody counted show a band
 * instead of a number, does the lifeline toggle do anything. Those are assertions, not opinions.
 *
 * What is left for a person is whether it *reads* — whether the dimming is legible at map zoom and
 * whether the map is worth looking at. That is the only part of the checkpoint a test cannot sign.
 *
 * The payload is the checked-in fixture, which an E2E test already pins to the live DTO — so this
 * cannot drift into asserting a shape the server does not produce.
 */
const health = {
  ok: true,
  injectorConnected: false,
  lastHeartbeatUtc: null,
  source: "none",
  simEnabled: true,
  ingestQueued: 0,
  lastFlushMs: 0,
  currentPlayerId: 1
};

const players = {
  items: [{ id: 1, name: "Default", createdUtc: "2026-01-01T00:00:00Z" }],
  currentPlayerId: 1
};

const header = {
  worldId: "first-light",
  templateId: "first-light",
  currentTurn: 0,
  state: "active",
  createdUtc: "2026-01-01T00:00:00Z",
  revision: 0
};

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
}

/** The fixture, with one sector aged into a memory and the homeworld made load-bearing. */
function worldState(options?: { staleAge?: number; lifelines?: boolean }) {
  const state = JSON.parse(JSON.stringify(fixture)) as typeof fixture;

  if (options?.staleAge != null)
    for (const sector of state.sectors)
      if (sector.sectorId === "ash-waste") {
        sector.intel = "Rumored";
        sector.intelAge = options.staleAge;
      }

  if (options?.lifelines)
    for (const sector of state.sectors)
      if (sector.sectorId === "homeworld") {
        sector.lifeline = true;
        sector.lifelineCost = 5000;
      }

  return state;
}

async function mockWorld(page: Page, options?: { staleAge?: number }) {
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => fulfillJson(route, null, 404));
  await page.route("**/api/world/1", (route) => fulfillJson(route, header));
  // world-stage W20: answers from the real, byte-pinned turn-report fixture for turns 0-5 — the
  // same 404 the live server gives for any turn outside what was actually played.
  await page.route("**/api/world/first-light/turn/**", (route) => {
    const match = /\/turn\/(\d+)/.exec(route.request().url());
    const turn = match ? Number(match[1]) : NaN;
    const report = turnReportsByTurn.get(turn);
    if (report) fulfillJson(route, report);
    else fulfillJson(route, null, 404);
  });

  // The lifeline sweep is only asked for while the overlay shows, so the mock answers accordingly.
  await page.route("**/api/world/first-light/state**", (route) =>
    fulfillJson(
      route,
      worldState({ staleAge: options?.staleAge, lifelines: route.request().url().includes("lifelines=true") })
    )
  );
}

async function openMap(page: Page) {
  await page.goto("/#/world");
  await expect(page.getByTestId("world-canvas")).toBeVisible();
  await expect(page.getByTestId("sector-node-homeworld")).toBeVisible();
}

test.describe("the map draws what you know", () => {
  test("ground nobody has seen is a silhouette without a name", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    const dark = page.getByTestId("sector-node-black-gate");
    await expect(dark).toBeVisible();
    await expect(dark).not.toContainText("Black Gate");
    await expect(dark.getByTestId("sector-status")).toHaveText("unscouted");
    await expect(dark.getByTestId("sector-slots").locator("span")).toHaveCount(0);
  });

  test("ground you are standing on is drawn in full", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    const home = page.getByTestId("sector-node-homeworld");
    await expect(home).toContainText("Homeworld");
    await expect(home).toHaveAttribute("data-ownership", "mine");

    // Count comes from the fixture itself, not a hardcoded literal — a fixed number here is exactly
    // what let this go stale once already (the loam program added a fourth "rootbed" slot to
    // homeworld and this assertion silently kept expecting three).
    const homeworldSlots = fixture.sectors.find((s) => s.sectorId === "homeworld")!.slots;
    await expect(home.getByTestId("sector-slots").locator("span")).toHaveCount(homeworldSlots.length);
  });

  test("a remembered sector says how old the memory is", async ({ page }) => {
    await mockWorld(page, { staleAge: 6 });
    await openMap(page);

    await expect(page.getByTestId("sector-node-ash-waste").getByTestId("sector-status"))
      .toHaveText("seen 6 turns ago");
  });

  test("a force you counted shows a number and one you glimpsed shows a band", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    // Dave's own legion, standing in the homeworld: counted.
    await expect(page.getByTestId("force-e-dave-legion-1")).toHaveText(/^\d+$/);

    // The wild pack at ash-waste, only ever glimpsed: named, never numbered.
    const glimpsed = page.getByTestId("force-e-wild-pack-1");
    await expect(glimpsed).toBeVisible();
    await expect(glimpsed).not.toHaveText(/^\d+$/);
  });
});

test.describe("the inspector", () => {
  test("picking a sector fills it in", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    await expect(page.getByTestId("world-inspector")).toContainText("Pick a sector");

    await page.getByTestId("sector-node-ember-hollow").click();

    const inspector = page.getByTestId("world-inspector");
    await expect(inspector).toContainText("Ember Hollow");
    await expect(inspector.getByTestId("inspector-slots")).toBeVisible();
  });

  test("it reports when the sector was last seen", async ({ page }) => {
    await mockWorld(page, { staleAge: 4 });
    await openMap(page);

    await page.getByTestId("sector-node-ash-waste").click();
    await expect(page.getByTestId("world-inspector")).toContainText("4 turns ago");
  });
});

test.describe("the lifeline overlay", () => {
  test("says nothing until you ask, then marks what is load-bearing", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    await expect(page.getByTestId("sector-lifeline")).toHaveCount(0);

    await page.getByTestId("toggle-lifelines").click();

    await expect(page.getByTestId("sector-node-homeworld").getByTestId("sector-lifeline"))
      .toHaveText("lifeline");
  });

  test("and turns off again", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    await page.getByTestId("toggle-lifelines").click();
    await expect(page.getByTestId("sector-lifeline").first()).toBeVisible();

    await page.getByTestId("toggle-lifelines").click();
    await expect(page.getByTestId("sector-lifeline")).toHaveCount(0);
  });
});

test.describe("orders", () => {
  test("marching somewhere queues an order rather than sending it", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    // Select the legion through the sector it is standing in, then a destination.
    await page.getByTestId("sector-node-homeworld").click();
    await page.getByRole("button", { name: "e-dave-legion-1", exact: false }).click();
    await page.getByTestId("sector-node-ember-hollow").click();
    await page.getByRole("button", { name: "March here" }).click();

    await expect(page.getByTestId("world-orders")).toContainText("Ember Hollow");
    await expect(page.getByRole("button", { name: /File 1 order/ })).toBeVisible();
  });

  test("a queued order can be taken back", async ({ page }) => {
    await mockWorld(page);
    await openMap(page);

    await page.getByTestId("sector-node-homeworld").click();
    await page.getByRole("button", { name: "e-dave-legion-1", exact: false }).click();
    await page.getByTestId("sector-node-ember-hollow").click();
    await page.getByRole("button", { name: "March here" }).click();

    await page.getByTestId("world-orders").getByRole("button", { name: "×" }).click();
    await expect(page.getByTestId("world-orders")).toContainText("Nothing queued");
  });
});
