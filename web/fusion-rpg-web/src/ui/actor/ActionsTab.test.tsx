import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { PLAYER_PENDING } from "@/contract/adapt";
import { known, pendingWithReason } from "@/contract/pending";
import type { ActorView } from "@/contract/types";
import { ActionsTab } from "./ActionsTab";

const enableMutate = vi.fn();
const disableMutate = vi.fn();

type UpkeepFixture = { resourceId: string; amountMin: number; amountMax: number; when: string };
let catalogData:
  | { items: { auraId: string; aptitudeId: string; upkeep: UpkeepFixture[] }[] }
  | undefined = {
  items: [
    { auraId: "Might", aptitudeId: "Might", upkeep: [] },
    { auraId: "Fortitude", aptitudeId: "Fortitude", upkeep: [] }
  ]
};
let runtimeData:
  | { activeAuraIds: string[]; equippedAuraIds: string[]; maxActiveAuras: number }
  | undefined = { activeAuraIds: [], equippedAuraIds: ["Might"], maxActiveAuras: 1 };

vi.mock("@/lib/bus/aura", () => ({
  useAuraCatalog: () => ({ data: catalogData, isLoading: catalogData === undefined }),
  useAuraRuntime: () => ({ data: runtimeData, isLoading: runtimeData === undefined }),
  useEnableAura: () => ({ mutate: enableMutate, isPending: false }),
  useDisableAura: () => ({ mutate: disableMutate, isPending: false })
}));

function actorView(): ActorView {
  return {
    instanceId: "a1",
    playerId: 1,
    side: "plant",
    typeId: 3,
    displayName: known("Emberling"),
    phase: "ActiveBound",
    level: 14,
    xp: 2140,
    xpToNext: pendingWithReason(PLAYER_PENDING.xpToNext),
    revision: 1,
    channelSummary: pendingWithReason(PLAYER_PENDING.channelSummary),
    elementTyping: pendingWithReason(PLAYER_PENDING.elementTyping),
    shieldStack: pendingWithReason(PLAYER_PENDING.shieldStack),
    equipSlots: pendingWithReason(PLAYER_PENDING.equipSlots)
  };
}

describe("ActionsTab", () => {
  beforeEach(() => {
    enableMutate.mockReset();
    disableMutate.mockReset();
    catalogData = {
      items: [
        { auraId: "Might", aptitudeId: "Might", upkeep: [] },
        { auraId: "Fortitude", aptitudeId: "Fortitude", upkeep: [] }
      ]
    };
    runtimeData = { activeAuraIds: [], equippedAuraIds: ["Might"], maxActiveAuras: 1 };
  });

  it("renders real aura slots above the still-locked placeholder action grid", () => {
    render(<ActionsTab data={actorView()} />);
    expect(screen.getByTestId("actions-tab-auras")).toBeInTheDocument();
    expect(screen.getByTestId("actions-tab-placeholder")).toBeInTheDocument();
    // The placeholder grid's own reason is unchanged.
    screen.getByTestId("actions-tab-placeholder").querySelectorAll("[title]").forEach((el) => {
      expect(el.getAttribute("title")).toMatch(/action system/i);
    });
  });

  it("an equipped-but-inactive aura shows the Equipped badge and an Enable button", () => {
    render(<ActionsTab data={actorView()} />);
    expect(screen.getByTestId("aura-slot-Might-badge")).toHaveTextContent("Equipped");
    expect(screen.getByTestId("aura-slot-Might-toggle")).toHaveTextContent("Enable");
  });

  it("a real aura not in the loadout renders locked with a real reason, never a generic string", () => {
    render(<ActionsTab data={actorView()} />);
    const fortitude = screen.getByTestId("aura-slot-Fortitude");
    expect(fortitude).toHaveAttribute("title", expect.stringContaining("Not equipped"));
    expect(screen.queryByTestId("aura-slot-Fortitude-toggle")).not.toBeInTheDocument();
  });

  it("clicking Enable calls the enable mutation with the real aura id", () => {
    render(<ActionsTab data={actorView()} />);
    fireEvent.click(screen.getByTestId("aura-slot-Might-toggle"));
    expect(enableMutate).toHaveBeenCalledWith("Might", expect.anything());
  });

  it("enabling at the cap names the aura that switched off (GG-55)", () => {
    runtimeData = { activeAuraIds: ["Fortitude"], equippedAuraIds: ["Might", "Fortitude"], maxActiveAuras: 1 };
    render(<ActionsTab data={actorView()} />);

    fireEvent.click(screen.getByTestId("aura-slot-Might-toggle"));
    const [, options] = enableMutate.mock.calls[0] as [string, { onSuccess: (r: unknown) => void }];
    act(() => {
      options.onSuccess({ playerId: 1, enabledAuraId: "Might", evictedAuraId: "Fortitude", activeAuraIds: ["Might"] });
    });

    expect(screen.getByTestId("aura-slot-Fortitude-refusal")).toHaveTextContent(/Might took its slot/);
  });

  it("a refusal names which reason, not a generic failure (GG-55)", () => {
    runtimeData = { activeAuraIds: [], equippedAuraIds: [], maxActiveAuras: 1 };
    catalogData = { items: [{ auraId: "Might", aptitudeId: "Might", upkeep: [] }] };
    // "Might" renders locked (not equipped) so there is no toggle to click here directly; instead
    // exercise the refusal path on an equipped aura whose enable call the server refuses.
    runtimeData = { activeAuraIds: [], equippedAuraIds: ["Might"], maxActiveAuras: 1 };
    render(<ActionsTab data={actorView()} />);

    fireEvent.click(screen.getByTestId("aura-slot-Might-toggle"));
    const [, options] = enableMutate.mock.calls[0] as [string, { onError: (e: unknown) => void }];
    act(() => {
      options.onError(new Error("AlreadyActive"));
    });

    expect(screen.getByTestId("aura-slot-Might-refusal")).toHaveTextContent("Already active");
  });

  it("a real authored upkeep cost is visible before committing (spec-aura-surface.md §2.1)", () => {
    catalogData = {
      items: [{ auraId: "Might", aptitudeId: "Might", upkeep: [{ resourceId: "stamina", amountMin: 5, amountMax: 5, when: "PerTick" }] }]
    };
    render(<ActionsTab data={actorView()} />);
    expect(screen.getByTestId("aura-slot-Might-upkeep")).toHaveTextContent("5 stamina per tick");
  });

  it("renders nothing for upkeep when no cost is authored yet, never a fabricated number", () => {
    render(<ActionsTab data={actorView()} />);
    expect(screen.queryByTestId("aura-slot-Might-upkeep")).not.toBeInTheDocument();
  });

  it("shows an honest loading state before catalog/runtime data arrives", () => {
    catalogData = undefined;
    render(<ActionsTab data={actorView()} />);
    expect(screen.getByTestId("actions-tab-loading")).toBeInTheDocument();
  });
});
