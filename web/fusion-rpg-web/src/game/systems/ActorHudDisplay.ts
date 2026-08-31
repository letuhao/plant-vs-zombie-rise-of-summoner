import type Phaser from "phaser";
import type { ActorHudSnapshot } from "@/features/lawn/lawnViewModel";
import { CELL_H } from "../gridMath";
import {
  STATUS_STRIP_MAX,
  TIER_STROKE,
  elementColorPhaser,
  statusInitials,
  tierBadgeLetter
} from "./actorHudDisplayTokens";

/** Row offset fractions — mirror `actor-hud.v1.json` `rowOffset*` (structural; web does not load tuning file). */
const ROW_OFFSET_IDENTITY = 0.42;
const ROW_OFFSET_RESOURCES = 0.28;
const ROW_OFFSET_STATUSES = 0.14;

const SHIELD_BAR_WIDTH = 40;

export function layoutHudRows(cellH: number = CELL_H): {
  identityY: number;
  resourcesY: number;
  statusesY: number;
} {
  return {
    identityY: -cellH * ROW_OFFSET_IDENTITY,
    resourcesY: -cellH * ROW_OFFSET_RESOURCES,
    statusesY: -cellH * ROW_OFFSET_STATUSES
  };
}

export function shieldSegmentWidths(
  stacks: { hp: number; max: number }[],
  barWidth: number
): number[] {
  const totalMax = stacks.reduce((sum, seg) => sum + Math.max(0, seg.max), 0);
  if (totalMax <= 0) return stacks.map(() => 0);
  return stacks.map((seg) => {
    const segmentShare = seg.max / totalMax;
    const fillRatio = seg.max > 0 ? Math.max(0, Math.min(1, seg.hp / seg.max)) : 0;
    const width = barWidth * segmentShare * fillRatio;
    return seg.hp > 0 ? Math.max(width, 1) : 0;
  });
}

export function shouldShowHud(hud: ActorHudSnapshot | undefined): hud is ActorHudSnapshot {
  return hud != null;
}

type ShieldResource = NonNullable<NonNullable<ActorHudSnapshot["resources"]>["shield"]>;

/** Unity parity — hide shield row when aggregate hp is depleted. */
export function shouldShowShield(shield: ShieldResource | undefined): shield is ShieldResource {
  return shield != null && shield.max > 0 && shield.hp > 0 && shield.stacks.length > 0;
}

export function setHudDisplay(
  scene: Phaser.Scene,
  container: Phaser.GameObjects.Container,
  hud: ActorHudSnapshot | undefined
): void {
  const existing = container.getByName("hudStack") as Phaser.GameObjects.Container | null;
  if (!shouldShowHud(hud)) {
    existing?.destroy();
    return;
  }

  existing?.destroy();

  const rows = layoutHudRows();
  const stack = scene.add.container(0, 0).setName("hudStack");

  const identityRow = scene.add.container(0, rows.identityY).setName("hudIdentity");
  const stroke = TIER_STROKE[hud.identity.tier] ?? TIER_STROKE.normal;
  identityRow.add(
    scene.add.rectangle(-14, 0, 14, 14, 0x000000, 0.35).setStrokeStyle(2, stroke, 1)
  );
  const letter = tierBadgeLetter(hud.identity.tier);
  if (letter) {
    identityRow.add(
      scene.add.text(-14, 0, letter, { fontSize: "8px", color: "#f2ead8" }).setOrigin(0.5)
    );
  }

  let identityX = -4;
  if (hud.identity.levelBand != null) {
    identityRow.add(
      scene.add
        .text(identityX, 0, String(hud.identity.levelBand), {
          fontSize: "7px",
          color: "#f2ead8",
          backgroundColor: "#2a231b"
        })
        .setOrigin(0.5)
    );
    identityX += 12;
  }

  const roleChar = hud.identity.role === "specimen" ? "S" : "V";
  identityRow.add(
    scene.add
      .text(identityX, 0, roleChar, { fontSize: "7px", color: "#c0b8a8" })
      .setOrigin(0.5)
  );
  stack.add(identityRow);

  const shield = hud.resources?.shield;
  if (shouldShowShield(shield)) {
    const shieldRow = scene.add.container(0, rows.resourcesY);
    shieldRow.add(
      scene.add
        .rectangle(0, 0, SHIELD_BAR_WIDTH, 4, 0x2a231b, 1)
        .setName("hudShield")
    );

    const widths = shieldSegmentWidths(shield.stacks, SHIELD_BAR_WIDTH);
    let segX = -SHIELD_BAR_WIDTH / 2;
    shield.stacks.forEach((seg, i) => {
      const w = widths[i] ?? 0;
      if (w <= 0) return;
      const color = elementColorPhaser(seg.element);
      shieldRow.add(
        scene.add.rectangle(segX + w / 2, 0, w, 4, color, 1).setOrigin(0.5)
      );
      segX += w;
    });
    stack.add(shieldRow);
  }

  const visibleStatuses = hud.statuses.slice(0, STATUS_STRIP_MAX);
  if (visibleStatuses.length > 0 || hud.overflow.statusCount > 0) {
    const statusRow = scene.add.container(0, rows.statusesY);
    const slotCount = visibleStatuses.length + (hud.overflow.statusCount > 0 ? 1 : 0);
    let sx = -((slotCount - 1) * 12) / 2;
    visibleStatuses.forEach((status, i) => {
      statusRow.add(
        scene.add
          .text(sx, 0, statusInitials(status.id), {
            fontSize: "7px",
            color: "#f2ead8",
            backgroundColor: "#3a3228"
          })
          .setOrigin(0.5)
          .setName(`hudStatus${i}`)
      );
      sx += 12;
    });
    if (hud.overflow.statusCount > 0) {
      statusRow.add(
        scene.add
          .text(sx, 0, `+${hud.overflow.statusCount}`, {
            fontSize: "6px",
            color: "#f2ead8",
            backgroundColor: "#3a3228"
          })
          .setOrigin(0.5)
          .setName("hudOverflow")
      );
    }
    stack.add(statusRow);
  }

  container.add(stack);
}
