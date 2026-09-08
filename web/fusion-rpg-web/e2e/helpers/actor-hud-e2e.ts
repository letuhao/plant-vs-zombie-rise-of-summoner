import { expect, type Page } from "@playwright/test";
import { normalizePtr } from "./live-debug-api-core";

export type CanvasHudExpect = {
  identity?: boolean;
  shield?: boolean;
  status0?: boolean;
  status1?: boolean;
  status2?: boolean;
  overflow?: boolean;
  chipRow?: boolean;
};

export async function appendLogEvents(page: Page, events: unknown[]) {
  await page.evaluate((batch) => {
    const append = window.__fusionRpgAppendLogEvent;
    if (!append) return;
    for (const ev of batch) append(ev);
  }, events);
}

export async function appendBoardWithHud(
  page: Page,
  opts: {
    matchKey: string;
    ptr: string;
    row: number;
    col: number;
    actorHud?: unknown;
    hp?: number;
    maxHp?: number;
  }
) {
  const { matchKey, ptr, row, col, actorHud, hp = 200, maxHp = 200 } = opts;
  const zombie: Record<string, unknown> = { ptr, typeId: 0, row, col, hp, maxHp };
  if (actorHud !== undefined) zombie.actorHud = actorHud;

  await appendLogEvents(page, [
    {
      t: new Date().toISOString(),
      game: "pvzrh-e2e",
      kind: "board.start",
      matchKey,
      payload: { levelName: "e2e" }
    },
    {
      t: new Date().toISOString(),
      game: "pvzrh-e2e",
      kind: "debug.board-stats",
      matchKey,
      payload: { plants: [], zombies: [zombie] }
    }
  ]);
}

export async function appendActorHudPatch(
  page: Page,
  opts: { matchKey: string; ptr: string; actorHud: unknown }
) {
  await appendLogEvents(page, [
    {
      t: new Date().toISOString(),
      game: "pvzrh-e2e",
      kind: "debug.actor-hud",
      matchKey: opts.matchKey,
      payload: { ptr: opts.ptr, actorHud: opts.actorHud }
    }
  ]);
}

export async function appendActorHudClear(
  page: Page,
  opts: { matchKey: string; ptr: string }
) {
  await appendActorHudPatch(page, { ...opts, actorHud: null });
}

export async function selectOccupant(page: Page, row: number, col: number) {
  const label = `${row},${col}`;
  await expect(page.getByTestId("lawn-occupant-list")).toContainText(label);
  await page.getByTestId("lawn-occupant-list").getByText(label, { exact: true }).click();
  await expect(page.getByTestId("lawn-occupant-sel")).toBeVisible();
}

export async function expectCanvasHud(page: Page, ptr: string, expected: CanvasHudExpect) {
  const ptrKey = normalizePtr(ptr);
  await page.waitForFunction(
    ([p, exp]) => {
      const has = window.__fusionRpgHasHudChild;
      if (!has) return false;
      for (const [name, want] of Object.entries(exp as Record<string, boolean | undefined>)) {
        if (want === undefined) continue;
        const child =
          name === "chipRow"
            ? "chipRow"
            : name === "identity"
              ? "hudIdentity"
              : name === "shield"
                ? "hudShield"
                : name === "overflow"
                  ? "hudOverflow"
                  : name === "status0"
                    ? "hudStatus0"
                    : name === "status1"
                      ? "hudStatus1"
                      : name === "status2"
                        ? "hudStatus2"
                        : null;
        if (!child) continue;
        if (has(p as string, child) !== want) return false;
      }
      return true;
    },
    [ptrKey, expected] as const
  );

  const snapshot = await page.evaluate((p) => ({
    identity: window.__fusionRpgHasHudChild?.(p, "hudIdentity") ?? false,
    shield: window.__fusionRpgHasHudChild?.(p, "hudShield") ?? false,
    status0: window.__fusionRpgHasHudChild?.(p, "hudStatus0") ?? false,
    status1: window.__fusionRpgHasHudChild?.(p, "hudStatus1") ?? false,
    status2: window.__fusionRpgHasHudChild?.(p, "hudStatus2") ?? false,
    overflow: window.__fusionRpgHasHudChild?.(p, "hudOverflow") ?? false,
    chipRow: window.__fusionRpgHasHudChild?.(p, "chipRow") ?? false
  }), ptrKey);

  for (const [key, want] of Object.entries(expected)) {
    if (want === undefined) continue;
    expect(snapshot[key as keyof typeof snapshot], key).toBe(want);
  }
}
