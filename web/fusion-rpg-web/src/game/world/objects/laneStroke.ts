import Phaser from "phaser";
import {
  laneChannelsFor,
  type LaneChannels,
  type LaneKind,
  type LaneState
} from "@/stages/world/render/laneChannels";
import type { WorldTheme } from "../snapshotTheme";

/** Lane width is per-mille of a full front — same mapping as `Lane.tsx`. */
export function strokeWidthFor(widthMilli: number): number {
  return Math.max(1.2, Math.min(6, (widthMilli / 1000) * 4));
}

function laneColor(theme: WorldTheme, token: LaneChannels["token"]): number {
  switch (token) {
    case "lane-severed":
      return Phaser.Display.Color.HexStringToColor(theme.sideZombie).color;
    case "lane-warded":
      return Phaser.Display.Color.HexStringToColor(theme.sun).color;
    case "lane-hazardous":
      return Phaser.Display.Color.HexStringToColor(theme.sideZombie).color;
    case "lane-open":
    default:
      return Phaser.Display.Color.HexStringToColor(theme.ink).color;
  }
}

function lerp(ax: number, ay: number, bx: number, by: number, t: number): { x: number; y: number } {
  return { x: ax + (bx - ax) * t, y: ay + (by - ay) * t };
}

function drawStroke(
  g: Phaser.GameObjects.Graphics,
  channels: LaneChannels,
  color: number,
  width: number,
  x0: number,
  y0: number,
  x1: number,
  y1: number
): void {
  g.lineStyle(width, color, 1);

  if (channels.severedGap) {
    const gapStart = lerp(x0, y0, x1, y1, 0.42);
    const gapEnd = lerp(x0, y0, x1, y1, 0.58);
    g.beginPath();
    g.moveTo(x0, y0);
    g.lineTo(gapStart.x, gapStart.y);
    g.strokePath();
    g.beginPath();
    g.moveTo(gapEnd.x, gapEnd.y);
    g.lineTo(x1, y1);
    g.strokePath();
    return;
  }

  const style = channels.strokeStyle;
  if (style === "dashed" || style === "long-dash") {
    const dash = style === "long-dash" ? 14 : 6;
    const gap = style === "long-dash" ? 8 : 4;
    const dx = x1 - x0;
    const dy = y1 - y0;
    const len = Math.hypot(dx, dy);
    const ux = dx / len;
    const uy = dy / len;
    let dist = 0;
    let draw = true;
    while (dist < len) {
      const seg = draw ? dash : gap;
      const next = Math.min(dist + seg, len);
      if (draw) {
        g.beginPath();
        g.moveTo(x0 + ux * dist, y0 + uy * dist);
        g.lineTo(x0 + ux * next, y0 + uy * next);
        g.strokePath();
      }
      dist = next;
      draw = !draw;
    }
    return;
  }

  g.beginPath();
  g.moveTo(x0, y0);
  g.lineTo(x1, y1);
  g.strokePath();

  if (style === "twin-rail") {
    g.lineStyle(Math.max(1, width / 2), color, 0.85);
    g.beginPath();
    g.moveTo(x0, y0 + width * 1.5);
    g.lineTo(x1, y1 + width * 1.5);
    g.strokePath();
  }
}

export type LaneStrokeInput = {
  id: string;
  kind: LaneKind;
  state: LaneState;
  widthMilli: number;
  x0: number;
  y0: number;
  x1: number;
  y1: number;
};

/** Draw a lane between two pin centres using `laneChannels` stroke language. */
export function createLaneStroke(
  scene: Phaser.Scene,
  theme: WorldTheme,
  input: LaneStrokeInput
): Phaser.GameObjects.Graphics {
  const channels = laneChannelsFor(input.kind, input.state);
  const g = scene.add.graphics();
  g.setName(`lane:${input.id}`);
  const color = laneColor(theme, channels.token);
  const width = strokeWidthFor(input.widthMilli);
  drawStroke(g, channels, color, width, input.x0, input.y0, input.x1, input.y1);
  g.setDepth(-10);
  return g;
}

/** Midpoint along a straight lane — v1 linear, no easing. */
export function pointOnLane(
  x0: number,
  y0: number,
  x1: number,
  y1: number,
  progressMilli: number
): { x: number; y: number } {
  const t = Math.max(0, Math.min(1, progressMilli / 1000));
  return lerp(x0, y0, x1, y1, t);
}
