import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { test, expect, type Page, type Route } from "@playwright/test";

const fixture = JSON.parse(
  readFileSync(fileURLToPath(new URL("../src/features/world/fixtures/first-light.json", import.meta.url)), "utf8")
);

const twoHearths = JSON.parse(
  readFileSync(fileURLToPath(new URL("../src/features/world/fixtures/two-hearths.json", import.meta.url)), "utf8")
);

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
const players = { items: [{ id: 1, name: "Default", createdUtc: "2026-01-01T00:00:00Z" }], currentPlayerId: 1 };
const header = {
  worldId: "first-light",
  templateId: "first-light",
  currentTurn: 0,
  state: "active",
  createdUtc: "2026-01-01T00:00:00Z",
  revision: 0
};

async function fulfillJson(route: Route, body: unknown) {
  await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
}

async function mockWorld(page: Page) {
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => route.fulfill({ status: 404 }));
  await page.route("**/api/world/1", (route) => fulfillJson(route, header));
  await page.route("**/api/world/first-light/state**", (route) => fulfillJson(route, fixture));
}

/** `two-hearths`, with one sector aged into `Scouted` memory and one into `Rumored` — the exact
 * shape W50's own stale-fog legibility check needs, and the same technique `e2e/world.spec.ts`'s
 * own `worldState({staleAge})` helper already uses (mutate the byte-pinned fixture for one named
 * test scenario, never invent a new one). */
function twoHeartsWorldState() {
  const state = JSON.parse(JSON.stringify(twoHearths)) as typeof twoHearths;
  for (const sector of state.sectors) {
    if (sector.sectorId === "d-flank-2") {
      sector.intel = "Scouted";
      sector.intelAge = 4;
    }
    if (sector.sectorId === "d-outpost") {
      sector.intel = "Rumored";
      sector.intelAge = 8;
    }
  }
  return state;
}

/** A maximal sector — every inspector block populated, matching W57's own "8 slots, 4 forces, a
 * warden, a construction in progress" density case — grafted onto the real `two-hearths` fixture
 * rather than a hand-built double, so the rest of the world (lanes, other sectors) stays real. */
function maximalWorldState() {
  const state = JSON.parse(JSON.stringify(twoHearths)) as typeof twoHearths;
  const sector = state.sectors.find((s: { sectorId: string }) => s.sectorId === "d-flank-2")!;
  sector.wardenBindingId = "e-dave-warden-1";
  sector.slots = [
    { slotIndex: 0, slotTypeId: "seat", element: null, state: "Claimed", ownerFactionId: "dave", guardWaveId: null, guardState: "Cleared", structureId: "well", constructionTurnsRemaining: null },
    { slotIndex: 1, slotTypeId: "wildland", element: null, state: "Claimed", ownerFactionId: "dave", guardWaveId: null, guardState: "Cleared", structureId: "waystation", constructionTurnsRemaining: 2 },
    { slotIndex: 2, slotTypeId: "essence-deposit", element: "Earth", state: "Intact", ownerFactionId: null, guardWaveId: "e-guard-1", guardState: "Intact", structureId: null, constructionTurnsRemaining: null },
    { slotIndex: 3, slotTypeId: "shard-vein", element: null, state: "Claimed", ownerFactionId: "dave", guardWaveId: null, guardState: "Cleared", structureId: null, constructionTurnsRemaining: null },
    { slotIndex: 4, slotTypeId: "material-seam", element: null, state: "Depleted", ownerFactionId: "dave", guardWaveId: null, guardState: "Cleared", structureId: null, constructionTurnsRemaining: null },
    { slotIndex: 5, slotTypeId: "lair", element: null, state: "Ruined", ownerFactionId: null, guardWaveId: null, guardState: "Cleared", structureId: null, constructionTurnsRemaining: null },
    { slotIndex: 6, slotTypeId: "vault", element: null, state: "Claimed", ownerFactionId: "dave", guardWaveId: null, guardState: "Cleared", structureId: null, constructionTurnsRemaining: null },
    { slotIndex: 7, slotTypeId: "spire", element: null, state: "Intact", ownerFactionId: null, guardWaveId: null, guardState: "Cleared", structureId: null, constructionTurnsRemaining: null }
  ];
  sector.forces = [
    { entityId: "e-dave-legion-1", ownerFactionId: "dave", kind: "Legion", exact: true, strength: 240 },
    { entityId: "e-dave-legion-2", ownerFactionId: "dave", kind: "Legion", exact: true, strength: 90 },
    { entityId: "e-wild-pack-1", ownerFactionId: "wild", kind: "Warband", exact: false, bandName: "a warband", bandCeiling: 200 },
    { entityId: "e-guard-1", ownerFactionId: "wild", kind: "Guard", exact: false, bandName: "a guard force", bandCeiling: 50 }
  ];
  return state;
}

