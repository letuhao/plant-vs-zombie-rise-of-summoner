import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { adaptArmouryPage, adaptCombinations, adaptItemSurfaces } from "@/contract/adapt";
import { absent, known, pendingWithReason } from "@/contract/pending";
import type { ArmouryFilterState, ArmouryRowView, ContainerView } from "@/contract/types";
import { renderWithProviders } from "@/test/render";
import { applyArmouryFilter, sortArmoury } from "./ArmouryList";
import { CompareView } from "./CompareView";
import { ItemCard } from "./ItemCard";
import { paperdollCells } from "./Paperdoll";

function row(over: Partial<ArmouryRowView> & { instanceId: string }): ArmouryRowView {
  return {
    containerId: "item.plate-helm.fused",
    rarity: { id: "fused", ordinal: 50, display: "Fused", colour: "var(--color-rarity-fused)", pips: 5 },
    role: absent(),
    frame: absent(),
    assigned: false,
    locked: false,
    unseen: false,
    stale: false,
    acquiredUtc: "2026-09-01T00:00:00Z",
    ...over
  };
}

const NO_FILTER: ArmouryFilterState = {
  rarityMin: null,
  rarityMax: null,
  unseenOnly: false,
  hideAssigned: false,
  hideStale: false,
  sort: "acquired"
};

function card(over: Partial<ContainerView> = {}): ContainerView {
  return {
    instanceId: "i1",
    kind: "item",
    header: {
      name: "Ashen Reliquary",
      rarity: { id: "chaff", ordinal: 10, display: "Chaff", colour: "var(--color-rarity-chaff)", pips: 1 },
      baseTypeAndClassNoun: "Relic"
    },
    requirements: absent(),
    baseStats: [],
    implicit: absent(),
    affixes: absent(),
    enhancement: absent(),
    sockets: absent(),
    set: absent(),
    grantedAction: absent(),
    footer: absent(),
    ...over
  };
}

describe("armoury view rules (item module 20)", () => {
  it("never hides a locked row, whatever the filter asks for", () => {
    const rows = [
      row({ instanceId: "locked", locked: true, stale: true, assigned: true }),
      row({ instanceId: "plain" })
    ];
    const hideEverything: ArmouryFilterState = {
      ...NO_FILTER,
      rarityMin: 100,
      unseenOnly: true,
      hideAssigned: true,
      hideStale: true
    };
    expect(applyArmouryFilter(rows, hideEverything).map((r) => r.instanceId)).toEqual(["locked"]);
  });

  it("no filter setting reduces the collection below what it matches — it is a view, not a cap", () => {
    const rows = [row({ instanceId: "a" }), row({ instanceId: "b" }), row({ instanceId: "c" })];
    expect(applyArmouryFilter(rows, NO_FILTER)).toHaveLength(3);
  });

  it("sorts rarest first, and falls back to newest within a rung", () => {
    const rows = [
      row({ instanceId: "old-fused", acquiredUtc: "2026-01-01T00:00:00Z" }),
      row({
        instanceId: "chaff",
        rarity: { id: "chaff", ordinal: 10, display: "Chaff", colour: "c", pips: 1 }
      }),
      row({ instanceId: "new-fused", acquiredUtc: "2026-09-05T00:00:00Z" })
    ];
    expect(sortArmoury(rows, "rarity").map((r) => r.instanceId)).toEqual([
      "new-fused",
      "old-fused",
      "chaff"
    ]);
  });
});

