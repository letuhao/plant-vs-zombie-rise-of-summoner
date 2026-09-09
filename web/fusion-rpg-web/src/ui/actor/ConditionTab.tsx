import { useEffect, useMemo, useState } from "react";
import type { ActorView } from "@/contract/types";
import type { ActorSurfaceCatalog } from "@/lib/bus/actorSurface";
import type { ActorSheetDto } from "@/lib/bus/aura";
import { bindSurface } from "@/features/gui-lego/bindSurface";
import { createConditionSurfaceBus } from "@/features/gui-lego/conditionSurfaceBus";
import { asSurfaceBusLike } from "@/features/gui-lego/createSurfaceBus";
import { foldConditionSurfaceVm } from "@/features/gui-lego/foldConditionSurfaceVm";
import { getRecipe } from "@/features/gui-lego/recipeRegistry";
import { RecipeMount } from "@/ui/gui-lego/RecipeMount";
import { ensureConditionGuiLegoRegistered } from "@/ui/gui-lego/registerCondition";

/**
 * Condition glance — GUI Lego host. Identity stays in the shell summarize.
 */
export function ConditionTab({
  data,
  surface,
  sheet,
  onOpenStatusTab
}: {
  data: ActorView;
  surface: ActorSurfaceCatalog;
  sheet?: ActorSheetDto | null;
  onOpenStatusTab?: () => void;
}) {
  ensureConditionGuiLegoRegistered();
  const [selectedPoolId, setSelectedPoolId] = useState<string | null>("hp");
  const typedBus = useMemo(() => createConditionSurfaceBus(), []);
  const bus = useMemo(() => asSurfaceBusLike(typedBus), [typedBus]);

  useEffect(() => {
    const offs = [
      typedBus.on("condition.pool.select", (p) => {
        const id = (p as { poolId?: string })?.poolId;
        if (typeof id === "string") setSelectedPoolId(id);
      }),
      typedBus.on("condition.status.open", () => onOpenStatusTab?.())
    ];
    return () => offs.forEach((off) => off());
  }, [typedBus, onOpenStatusTab]);

  const vm = useMemo(
    () =>
      foldConditionSurfaceVm({
        data,
        sheet,
        surface,
        selectedPoolId,
        availability: "ready"
      }),
    [data, sheet, surface, selectedPoolId]
  );
  const recipe = getRecipe("condition-console");
  const plan = useMemo(() => (recipe ? bindSurface(recipe, vm) : null), [recipe, vm]);
  if (!plan) return <p className="rd">Condition recipe not registered.</p>;
  return <RecipeMount plan={plan} bus={bus} />;
}
