import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";
import { clearLogEvents } from "@/lib/bus/log-store";
// Activates the (module-global) Lingui i18n singleton once for every test
// run, mirroring the real boot order (main.tsx -> providers.tsx -> "@/i18n").
// The `t` macro compiles to a call against this same singleton regardless of
// whether a test wraps its render in <I18nProvider> — without this import
// somewhere in the module graph, any component using `t`/`Trans` throws
// "Attempted to call a translation function without setting a locale."
import "@/i18n";

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