async function mockMaximal(page: Page) {
  const twoHeartsHeader = { ...header, worldId: "two-hearths", templateId: "two-hearths" };
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => route.fulfill({ status: 404 }));
  await page.route("**/api/world/1", (route) => fulfillJson(route, twoHeartsHeader));
  await page.route("**/api/world/two-hearths/state**", (route) => fulfillJson(route, maximalWorldState()));
}

async function mockTwoHearths(page: Page) {
  const twoHeartsHeader = { ...header, worldId: "two-hearths", templateId: "two-hearths" };
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => route.fulfill({ status: 404 }));
  await page.route("**/api/world/1", (route) => fulfillJson(route, twoHeartsHeader));
  await page.route("**/api/world/two-hearths/state**", (route) => fulfillJson(route, twoHeartsWorldState()));
}

/** `two-hearths` with `z-outpost`'s one and only lane severed, isolating it entirely from the rest
 * of the map — the real "genuinely no route" case `reachableFromLegion`/`routeForLegion` already
 * refuse honestly, used here to prove the client-side blocked-target path (world-stage W71) rather
 * than only the reachable one. */
function severedTwoHeartsWorldState() {
  const state = twoHeartsWorldState();
  const lane = state.lanes.find((l: { laneId: string }) => l.laneId === "l-zf2-zo");
  lane.state = "Severed";
  return state;
}

async function mockSeveredTwoHearths(page: Page) {
  const twoHeartsHeader = { ...header, worldId: "two-hearths", templateId: "two-hearths" };
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) => fulfillJson(route, players));
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => route.fulfill({ status: 404 }));
  await page.route("**/api/world/1", (route) => fulfillJson(route, twoHeartsHeader));
  await page.route("**/api/world/two-hearths/state**", (route) => fulfillJson(route, severedTwoHeartsWorldState()));
}

/**
 * The scene-composition wiring, proven in a real browser (closed 2026-09-04 — the gap W50/W57/W65/
 * W71 each named): a real sector renders from real state and is clickable, through the actual
 * `#/world-stage` route, not merely a jsdom component render.
 */
