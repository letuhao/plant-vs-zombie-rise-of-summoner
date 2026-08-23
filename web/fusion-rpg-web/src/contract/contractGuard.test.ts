import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { findEmptyPendingReasons, scanForRestDtoImports } from "./contractGuard";

const srcDir = join(__dirname, "..");

describe("contractGuard — real tree", () => {
  it("no file under stages/, layers/ or ui/ imports a REST DTO type", () => {
    expect(scanForRestDtoImports(srcDir)).toEqual([]);
  });
});

describe("findEmptyPendingReasons", () => {
  it("flags a pending field with an empty reason", () => {
    const value = { foo: { state: "pending", reason: "" } };
    expect(findEmptyPendingReasons(value)).toHaveLength(1);
  });

  it("flags a pending field with a whitespace-only reason", () => {
    const value = { foo: { state: "pending", reason: "   " } };
    expect(findEmptyPendingReasons(value)).toHaveLength(1);
  });

  it("flags a pending field with a missing reason", () => {
    const value = { foo: { state: "pending" } };
    expect(findEmptyPendingReasons(value)).toHaveLength(1);
  });

  it("does not flag a pending field with a real reason", () => {
    const value = { foo: { state: "pending", reason: "no endpoint yet" } };
    expect(findEmptyPendingReasons(value)).toEqual([]);
  });

  it("does not flag known/absent states", () => {
    const value = { a: { state: "known", value: 1 }, b: { state: "absent" } };
    expect(findEmptyPendingReasons(value)).toEqual([]);
  });

  it("walks nested objects and arrays", () => {
    const value = { list: [{ nested: { state: "pending", reason: "" } }] };
    expect(findEmptyPendingReasons(value)).toHaveLength(1);
  });
});

describe("scanForRestDtoImports — fixtures", () => {
  let fixtureDir: string;

  afterEach(() => {
    if (fixtureDir) rmSync(fixtureDir, { recursive: true, force: true });
  });

  it("flags a type-only import of a bus module in ui/", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "contract-guard-"));
    mkdirSync(join(fixtureDir, "ui"));
    writeFileSync(
      join(fixtureDir, "ui", "Rogue.tsx"),
      'import type { UniqueActorDto } from "@/lib/bus/types";\n'
    );
    expect(scanForRestDtoImports(fixtureDir)).toHaveLength(1);
  });

  it("flags an inline type import mixed with value imports in stages/", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "contract-guard-"));
    mkdirSync(join(fixtureDir, "stages"), { recursive: true });
    writeFileSync(
      join(fixtureDir, "stages", "Rogue.tsx"),
      'import { type RunItem, useRuns } from "@/lib/bus";\n'
    );
    expect(scanForRestDtoImports(fixtureDir)).toHaveLength(1);
  });

  it("does not flag a plain value/hook import from the bus", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "contract-guard-"));
    mkdirSync(join(fixtureDir, "ui"));
    writeFileSync(join(fixtureDir, "ui", "Fine.tsx"), 'import { useUniqueActor } from "@/lib/bus";\n');
    expect(scanForRestDtoImports(fixtureDir)).toEqual([]);
  });

  it("does not scan a directory that doesn't exist yet (e.g. layers/ before T10)", () => {
    fixtureDir = mkdtempSync(join(tmpdir(), "contract-guard-"));
    expect(() => scanForRestDtoImports(fixtureDir)).not.toThrow();
    expect(scanForRestDtoImports(fixtureDir)).toEqual([]);
  });
});
