using FusionRpg.Contracts;
using FusionRpg.Core.Match;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector.Match;

/// <summary>
/// On Bound: absolute Writer on that ptr only + Effect grants with <c>entity:{ptr}</c> (W5-C).
/// Never applies type-wide <c>plant:N</c> / <c>zombie:N</c> loadout keys.
/// </summary>
public static class UniqueBoundLoadout
{
    public static void TryApply(UniqueBinding? bound)
    {
        if (bound == null || bound.Phase != UniqueBindingPhase.Bound) return;
        if (string.IsNullOrWhiteSpace(bound.Ptr)) return;

        try
        {
            var spec = UniqueLoadoutSpec.Parse(bound.LoadoutJson).BindToPtr(bound.Ptr!);
            if (spec.IsEmpty) return;

            ApplyAbsolutes(bound.Side, bound.Ptr!, spec);
            var funnel = EffectRuntime.Bag.Funnel;
            foreach (var g in spec.Grants)
            {
                if (UniqueOwnerBinder.WouldRejectOnHot(g.OwnerKey))
                    continue; // binder must have rewritten; fail-closed skip
                try { funnel?.EnqueueModifier(g); } catch { /* fail-closed */ }
            }

            try { funnel?.Flush(); } catch { /* fail-closed */ }
        }
        catch (Exception ex)
        {
            try { CheatState.Error("UniqueBoundLoadout: " + ex.Message); } catch { }
        }
    }

    static void ApplyAbsolutes(string side, string ptrHex, UniqueLoadoutSpec spec)
    {
        var abs = spec.ToCheatAbsoluteMap();
        if (abs.Count == 0) return;

        var want = MatchUniqueBindingsFacet.NormalizePtr(ptrHex);
        var isPlant = string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase);
        var hasHp = abs.TryGetValue("hp", out var hp);
        var hasMax = abs.TryGetValue("maxHp", out var maxHp);
        var hasAtk = abs.TryGetValue("atk", out var atk);

        if (isPlant)
        {
            foreach (var p in UnityEngine.Object.FindObjectsOfType<Plant>())
            {
                try
                {
                    if (p == null) continue;
                    if (!string.Equals(MatchUniqueBindingsFacet.NormalizePtr(GameDumps.Ptr(p)), want,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (hasMax || hasHp)
                    {
                        var max = Math.Max(1, hasMax ? maxHp : hp);
                        var cur = hasHp ? Math.Max(1, hp) : max;
                        if (cur > max) cur = max;
                        p.thePlantMaxHealth = max;
                        p.thePlantHealth = cur;
                        try { p.UpdateText(); } catch { }
                        EntityStatWriter.ForceSetPlantHp(p, cur, "unique.bound");
                    }
                    if (hasAtk)
                    {
                        p.attackDamage = Math.Max(1, atk);
                        try { p.UpdateText(); } catch { }
                    }
                    return;
                }
                catch { /* next */ }
            }
        }
        else
        {
            foreach (var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
            {
                try
                {
                    if (z == null) continue;
                    if (!string.Equals(MatchUniqueBindingsFacet.NormalizePtr(GameDumps.Ptr(z)), want,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (hasMax || hasHp)
                    {
                        var max = Math.Max(1L, hasMax ? maxHp : hp);
                        var cur = hasHp ? Math.Max(1L, hp) : max;
                        if (cur > max) cur = max;
                        Bridges.ZombieCombatFields.SetMaxHp(z, max);
                        Bridges.ZombieCombatFields.SetHp(z, cur);
                        try { z.UpdateHealthText(); } catch { }
                        EntityStatWriter.ForceSetZombieHp(z, cur, "unique.bound");
                    }
                    if (hasAtk)
                        z.theAttackDamage = Math.Max(1, atk);
                    return;
                }
                catch { /* next */ }
            }
        }
    }
}
