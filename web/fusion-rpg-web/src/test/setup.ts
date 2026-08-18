import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";
import { clearLogEvents } from "@/lib/bus/log-store";

// Recharts ResponsiveContainer needs layout APIs in jsdom.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
if (typeof globalThis.ResizeObserver === "undefined") {
  globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;
}

Object.defineProperty(HTMLElement.prototype, "clientWidth", {
  configurable: true,
  get() {
    return 480;
  }
});
Object.defineProperty(HTMLElement.prototype, "clientHeight", {
  configurable: true,
  get() {
    return 240;
  }
});
Object.defineProperty(HTMLElement.prototype, "getBoundingClientRect", {
  configurable: true,
  value() {
    return {
      width: 480,
      height: 240,
      top: 0,
      left: 0,
      bottom: 240,
      right: 480,
      x: 0,
      y: 0,
      toJSON() {
        return {};
      }
    };
  }
});

afterEach(() => {
  cleanup();
  clearLogEvents();
});
