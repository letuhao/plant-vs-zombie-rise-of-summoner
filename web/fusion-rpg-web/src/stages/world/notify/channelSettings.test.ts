import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { channelFor, clearChannelSettingsForTests, currentChannels, setChannel } from "./channelSettings";
import { CATEGORY_DEFAULT_CHANNEL } from "./categories";

// This environment's default window.localStorage is incomplete (see keybindings.test.ts / same
// pattern) — stub a real in-memory Storage before each test.
beforeEach(() => {
  const mem: Record<string, string> = {};
  const ls = {
    getItem: (k: string) => mem[k] ?? null,
    setItem: (k: string, v: string) => {
      mem[k] = v;
    },
    removeItem: (k: string) => {
      delete mem[k];
    },
    clear: () => {
      for (const key of Object.keys(mem)) delete mem[key];
    },
    key: (i: number) => Object.keys(mem)[i] ?? null,
    get length() {
      return Object.keys(mem).length;
    }
  };
  Object.defineProperty(window, "localStorage", { configurable: true, value: ls });
});

afterEach(() => {
  clearChannelSettingsForTests();
});

describe("channelSettings — persisted, player settings (world-stage W88)", () => {
  it("defaults to the declared default channel when nothing is overridden", () => {
    expect(channelFor("battle.result")).toBe("rail");
    expect(currentChannels()).toEqual(CATEGORY_DEFAULT_CHANNEL);
  });

  it("changing a channel persists and is read back — across a fresh read, not a cached one", () => {
    setChannel("battle.result", "toast");
    expect(channelFor("battle.result")).toBe("toast");

    // No in-memory cache to reset here — every read goes straight to localStorage, which is what
    // "persists across a reload" actually means for a module with no module-level state at all.
    expect(currentChannels()["battle.result"]).toBe("toast");
    expect(currentChannels()["growth"]).toBe(CATEGORY_DEFAULT_CHANNEL["growth"]); // untouched
  });

  it("degrades to session-only rather than throwing if storage is unavailable", () => {
    const original = window.localStorage.setItem;
    window.localStorage.setItem = () => {
      throw new Error("storage unavailable");
    };
    expect(() => setChannel("growth", "off")).not.toThrow();
    window.localStorage.setItem = original;
  });
});
