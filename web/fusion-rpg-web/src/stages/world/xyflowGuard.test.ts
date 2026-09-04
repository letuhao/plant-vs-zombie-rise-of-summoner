import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

/**
 * world-stage W36: the new stage never imports `@xyflow/react` — a guard, not a review, since a
 * library that quietly stops being imported produces no compile error and no runtime error of its
 * own (the exact failure mode `stageIds.ts`'s own migration-risk note names for `LegionMarker`).
 *
 * Matches a *quoted module specifier* (the exact form a real `import`, a CSS side-effect import, or
 * a `vi.mock(...)` call always uses) rather than a bare substring, so prose that names the library
 * in a comment or a test's own description — this file's own doc comment among them — never trips
 * the guard. A backtick-quoted or unquoted mention of the library name is deliberately not enough
 * to match.
 */
const XYFLOW_REFERENCE = /["']@xyflow\/react(?:\/[^"']*)?["']/;

function walk(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules") continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (/\.(ts|tsx)$/.test(entry)) out.push(full);
  }
  return out;
}

function findReferences(dir: string): { file: string; line: number }[] {
  const hits: { file: string; line: number }[] = [];
  for (const file of walk(dir)) {
    const lines = readFileSync(file, "utf8").split("\n");
    lines.forEach((text, i) => {
      if (XYFLOW_REFERENCE.test(text)) {
        const relPath = relative(join(__dirname, "..", "..", ".."), file).split("\\").join("/");
        hits.push({ file: relPath, line: i + 1 });
      }
    });
  }
  return hits;
}

describe("xyflowGuard", () => {
  it("no file under stages/ imports @xyflow/react", () => {
    expect(findReferences(join(__dirname, ".."))).toEqual([]);
  });

  it("the whole src tree is clean — the old tree's five references were retired early (world-stage routing work, 2026-09-05), not at Phase 4's W108", () => {
    const references = findReferences(join(__dirname, "..", "..")).map((r) => `${r.file}:${r.line}`);

    expect(references).toEqual([]);
  });
});
