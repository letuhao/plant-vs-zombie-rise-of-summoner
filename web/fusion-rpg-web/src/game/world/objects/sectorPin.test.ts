import { describe, expect, it, vi } from "vitest";

vi.mock("phaser", () => ({
  default: {
    Display: { Color: { HexStringToColor: () => ({ color: 0xffffff }) } }
  }
}));

import { channelsFor } from "@/stages/world/render/sectorChannels";
import { fogTreatmentFor } from "@/stages/world/render/fogTreatments";
import { pinDescriptor } from "./sectorPin";

describe("pinDescriptor — intel-first pin matrix (spec-world-map-runtime)", () => {
  it("Unknown vs Watched kinds differ — never a dimmed disc", () => {
    const unknown = pinDescriptor({
      channels: channelsFor({ intel: "Unknown", ownership: "open", health: "anchored", stabilityMilli: 0 }),
      fog: fogTreatmentFor("Unknown", 0),
      zoom: "map"
    });
    const watched = pinDescriptor({
      channels: channelsFor({ intel: "Watched", ownership: "yours", health: "anchored", stabilityMilli: 900 }),
      fog: fogTreatmentFor("Watched", 0),
      zoom: "map"
    });
    expect(unknown.kind).toBe("unknown");
    expect(watched.kind).toBe("disc");
    expect(unknown.kind).not.toBe(watched.kind);
  });

  it("descriptor never carries an opacity field", () => {
    const desc = pinDescriptor({
      channels: channelsFor({ intel: "Watched", ownership: "yours", health: "fading", stabilityMilli: 400 }),
      fog: fogTreatmentFor("Watched", 0),
      zoom: "detail"
    });
    expect("opacity" in desc).toBe(false);
    expect("opacity" in desc.channels).toBe(false);
  });

  it("fog forces strip is absent from pin fog encoding", () => {
    for (const intel of ["Scouted", "Rumored"] as const) {
      const desc = pinDescriptor({
        channels: channelsFor({ intel, ownership: "open", health: "anchored", stabilityMilli: 500 }),
        fog: fogTreatmentFor(intel, 3),
        zoom: "map"
      });
      expect(desc.fog).not.toBeNull();
      expect(desc.fog).not.toHaveProperty("forcesStrip");
      expect(JSON.stringify(desc)).not.toContain("who stands here is not known");
    }
  });
});