test.describe("the new stage draws real sectors and they are clickable", () => {
  test("a known sector renders and clicking it selects it", async ({ page }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    const homeworld = page.getByTestId("world-scene-sector-homeworld");
    await expect(homeworld).toBeVisible();
    await expect(page.getByTestId("sector-node-homeworld")).toBeVisible();

    await homeworld.click();
    await expect(page.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "homeworld");
  });

  // Ported from the old `#/world` page's own `e2e/world.spec.ts` (retired when `#/world` started
  // serving this stage, world-stage routing work 2026-09-05) — the one assertion there that this
  // file did not already cover under its own testids: a sector nobody has scouted renders as a
  // silhouette with no name, real-browser fidelity for `sectorChannels.ts`'s `shape: "unknown"`
  // branch rather than only the jsdom coverage `WorldScene.test.tsx` already has.
  test("ground nobody has seen is a silhouette without a name", async ({ page }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    const dark = page.getByTestId("sector-node-black-gate");
    await expect(dark).toBeVisible();
    await expect(dark).toHaveAttribute("data-shape", "unknown");
    await expect(dark).not.toContainText("Black Gate");
    await expect(dark).toContainText("unexplored");
  });

  test("clicking the same sector again deselects it (world-stage W65)", async ({ page }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    // `ash-waste` (layoutX 4 → x=880px), not `homeworld` (layoutX 0 → x=0px): with the inspector
    // open, its own dock — `left-[92px] w-[380px]`, `92px`–`472px` — visually covers a sector
    // authored at the map's own origin, and a *second* click there hits the dock, not the sector
    // underneath it (Playwright: "element ... subtree intercepts pointer events", found live, not
    // assumed). That is a real map-layout gap (the camera doesn't yet reserve the chrome budget
    // `spec-world-hud.md` §1 already specifies — `world-hud`'s own frame is a separate, still-
    // unmounted piece), not a defect in the reselect-toggle logic itself, which is what this test
    // actually proves; a sector clear of the dock's own footprint is the honest way to prove it
    // through a real click rather than quietly asserting past the real finding.
    const ashWaste = page.getByTestId("world-scene-sector-ash-waste");
    await ashWaste.click();
    await expect(page.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "ash-waste");

    await ashWaste.click();
    await expect(page.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "");
  });

  test("clicking a sector opens the real inspector, and closing it deselects (world-stage W57/W65)", async ({ page }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    await page.getByTestId("world-scene-sector-homeworld").click();
    const inspector = page.getByTestId("sector-inspector");
    await expect(inspector).toBeVisible();
    await expect(inspector).toContainText("homeworld");

    await page.getByTestId("sector-inspector-close").click();
    await expect(inspector).not.toBeVisible();
    await expect(page.getByTestId("world-stage-svg")).toHaveAttribute("data-selected-sector", "");
  });

  test("Esc closes the inspector and only the inspector — the map's own camera/selection survive the cycle (world-stage W65)", async ({ page }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    const svg = page.getByTestId("world-stage-svg");
    const viewBoxBefore = await svg.getAttribute("viewBox");

    await page.getByTestId("world-scene-sector-homeworld").click();
    await expect(page.getByTestId("sector-inspector")).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.getByTestId("sector-inspector")).not.toBeVisible();
    // The stage itself is still mounted (GG-11) and its own camera viewBox is untouched by the
    // open/close cycle — proven directly, not merely assumed, by comparing the real attribute.
    await expect(svg).toHaveAttribute("viewBox", viewBoxBefore!);
  });
});

/**
 * The stale-fog legibility check (world-stage W50), run on `two-hearths` per its own acceptance
 * (not `first-light` — six sectors was reshaped precisely because one march lit the whole map).
 * The question is not "can you tell them apart" but "can you still plan a march against them" —
 * the wash must never wash out the static facts a march decision actually needs (climate, danger,
 * ownership, the stamp naming how stale the memory is).
 */
test.describe("stale-fog legibility on two-hearths (world-stage W50)", () => {
  test("a Scouted and a Rumored sector both keep their static facts and stamp readable under the wash", async ({ page }) => {
    await mockTwoHearths(page);
    await page.goto("/#/world-stage");

    const scouted = page.getByTestId("world-scene-sector-d-flank-2");
    const rumored = page.getByTestId("world-scene-sector-d-outpost");
    await expect(scouted).toBeVisible();
    await expect(rumored).toBeVisible();

    // The wash is on; the stamp naming staleness is real, visible text, not merely implied by tint.
    await expect(scouted.getByTestId("fog-stamp")).toHaveText("seen 4 turns ago");
    await expect(rumored.getByTestId("fog-stamp")).toHaveText("hearsay");
    await expect(scouted.getByTestId("fog-wrapper")).toHaveAttribute("data-wash", "parchment");
    await expect(rumored.getByTestId("fog-wrapper")).toHaveAttribute("data-wash", "torn");

    // Real rendered screenshots of both cards at their native on-screen size — read and judged
    // directly (this session, 2026-09-04) rather than a hand-rolled compositing calculation
    // guessing what the browser actually painted. Recorded result: see the W50 evidence in
    // tasks/world-stage-todo.md.
    await scouted.screenshot({ path: "e2e/.artifacts/w50-scouted.png" });
    await rumored.screenshot({ path: "e2e/.artifacts/w50-rumored.png" });
  });
});

/**
 * The GG-61 proof (world-stage W57) — a maximal sector renders inside the dock's own bound with
 * the body scrolling, and the stage behind it never scrolls to compensate, at both named floors.
 * Proven through the real, now-wired inspector rather than left as a jsdom-only claim.
 */
