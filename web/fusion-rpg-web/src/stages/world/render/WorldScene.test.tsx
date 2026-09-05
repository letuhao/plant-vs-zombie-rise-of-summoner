import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import fixture from "@/stages/world/fixtures/first-light.json";
import type { WorldStateDto } from "@/lib/bus/world";
import { adaptWorldState } from "@/contract/adapt";
import { WorldScene } from "./WorldScene";

const state = fixture as unknown as WorldStateDto;
const world = adaptWorldState(state);

/**
 * The scene composition, proven against the real byte-pinned fixture (world-stage scene-wiring
 * gap, closed 2026-09-04) — not a hand-built double. This is the one thing four other tasks
 * (W50, W57, W65, W71) were blocked on: real sectors, rendered from real state, clickable.
 */
describe("WorldScene — real sectors render from real state and are clickable", () => {
  it("renders every sector in the fixture", () => {
    render(
      <svg>
        <WorldScene world={world} playerFactionId="dave" selectedSectorId={null} onSelectSector={() => {}} zoom="map" />
      </svg>
    );
    for (const sector of state.sectors) {
      expect(screen.getByTestId(`world-scene-sector-${sector.sectorId}`)).toBeInTheDocument();
    }
  });

  it("a known, owned sector renders through SectorNode, not the unknown silhouette", () => {
    render(
      <svg>
        <WorldScene world={world} playerFactionId="dave" selectedSectorId={null} onSelectSector={() => {}} zoom="map" />
      </svg>
    );
    const homeworld = screen.getByTestId("world-scene-sector-homeworld");
    expect(homeworld.querySelector('[data-testid="sector-node-homeworld"]')).toBeInTheDocument();
  });

  it("a genuinely unknown sector renders the silhouette, never a fabricated card", () => {
    render(
      <svg>
        <WorldScene world={world} playerFactionId="dave" selectedSectorId={null} onSelectSector={() => {}} zoom="map" />
      </svg>
    );
    const unknownSector = state.sectors.find((s) => s.intel === "Unknown");
    expect(unknownSector, "fixture must contain an Unknown sector for this test to mean anything").toBeTruthy();
    const node = screen.getByTestId(`sector-node-${unknownSector!.sectorId}`);
    expect(node).toHaveAttribute("data-shape", "unknown");
  });

  it("clicking a sector dispatches its id — the click target that was missing before this composition existed", async () => {
    const user = userEvent.setup();
    const onSelectSector = vi.fn();
    render(
      <svg>
        <WorldScene world={world} playerFactionId="dave" selectedSectorId={null} onSelectSector={onSelectSector} zoom="map" />
      </svg>
    );
    await user.click(screen.getByTestId("world-scene-sector-homeworld"));
    expect(onSelectSector).toHaveBeenCalledWith("homeworld");
  });

  it("the selected sector carries data-selected=true, and only that one", () => {
    render(
      <svg>
        <WorldScene
          world={world}
          playerFactionId="dave"
          selectedSectorId="homeworld"
          onSelectSector={() => {}}
          zoom="map"
        />
      </svg>
    );
    expect(screen.getByTestId("world-scene-sector-homeworld")).toHaveAttribute("data-selected", "true");
    const others = state.sectors.filter((s) => s.sectorId !== "homeworld");
    for (const s of others) {
      expect(screen.getByTestId(`world-scene-sector-${s.sectorId}`)).toHaveAttribute("data-selected", "false");
    }
  });

  it("renders a lane between two real sectors", () => {
    render(
      <svg>
        <WorldScene world={world} playerFactionId="dave" selectedSectorId={null} onSelectSector={() => {}} zoom="map" />
      </svg>
    );
    const lane = state.lanes[0];
    expect(screen.getByTestId(`lane-${lane.laneId}`)).toBeInTheDocument();
  });
});
