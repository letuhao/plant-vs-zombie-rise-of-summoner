# GUI Lego — shared payload fragments

**Program:** `gui-lego`  
**Used by:** every `spec-<piece-id>.md`  
**Authoring:** [../gui-lego-authoring.md](../gui-lego-authoring.md)

Design-time TypeScript shapes (implementation may live under FE later). Pieces share these
fragments — do not redefine per piece.

---

## Enums

```ts
/** Lifecycle of a bound payload or surface slice. */
type Phase = "ready" | "loading" | "empty" | "error" | "pending";

/**
 * Derived channel six render states (SSOT: design/spec-derived-stat-sheet.md).
 * Do not invent a seventh.
 */
type DerivedRenderState =
  | "active"
  | "default"
  | "capped"
  | "stub"
  | "no-producer"
  | "unregistered";
```

---

## Theme and glyph

```ts
type ThemeKind =
  | "element"
  | "status-category"
  | "resource"
  | "action-category"
  | "rarity"
  | "side"
  | "cook-tab"
  | "neutral";

interface ThemeRef {
  kind: ThemeKind;
  id: string; // e.g. "fire", "dot", "plant"
}

/** After bindThemes — views consume this, not raw ThemeRef lookups. */
interface ThemeResolved {
  themeId: string; // "element.fire"
  css: Record<string, string>; // custom properties for piece root
  paint: {
    accent: string; // #rrggbb
    accentMuted: string;
    onAccent: string;
  };
  vfx: { select: string | null; idle: string | null };
}

interface GlyphRef {
  catalogIcon?: string; // lucide / CatalogIcon key
  hudToken?: string; // authored token text
  fallbackText?: string; // never idWords
}
```

---

## Magnitudes (GG-46)

```ts
interface MagnitudeDisplay {
  valueRaw: number | string; // sheet/long-safe; string if decimal wire
  valueText: string; // locale-formatted by the fold
  unitLabel?: string | null;
  formatterId: string; // UnitClass / formatter key
}
```

---

## Common piece envelope

```ts
interface PieceEnvelope {
  piece: string; // registry pieceId
  instanceId: string; // stable mount id
  phase: Phase;
  themeRef?: ThemeRef;
  themeResolved?: ThemeResolved; // attached by bindThemes
}
```

---

## Bus events (Derived surface v1)

Closed catalog — expanding requires an ideal change:

| Event | Payload |
|---|---|
| `derived.search.set` | `{ query: string }` |
| `derived.showUnchanged.set` | `{ value: boolean }` |
| `derived.tab.set` | `{ tabId: string }` |
| `derived.variant.set` | `{ variantId: string }` |
| `derived.channel.select` | `{ channelId: string }` |
| `derived.retry` | `{}` |
