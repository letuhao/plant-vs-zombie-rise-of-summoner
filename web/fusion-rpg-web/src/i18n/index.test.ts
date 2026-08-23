import { afterEach, describe, expect, it } from "vitest";
import { messages as enMessages } from "./locales/en/messages.po";
import { i18n, setLocale } from "./index";

const firstMessageId = Object.keys(enMessages)[0]!;

describe("i18n locale switching", () => {
  afterEach(() => {
    setLocale("en");
  });

  it("defaults to en with the real source text", () => {
    expect(i18n.locale).toBe("en");
    expect(i18n._(firstMessageId)).toBe((enMessages[firstMessageId] as [string])[0]);
  });

  it("pseudo wraps every message in [!! … !!] without changing its content", () => {
    setLocale("pseudo");
    expect(i18n.locale).toBe("pseudo");
    for (const [id, compiled] of Object.entries(enMessages)) {
      const source = (compiled as [string])[0];
      expect(i18n._(id)).toBe(`[!!${source}!!]`);
    }
  });

  it("switching back to en restores the plain source text", () => {
    setLocale("pseudo");
    setLocale("en");
    expect(i18n._(firstMessageId)).toBe((enMessages[firstMessageId] as [string])[0]);
  });

  // The "never outside dev" half of setLocale("pseudo")'s guard can't be
  // unit-tested here — Vitest always runs with import.meta.env.DEV === true,
  // and that flag is a Vite compile-time constant, not a runtime value this
  // module can be made to see differently. It's verified instead by
  // inspecting the production build output for the [!! marker (see the
  // task's manual verification step) — a real grep on real dead-code
  // elimination, not a mock of a compile-time constant.
});
