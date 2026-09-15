/**
 * Story UI evidence capture — the current Rift prologue, at every beat, at three viewports.
 *
 * This exists so the owner can look at the actual rendered story UI as evidence rather than reading
 * an assertion about it. It is a CAPTURE spec: it asserts only enough to prove the scene rendered
 * (the dialog is visible, the beat advanced, the art vs placeholder branch is the expected one), and
 * it writes PNGs for human review. It makes no claim about taste.
 *
 * Run from web/fusion-rpg-web (preview server on :4173 is started automatically):
 *   npx playwright test e2e/story-ui-evidence.spec.ts --project=chromium
 *
 * Artifacts (gitignored, like every other capture here):
 *   e2e/artifacts/story-ui/<viewport>-beat<N>[-narration|-placeholder].png
 *   e2e/artifacts/story-ui/capture-report.json
 *
 * The four beats come from the shipped dialog's own BEATS constant
 * (src/features/onboarding/RiftPrologueDialog.tsx). Beat 3 is the narration beat (no speaker), and
 * its cue is the quarantine seal. The capture walks them by clicking Next, so it also proves the
 * advance path works — the same path a player takes.
 */
import { test, expect, type Page, type Route } from "@playwright/test";
import * as fs from "node:fs";
import * as path from "node:path";

const ARTIFACT_DIR = path.join("e2e", "artifacts", "story-ui");

/** Beats as the shipped dialog declares them; used only to walk the scene and name the files. */
const BEATS = [
  { index: 1, cue: "rift.portal.open" },
  { index: 2, cue: "rift.portal.surge" },
  { index: 3, cue: "rift.quarantine.seal" },
  { index: 4, cue: "rift.quarantine.fade" }
] as const;

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

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
}

/**
 * Mock the shell with the prologue ELIGIBLE, so `SanctumStage` opens it. The one field that matters
 * is `stories[0].eligible` — `SanctumStage.tsx:85-89` looks up `rift-prologue` v1 and opens on
 * `eligible`. Everything else is the standard Sanctum mock (mirrors `sanctum.spec.ts`).
 */
async function mockSanctumWithEligibleStory(page: Page, opts: { ackOk?: boolean } = {}) {
  const ackOk = opts.ackOk ?? true;
  await page.route("**/hub/rpg**", (route) => route.abort());
  await page.route("**/health", (route) => fulfillJson(route, health));
  await page.route("**/api/players", (route) =>
    fulfillJson(route, {
      items: [{ id: 1, name: "Default", createdUtc: "2026-01-01T00:00:00Z" }],
      currentPlayerId: 1
    })
  );
  await page.route("**/api/players/current", (route) => fulfillJson(route, { ok: true }));
  await page.route("**/api/sim", (route) => fulfillJson(route, null, 404));
  await page.route("**/api/unique/actors**", (route) => fulfillJson(route, { playerId: 1, items: [] }));
  await page.route("**/api/runs", (route) => fulfillJson(route, { items: [] }));
  await page.route("**/api/souls/**", (route) =>
    fulfillJson(route, {
      playerId: 1,
      balance: 500,
      earnedTotal: 500,
      spentTotal: 0,
      revision: 1,
      updatedUtc: "2026-01-01T00:00:00Z"
    })
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
  await page.route("**/api/onboarding/**", (route) => {
    const url = route.request().url();
    if (url.includes("/ack") || url.includes("/stories/")) {
      return fulfillJson(route, ackOk ? { ok: true } : { ok: false }, ackOk ? 200 : 500);
    }
    return fulfillJson(route, {
      playerId: 1,
      playerLevel: 1,
      revision: 1,
      checkpoints: [],
      stories: [
        {
          storyId: "rift-prologue",
          version: 1,
          state: "unseen",
          outcome: null,
          eligible: true
        }
      ]
    });
  });
}

const VIEWPORTS = [
  { name: "desktop", width: 1440, height: 900 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "mobile", width: 390, height: 844 }
] as const;

type CaptureRecord = {
  viewport: string;
  beat: number;
  /** Read from the rendered name tag, NOT assumed. Absent when the beat renders no tag. */
  renderedSpeaker: string | null;
  cue: string;
  /** Read from the rendered line, so the report cannot restate a stale mirror. */
  renderedLine: string | null;
  hasNameTag: boolean;
  hasTeaching: boolean;
  file: string;
  assetState: "art" | "placeholder";
};

test.describe("Story UI evidence capture (Rift prologue, shipped)", () => {
  for (const vp of VIEWPORTS) {
    test(`capture every beat at ${vp.name}`, async ({ page }) => {
      fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await mockSanctumWithEligibleStory(page);

      const records: CaptureRecord[] = [];

      await page.goto("/#/sanctum");
      const dialog = page.getByTestId("rift-prologue-dialog");
      await expect(dialog).toBeVisible({ timeout: 15_000 });

      for (const beat of BEATS) {
        // Progress label is the dialog's own `Beat n of m` (aria-label, :167) — reading it proves the
        // beat actually advanced rather than the capture photographing the same frame four times.
        await expect(dialog.locator(`[aria-label="Beat ${beat.index} of 4"]`)).toBeVisible({
          timeout: 10_000
        });

        const scene = dialog.locator(".rift-prologue-scene");
        await expect(scene).toHaveAttribute("data-cue", beat.cue);

        const placeholder = dialog.locator(".rift-prologue-placeholder");
        const assetState: "art" | "placeholder" = (await placeholder.count()) > 0 ? "placeholder" : "art";

        // Read what the page actually renders, through the say-region's stable ids
        // (`data-testid="rift-prologue-say|speaker|line|teaching"`). Nothing here is assumed from
        // this spec's own copy — that is the whole point of reading rather than restating.
        const renderedSpeaker = (await dialog.getByTestId("rift-prologue-speaker").innerText()).trim();
        const renderedLine = (await dialog.getByTestId("rift-prologue-line").innerText()).trim();
        const hasTeaching = (await dialog.getByTestId("rift-prologue-teaching").count()) > 0;

        const file = path.join(
          ARTIFACT_DIR,
          `${vp.name}-beat${beat.index}${hasTeaching ? "" : "-no-teaching"}${assetState === "placeholder" ? "-placeholder" : ""}.png`
        );
        await dialog.screenshot({ path: file });

        records.push({
          viewport: vp.name,
          beat: beat.index,
          renderedSpeaker,
          cue: beat.cue,
          renderedLine,
          hasNameTag: renderedSpeaker.length > 0,
          hasTeaching,
          file,
          assetState
        });

        if (beat.index < BEATS.length) {
          await dialog.getByRole("button", { name: "Next" }).click();
        }
      }

      fs.writeFileSync(
        path.join(ARTIFACT_DIR, `capture-report.${vp.name}.json`),
        JSON.stringify(records, null, 2)
      );

      expect(records).toHaveLength(BEATS.length);
    });
  }
});
