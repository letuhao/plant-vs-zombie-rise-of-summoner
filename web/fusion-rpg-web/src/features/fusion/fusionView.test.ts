import { describe, expect, it } from "vitest";
import { haveNeed, recipeLabel, starPips, STAR_CAPS } from "./fusionView";

const cost = {
  souls: 150,
  shardMaterialId: "shard.common",
  shardCount: 2,
  essenceMaterialId: "essence.fire",
  essenceCount: 2
};

describe("haveNeed", () => {
  it("marks lines and overall affordability", () => {
    const { lines, affordable } = haveNeed(
      cost,
      [{ materialId: "shard.common", qty: 5 }, { materialId: "essence.fire", qty: 1 }],
      1000
    );
    expect(affordable).toBe(false); // essence short
    expect(lines[0]).toMatchObject({ label: "Souls", have: 1000, need: 150, enough: true });
    expect(lines[1]).toMatchObject({ have: 5, need: 2, enough: true });
    expect(lines[2]).toMatchObject({ have: 1, need: 2, enough: false });
  });

  it("missing shelf entries count as zero", () => {
    const { affordable, lines } = haveNeed(cost, [], 0);
    expect(affordable).toBe(false);
    expect(lines.every((l) => !l.enough || l.need === 0)).toBe(true);
  });
});

describe("starPips", () => {
  it("fills to the star and hollows to the cap", () => {
    expect(starPips(1, STAR_CAPS.common)).toBe("★☆☆");
    expect(starPips(3, STAR_CAPS.common)).toBe("★★★");
    expect(starPips(0, STAR_CAPS.rare)).toBe("☆☆☆☆");
    expect(starPips(9, STAR_CAPS.epic)).toBe("★★★★★"); // clamped
  });
});

describe("recipeLabel silhouettes", () => {
  const name = (id: string) => ({ a: "Alpha", b: "Beta", out: "Omega" }[id] ?? id);

  it("discovered recipes show their identity", () => {
    expect(recipeLabel({ discovered: true, resultSpeciesId: "out", inputs: ["a", "b"] }, name))
      .toEqual({ title: "Omega", subtitle: "Alpha + Beta" });
  });

  it("undiscovered recipes stay ??? with optional ingredient hints", () => {
    expect(recipeLabel({ discovered: false, inputs: ["a", "b"] }, name))
      .toEqual({ title: "???", subtitle: "Alpha + Beta …?" });
    expect(recipeLabel({ discovered: false, inputs: null }, name))
      .toEqual({ title: "???", subtitle: "Undiscovered" });
  });
});
