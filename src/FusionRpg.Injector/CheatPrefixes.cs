using HarmonyLib;
using UnityEngine;

namespace FusionRpg.Injector;

/// <summary>Prefix/postfix cheats for godmode, extra DEF paths, bullets, QoL, free-set.</summary>
public static class CheatPrefixes
{
    [HarmonyPatch(typeof(Plant), nameof(Plant.RealTakeDamage))]
    public static class PlantRealTakeCheat
    {
        // DEF %/flat only on Plant.TakeDamage (GameHooks) — avoid double scale on RealTakeDamage.
        public static void Prefix(ref int damage)
        {
            if (CheatState.On("P-GOD")) damage = 0;
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Crashed))]
    public static class PlantCrashCheat
    {
        public static bool Prefix()
        {
            if (CheatState.On("P-GOD")) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Die))]
    public static class PlantDieCheat
    {
        public static bool Prefix(Plant __instance, Plant.DieReason reason)
        {
            if (!CheatState.On("P-GOD-DIE")) return true;
            if (reason == Plant.DieReason.ByShovel) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.TakeDamage))]
    public static class ZombieTakeCheat
    {
        public static void Prefix(ref int theDamage)
        {
            if (CheatState.On("Z-GOD")) theDamage = 0;
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.BodyTakeDamage))]
    public static class ZombieBodyCheat
    {
        // DEF only on Zombie.TakeDamage — BodyTakeDamage is godmode only (Z-DEF-BODY = enable note for Take path).
        public static void Prefix(ref int theDamage)
        {
            if (CheatState.On("Z-GOD")) theDamage = 0;
        }
    }

    [HarmonyPatch(typeof(Zombie), nameof(Zombie.ApplyDamage))]
    public static class ZombieApplyCheat
    {
        public static void Prefix(ref int dmg)
        {
            if (CheatState.On("Z-GOD")) dmg = 0;
        }
    }

    [HarmonyPatch(typeof(Bullet), nameof(Bullet.InitData))]
    public static class BulletInitCheat
    {
        public static void Postfix(Bullet __instance)
        {
            if (__instance == null) return;
            try
            {
                if (CheatState.On("D-PROBE-PLANT")) return;
                var set = CheatState.IVal("D-DMG-SET");
                if (set >= 0) __instance.Damage = set;
                var pct = CheatState.FVal("D-DMG-%");
                if (Math.Abs(pct - 1f) > 0.001f)
                    __instance.Damage = Math.Max(1, (int)Math.Round(__instance.Damage * pct));
                var swap = CheatState.IVal("D-TYPE-SWAP");
                if (swap >= 0)
                    try { __instance.theBulletType = (BulletType)swap; } catch { }
                if (CheatState.On("D-HOMING"))
                {
                    try { __instance.MoveWay = BulletMoveWay.Track; } catch { }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(CreatePlant), nameof(CreatePlant.SetPlant))]
    public static class FreeSetPlant
    {
        public static void Prefix(ref bool isFreeSet)
        {
            if (CheatState.On("G-FREE-SET")) isFreeSet = true;
        }
    }

    [HarmonyPatch(typeof(CreatePlant), nameof(CreatePlant.CheckBox))]
    public static class PlantAnywhere
    {
        // Force allow plant box checks when H-ANYWHERE is on (safe alternative to banned CheckMix).
        public static void Postfix(ref bool __result)
        {
            if (CheatState.On("H-ANYWHERE")) __result = true;
        }
    }

    [HarmonyPatch(typeof(CardUI), nameof(CardUI.UseOnce))]
    public static class CardNoCd
    {
        public static void Postfix(CardUI __instance)
        {
            if (!CheatState.On("H-NOCD-CARD") || __instance == null) return;
            try
            {
                __instance.CD = 0f;
                __instance.fullCD = 0.01f;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Mower), nameof(Mower.Die))]
    public static class MowerInf
    {
        public static bool Prefix(Mower __instance)
        {
            if (!CheatState.On("H-MOWER-INF")) return true;
            try
            {
                return false;
            }
            catch { return true; }
        }
    }
}