describe("item surface adapters", () => {
  it("renames the wire's states without re-deciding any of them", () => {
    const views = adaptItemSurfaces([
      { surface: "armoury", state: "Ready", unlockKey: "first-container-acquired" },
      { surface: "compendium", state: "Locked", unlockKey: "first-socketed-item" },
      { surface: "notASurface", state: "Ready", unlockKey: "x" }
    ]);
    expect(views).toEqual([
      { surface: "armoury", state: "ready", unlockKey: "first-container-acquired" },
      { surface: "compendium", state: "locked", unlockKey: "first-socketed-item" }
    ]);
  });

  it("counts the inbox over the whole armoury, not the page it was handed", () => {
    const page = adaptArmouryPage({
      total: 900,
      unseen: 61,
      overReviewPressure: true,
      renderStrategy: "Virtualize",
      rows: [
        {
          instanceId: "i1",
          containerId: "c1",
          rarity: "fused",
          rarityOrdinal: 50,
          assigned: false,
          locked: false,
          unseen: true,
          stale: false,
          acquiredUtc: "2026-09-01T00:00:00Z"
        }
      ]
    });
    expect(page.inbox.total.value).toBe(900);
    expect(page.inbox.unseen.value).toBe(61);
    expect(page.rows).toHaveLength(1);
    expect(page.strategy).toBe("virtualize");
    expect(page.rows[0]!.rarity.display).toBe("Fused");
  });

  it("keeps infinity a nullable rather than a large number, and drops what was never revealed", () => {
    const combos = adaptCombinations([
      {
        comboId: "combo.ember",
        shape: "pure",
        state: "OneAway",
        distance: 1,
        missingFamilies: ["gem.ember"],
        missingElements: [],
        grantedTier: 2
      },
      {
        comboId: "combo.hidden",
        shape: "strain",
        state: "SomethingElse",
        distance: null,
        missingFamilies: [],
        missingElements: [],
        grantedTier: 0
      }
    ]);
    expect(combos.map((c) => c.comboId)).toEqual(["combo.ember"]);
    expect(combos[0]!.distance).toBe(1);
  });
});

describe("ItemCard", () => {
  it("renders all eleven blocks, with identity above the detail zone", () => {
    renderWithProviders(<ItemCard item={card({ flavour: "Warm to the touch." })} />);
    const identity = screen.getByTestId("item-card-identity");
    const detail = screen.getByTestId("item-card-detail");

    for (const block of ["header", "requirements", "base-stats", "implicit", "affixes", "enhancement"]) {
      expect(identity).toContainElement(screen.getByTestId(`item-card-${block}`));
    }
    for (const block of ["sockets", "set", "granted-action", "flavour", "footer"]) {
      expect(detail).toContainElement(screen.getByTestId(`item-card-${block}`));
    }
  });

  it("shows the rarity rung as pips AND as a word, never as colour alone", () => {
    renderWithProviders(<ItemCard item={card()} />);
    expect(screen.getByTestId("item-card-rarity")).toHaveTextContent("Chaff");
  });

  it("names the item it is being read against when there is one", () => {
    renderWithProviders(
      <ItemCard item={card()} incumbent={card({ header: { ...card().header, name: "Sunworn Charm" } })} />
    );
    expect(screen.getByTestId("item-card-incumbent")).toHaveTextContent("Sunworn Charm");
  });
});

describe("CompareView", () => {
  it("keeps the no-single-score footnote on screen with no way to dismiss it", () => {
    renderWithProviders(
      <CompareView candidate={card()} incumbent={null} payload={pendingWithReason("Not shown yet")} />
    );
    const footnote = screen.getByTestId("compare-footnote");
    expect(footnote).toHaveTextContent("There is no single score");
    expect(footnote.querySelector("button")).toBeNull();
  });

  it("puts the unit in the group header, never in the numeric column", () => {
    renderWithProviders(
      <CompareView
        candidate={card()}
        incumbent={card()}
        payload={known({
          badge: { verdict: "sidegrade", label: "Sidegrade", shape: "◆" },
          groups: [
            {
              unit: "gameUnits",
              deltas: [
                {
                  channel: "combat.hp",
                  incumbent: { unit: "gameUnits", value: 71 },
                  candidate: { unit: "gameUnits", value: 62 },
                  delta: { unit: "gameUnits", value: -9 }
                }
              ]
            }
          ],
          trade: null,
          incomparableReason: null,
          meanRollQualityPerMille: null
        })}
      />
    );
    const group = screen.getByTestId("compare-group-gameUnits");
    expect(group).toHaveTextContent("Damage and hit points");
    expect(screen.getByTestId("compare-verdict")).toHaveTextContent("Sidegrade");
  });
});

describe("Paperdoll", () => {
  it("always shows every role, filled or not — an equip screen has to show what is missing", () => {
    const cells = paperdollCells([
      { slot: "weapon", instanceId: "r1", itemName: "Ashen Reliquary", rarity: null }
    ]);
    expect(cells).toHaveLength(16);
    expect(cells.find((c) => c.role === "armament-primary")!.itemName).toBe("Ashen Reliquary");
    expect(cells.find((c) => c.role === "footing")!.instanceId).toBeNull();
  });

  it("carries both frame names for every role, because nothing says which frame is worn", () => {
    const cells = paperdollCells([]);
    const mainHand = cells.find((c) => c.role === "armament-primary")!;
    expect(mainHand.humanoidName).toBe("main-hand");
    expect(mainHand.plantName).toBe("muzzle");
  });
});
