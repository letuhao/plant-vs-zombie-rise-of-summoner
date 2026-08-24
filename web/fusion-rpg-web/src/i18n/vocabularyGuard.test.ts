import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { scanForBannedVocabulary } from "./vocabularyGuard";

const srcDir = join(__dirname, "..");

describe("vocabularyGuard — real tree", () => {
  it("no player surface renders a banned engine/protocol word (GG-23)", () => {
    expect(scanForBannedVocabulary(srcDir)).toEqual([]);
  });
});

describe("vocabularyGuard — fixtures", () => {
  let fixtureDir: string;

  afterEach(() => {
    if (fixtureDir) rmSync(fixtureDir, { recursive: true, force: true });
  });

  it("flags a banned word rendered as JSX text on a player surface", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "layers"));
    writeFileSync(
      join(fixtureDir, "layers", "Rogue.tsx"),
      "export const Rogue = () => <p>Equip compiles grant templates into mods_json</p>;\n"
    );
    const violations = scanForBannedVocabulary(fixtureDir);
    expect(violations).toHaveLength(1);
    expect(violations[0]).toMatchObject({ file: "layers/Rogue.tsx", line: 1 });
  });

  it("flags a banned word inside a rendered string literal", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "layers"));
    writeFileSync(
      join(fixtureDir, "layers", "Rogue.tsx"),
      'export const label = "UniqueActor Cold specimens";\n'
    );
    expect(scanForBannedVocabulary(fixtureDir)).toHaveLength(1);
  });

  it("does not flag the word used as a code identifier, not copy", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "layers"));
    writeFileSync(
      join(fixtureDir, "layers", "Clean.tsx"),
      "export function f(actor: { typeId: number }) { return actor.typeId; }\n"
    );
    expect(scanForBannedVocabulary(fixtureDir)).toEqual([]);
  });

  it("does not flag a data-testid carrying a banned word as an identifier", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "layers"));
    writeFileSync(
      join(fixtureDir, "layers", "Clean.tsx"),
      'export const x = <button data-testid="lawn-spawn-typeid">Go</button>;\n'
    );
    expect(scanForBannedVocabulary(fixtureDir)).toEqual([]);
  });

  it("does not flag developer surfaces (GG-41 allow-list)", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "dev"), { recursive: true });
    writeFileSync(
      join(fixtureDir, "dev", "Rogue.tsx"),
      "export const Rogue = () => <p>UniqueActor typeId ptr matchKey Admit revision</p>;\n"
    );
    expect(scanForBannedVocabulary(fixtureDir)).toEqual([]);
  });

  it("does not flag an unrelated word that merely contains a banned substring", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "vocab-guard-"));
    mkdirSync(join(fixtureDir, "layers"));
    writeFileSync(
      join(fixtureDir, "layers", "Clean.tsx"),
      'export const label = "A coldwind swept the revisions of the plan";\n'
    );
    expect(scanForBannedVocabulary(fixtureDir)).toEqual([]);
  });
});
