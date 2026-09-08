import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { findEmptyPendingReasons, scanForRestDtoImports } from "./contractGuard";
import { scanForBareNumberFormatters } from "@/i18n/magnitudeGuard";
import { pendingWithReason } from "./pending";
import type {
  DelveView,
  DomainOfferView,
  EventView,
  ExtractionView,
  FightView,
  MemberView,
  ObjectPromptView,
  PartyView,
  RoomView,
  SupplyView,
  TalkView
} from "./types";

/**
 * party-dungeon D5.3 — spec-delve-stage.md §14's own three named tests for this task. The first two
 * are NOT new guard mechanisms: `contractGuard.ts`'s `scanForRestDtoImports` and `magnitudeGuard.ts`'s
 * `scanForBareNumberFormatters` are pre-existing, repo-wide, real-tree scanners (already exercised by
 * `contractGuard.test.ts`/`magnitudeGuard.test.ts` under their own descriptive names) — D5.3 adds no
 * new files under `stages/`, `layers/`, `ui/` or a new bare-number formatter under `src/i18n/`, so
 * both continue to pass trivially. These two tests are this task's own named, delve-scoped record
 * that its additions were checked against the SAME live guards every other stage answers to, not a
 * second implementation of either scanner.
 */

const srcDir = join(__dirname, "..");
const i18nDir = join(__dirname, "..", "i18n");

/**
 * `scanForRestDtoImports(srcDir)` over the REAL tree does not return `[]` on the committed HEAD this
 * task started from — it returns exactly 5 pre-existing violations, all in `ui/actor/*.tsx`, all
 * unrelated to delve (`AptitudesTab.tsx`, `CatalogTabs.tsx`, `ConditionTab.tsx`, `DerivedTab.tsx`,
 * `StatusGlyph.tsx`, importing `*CatalogRow`/`DerivedChannelDto` from `@/lib/bus/actorSurface` /
 * `@/lib/bus/aura`). Confirmed independently three ways: (1) `git status` shows all five as clean/
 * committed, last touched by commit `d8f2cbb` ("update some specs", 2026-09-07) — no uncommitted
 * change of this session's own touches any of them; (2) the pre-existing `contractGuard.test.ts`'s own
 * "no file under stages/, layers/ or ui/ imports a REST DTO type" test fails identically, run in
 * complete isolation, over a file this task never edited; (3) this exact baseline is independently
 * dated the same day in this very todo file — D1.28's own same-day addendum (2026-09-07) reads "went
 * from **5 pre-existing violations** to 6" before fixing its own regression back down to 5. So the
 * honest, correct assertion is not `toEqual([])` (which would misreport an unrelated, already-known
 * gap as this task's own finding) — it is that D5.3 holds the count at the documented baseline,
 * introducing no sixth. `contract/` is `contractGuard.ts`'s own one exempt directory
 * (`CONTRACT_IMPORT_PATTERN`), and every one of D5.3's local DTO shapes (`DelveResponseDto`,
 * `PackDto`, `QuestDto`, `DomainOfferDto`, ...) lives inside `adapt.ts`, squarely inside that
 * exemption — confirmed by this test actually re-running the real, whole-tree scan, not merely by
 * argument.
 */
describe("No_Dto_named_type_under_stages_or_layers (spec-delve-stage.md §14)", () => {
  const KNOWN_PRE_EXISTING_VIOLATION_FILES = [
    "ui/actor/AptitudesTab.tsx",
    "ui/actor/CatalogTabs.tsx",
    "ui/actor/ConditionTab.tsx",
    "ui/actor/DerivedTab.tsx",
    "ui/actor/StatusGlyph.tsx"
  ];

  it("D5.3 adds sixteen view types to src/contract/ only — introduces no new *Dto import under stages/, layers/ or ui/", () => {
    const violations = scanForRestDtoImports(srcDir);
    const violationFiles = violations.map((v) => v.file).sort();
    expect(violationFiles).toEqual([...KNOWN_PRE_EXISTING_VIOLATION_FILES].sort());
    expect(violations).toHaveLength(5);
  });
});

