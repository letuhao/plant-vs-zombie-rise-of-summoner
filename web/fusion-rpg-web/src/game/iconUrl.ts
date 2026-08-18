import { apiBase } from "@/lib/bus/rest";
import { getIconEpoch } from "@/lib/bus/icon-epoch";

/** Same composed PNG as TypeIcon, with epoch so Phaser shares the GUI cache. */
export function lawnIconUrl(side: string, typeId: number, epoch?: number): string {
  const r = epoch ?? getIconEpoch();
  return `${apiBase()}/api/icons/${side}/${typeId}.png?r=${r}`;
}

export function lawnIconTextureKey(side: string, typeId: number, epoch?: number): string {
  const r = epoch ?? getIconEpoch();
  return `icon-${side}-${typeId}-e${r}`;
}
