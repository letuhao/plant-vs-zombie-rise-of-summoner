import type Phaser from "phaser";
import type { Occupant } from "@/features/lawn/lawnViewModel";

/** View mirror only — never invent occupants (RT-01 / invariant 2). */
export type PtrViewRecord = {
  ptr: string;
  side: Occupant["side"] | "grid" | "mower" | "pet";
  typeId: number;
  row?: number;
  col?: number;
  chips: string[];
  selected: boolean;
  instanceId?: string;
  go: Phaser.GameObjects.Container;
};

export class PtrEntityRegistry {
  private readonly byPtr = new Map<string, PtrViewRecord>();

  get(ptr: string): PtrViewRecord | undefined {
    return this.byPtr.get(ptr.trim().toUpperCase());
  }

  entries(): IterableIterator<PtrViewRecord> {
    return this.byPtr.values();
  }

  set(rec: PtrViewRecord): void {
    this.byPtr.set(rec.ptr.trim().toUpperCase(), rec);
  }

  delete(ptr: string): PtrViewRecord | undefined {
    const key = ptr.trim().toUpperCase();
    const prev = this.byPtr.get(key);
    this.byPtr.delete(key);
    return prev;
  }

  clear(): void {
    for (const rec of this.byPtr.values()) {
      rec.go.destroy(true);
    }
    this.byPtr.clear();
  }

  keys(): string[] {
    return [...this.byPtr.keys()];
  }
}
