import { i18n, type Messages } from "@lingui/core";
import { messages as enMessages } from "./locales/en/messages.po";

/**
 * English-first (web/spec.md §10 — a second locale is enabled by this work,
 * not delivered by it). The dev-only pseudolocale wraps every translated
 * string in `[!! … !!]` so a hardcoded string or a layout that can't survive
 * a longer translation is visible immediately, without a translator in the
 * loop.
 *
 * `src/i18n/locales/pseudo/messages.po` is `lingui extract`'s own scratch
 * catalog (kept so its reference/line tracking works) but is not read here —
 * pseudo strings are derived programmatically from the compiled `en`
 * catalog instead of hand-maintained, so they can never drift out of sync
 * with it. `import.meta.env.DEV` is a Vite compile-time constant; the branch
 * that builds the pseudo catalog is dead code in a production build and
 * does not ship (verified by grep on the built bundle).
 */
export type SupportedLocale = "en" | "pseudo";

const PSEUDO_PREFIX = "[!!";
const PSEUDO_SUFFIX = "!!]";

function toPseudoMessages(source: Messages): Messages {
  const pseudo: Messages = {};
  for (const [id, compiled] of Object.entries(source)) {
    // Compiled shape is `[text, ...]` for a simple message (verified against
    // the real output of importing en/messages.po through @lingui/vite-plugin).
    // An ICU/plural/interpolated message compiles to something richer; those
    // are passed through untouched rather than guessed at.
    if (Array.isArray(compiled) && compiled.length === 1 && typeof compiled[0] === "string") {
      pseudo[id] = [`${PSEUDO_PREFIX}${compiled[0]}${PSEUDO_SUFFIX}`];
    } else {
      pseudo[id] = compiled;
    }
  }
  return pseudo;
}

i18n.load("en", enMessages);
i18n.activate("en");

let pseudoLoaded = false;

export function setLocale(locale: SupportedLocale): void {
  if (locale === "pseudo") {
    if (!import.meta.env.DEV) return; // never activates outside dev
    if (!pseudoLoaded) {
      i18n.load("pseudo", toPseudoMessages(enMessages));
      pseudoLoaded = true;
    }
  }
  i18n.activate(locale);
}

export { i18n };

// Dev-only debug hook, stripped from production like the pseudolocale branch
// above (same import.meta.env.DEV mechanism). A locale toggle in the UI is
// System settings' job (T20); until then this is the one way to exercise
// `setLocale` against the app's real, running i18n instance instead of a
// fresh one — QA/live-verification convenience, not a shipped feature.
if (import.meta.env.DEV) {
  (window as unknown as { __i18nDebug?: { i18n: typeof i18n; setLocale: typeof setLocale } }).__i18nDebug = {
    i18n,
    setLocale
  };
}
