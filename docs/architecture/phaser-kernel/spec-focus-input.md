# Spec: `focus-input` (deferred)

**Module id:** `focus-input` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Deferred — Wave 2+.  
**Depends on:** `island-host`

---

## Objective

GG-18: Phaser keyboard is gated when a React layer owns input. Wire focus-gate **before** optional
`wireKeyboardNav` on the lawn.

---

## Contract (frozen for later)

- Focus-gate reads top stack band / keymap ownership from React → bus or host flag.
- `wireKeyboardNav` only after gate exists.
- World already routes arrows via React → bus — do not break that.

---

## Success criteria (when activated)

1. With a panel open, Phaser keyboard nav does not move the lawn cursor.
2. With focus on canvas / no blocking layer, keyboard nav works if wired.
3. Tests cover mute/unmute without requiring owner eyeball.

---

## Boundaries

- **Never:** Phaser owns Esc/HUD; skip focus-gate “temporarily.”
