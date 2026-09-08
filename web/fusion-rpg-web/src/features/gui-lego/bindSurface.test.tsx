import { describe, expect, it, beforeEach, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { bindSurface } from "./bindSurface";
import { clearPieceRegistryForTests, registerPiece } from "./pieceRegistry";
import { clearRecipeRegistryForTests, registerRecipe } from "./recipeRegistry";
import { createSurfaceBus } from "./createSurfaceBus";
import { RecipeMount } from "@/ui/gui-lego/RecipeMount";
import type { RecipeDocument } from "./types";

const fixtureRecipe: RecipeDocument = {
  surfaceId: "fixture-array",
  root: {
    piece: "surface-shell",
    instanceId: "shell:fx",
    bind: "vm",
    slots: {
      railPrimary: {
        piece: "rail-primary",
        instanceId: "rail:primary",
        bind: "vm.primaryRail",
        slots: {
          chips: {
            $bindArray: "vm.primaryRail.chips",
            piece: "chip",
            instanceIdTemplate: "chip:primary:{id}"
          }
        }
      },
      main: {
        piece: "family-list",
        instanceId: "families:fx",
        bind: "vm.families",
        slots: {
          blocks: {
            $bindArray: "vm.families",
            piece: "family-block",
            instanceIdTemplate: "family:{familyId}",
            slots: {
              rows: {
                $bindArray: "rows",
                piece: "channel-row",
                instanceIdTemplate: "row:{channelId}"
              }
            }
          }
        }
      }
    }
  },
  lifecycleOverlays: {
    loading: { piece: "phase-loading", instanceId: "overlay:loading", bind: "vm.phasePayload" },
    empty: { piece: "phase-empty", instanceId: "overlay:empty", bind: "vm.phasePayload" },
    error: { piece: "phase-error", instanceId: "overlay:error", bind: "vm.phasePayload" }
  }
};

function readyVm() {
  return {
    phase: "ready",
    revision: 3,
    piece: "surface-shell",
    instanceId: "shell:fx",
    primaryRail: {
      piece: "rail-primary",
      instanceId: "rail:primary",
      phase: "ready",
      chips: [
        { piece: "chip", id: "elements", label: "Elements", phase: "ready", selected: true },
        { piece: "chip", id: "status", label: "Status", phase: "ready", selected: false }
      ]
    },
    families: [
      {
        piece: "family-block",
        familyId: "power",
        title: "Power",
        phase: "ready",
        rows: [
          {
            piece: "channel-row",
            channelId: "combat.power.fire",
            phase: "ready",
            title: "Power"
          }
        ]
      }
    ],
    phasePayload: { piece: "phase-loading", phase: "loading", message: "Loading…" }
  };
}

describe("bindSurface", () => {
  beforeEach(() => {
    clearPieceRegistryForTests();
    clearRecipeRegistryForTests();
    registerRecipe(fixtureRecipe);
    for (const id of [
      "surface-shell",
      "rail-primary",
      "chip",
      "family-list",
      "family-block",
      "channel-row",
      "phase-loading",
      "phase-empty",
      "phase-error"
    ]) {
      registerPiece({ pieceId: id, slots: [] });
    }
  });

  afterEach(() => {
    clearPieceRegistryForTests();
    clearRecipeRegistryForTests();
  });

  it("expands $bindArray with instanceIdTemplate", () => {
    const plan = bindSurface(fixtureRecipe, readyVm());
    expect(plan.root).not.toBeNull();
    const chips = plan.root!.slots.railPrimary as import("./types").MountNode;
    const chipNodes = chips.slots.chips as import("./types").MountNode[];
    expect(chipNodes.map((c) => c.instanceId)).toEqual([
      "chip:primary:elements",
      "chip:primary:status"
    ]);
    const list = plan.root!.slots.main as import("./types").MountNode;
    const blocks = list.slots.blocks as import("./types").MountNode[];
    expect(blocks[0]!.instanceId).toBe("family:power");
    const rows = blocks[0]!.slots.rows as import("./types").MountNode[];
    expect(rows[0]!.instanceId).toBe("row:combat.power.fire");
  });

  it("applies lifecycle overlay when phase is not ready", () => {
    const vm = { ...readyVm(), phase: "loading" };
    const plan = bindSurface(fixtureRecipe, vm);
    expect(plan.root).toBeNull();
    expect(plan.overlay?.pieceId).toBe("phase-loading");
    expect(plan.overlay?.instanceId).toBe("overlay:loading");
  });

  it("attaches themeResolved from themeRef", () => {
    const vm = readyVm();
    (vm.primaryRail.chips[0] as { themeRef?: { kind: string; id: string } }).themeRef = {
      kind: "element",
      id: "fire"
    };
    const plan = bindSurface(fixtureRecipe, vm);
    const chips = (plan.root!.slots.railPrimary as import("./types").MountNode).slots
      .chips as import("./types").MountNode[];
    expect(chips[0]!.payload.themeResolved?.themeId).toBe("element.fire");
    expect(chips[0]!.payload.themeResolved?.paint.accent).toMatch(/^#/);
  });
});

describe("RecipeMount", () => {
  beforeEach(() => {
    clearPieceRegistryForTests();
    registerPiece({
      pieceId: "surface-shell",
      slots: ["railPrimary"],
      factory: ({ slots }) => (
        <div className="console" data-testid="fx-console">
          {slots.railPrimary}
        </div>
      )
    });
    registerPiece({
      pieceId: "rail-primary",
      slots: ["chips"],
      factory: ({ slots }) => (
        <div className="rail-primary" role="tablist" data-testid="fx-rail">
          {slots.chips}
        </div>
      )
    });
    registerPiece({
      pieceId: "chip",
      slots: [],
      factory: ({ payload }) => (
        <button type="button" role="tab" data-instance={payload.instanceId}>
          {String(payload.label ?? "")}
        </button>
      )
    });
  });

  afterEach(() => clearPieceRegistryForTests());

  it("renders chips as direct children of rail (no contents wrapper)", () => {
    const recipe: RecipeDocument = {
      surfaceId: "fx-dom",
      root: {
        piece: "surface-shell",
        instanceId: "shell",
        bind: "vm",
        slots: {
          railPrimary: {
            piece: "rail-primary",
            instanceId: "rail",
            bind: "vm.primaryRail",
            slots: {
              chips: {
                $bindArray: "vm.primaryRail.chips",
                piece: "chip",
                instanceIdTemplate: "chip:{id}"
              }
            }
          }
        }
      }
    };
    const vm = {
      phase: "ready",
      revision: 1,
      piece: "surface-shell",
      primaryRail: {
        piece: "rail-primary",
        phase: "ready",
        chips: [
          { piece: "chip", id: "a", label: "A", phase: "ready" },
          { piece: "chip", id: "b", label: "B", phase: "ready" }
        ]
      }
    };
    const plan = bindSurface(recipe, vm);
    const bus = createSurfaceBus();
    const { container } = render(<RecipeMount plan={plan} bus={bus} />);
    const rail = container.querySelector(".rail-primary");
    expect(rail).toBeTruthy();
    const kids = [...(rail?.children ?? [])];
    expect(kids.every((el) => el.tagName === "BUTTON")).toBe(true);
    expect(container.querySelector(".contents")).toBeNull();
  });
});
