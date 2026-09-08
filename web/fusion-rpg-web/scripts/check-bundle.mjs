// GG-38 (tech-stack.md §6): the entry chunk is the one thing every launch pays for, so it gets a
// hard ceiling and a check that a heavy dependency can't sneak back into it. Run after `npm run
// build` (or via `npm run build:check`) — reads the real build output, not an estimate.
import { existsSync, readFileSync } from "node:fs";
import { gzipSync } from "node:zlib";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(scriptDir, "../../../src/FusionRpg.Server/wwwroot");
const indexHtmlPath = path.join(wwwroot, "index.html");

const ENTRY_BUDGET_BYTES = 180 * 1024; // tech-stack.md §6

if (!existsSync(indexHtmlPath)) {
  console.error(`check-bundle: ${indexHtmlPath} not found — run "npm run build" first.`);
  process.exit(1);
}

const indexHtml = readFileSync(indexHtmlPath, "utf8");
const entryMatch = indexHtml.match(/<script[^>]+type="module"[^>]+src="\.\/(assets\/[^"]+\.js)"/);
if (!entryMatch) {
  console.error("check-bundle: could not find the entry <script type=\"module\"> tag in index.html.");
  process.exit(1);
}

const entryRelativePath = entryMatch[1];
const entryPath = path.join(wwwroot, entryRelativePath);
const entrySource = readFileSync(entryPath, "utf8");
const entryGzipBytes = gzipSync(Buffer.from(entrySource, "utf8")).length;

let failed = false;

if (entryGzipBytes > ENTRY_BUDGET_BYTES) {
  console.error(
    `check-bundle: entry chunk (${entryRelativePath}) is ${(entryGzipBytes / 1024).toFixed(1)} KB gz, ` +
      `over the ${(ENTRY_BUDGET_BYTES / 1024).toFixed(0)} KB budget.`
  );
  failed = true;
} else {
  console.log(
    `check-bundle: entry chunk (${entryRelativePath}) is ${(entryGzipBytes / 1024).toFixed(1)} KB gz ` +
      `(budget ${(ENTRY_BUDGET_BYTES / 1024).toFixed(0)} KB) — OK`
  );
}

// A budget without this half passes the day someone imports Phaser at the top of a shared module
// — the number alone can't tell "180 KB of app code" from "180 KB because Phaser got smaller".
if (entrySource.includes("Phaser")) {
  console.error(`check-bundle: "Phaser" found inside the entry chunk (${entryRelativePath}) — it must load with the lawn stage, not the shell.`);
  failed = true;
} else {
  console.log("check-bundle: Phaser is absent from the entry chunk — OK");
}

// `recharts` is fully removed (T19). `@xyflow/react` is gone too (world-stage routing work,
// 2026-09-05 — the old `WorldPage` it backed is deleted and `#/world` now serves the SVG stage),
// so both dependencies are asserted absent here rather than just one.
const packageJson = JSON.parse(readFileSync(path.resolve(scriptDir, "../package.json"), "utf8"));
if (packageJson.dependencies?.recharts) {
  console.error("check-bundle: \"recharts\" is still a dependency — it was supposed to be fully removed (T19).");
  failed = true;
} else {
  console.log("check-bundle: recharts is absent from package.json — OK");
}

if (packageJson.dependencies?.["@xyflow/react"]) {
  console.error("check-bundle: \"@xyflow/react\" is still a dependency — it was supposed to be fully removed (world-stage routing work).");
  failed = true;
} else {
  console.log("check-bundle: @xyflow/react is absent from package.json — OK");
}

if (failed) {
  process.exit(1);
}
