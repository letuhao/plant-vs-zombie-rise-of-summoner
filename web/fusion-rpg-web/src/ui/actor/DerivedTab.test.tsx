import { describe, expect, it, beforeEach, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import { actorSurfaceFixture } from "@/lib/bus/actorSurface";
import type { ActorView } from "@/contract/types";
import {
  bucketContributions,
  expandDerivedFamily,
  isUnchangedState,
  joinDerivedChannelId,
  resolveDerivedRenderState,
  DerivedTab,
  type LiveChannelView
} from "./DerivedTab";

vi.mock("@/lib/bus/aura", async () => {
  const actual = await vi.importActual<typeof import("@/lib/bus/aura")>("@/lib/bus/aura");
  return {
    ...actual,
    useActorSheet: () => sheetQuery,
    useActorDerived: () => derivedQuery
  };
});

vi.mock("@/lib/bus/actorSurface", async () => {
  const actual = await vi.importActual<typeof import("@/lib/bus/actorSurface")>(
    "@/lib/bus/actorSurface"
  );
  return {
    ...actual,
    useDerivedSurface: () => ({
      data: actual.derivedSurfaceFromFixture(actual.actorSurfaceFixture()),
      isLoading: false,
      isError: false
    })
  };
});

let sheetQuery: {
  data: unknown;
  isLoading: boolean;
  isError: boolean;
  refetch: () => Promise<unknown>;
};
let derivedQuery: {
  data: unknown;
  isLoading: boolean;
  isError: boolean;
  refetch: () => Promise<unknown>;
};
const sheetRefetch = vi.fn(async () => ({}));
const derivedRefetch = vi.fn(async () => ({}));

function live(
  channelId: string,
  value: number,
  contributions: { sourceId: string; label: string; op: string; value: number }[] = [],
  present = true
): LiveChannelView {
  return {
    channelId,
    value,
    displayName: channelId,
    reading: "",
    composeKind: "FlatSum",
    contributions,
    present
  };
}

function wrap(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function minimalActor(): ActorView {
  return {
    instanceId: "actor-1",
    playerId: 1,
    side: "plant",
    typeId: 1,
    displayName: known("Emberling"),
    phase: "ActiveBound",
    level: 24,
    xp: 0,
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 1,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

describe("joinDerivedChannelId", () => {
  it("joins variant axes and leaves expand:none bare", () => {
    expect(joinDerivedChannelId("combat.power", "element", "fire")).toBe("combat.power.fire");
    expect(joinDerivedChannelId("status.resist", "status-id", "butter")).toBe("status.resist.butter");
    expect(joinDerivedChannelId("progression.power", "none", "attack")).toBe("progression.power");
  });
});

describe("expandDerivedFamily", () => {
  const surface = actorSurfaceFixture();

  it("expands element families over omni + every concrete element", () => {
    const family = surface.families.find((row) => row.family === "combat.power")!;
    expect(family.expand).toBe("element");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded.map((row) => row.channelId)).toEqual([
      "combat.power.omni",
      "combat.power.fire",
      "combat.power.ice",
      "combat.power.air",
      "combat.power.earth",
      "combat.power.light",
      "combat.power.dark"
    ]);
  });

  it("expands status-id families over Omni + status-catalog ids", () => {
    const family = surface.families.find((row) => row.family === "status.resist")!;
    expect(family.expand).toBe("status-id");
    const expanded = expandDerivedFamily(
      family,
      surface.elements,
      surface.resources,
      surface.statuses
    );
    expect(expanded[0]?.channelId).toBe("status.resist.omni");
    expect(expanded).toHaveLength(1 + surface.statuses.length);
    expect(expanded.map((row) => row.channelId)).toContain("status.resist.butter");
    expect(expanded.map((row) => row.channelId)).toContain("status.resist.nerve.afflicted");
  });

  it("expands resource families over resource-catalog ids", () => {
    const family = surface.families.find((row) => row.family === "resource.max")!;
    expect(family.expand).toBe("resource");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded.map((row) => row.channelId)).toEqual(
      surface.resources.map((r) => `resource.max.${r.id}`)
    );
  });

  it("leaves expand:none families as a single channel id", () => {
    const family = surface.families.find((row) => row.family === "progression.power")!;
    expect(family.expand).toBe("none");
    const expanded = expandDerivedFamily(family, surface.elements, surface.resources);
    expect(expanded[0]?.channelId).toBe("progression.power");
  });
});

describe("resolveDerivedRenderState", () => {
  it("marks join holes as no-producer", () => {
    expect(resolveDerivedRenderState("combat.power.fire", undefined, null)).toBe("no-producer");
  });

  it("marks stub progression channels", () => {
    expect(resolveDerivedRenderState("progression.power", live("progression.power", 1), null)).toBe(
      "stub"
    );
  });

  it("marks capped resist.dot while omni sibling stays uncapped", () => {
    expect(
      resolveDerivedRenderState(
        "status.resist.dot",
        live("status.resist.dot", 0.95, [
          { sourceId: "tree.x", label: "Tree", op: "Increased", value: 0.95 }
        ]),
        "categoryResistCap"
      )
    ).toBe("capped");
    expect(
      resolveDerivedRenderState(
        "status.resist.omni",
        live("status.resist.omni", 0.99, [
          { sourceId: "tree.x", label: "Tree", op: "Increased", value: 0.99 }
        ]),
        "categoryResistCap"
      )
    ).toBe("active");
  });

  it("marks zero untouched as default", () => {
    expect(resolveDerivedRenderState("combat.power.ice", live("combat.power.ice", 0), null)).toBe(
      "default"
    );
  });

  it("marks touched non-zero as active", () => {
    expect(
      resolveDerivedRenderState(
        "combat.power.fire",
        live("combat.power.fire", 100, [
          { sourceId: "aptitude.Might", label: "Aptitude · Might", op: "Flat", value: 100 }
        ]),
        null
      )
    ).toBe("active");
  });
});

describe("bucketContributions", () => {
  it("groups GG-49 sources into stack buckets", () => {
    const buckets = bucketContributions([
      { sourceId: "rpg.progression", label: "Progression", op: "Flat", value: 10 },
      { sourceId: "equip:muzzle:x", label: "Equip", op: "Flat", value: 20 },
      { sourceId: "status:s1", label: "Status", op: "Flat", value: -5 }
    ]);
    expect(buckets.find((b) => b.key === "base")?.value).toBe(10);
    expect(buckets.find((b) => b.key === "equip")?.value).toBe(20);
    expect(buckets.find((b) => b.key === "neg")?.value).toBe(5);
  });
});

describe("isUnchangedState", () => {
  it("treats default and no-producer as suppressible", () => {
    expect(isUnchangedState("default")).toBe(true);
    expect(isUnchangedState("no-producer")).toBe(true);
    expect(isUnchangedState("active")).toBe(false);
  });
});

describe("DerivedTab UI", () => {
  beforeEach(() => {
    try {
      localStorage.removeItem("derived.showUnchanged");
    } catch {
      /* vitest env may lack full Storage */
    }
    sheetQuery = {
      isLoading: false,
      isError: false,
      refetch: sheetRefetch,
      data: {
        instanceId: "actor-1",
        playerId: 1,
        side: "plant",
        typeId: 1,
        displayName: "Emberling",
        level: 24,
        derived: [
          {
            channelId: "combat.power.fire",
            displayName: "Power",
            reading: "Fire power",
            composeKind: "FlatSum",
            value: 2847,
            contributions: [
              {
                sourceId: "equip:muzzle:ember",
                label: "Equip · muzzle (ember)",
                op: "Flat",
                value: 980
              },
              {
                sourceId: "aptitude.Might",
                label: "Aptitude · Might",
                op: "Flat",
                value: 185
              }
            ]
          },
          {
            channelId: "status.resist.butter",
            displayName: "Resist",
            reading: "Butter",
            composeKind: "SumIncreased",
            value: 0.95,
            contributions: [
              {
                sourceId: "tree.ward.butter",
                label: "Tree · ward/butter",
                op: "Increased",
                value: 0.95
              }
            ]
          },
          {
            channelId: "status.resist.omni",
            displayName: "Resist",
            reading: "Omni",
            composeKind: "SumIncreased",
            value: 0.22,
            contributions: [
              {
                sourceId: "tree.ward.omni",
                label: "Tree · ward/omni",
                op: "Increased",
                value: 0.22
              }
            ]
          },
          {
            channelId: "progression.power",
            displayName: "Tier power",
            reading: "stub",
            composeKind: "FlatReplace",
            value: 1,
            contributions: [
              { sourceId: "rpg.progression", label: "Progression", op: "Replace", value: 1 }
            ]
          }
        ],
        primary: []
      }
    };
    derivedQuery = {
      data: undefined,
      isLoading: false,
      isError: false,
      refetch: derivedRefetch
    };
    sheetRefetch.mockClear();
    derivedRefetch.mockClear();
  });

  it("renders cook tabs + element sub-tabs without matrix bloat", () => {
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    expect(screen.getByTestId("derived-tab")).toBeInTheDocument();
    expect(screen.getByTestId("derived-tab-elements")).toBeInTheDocument();
    expect(screen.getByTestId("derived-tab-status")).toBeInTheDocument();
    expect(screen.getByTestId("derived-variant-rail")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("derived-tab-elements"));
    fireEvent.click(screen.getByTestId("derived-variant-fire"));
    fireEvent.click(screen.getByTestId("derived-channel-combat.power.fire"));
    expect(screen.getByTestId("derived-inspector")).toBeInTheDocument();
    expect(screen.getByTestId("derived-contribution-chart")).toBeInTheDocument();
    // One channel per family for Fire — ice sibling is not listed under Fire.
    expect(screen.queryByTestId("derived-channel-combat.power.ice")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("derived-tab-status"));
    const statusVariants = screen.getByTestId("derived-variant-rail");
    // Omni + 24 status-catalog chips (not L2b category rail).
    expect(statusVariants.querySelectorAll('[data-testid^="derived-variant-"]').length).toBe(
      1 + actorSurfaceFixture().statuses.length
    );
    fireEvent.click(screen.getByTestId("derived-variant-butter"));
    expect(screen.getByTestId("derived-channel-status.resist.butter")).toHaveAttribute(
      "data-state",
      "active"
    );
    fireEvent.click(screen.getByTestId("derived-variant-omni"));
    expect(screen.getByTestId("derived-channel-status.resist.omni")).toHaveAttribute(
      "data-state",
      "active"
    );
  });

  it("join hole expands to no-producer when show unchanged", () => {
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    fireEvent.click(screen.getByTestId("derived-show-unchanged"));
    fireEvent.click(screen.getByTestId("derived-tab-elements"));
    fireEvent.click(screen.getByTestId("derived-variant-ice"));
    expect(screen.getByTestId("derived-channel-combat.power.ice")).toHaveAttribute(
      "data-state",
      "no-producer"
    );
  });

  it("GG-19 focuses search once when ready", () => {
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    expect(screen.getByTestId("derived-search")).toHaveFocus();
  });

  it("empty filter shows phase-empty in dock", () => {
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    fireEvent.change(screen.getByTestId("derived-search"), {
      target: { value: "zzz-no-such-channel" }
    });
    const empty = screen.getByTestId("derived-phase-empty");
    expect(empty).toBeInTheDocument();
    expect(empty.textContent ?? "").not.toMatch(/phase-empty/);
    expect(empty).toHaveTextContent(/No channels in this filter/i);
    expect(screen.getByTestId("derived-combat-console")).toBeInTheDocument();
  });

  it("Show unchanged switch toggles aria-checked and reveals join holes", () => {
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    const toggle = screen.getByTestId("derived-show-unchanged");
    expect(toggle).toHaveAttribute("role", "switch");
    expect(toggle).toHaveAttribute("aria-checked", "false");
    fireEvent.click(toggle);
    expect(screen.getByTestId("derived-show-unchanged")).toHaveAttribute("aria-checked", "true");
    fireEvent.click(screen.getByTestId("derived-tab-elements"));
    fireEvent.click(screen.getByTestId("derived-variant-ice"));
    expect(screen.getByTestId("derived-channel-combat.power.ice")).toHaveAttribute(
      "data-state",
      "no-producer"
    );
  });

  it("one-side loading shows loading overlay", () => {
    sheetQuery = { ...sheetQuery, isLoading: true, data: undefined };
    derivedQuery = { ...derivedQuery, isLoading: false };
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    expect(document.querySelector(".phase-loading")).toBeTruthy();
  });

  it("Retry on error refetches sheet and derived", () => {
    sheetQuery = {
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: sheetRefetch
    };
    derivedQuery = {
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: derivedRefetch
    };
    wrap(<DerivedTab data={minimalActor()} surface={actorSurfaceFixture()} />);
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(sheetRefetch).toHaveBeenCalled();
    expect(derivedRefetch).toHaveBeenCalled();
  });
});