for (const [width, height] of [
  [1280, 720],
  [1440, 900]
] as const) {
  test(`a maximal sector's inspector stays inside its own bound at ${width}x${height}, body scrolling, stage untouched (world-stage W57)`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    await mockMaximal(page);
    await page.goto("/#/world-stage");

    await page.getByTestId("world-scene-sector-d-flank-2").click();
    const inspector = page.getByTestId("sector-inspector");
    await expect(inspector).toBeVisible();
    // Every block's real content is what makes this a *maximal* density case, not a placeholder.
    await expect(inspector).toContainText("e-dave-warden-1");
    await expect(inspector).toContainText("240"); // the exact legion's own real strength
    await expect(inspector).toContainText("Guarded by guard"); // the guard, named as a force

    const inspectorBox = await inspector.boundingBox();
    expect(inspectorBox).not.toBeNull();
    // DockShell's own bound is `inset-y-0` — full viewport height, never more.
    expect(inspectorBox!.height).toBeLessThanOrEqual(height + 1);

    const bodyOverflow = await page.evaluate(() => {
      const body = document.querySelector('[data-testid="sector-inspector-body"]');
      return body ? { scrollHeight: body.scrollHeight, clientHeight: body.clientHeight } : null;
    });
    expect(bodyOverflow).not.toBeNull();
    expect(bodyOverflow!.scrollHeight).toBeGreaterThan(bodyOverflow!.clientHeight);

    const documentOverflow = await page.evaluate(
      () => document.documentElement.scrollHeight > document.documentElement.clientHeight
    );
    expect(documentOverflow).toBe(false);
  });
}

test("the same maximal sector holds its bound at 200% text scale (world-stage W57)", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await mockMaximal(page);
  await page.goto("/#/world-stage");
  // A real 200% text-zoom reflow, not merely a larger viewport — `rem`-based sizing is what GG-56/
  // the type floor actually promise to survive, and only reflowing root font-size proves it.
  await page.addStyleTag({ content: "html { font-size: 200% !important; }" });

  await page.getByTestId("world-scene-sector-d-flank-2").click();
  const inspector = page.getByTestId("sector-inspector");
  await expect(inspector).toBeVisible();

  const inspectorBox = await inspector.boundingBox();
  expect(inspectorBox!.height).toBeLessThanOrEqual(721);

  const bodyOverflow = await page.evaluate(() => {
    const body = document.querySelector('[data-testid="sector-inspector-body"]');
    return body ? { scrollHeight: body.scrollHeight, clientHeight: body.clientHeight } : null;
  });
  expect(bodyOverflow!.scrollHeight).toBeGreaterThan(bodyOverflow!.clientHeight);

  const documentOverflow = await page.evaluate(
    () => document.documentElement.scrollHeight > document.documentElement.clientHeight
  );
  expect(documentOverflow).toBe(false);
});

/**
 * world-stage W34: the stage route is measured against the viewport, not the page. Sweeps both
 * named floors — the 1280×720 minimum and the 1440×900 the plan's own numbers cite — and asserts
 * the *document* never scrolls and nothing overflows horizontally, which is what GG-36 actually
 * forbids (`overflow-auto` dressed as a feature).
 */
for (const [width, height] of [
  [1280, 720],
  [1440, 900]
] as const) {
  test(`the stage route never grows the document past the viewport at ${width}x${height}`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    await page.goto("/#/world-stage");

    await expect(page.getByTestId("world-stage-svg")).toBeVisible();

    const { scrollHeight, viewportHeight, hasHorizontalOverflow } = await page.evaluate(() => {
      const doc = document.scrollingElement ?? document.documentElement;
      return {
        scrollHeight: doc.scrollHeight,
        viewportHeight: window.innerHeight,
        hasHorizontalOverflow: doc.scrollWidth > doc.clientWidth
      };
    });

    expect(scrollHeight).toBe(viewportHeight);
    expect(hasHorizontalOverflow).toBe(false);
  });
}

