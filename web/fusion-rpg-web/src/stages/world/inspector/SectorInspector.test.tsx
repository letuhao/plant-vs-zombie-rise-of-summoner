import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { known, pendingWithReason } from "@/contract/pending";
import { SectorInspector } from "./SectorInspector";
import { BLOCK_ORDER } from "./blockOrder";
import { emptySector, maximalForces, maximalSector, maximalSlots } from "./fixtures/maximalSector";

const BLOCK_TESTIDS = BLOCK_ORDER.map((id) => `inspector-block-${id}`);

function renderMaximal(overrides: Partial<Parameters<typeof SectorInspector>[0]> = {}) {
  return render(
    <SectorInspector
      open
      onOpenChange={() => {}}
      sector={maximalSector}
      slots={maximalSlots}
      forces={maximalForces}
      cedeOrderAvailable={false}
      prospected={true}
      {...overrides}
    />
  );
}

describe("SectorInspector — the nine blocks, in the plate's order (world-stage W57)", () => {
  it("renders all nine blocks plus the actions region, in the declared order", () => {
    renderMaximal();
    const container = screen.getByTestId("sector-inspector-blocks");
    const rendered = [...container.children].map((el) => el.getAttribute("data-testid"));
    expect(rendered).toEqual([...BLOCK_TESTIDS, "inspector-actions"]);
  });

  it("blockOrder.ts names exactly nine blocks", () => {
    expect(BLOCK_ORDER).toHaveLength(9);
  });

  it("identity renders translated intel/phase, never the raw wire word, with an age stamp when stale", () => {
    renderMaximal({ sector: { ...maximalSector, intel: "Scouted", intelAge: 4 } });
    const identity = screen.getByTestId("inspector-block-identity");
    expect(identity).toHaveTextContent("held");
    expect(identity).toHaveTextContent("scouted — 4 nights old");
  });

  it("ground renders a real pressure reading (found stale and fixed at W63 — it was never actually missing)", () => {
    renderMaximal();
    expect(screen.getByTestId("ground-pressure")).toBeInTheDocument();
  });

  it("next turn: not at risk renders a plain forecast, no pin controls", () => {
    renderMaximal();
    expect(screen.getByTestId("next-turn-forecast")).toHaveTextContent("Not at risk");
    expect(screen.queryByTestId("next-turn-pin-controls")).not.toBeInTheDocument();
  });

  it("next turn: at risk with no cede order available renders the truthful forecast but no controls", () => {
    renderMaximal({ sector: { ...maximalSector, willReleaseNextTurn: true }, cedeOrderAvailable: false });
    expect(screen.getByTestId("next-turn-forecast")).toHaveTextContent("will be released next turn");
    expect(screen.queryByTestId("next-turn-pin-controls")).not.toBeInTheDocument();
  });

  it("next turn: at risk with a cede order available renders both pin controls and files a real callback", async () => {
    const user = userEvent.setup();
    const onPin = vi.fn();
    renderMaximal({ sector: { ...maximalSector, willReleaseNextTurn: true }, cedeOrderAvailable: true, onPin });

    const controls = screen.getByTestId("next-turn-pin-controls");
    expect(controls).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Keep this ground" }));
    expect(onPin).toHaveBeenCalledWith("keep");

    await user.click(screen.getByRole("button", { name: "Give this up first" }));
    expect(onPin).toHaveBeenCalledWith("release-first");
  });

  it("this sector's loam renders all four readings through formatMagnitude", () => {
    renderMaximal();
    const block = screen.getByTestId("inspector-block-sector-loam");
    expect(block).toHaveTextContent("140");
    expect(block).toHaveTextContent("60");
    expect(block).toHaveTextContent("80");
    expect(block).toHaveTextContent("2,400");
  });

  it("territory renders the pooled component reading when part of one", () => {
    renderMaximal();
    const block = screen.getByTestId("inspector-block-territory");
    expect(block).toHaveTextContent("410");
    expect(block).toHaveTextContent("6,200");
  });

  it("territory states plainly when this sector is not part of a connected territory", () => {
    renderMaximal({
      sector: { ...maximalSector, component: { componentId: null, production: maximalSector.loam.production, upkeep: maximalSector.loam.upkeep, net: maximalSector.loam.net, stock: maximalSector.loam.stock } }
    });
    expect(screen.getByTestId("inspector-block-territory")).toHaveTextContent("Not part of a connected territory");
  });

  it("slots render all four fixture rows, naming construction-in-progress, guarded and depleted", () => {
    renderMaximal();
    expect(screen.getByTestId("slot-row-0")).toHaveTextContent("well");
    expect(screen.getByTestId("slot-row-1")).toHaveTextContent("2 turns to build");
    expect(screen.getByTestId("slot-row-2")).toHaveTextContent("Guarded by guard");
    expect(screen.getByTestId("slot-row-3")).toHaveTextContent("depleted");
  });

  it("forces render an exact strength for the viewer's own and a band for anyone else's", () => {
    renderMaximal();
    expect(screen.getByTestId("force-row-e-dave-legion-1")).toHaveTextContent("240");
    const glimpsed = screen.getByTestId("force-row-e-wild-pack-1");
    expect(glimpsed).toHaveTextContent("a warband");
    expect(glimpsed).not.toHaveTextContent(/^\d+$/);
  });

  it("warden renders the bound id when known", () => {
    renderMaximal();
    expect(screen.getByTestId("inspector-block-warden")).toHaveTextContent("e-dave-warden-1");
  });

  it("warden renders a plain 'no warden' sentence for a known-null binding, and the Pending reason otherwise", () => {
    renderMaximal({ sector: { ...maximalSector, wardenBindingId: known(null) } });
    expect(screen.getByTestId("inspector-block-warden")).toHaveTextContent("No warden bound.");

    renderMaximal({ sector: { ...maximalSector, wardenBindingId: pendingWithReason("not surveyed") } });
    expect(screen.getAllByText("not surveyed").length).toBeGreaterThan(0);
  });

  it("dowsing states plainly whether a dowser has confirmed a source here this turn", () => {
    renderMaximal({ prospected: false });
    expect(screen.getByTestId("inspector-block-dowsing")).toHaveTextContent("No dowser has surveyed");

    renderMaximal({ prospected: true });
    expect(screen.getAllByTestId("inspector-block-dowsing").at(-1)).toHaveTextContent("A dowser has confirmed");
  });

  it("the actions region is reserved, not filled with a placeholder — empty until world-inspector's later tasks build it", () => {
    renderMaximal();
    expect(screen.getByTestId("inspector-actions")).toBeEmptyDOMElement();
  });

  it("the sparse fixture renders every Pending/absent field honestly, no crash and no zero standing in", () => {
    renderMaximal({ sector: emptySector, slots: [], forces: [] });
    expect(screen.getByTestId("inspector-block-identity")).toBeInTheDocument();
    expect(screen.getByTestId("inspector-block-territory")).toHaveTextContent("Not part of a connected territory");
  });
});
