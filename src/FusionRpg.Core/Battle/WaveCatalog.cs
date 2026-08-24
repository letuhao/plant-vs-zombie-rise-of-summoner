using FusionRpg.Core.Demons;

namespace FusionRpg.Core.Battle;

/// <summary>
/// content-authoring (T2.3, spec-content-authoring.md §2.2): <c>ContentIndex</c> is Θ_content —
/// same values as the old <c>RecommendedLevel</c> name (1/3/6/10), a vocabulary rename only. No
/// external reader referenced the old name (verified: zero hits for "RecommendedLevel" anywhere else
/// in the repo), so this is a full rename, not an alias — unlike <see cref="BattleActorSetup.Level"/>,
/// nothing serializes <c>WaveDef</c> itself.
/// </summary>
public sealed record WaveDef(string WaveId, string Name, int ContentIndex, IReadOnlyList<BattleActorSetup> Enemies);

/// <summary>
/// Code-authored wave roster built over the generated demon species catalog — enemies are wild
/// demons wearing the same species the player collects. Deterministic: same catalog ⇒ same waves.
/// </summary>
public static class WaveCatalog
{
    public static readonly IReadOnlyList<WaveDef> All = Build();

    public static WaveDef Get(string waveId) =>
        All.FirstOrDefault(w => string.Equals(w.WaveId, waveId, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Unknown wave id '{waveId}'.");

    public static bool IsKnown(string? waveId) =>
        waveId != null && All.Any(w => string.Equals(w.WaveId, waveId, StringComparison.Ordinal));

    static IReadOnlyList<WaveDef> Build()
    {
        // Ordered, stable species pools by rarity band.
        var commons = Band(DemonRarity.Common);
        var rares = Band(DemonRarity.Rare);
        var epics = Band(DemonRarity.Epic);
        var legendaries = Band(DemonRarity.Legendary);

        return new[]
        {
            new WaveDef("rift-skirmish", "Rift Skirmish", 1, Enemies(theta: 1, (commons, 4))),
            new WaveDef("rift-warband", "Rift Warband", 3, Enemies(theta: 3, (commons, 4), (rares, 2))),
            new WaveDef("rift-onslaught", "Rift Onslaught", 6, Enemies(theta: 6, (commons, 3), (rares, 3), (epics, 1))),
            new WaveDef("rift-tyrant", "Rift Tyrant", 10, Enemies(theta: 10, (rares, 3), (epics, 2), (legendaries, 1)))
        };
    }

    static List<DemonSpeciesDef> Band(DemonRarity rarity) =>
        DemonSpeciesCatalog.All.Where(s => s.BaseRarity == rarity).OrderBy(s => s.SpeciesId, StringComparer.Ordinal).ToList();

    static IReadOnlyList<BattleActorSetup> Enemies(int theta, params (List<DemonSpeciesDef> Pool, int Count)[] picks)
    {
        var list = new List<BattleActorSetup>();
        var n = 0;
        foreach (var (pool, count) in picks)
        {
            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var species = pool[i % pool.Count];
                list.Add(new BattleActorSetup
                {
                    Key = $"wave:{n++}",
                    Side = "wave",
                    SpeciesId = species.SpeciesId,
                    TypeId = species.DemonTypeId,
                    Level = theta,
                    ElementPrimary = species.ElementPrimary,
                    ElementSecondary = species.ElementSecondary,
                    TraitIds = species.TraitPool,
                    MaxHp = BattleRuleset.BaseHp(theta),
                    Atk = BattleRuleset.BaseAtk(theta),
                    Defense = BattleRuleset.BaseDefense(theta)
                });
            }
        }

        return list;
    }
}
