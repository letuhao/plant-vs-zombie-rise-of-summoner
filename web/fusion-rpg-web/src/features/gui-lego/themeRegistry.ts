import type { ThemePack, ThemeRef, ThemeResolved } from "./types";

import neutral from "./themes/neutral.json";
import sidePlant from "./themes/side-plant.json";
import sideZombie from "./themes/side-zombie.json";
import elementFire from "./themes/element-fire.json";
import elementIce from "./themes/element-ice.json";
import elementAir from "./themes/element-air.json";
import elementEarth from "./themes/element-earth.json";
import elementLight from "./themes/element-light.json";
import elementDark from "./themes/element-dark.json";
import elementOmni from "./themes/element-omni.json";
import statusDot from "./themes/status-category-dot.json";
import statusCc from "./themes/status-category-cc.json";
import statusContagion from "./themes/status-category-contagion.json";
import statusOmni from "./themes/status-category-omni.json";

/**
 * FE copies of docs/design/gui-lego/themes/packs — design pack remains SSOT on conflict.
 * Sync note: re-copy packs when design JSON changes (manual until a sync script exists).
 */
const PACKS: ThemePack[] = [
  neutral as ThemePack,
  sidePlant as ThemePack,
  sideZombie as ThemePack,
  elementFire as ThemePack,
  elementIce as ThemePack,
  elementAir as ThemePack,
  elementEarth as ThemePack,
  elementLight as ThemePack,
  elementDark as ThemePack,
  elementOmni as ThemePack,
  statusDot as ThemePack,
  statusCc as ThemePack,
  statusContagion as ThemePack,
  statusOmni as ThemePack
];

const byId = new Map(PACKS.map((p) => [p.themeId, p]));
const NEUTRAL = byId.get("neutral") ?? (neutral as ThemePack);

export function themeIdFor(ref: ThemeRef): string {
  if (ref.kind === "neutral") return "neutral";
  return `${ref.kind}.${ref.id}`;
}

export function lookupThemePack(ref: ThemeRef | undefined | null): ThemePack {
  if (!ref) return NEUTRAL;
  return byId.get(themeIdFor(ref)) ?? NEUTRAL;
}

export function resolveTheme(ref: ThemeRef | undefined | null): ThemeResolved {
  const pack = lookupThemePack(ref);
  return {
    themeId: pack.themeId,
    css: { ...pack.css },
    paint: { ...pack.paint },
    vfx: { ...pack.vfx }
  };
}

export function listThemePacks(): readonly ThemePack[] {
  return PACKS;
}
