import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";

export type GuardViolation = {
  file: string;
  line: number;
  text: string;
};

const SCANNABLE_EXTENSIONS = new Set([".ts", ".tsx", ".css"]);
const SKIPPED_DIR_NAMES = new Set(["node_modules", "dist", "coverage"]);
const TEST_FILE_PATTERN = /\.(test|spec)\.[jt]sx?$/;

function walk(rootDir: string, onFile: (filePath: string) => void): void {
  for (const entry of readdirSync(rootDir)) {
    if (SKIPPED_DIR_NAMES.has(entry)) continue;
    const fullPath = join(rootDir, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) {
      walk(fullPath, onFile);
    } else if (SCANNABLE_EXTENSIONS.has(extname(fullPath)) && !TEST_FILE_PATTERN.test(entry)) {
      onFile(fullPath);
    }
  }
}

function scanLines(
  rootDir: string,
  shouldSkip: (relPath: string) => boolean,
  patterns: RegExp[]
): GuardViolation[] {
  const violations: GuardViolation[] = [];
  walk(rootDir, (filePath) => {
    const relPath = relative(rootDir, filePath).split("\\").join("/");
    if (shouldSkip(relPath)) return;
    const lines = readFileSync(filePath, "utf8").split(/\r?\n/);
    lines.forEach((line, index) => {
      if (patterns.some((pattern) => pattern.test(line))) {
        violations.push({ file: relPath, line: index + 1, text: line.trim() });
      }
    });
  });
  return violations;
}

/**
 * GG-5's guard: nothing outside the token definitions and the seven `.band-*`
 * utility classes (`stage`/`scrim`/`hud`/`panel`/`dialog`/`toast`/`system` — `scrim` added
 * 2026-09-04, world-stage W55's GG-5 amendment) may set `z-index` or use a Tailwind `z-*` class.
 * Any hit means a surface picked its own stacking tier instead of one of the seven.
 */
const STRAY_Z_INDEX_PATTERNS = [
  /z-index\s*:/,
  /(^|[\s"'`])z-\[[^\]]+\]/,
  /(^|[\s"'`])z-(0|10|20|30|40|50)\b/
];

export function scanForStrayZIndex(srcDir: string): GuardViolation[] {
  return scanLines(
    srcDir,
    (relPath) => relPath === "theme/tokens.css",
    STRAY_Z_INDEX_PATTERNS
  );
}

/**
 * T1's other guard: `LayerStack` is a shell-only mechanism (GG-1's stack is
 * one owner of visibility). Only the shells that register themselves in it —
 * currently `PanelShell` and `DialogShell` — and the store's own test may
 * import it.
 */
const LAYER_STACK_IMPORT_PATTERN = /from\s+["'](?:@\/shell\/layerStack|\.{1,2}\/.*layerStack)["']/;

export function scanForLayerStackImports(srcDir: string): GuardViolation[] {
  return scanLines(
    srcDir,
    (relPath) => relPath.startsWith("shell/"),
    [LAYER_STACK_IMPORT_PATTERN]
  );
}

/**
 * GG-53 / D6: only run-ending results may open a blocking (band-3) surface unprompted — level-ups,
 * drops and contract offers report at band 4 and queue for the sanctum instead. No run-result
 * screen exists yet (it's part of T24, excluded this phase), so today's real invariant is
 * narrower and fully checkable: nothing outside `src/shell/`, `ui/ConfirmDialog.tsx` and the
 * `world-confirms` module's three dialogs may render `DialogShell` or claim the `band-dialog`
 * class. Each is exempt by construction, not by assumption — a fully controlled component (the
 * caller's own `open` state decides visibility), never self-opening from a background event
 * (`stages/world/confirms/noSelfOpen.test.tsx`, world-stage W105, is that module's own standing
 * proof); GG-41 also exempts developer surfaces from GG-53 entirely, so the same allow-list
 * `vocabularyGuard.ts` uses for those applies here.
 */
const DIALOG_BAND_PATTERNS = [/<DialogShell\b/, /\bband-dialog\b/, /band:\s*["']dialog["']/];

const DIALOG_BAND_ALLOWED_PATHS = new Set([
  "ui/ConfirmDialog.tsx",
  "stages/world/confirms/CommitLegionDialog.tsx",
  "stages/world/confirms/BindWardenDialog.tsx",
  "stages/world/confirms/ReleaseGroundDialog.tsx"
]);

const DEV_SURFACE_PREFIXES = [
  "dev/",
  "features/status/",
  "features/stats/",
  "features/pvz-activity/",
  "features/icon-dump/",
  "features/almanac-dump/",
  "features/cheats/",
  "features/sim/",
  "features/log/",
  "features/metrics/",
  "features/lawn/",
  "features/roster/",
  "stages/lawn/"
];

export function scanForUnvettedDialogBandOwners(srcDir: string): GuardViolation[] {
  return scanLines(
    srcDir,
    (relPath) =>
      relPath.startsWith("shell/") ||
      relPath === "theme/tokens.css" ||
      DIALOG_BAND_ALLOWED_PATHS.has(relPath) ||
      DEV_SURFACE_PREFIXES.some((p) => relPath.startsWith(p)),
    DIALOG_BAND_PATTERNS
  );
}
