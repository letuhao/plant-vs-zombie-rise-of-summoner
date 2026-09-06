import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

/**
 * spec-board-render.md §2 rule 3: "the FE renders and commands; it never rolls. No client prediction
 * of the living set... Interpolating between two server-confirmed states is rendering; extrapolating
 * past the last one is prediction." Today this module has no unit-movement animation of any kind —
 * `bindCamera.ts` writes `setZoom`/`centerOn` synchronously (proven by `bindCamera.test.ts`'s own
 * "exactly one write" tests) and every other file here is pure data transformation. So this scan is a
 * tripwire, not a report of a defect this module could otherwise have: it fails the moment someone
 * adds a tween, an animation-frame loop, or a lerp/extrapolation call to this layer without also
 * deciding how that squares with rule 3 — which is also why `Reduced_motion_is_respected`
 * (spec-board-render.md's own named test) is vacuously true today: there is no motion anywhere in
 * this layer yet for a `prefers-reduced-motion` check to gate.
 */

const SCANNED_DIRS = ["board", "camera"];
const FORBIDDEN_PATTERNS: RegExp[] = [
  /\.tween\(/,
  /\btweens\.add\(/i,
  /requestAnimationFrame\(/,
  /\blerp\(/,
  /\binterpolate\(/,
  /\bextrapolate\(/
];

function sourceFiles(dir: string): Array<{ path: string; contents: string }> {
  return readdirSync(dir)
    .filter((name) => name.endsWith(".ts") && !name.endsWith(".test.ts"))
    .map((name) => {
      const path = join(dir, name);
      return { path, contents: readFileSync(path, "utf8") };
    });
}

describe("board-render has no client-side prediction or animation of the living set (§2 rule 3)", () => {
  const gameDir = dirname(dirname(fileURLToPath(import.meta.url)));
  const files = SCANNED_DIRS.flatMap((d) => sourceFiles(join(gameDir, d)));

  it("scanned at least one file per directory, so this test cannot pass by scanning nothing", () => {
    expect(files.length).toBeGreaterThanOrEqual(SCANNED_DIRS.length);
  });

  it.each(FORBIDDEN_PATTERNS.map((pattern) => [pattern.toString(), pattern] as const))(
    "no file matches %s",
    (_label, pattern) => {
      const hit = files.find((f) => pattern.test(f.contents));
      expect(hit, hit ? `${pattern} matched in ${hit.path}` : undefined).toBeUndefined();
    }
  );
});
