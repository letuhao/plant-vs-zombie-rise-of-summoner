using FusionRpg.Core.Combat;
using FusionRpg.Injector.Lawn;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace FusionRpg.Injector.Effects;

/// <summary>Lawn census → Core <see cref="BoardSnapshot"/> for TargetResolver.</summary>
public static class InjectorBoardSnapshot
{
    public static BoardSnapshot Capture()
    {
        var list = new List<BoardEntitySnap>();
        try
        {
            foreach (var p in UObject.FindObjectsOfType<Plant>())
            {
                try
                {
                    if (p == null || p.thePlantType == PlantType.Nothing) continue;
                    list.Add(new BoardEntitySnap
                    {
                        Ptr = GameDumps.Ptr(p),
                        Side = "plant",
                        TypeId = (int)p.thePlantType,
                        Col = p.thePlantColumn,
                        Row = p.thePlantRow
                    });
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var z in UObject.FindObjectsOfType<Zombie>())
            {
                try
                {
                    if (z == null || z.theZombieType == ZombieType.Nothing) continue;
                    var col = -1;
                    try { col = LawnCoords.ColFromX(z.transform.position.x); } catch { }
                    if (col < 0)
                    {
                        try { col = z.Column; } catch { }
                    }

                    var row = 0;
                    try { row = z.theZombieRow; } catch { }

                    var mc = false;
                    try { mc = z.isMindControlled; } catch { }

                    list.Add(new BoardEntitySnap
                    {
                        Ptr = GameDumps.Ptr(z),
                        Side = "zombie",
                        TypeId = (int)z.theZombieType,
                        Col = col,
                        Row = row,
                        MindControlled = mc
                    });
                }
                catch { }
            }
        }
        catch { }

        return new BoardSnapshot(list);
    }
}
