/**
 * The nine blocks, in the plate's own deliberate order (`spec-world-inspector.md` §2): identity
 * first, then the thing that can take the ground away from you, then the two economies, then what
 * is on the ground, then what you can do about it. The Actions cluster is a tenth, unnumbered region
 * — every block above states a fact; Actions is the one region that takes an order.
 */
export const BLOCK_ORDER = [
  "identity",
  "ground",
  "next-turn",
  "sector-loam",
  "territory",
  "slots",
  "forces",
  "warden",
  "dowsing"
] as const;

export type BlockId = (typeof BLOCK_ORDER)[number];
