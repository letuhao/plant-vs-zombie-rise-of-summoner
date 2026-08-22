import { describe, expect, it } from "vitest";
import fixture from "./fixtures/first-light.json";
import type { WorldStateDto } from "./worldTypes";
import { GRID_X, GRID_Y, ownershipOf, sectorLabel, toGraph } from "./worldViewModel";

const world = fixture as WorldStateDto;

describe("worldViewModel", () => {
  it("turns every sector into a node and every lane into an edge", () => {
    const { nodes, edges } = toGraph(world);

    expect(nodes).toHaveLength(world.sectors.length);
    expect(edges).toHaveLength(world.lanes.length);
    expect(new Set(nodes.map((n) => n.id)).size).toBe(nodes.length);
  });

  it("places sectors on the authored grid rather than laying them out", () => {
    const { nodes } = toGraph(world);
    const home = nodes.find((n) => n.id === "homeworld")!;
    const ember = nodes.find((n) => n.id === "ember-hollow")!;

    expect(home.position).toEqual({ x: 0, y: 0 });
    expect(ember.position).toEqual({ x: 2 * GRID_X, y: -1 * GRID_Y });
  });

  it("reads the player faction from the payload instead of assuming a name", () => {
    const { nodes } = toGraph(world);
    expect(nodes.find((n) => n.id === "homeworld")!.data.ownership).toBe("mine");
    expect(nodes.find((n) => n.id === "ash-waste")!.data.ownership).toBe("neutral");

    const renamed: WorldStateDto = {
      ...world,
      factions: world.factions.map((f) => (f.kind === "Player" ? { ...f, factionId: "someone-else" } : f))
    };
    expect(toGraph(renamed).nodes.find((n) => n.id === "homeworld")!.data.ownership).toBe("enemy");
  });

  it("marks a slot guard as intact, cleared, or never guarded at all", () => {
    const ember = toGraph(world).nodes.find((n) => n.id === "ember-hollow")!;

    expect(ember.data.slots.find((s) => s.slotIndex === 0)!.guard).toBe("none");
    expect(ember.data.slots.find((s) => s.slotIndex === 2)!.guard).toBe("intact");
  });

  it("calls a sector claimable only when every guard is down and nothing hostile is in it", () => {
    const before = toGraph(world).nodes.find((n) => n.id === "ember-hollow")!;
    expect(before.data.claimable).toBe(false);

    const cleared: WorldStateDto = {
      ...world,
      sectors: world.sectors.map((s) =>
        s.sectorId === "ember-hollow"
          ? { ...s, slots: s.slots.map((sl) => ({ ...sl, guardState: "Cleared" })) }
          : s
      )
    };
    expect(toGraph(cleared).nodes.find((n) => n.id === "ember-hollow")!.data.claimable).toBe(true);

    // ...and an enemy you can see standing in it takes it straight back off the table. Forces come
    // from belief now, not from the entity list — that only ever carries your own.
    const occupied: WorldStateDto = {
      ...cleared,
      sectors: cleared.sectors.map((s) =>
        s.sectorId === "ember-hollow"
          ? {
              ...s,
              forces: [
                {
                  entityId: "e-wild-pack-1",
                  ownerFactionId: "wild",
                  kind: "Warband",
                  exact: false,
                  strength: 0,
                  bandName: "warband",
                  bandCeiling: 1499
                }
              ]
            }
          : s
      )
    };
    expect(toGraph(occupied).nodes.find((n) => n.id === "ember-hollow")!.data.claimable).toBe(false);
  });

  it("never calls a sector you already hold claimable", () => {
    expect(toGraph(world).nodes.find((n) => n.id === "homeworld")!.data.claimable).toBe(false);
  });

  it("puts each force on the sector it stands in", () => {
    const { nodes } = toGraph(world);

    expect(nodes.find((n) => n.id === "homeworld")!.data.forces.map((f) => f.entityId))
      .toEqual(["e-dave-legion-1"]);
    expect(nodes.find((n) => n.id === "ash-waste")!.data.forces[0].ownership).toBe("enemy");
    expect(nodes.find((n) => n.id === "verdant-shelf")!.data.forces).toHaveLength(0);
  });

  it("puts a marching force on its lane, with how far along it is", () => {
    const marching: WorldStateDto = {
      ...world,
      entities: world.entities.map((e) =>
        e.entityId === "e-dave-legion-1"
          ? {
              ...e,
              atSectorId: null,
              onLaneId: "l-home-ember",
              onLaneTowardSectorId: "ember-hollow",
              laneProgressMilli: 420
            }
          : e
      )
    };

    const onTheRoad: WorldStateDto = {
      ...marching,
      // Belief follows the force off the ground it left.
      sectors: marching.sectors.map((s) => (s.sectorId === "homeworld" ? { ...s, forces: [] } : s))
    };

    const lane = toGraph(onTheRoad).edges.find((e) => e.id === "l-home-ember")!;
    expect(lane.data.forces).toHaveLength(1);
    expect(lane.data.forces[0]).toMatchObject({
      entityId: "e-dave-legion-1",
      progressMilli: 420,
      towardSectorId: "ember-hollow"
    });

    // ...and it is no longer drawn in the sector it left.
    expect(toGraph(onTheRoad).nodes.find((n) => n.id === "homeworld")!.data.forces).toHaveLength(0);
  });

  it("shows a surveyed force's exact strength and a glimpsed one's band", () => {
    const surveyed = toGraph(world).nodes.find((n) => n.id === "homeworld")!.data.forces[0];
    expect(surveyed.exact).toBe(true);
    expect(surveyed.strength).toBeGreaterThan(0);

    // ash-waste is only rumoured, so its occupant is banded rather than counted — showing a number
    // there would imply a head count nobody made.
    const glimpsed = toGraph(world).nodes.find((n) => n.id === "ash-waste")!.data.forces[0];
    expect(glimpsed.exact).toBe(false);
    expect(glimpsed.strength).toBe(0);
    expect(glimpsed.bandName).not.toBe("");
  });

  it("marks a severed lane", () => {
    const cut: WorldStateDto = {
      ...world,
      lanes: world.lanes.map((l) => (l.laneId === "l-home-ember" ? { ...l, state: "Severed" } : l))
    };

    expect(toGraph(cut).edges.find((e) => e.id === "l-home-ember")!.data.severed).toBe(true);
    expect(toGraph(world).edges.find((e) => e.id === "l-home-ember")!.data.severed).toBe(false);
  });

  it("shrouds a sector nobody has looked at", () => {
    const { nodes } = toGraph(world);
    expect(nodes.find((n) => n.id === "black-gate")!.data.unknown).toBe(true);
    expect(nodes.find((n) => n.id === "homeworld")!.data.unknown).toBe(false);
  });

  it("remembers where a force was on the lane, so the marker slides instead of snapping", () => {
    const onLane = (progress: number): WorldStateDto => ({
      ...world,
      entities: world.entities.map((e) =>
        e.entityId === "e-dave-legion-1"
          ? {
              ...e,
              atSectorId: null,
              onLaneId: "l-home-ember",
              onLaneTowardSectorId: "ember-hollow",
              laneProgressMilli: progress
            }
          : e
      )
    });

    const moved = toGraph(onLane(700), onLane(200));
    expect(moved.edges.find((e) => e.id === "l-home-ember")!.data.forces[0]).toMatchObject({
      fromMilli: 200,
      alongMilli: 700
    });

    // No history means it simply appears where it is — a force that just stepped onto the lane.
    const fresh = toGraph(onLane(700));
    expect(fresh.edges.find((e) => e.id === "l-home-ember")!.data.forces[0].fromMilli).toBeUndefined();
  });

  it("mirrors the remembered position too, for a march against the lane's direction", () => {
    const against = (progress: number): WorldStateDto => ({
      ...world,
      entities: world.entities.map((e) =>
        e.entityId === "e-dave-legion-1"
          ? {
              ...e,
              atSectorId: null,
              onLaneId: "l-ember-ash",
              onLaneTowardSectorId: "ember-hollow",
              laneProgressMilli: progress
            }
          : e
      )
    });

    // l-ember-ash runs ember-hollow → ash-waste, so travelling toward ember-hollow is backwards.
    const force = toGraph(against(600), against(100)).edges
      .find((e) => e.id === "l-ember-ash")!.data.forces[0];

    expect(force).toMatchObject({ fromMilli: 900, alongMilli: 400 });
  });

  it("is a pure fold — the same payload gives an equal graph every time", () => {
    expect(toGraph(world)).toEqual(toGraph(world));
  });
});

describe("helpers", () => {
  it("titles a sector id without touching the id itself", () => {
    expect(sectorLabel("ember-hollow")).toBe("Ember Hollow");
    expect(sectorLabel("homeworld")).toBe("Homeworld");
    expect(sectorLabel("")).toBe("");
  });

  it("treats an unowned thing as neutral, not as the enemy's", () => {
    expect(ownershipOf(null, "dave")).toBe("neutral");
    expect(ownershipOf("dave", "dave")).toBe("mine");
    expect(ownershipOf("wild", "dave")).toBe("enemy");
    expect(ownershipOf("dave", null)).toBe("enemy");
  });
});
