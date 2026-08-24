import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";
import type { GuardViolation } from "@/shell/bandGuard";

const SCANNABLE_EXTENSIONS = new Set([".ts", ".tsx"]);
const SKIPPED_DIR_NAMES = new Set(["node_modules", "dist", "coverage"]);
const TEST_FILE_PATTERN = /\.(test|spec)\.[jt]sx?$/;

/**
 * GG-41: developer surfaces keep engine vocabulary — the nine gated dev pages, the ungated but
 * pre-existing debug consoles this refactor deliberately left untouched rather than rewrite
 * (`LawnPage.tsx`'s Spawn/Inspector/Overlay-FX panels, `RosterPage.tsx`, both predate the layer
 * system and are legacy/debug tooling in substance even though neither sits behind the `dev` gate),
 * and this guard's own source (which has to name the banned words to forbid them).
 */
const ALLOW_LISTED_PREFIXES = [
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
  "stages/lawn/",
  // The Phaser game layer (hexGuard.ts already treats `game/` as its own rules zone for the same
  // reason: it's debug/inspector rendering over the live board, not player chrome) and the API
  // client layer (`lib/bus/`: pure fetch/query/mutation code, no JSX, field/param names here
  // mirror the real wire contract by necessity — never player copy).
  "game/",
  "lib/bus/",
  "i18n/vocabularyGuard.ts"
];

// GG-23's own forbidden list — engine/protocol/schema vocabulary that must never appear as
// player-facing copy. Matched as whole words so e.g. "Coldwind" or "revisions" (an unrelated
// plural) don't false-positive.
const BANNED_WORDS = [
  "typeId",
  "ptr",
  "Intent",
  "UniqueActor",
  "Cold",
  "mods_json",
  "Admit",
  "revision",
  "ingest queue",
  "matchKey"
];

const BANNED_WORD_PATTERN = new RegExp(`\\b(${BANNED_WORDS.map((w) => w.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})\\b`);

// A banned word only counts when it could plausibly be rendered as text: inside a quoted string
// literal or JSX text content. A code identifier (`actor.typeId`, `const ptr = ...`, `type: string`)
// is fine — GG-23 forbids the *word appearing as copy*, not the field existing.
const STRING_LITERAL_PATTERN = /"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|`(?:[^`\\]|\\.)*`/g;

// A literal assigned to one of these never renders — it's an internal identifier (a `<select>`
// option's real value, a DOM/React plumbing prop), not player copy. `title`/`placeholder`/
// `aria-label`/`alt` are deliberately absent: those genuinely render (a tooltip, a hint, an
// accessible name), so a banned word there is a real violation, not a false positive.
const NON_RENDERING_ATTR_PATTERN =
  /\b(value|id|key|name|htmlFor|className|class|type|role|href|src|to|path|testId)\s*=\s*$/;

// TypeScript generic type arguments (`useState<"level" | "typeId">(...)`) are never rendered —
// this is a type-level union of allowed values, not text. Detected structurally: a `<` opens a
// generic when it's immediately preceded by an identifier character (a JSX tag's `<` is always
// followed by an identifier, never preceded by one) and the bracketed content is quote-led.
const GENERIC_TYPE_ARGS_PATTERN = /\w<"[^>(]*>/;

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

/** GG-23: scans player-facing source for engine/protocol/schema words appearing inside string
 * literals or JSX text — the shape the rule's own "Testable as" line names — skipping developer
 * surfaces (GG-41) and `data-testid`/import lines, which are identifiers, not player copy. */
export function scanForBannedVocabulary(srcDir: string): GuardViolation[] {
  const violations: GuardViolation[] = [];
  walk(srcDir, (filePath) => {
    const relPath = relative(srcDir, filePath).split("\\").join("/");
    if (ALLOW_LISTED_PREFIXES.some((p) => relPath.startsWith(p) || relPath === p)) return;

    const lines = readFileSync(filePath, "utf8").split(/\r?\n/);
    lines.forEach((line, index) => {
      const trimmed = line.trim();
      if (trimmed.startsWith("//") || trimmed.startsWith("*") || trimmed.startsWith("/*")) return;
      if (line.includes("data-testid") || line.includes("data-test-id")) return;
      if (/^\s*import\s/.test(line) || /\bfrom\s+["']/.test(line)) return;
      if (GENERIC_TYPE_ARGS_PATTERN.test(line)) return;

      // JSX text content: strip tags, check what's left between them.
      const jsxTextSegments = line.match(/>[^<>{]*</g) ?? [];
      for (const seg of jsxTextSegments) {
        const inner = seg.slice(1, -1);
        if (BANNED_WORD_PATTERN.test(inner)) {
          violations.push({ file: relPath, line: index + 1, text: line.trim() });
          return;
        }
      }

      // Quoted string literals. Template-literal `${...}` interpolations are stripped first —
      // `` `#${typeId}` `` renders as "#7" at runtime, never the word "typeId"; only the STATIC
      // parts of a literal are real candidate player copy. A literal assigned to a non-rendering
      // attribute (`value="typeId"` on an <option>) is skipped — the text between the tags is
      // the real rendered copy, already checked above.
      for (const match of line.matchAll(STRING_LITERAL_PATTERN)) {
        const literal = match[0];
        const before = line.slice(0, match.index);
        if (NON_RENDERING_ATTR_PATTERN.test(before)) continue;
        const staticParts = literal.startsWith("`") ? literal.replace(/\$\{[^}]*\}/g, "") : literal;
        if (BANNED_WORD_PATTERN.test(staticParts)) {
          violations.push({ file: relPath, line: index + 1, text: line.trim() });
          return;
        }
      }
    });
  });
  return violations;
}