/**
 * The queued-order path (world-stage W71) — the real wiring gap this program named four times
 * (W50/W57/W65/W71) closed for good: a force actually rendered at its own sector, selectable,
 * showing its reachable range, filing a march that is visibly queued without moving anything, and
 * taking it back cleanly. `first-light`'s own `e-dave-legion-1` stands at `homeworld`, connected by
 * open lanes to `ember-hollow` and `frost-mire` (1 hop each) and onward to `ash-waste` (2),
 * `black-gate` (3) and `verdant-shelf` (4) — every other sector on this small map is reachable.
 */
test.describe("the queued order — filed, drawn, and takeable back (world-stage W71)", () => {
  test("select a force, see its range, file a march, watch it queue without moving the marker, then take it back", async ({
    page
  }) => {
    await mockWorld(page);
    await page.goto("/#/world-stage");

    const marker = page.getByTestId("legion-marker-e-dave-legion-1");
    await expect(marker).toBeVisible();
    await expect(marker).toHaveAttribute("data-selected", "false");

    // The `transform` attribute is the one place this marker's actual position lives — checked
    // directly rather than via `boundingBox()`, which also reflects the selected-ring's own thicker
    // stroke (a real, deliberate cosmetic change on selection) and would make an honest "never
    // moved" proof look like a false failure.
    const transformBeforeSelecting = await marker.getAttribute("transform");
    expect(transformBeforeSelecting).toMatch(/^translate\(/);

    // Select the force — its range lights up, hop numbers and all.
    await marker.click();
    await expect(marker).toHaveAttribute("data-selected", "true");
    await expect(page.getByTestId("range-ring-ember-hollow")).toHaveAttribute("data-hops", "1");
    await expect(page.getByTestId("range-hop-number-ember-hollow")).toHaveText("1");
    await expect(page.getByTestId("range-ring-ash-waste")).toHaveAttribute("data-hops", "2");

    // Selecting the force must not itself have moved it.
    expect(await marker.getAttribute("transform")).toBe(transformBeforeSelecting);

    // Click a reachable destination — this files the march.
    await page.getByTestId("world-scene-sector-ember-hollow").click();

    const commandId = "t0-move-e-dave-legion-1";
    await expect(page.getByTestId("queued-orders")).toBeVisible();
    await expect(page.getByTestId(`queued-order-${commandId}`)).toBeVisible();
    await expect(page.getByTestId(`queued-order-label-${commandId}`)).toContainText("Ember Hollow");
    await expect(page.getByTestId(`destination-flag-${commandId}`)).toBeVisible();
    await expect(page.getByTestId("lane-queued-l-home-ember")).toBeVisible();

    // The token itself never moved — filing an order is not the same act as the turn resolving.
    expect(await marker.getAttribute("transform")).toBe(transformBeforeSelecting);

    // Take it back — the queue empties and the destination flag disappears with it.
    await page.getByTestId(`queued-order-take-back-${commandId}`).click();
    await expect(page.getByTestId("queued-orders-empty")).toBeVisible();
    await expect(page.getByTestId(`destination-flag-${commandId}`)).toHaveCount(0);

    // The marker, one final time, is still exactly where it always was.
    expect(await marker.getAttribute("transform")).toBe(transformBeforeSelecting);
  });

  test("clicking a genuinely unreachable sector while a force is selected shows the refusal, and queues nothing", async ({
    page
  }) => {
    await mockSeveredTwoHearths(page);
    await page.goto("/#/world-stage");

    await page.getByTestId("legion-marker-e-dave-legion-1").click();
    await expect(page.getByTestId("legion-marker-e-dave-legion-1")).toHaveAttribute("data-selected", "true");

    // `z-outpost`'s only lane (`l-zf2-zo`) is severed in this fixture — no route exists from
    // `d-home` no matter how far the legion could otherwise march.
    await page.getByTestId("world-scene-sector-z-outpost").click();

    const blocked = page.getByTestId("blocked-target");
    await expect(blocked).toBeVisible();
    await expect(blocked).toHaveAttribute("data-kind", "blocked");
    await expect(page.getByTestId("blocked-target-caption")).toHaveText("Order refused — no route given.");
    await expect(page.getByTestId("queued-orders-empty")).toBeVisible();
  });
});
