import Phaser from "phaser";
import type { Channels, ColorToken } from "@/stages/world/render/sectorChannels";
import type { FogTreatment } from "@/stages/world/render/fogTreatments";
import type { WorldTheme } from "../snapshotTheme";
import { lodChannels, type ZoomTier } from "../zoomTier";
import { PIN_DISC_PX } from "./pinConstants";

export type PinKind = "unknown" | "disc";

/** Pure paint descriptor for matrix tests — no Phaser types, no opacity field. */
export type PinDescriptor = {
  kind: PinKind;
  lod: ReadonlySet<string>;
  channels: Channels;
  /** Fog ring/pip/wash flags relevant at this zoom — forces strip never included. */
  fog: {
    raggedRing: boolean;
    doubledRing: boolean;
    pip: boolean;
    wash: boolean;
  } | null;
};

export type SectorPinInput = {
  id: string;
  channels: Channels;
  fog: FogTreatment;
  zoom: ZoomTier;
  x: number;
  y: number;
};

function hex(theme: WorldTheme, token: ColorToken): number {
  switch (token) {
    case "ownership-mine":
      return Phaser.Display.Color.HexStringToColor(theme.sidePlant).color;
    case "ownership-enemy":
      return Phaser.Display.Color.HexStringToColor(theme.sideZombie).color;
    case "ownership-contested":
      return Phaser.Display.Color.HexStringToColor(theme.sun).color;
    case "ownership-neutral":
    default:
      return Phaser.Display.Color.HexStringToColor(theme.panel).color;
  }
}

function fogOnPin(fog: FogTreatment, zoom: ZoomTier): PinDescriptor["fog"] {
  if (!fog.raggedBorder && !fog.doubledBorder && fog.wash === "none") return null;
  const lod = lodChannels(zoom);
  return {
    raggedRing: fog.raggedBorder,
    doubledRing: fog.doubledBorder,
    pip: lod.has("fogPip") && fog.stamp != null,
    wash: lod.has("fogWash") && fog.wash !== "none"
  };
}

/** Descriptor-only path for unit tests — encodes kind, LOD channels, fog-on-pin density. */
export function pinDescriptor(input: Pick<SectorPinInput, "channels" | "fog" | "zoom">): PinDescriptor {
  const kind: PinKind = input.channels.shape === "unknown" ? "unknown" : "disc";
  return {
    kind,
    lod: lodChannels(input.zoom),
    channels: input.channels,
    fog: kind === "disc" ? fogOnPin(input.fog, input.zoom) : null
  };
}

function drawDiamond(g: Phaser.GameObjects.Graphics, theme: WorldTheme, channels: Channels, r: number): void {
  const fill = hex(theme, channels.token);
  g.fillStyle(fill, 1);
  g.beginPath();
  g.moveTo(0, -r);
  g.lineTo(r, 0);
  g.lineTo(0, r);
  g.lineTo(-r, 0);
  g.closePath();
  g.fillPath();
  g.lineStyle(2, Phaser.Display.Color.HexStringToColor(theme.ink).color, 0.9);
  g.strokePath();
}

function drawDisc(
  g: Phaser.GameObjects.Graphics,
  theme: WorldTheme,
  channels: Channels,
  fog: FogTreatment,
  zoom: ZoomTier,
  r: number
): void {
  const fill = hex(theme, channels.token);
  g.fillStyle(fill, 1);
  g.fillCircle(0, 0, r);

  const border = Phaser.Display.Color.HexStringToColor(theme.ink).color;
  const dash = channels.border.style === "dashed";
  g.lineStyle(channels.border.weight === "heavy-left" ? 3 : 2, border, 0.95);
  if (dash) {
    // Approximate dashed ring with segments.
    const segments = 24;
    for (let i = 0; i < segments; i += 2) {
      const a0 = (i / segments) * Math.PI * 2;
      const a1 = ((i + 1) / segments) * Math.PI * 2;
      g.beginPath();
      g.arc(0, 0, r + 2, a0, a1);
      g.strokePath();
    }
  } else {
    g.strokeCircle(0, 0, r + 2);
  }

  const desc = fogOnPin(fog, zoom);
  if (desc?.doubledRing) {
    g.lineStyle(1, border, 0.7);
    g.strokeCircle(0, 0, r + 6);
  }
  if (desc?.raggedRing) {
    g.lineStyle(1, border, 0.65);
    const bumps = 12;
    for (let i = 0; i < bumps; i++) {
      const a = (i / bumps) * Math.PI * 2;
      const rr = r + 5 + (i % 2 === 0 ? 2 : 0);
      g.fillStyle(border, 0.65);
      g.fillCircle(Math.cos(a) * rr, Math.sin(a) * rr, 1.5);
    }
  }
  if (desc?.wash) {
    g.fillStyle(Phaser.Display.Color.HexStringToColor(theme.ink).color, fog.washCapPercent / 100);
    g.fillCircle(0, 0, r);
  }
}

/**
 * Pin factory. Unknown → diamond silhouette; card channels → 44px disc.
 * Fog-on-pin: Rumored ragged ring + hearsay pip; Scouted doubled ring + dated pip; wash at detail only.
 */
export function createSectorPin(
  scene: Phaser.Scene,
  theme: WorldTheme,
  input: SectorPinInput
): Phaser.GameObjects.Container {
  const container = scene.add.container(input.x, input.y);
  container.setName(`pin:${input.id}`);
  container.setData("sectorId", input.id);

  const g = scene.add.graphics();
  container.add(g);

  const r = PIN_DISC_PX / 2;
  if (input.channels.shape === "unknown") {
    drawDiamond(g, theme, input.channels, r);
  } else {
    drawDisc(g, theme, input.channels, input.fog, input.zoom, r);
    const lod = lodChannels(input.zoom);
    if (lod.has("fogPip") && input.fog.stamp) {
      const pip = scene.add.text(r - 4, -r + 2, "·", {
        fontFamily: theme.fontFamily,
        fontSize: "14px",
        color: theme.ink
      });
      pip.setOrigin(0.5);
      container.add(pip);
    }
    if (lod.has("name")) {
      const label = scene.add.text(0, r + 10, input.id, {
        fontFamily: theme.fontFamily,
        fontSize: "12px",
        color: theme.ink
      });
      label.setOrigin(0.5, 0);
      container.add(label);
    }
  }

  const hit = scene.add.zone(0, 0, PIN_DISC_PX, PIN_DISC_PX);
  hit.setInteractive({ useHandCursor: true });
  container.add(hit);
  container.setSize(PIN_DISC_PX, PIN_DISC_PX);
  container.setInteractive(
    new Phaser.Geom.Circle(0, 0, PIN_DISC_PX / 2),
    Phaser.Geom.Circle.Contains
  );

  return container;
}