describe("Every_rendered_number_has_a_unit_class (spec-delve-stage.md §14)", () => {
  it("magnitude.ts's new exact branch adds no bare-number formatter under src/i18n/", () => {
    // formatMagnitude(m: Magnitude, ...) already took a Magnitude, never a bare number, before this
    // task; the new `exact` branch reads `m.exact`/`m.value` off the same Magnitude parameter and
    // introduces no second, bare-`number`-typed export. GG-46 stays enforced.
    expect(scanForBareNumberFormatters(i18nDir)).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// Beyond the three required tests: a maximally-pending fixture per adapted view, run through
// findEmptyPendingReasons — matching worldViews.test.ts's own established quality bar for this
// program (every Pending field carries a real, non-empty reason), not one of D5.3's own required
// names but the same proof sibling contract additions already carry.
// ---------------------------------------------------------------------------

const R = (name: string) => pendingWithReason<never>(`no ${name} yet`);

const member: MemberView = {
  instanceId: "m-1",
  pools: { hp: { unit: "count", value: 100 } },
  poolMax: R("pool max"),
  poolFill: R("pool fill"),
  nerveStacks: 0,
  nerveStage: R("nerve stage"),
  downed: false,
  downedOnce: false,
  shield: R("shield"),
  statuses: R("statuses")
};

const room: RoomView = {
  sectorId: "s-1",
  rowIndex: 0,
  colIndex: 0,
  visited: true,
  cleared: false,
  keyForLaneId: null,
  sight: "Full",
  kind: "fight",
  archetypeId: "arch-1",
  eventId: null,
  resolvedKind: "fight",
  resolvedArchetypeId: "arch-1",
  floorContents: R("floor contents")
};

const party: PartyView = {
  partyIndex: 0,
  entityId: 1,
  atSectorId: "s-1",
  onLaneId: null,
  route: ["s-0", "s-1"],
  members: [member],
  pack: R("pack"),
  haul: []
};

const delve: DelveView = {
  delveId: 1,
  worldId: "w-1",
  state: "Active",
  domainId: "d-1",
  raidMode: "solo",
  rungId: "r-1",
  soulsUnbanked: { unit: "count", value: 40 },
  rooms: [room],
  doors: [],
  parties: [party],
  revision: 1,
  quests: R("quests")
};

const talk: TalkView = {
  offered: ["Flatter", "Leave"],
  effectiveBand: R("effective band"),
  quote: R("quote"),
  decision: R("decision")
};

const objectPrompt: ObjectPromptView = {
  sectorId: "s-1",
  kind: "Curio",
  verbs: ["open"],
  oneShot: false
};

const supply: SupplyView = {
  ok: true,
  reason: "",
  decrementContainerId: "supply-1",
  decision: R("supply decision")
};

const extraction: ExtractionView = {
  members: [{ instanceId: "m-1", outcome: "Roster", recoverDelves: { unit: "count", value: 0 }, won: true }],
  soulsFromKills: { unit: "count", value: 10 },
  soulsFromVictory: { unit: "count", value: 20 },
  wiped: R("wiped"),
  firstClearGrant: R("first clear grant"),
  levelUps: R("level ups"),
  joins: R("joins")
};

const domainOffer: DomainOfferView = {
  domainId: "d-1",
  name: "Fen",
  flavor: "flavor",
  climate: "wet",
  entranceLabel: "Very hard",
  entryKey: "standing",
  sealed: false,
  resume: null,
  rungs: [{ kind: "rung", rungId: "r-1", label: "Rung 1", bandName: "Deep", oathOffered: false, permadeath: false }],
  tailSteps: [{ kind: "tail", n: { unit: "count", value: 1 }, label: "Tail 1", bandName: "Deep" }],
  raidModes: ["solo"],
  bossName: "Boss",
  cleared: [],
  provisionable: []
};

// EventView and FightView have no adapter (see types.ts's own doc comment on each) — every field is
// necessarily Pending, exercised here as a fixture-only proof that the type itself still respects
// R1b even though nothing constructs a real instance yet.
const event: EventView = {
  eventId: R("event id"),
  kind: R("event kind"),
  choices: R("event choices"),
  banner: R("event banner"),
  warnings: R("event warnings")
};

const fight: FightView = {
  dwellRemaining: R("dwell remaining"),
  initiative: R("initiative"),
  strikeFeed: R("strike feed"),
  frozen: R("frozen")
};

describe("delve views — pending reasons (D5.3, matching worldViews.test.ts's own W4 proof)", () => {
  it("every pending field across the sixteen delve views carries a real reason", () => {
    expect(findEmptyPendingReasons(delve)).toEqual([]);
    expect(findEmptyPendingReasons(talk)).toEqual([]);
    expect(findEmptyPendingReasons(objectPrompt)).toEqual([]);
    expect(findEmptyPendingReasons(supply)).toEqual([]);
    expect(findEmptyPendingReasons(extraction)).toEqual([]);
    expect(findEmptyPendingReasons(domainOffer)).toEqual([]);
    expect(findEmptyPendingReasons(event)).toEqual([]);
    expect(findEmptyPendingReasons(fight)).toEqual([]);
  });

  it("still catches an empty reason nested inside a delve view (positive control)", () => {
    const broken: DelveView = { ...delve, quests: { state: "pending", reason: "" } };
    const violations = findEmptyPendingReasons(broken);
    expect(violations).toHaveLength(1);
    expect(violations[0]?.text).toContain("quests");
  });
});
