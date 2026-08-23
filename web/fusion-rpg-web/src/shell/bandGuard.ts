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
 * GG-5's guard: nothing outside the token definitions and the six `.band-*`
 * utility classes may set `z-index` or use a Tailwind `z-*` class. Any hit
 * means a surface picked its own stacking tier instead of one of the six.
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
