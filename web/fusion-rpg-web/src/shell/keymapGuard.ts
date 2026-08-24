import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";

export type GuardViolation = {
  file: string;
  line: number;
  text: string;
};

const SCANNABLE_EXTENSIONS = new Set([".ts", ".tsx"]);
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
 * F10 is the injector overlay's own resume hotkey (see `CLAUDE.md`'s local
 * dev notes) — the app must never bind it. `keymap.ts` names it once, to
 * reject it at runtime; that mention is the only allowed one.
 */
const F10_PATTERN = /F10/;

const F10_ALLOWED_FILES = new Set([
  "shell/keymap.ts",
  "shell/keymapGuard.ts",
  // T20's Controls screen renders keymap.ts's own forbidden-keys list (`listForbiddenKeys()`) as
  // a read-only reserved row — it reads the literal, never assigns it.
  "layers/system/SystemLayer.tsx"
]);

export function scanForF10Bindings(srcDir: string): GuardViolation[] {
  return scanLines(srcDir, (relPath) => F10_ALLOWED_FILES.has(relPath), [F10_PATTERN]);
}

/**
 * T3's guard: a global (window/document-level) `keydown` listener is a
 * second owner of Esc/global-verb semantics competing with the keymap (GG-6
 * — the stack is the *single* source of truth). `useGlobalKeys.ts` is the
 * one place this is allowed.
 *
 * `ui/ConfirmDialog.tsx` is a known, accepted pre-existing exception: it
 * predates this refactor, is not on the LayerStack, and is slated to be
 * replaced by `DialogShell` opportunistically as call sites are touched
 * (matches the "no flag day" migration approach used elsewhere in this
 * initiative) rather than as a side effect of building the keymap itself.
 */
const GLOBAL_KEYDOWN_PATTERN = /(window|document)\.addEventListener\(\s*["']keydown["']/;
const ACCEPTED_LEGACY_EXCEPTIONS = new Set(["ui/ConfirmDialog.tsx"]);

export function scanForStrayGlobalKeydownBindings(srcDir: string): GuardViolation[] {
  return scanLines(
    srcDir,
    (relPath) => relPath === "shell/useGlobalKeys.ts" || ACCEPTED_LEGACY_EXCEPTIONS.has(relPath),
    [GLOBAL_KEYDOWN_PATTERN]
  );
}
