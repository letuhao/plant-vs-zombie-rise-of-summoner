using FusionRpg.Core.Demons;

namespace FusionRpg.Core.Battle;

/// <summary>
/// content-authoring (T2.3, spec-content-authoring.md §2.2): <c>ContentIndex</c> is Θ_content —
/// same values as the old <c>RecommendedLevel</c> name (1/3/6/10), a vocabulary rename only. No
/// external reader referenced the old name (verified: zero hits for "RecommendedLevel" anywhere else
/// in the repo), so this is a full rename, not an alias — unlike <see cref="BattleActorSetup.Level"/>,
/// nothing serializes <c>WaveDef</c> itself.
/// </summary>
/// <summary>
/// <see cref="Profile"/> is a battle-timeline mode-profile id (B12, battle-timeline-map.md §"Decision
/// 4"), resolved via <c>BattleModeProfileCatalog.Resolve</c> — <c>null</c> means "content did not
/// choose," which resolves to <c>classic-round</c>. Deliberately optional-with-default rather than a
/// required 5th positional argument, so the four waves already authored below need no edit and this
/// stays additive. **Never reaches <c>BattleSetup</c>** — the profile is looked up from the existing
/// <c>WaveId</c> at resolve time, never serialized (a field on `BattleSetup` would move all four
/// expedition hashes; named a "Never" in both `battle-timeline-map.md` and `spec-mode-profiles.md`).
/// </summary>
/// <param name="W">battle-timeline T15/B33 — a per-wave override of the resolved profile's
/// concurrency width, from the owner decision of 2026-09-04 ("`W` is content-configurable per wave").
/// <c>null</c> means "this wave does not care", which is every wave shipped today: the mechanism lands
/// inert on purpose, so the profile migration's own delta stays attributable to the profile switch
/// alone. Authoring a strictly-serialized encounter (<c>W = 1</c> on a wide profile) is content work.
///
/// <para>Optional-with-default, exactly like <paramref name="Profile"/>, so the four authored rows
/// below need no edit. And like <paramref name="Profile"/> it **never reaches <c>BattleSetup</c>** —
/// a field there would move all four expedition hashes for no gameplay reason, which both
/// <c>battle-timeline-map.md</c> and <c>spec-mode-profiles.md</c> name as a "Never".</para></param>
public sealed record WaveDef(string WaveId, string Name, int ContentIndex, IReadOnlyList<BattleActorSetup> Enemies, string? Profile = null, int? W = null);

/// <summary>
/// Code-authored wave roster built over the generated demon species catalog — enemies are wild
/// demons wearing the same species the player collects. Deterministic: same catalog ⇒ same waves.
/// </summary>
public static class WaveCatalog
{
    // Lazy, not `static readonly ... = Build()` (T4.7, catalog-runtime §3a): first touch must happen
    // after DemonSpeciesCatalog.Configure runs, not at an unpredictable point tied to class-load
    // order. Behaviour-preserving today — the source is still the compiled roster either way.
    static IReadOnlyList<WaveDef>? _all;
    public static IReadOnlyList<WaveDef> All => _all ??= Build();

    public static WaveDef Get(string waveId) =>
        All.FirstOrDefault(w => string.Equals(w.WaveId, waveId, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Unknown wave id '{waveId}'.");

    /// <summary>
    /// T15/B33 — the profile a wave actually runs under: <c>BattleModeProfileCatalog.Resolve</c> for
    /// the row's <see cref="WaveDef.Profile"/>, then the row's own <see cref="WaveDef.W"/> applied on
    /// top if it set one.
    ///
    /// <para>When the wave sets no <c>W</c> — every wave today — this returns the catalog's <b>cached
    /// instance itself</b>, not a copy. That is deliberate and load-bearing: it keeps reference
    /// identity for callers that compare with <c>Assert.Same</c>, and it makes "wave did not override"
    /// byte-identical to "no per-wave W mechanism exists" rather than merely equal to it.</para>
    /// </summary>
    /// <summary>
    /// T6/B21 — an expedition may never run an interactive profile. **An assertion, not a
    /// convention**: an expedition resolves server-side with nobody watching, so an interactive
    /// profile could only ever time out every turn — a slow way to produce a worse auto-resolve. It
    /// fails loudly instead of degrading quietly.
    /// </summary>
    public static Timeline.BattleModeProfile ProfileForExpedition(string waveId)
    {
        var profile = ProfileFor(waveId);
        if (profile.RequiresLiveInput)
            throw new InvalidOperationException(
                $"wave '{waveId}' selects the interactive profile '{profile.ProfileId}', but an expedition " +
                "resolves with no player present — an interactive profile there would time out every turn. " +
                "Expeditions are barred from interactive profiles by assertion (spec-interactive-turns.md §5).");
        return profile;
    }

    public static Timeline.BattleModeProfile ProfileFor(string waveId)
    {
        var wave = Get(waveId);
        var profile = Timeline.BattleModeProfileCatalog.Resolve(wave.Profile);
        if (wave.W is not { } w) return profile;
        if (w <= 0)
            throw new ArgumentOutOfRangeException(nameof(waveId), w,
                $"wave '{waveId}' sets W = {w}; a concurrency width is a slot count and must be > 0.");
        return profile with { W = w };
    }

    public static bool IsKnown(string? waveId) =>
        waveId != null && All.Any(w => string.Equals(w.WaveId, waveId, StringComparison.Ordinal));

    static IReadOnlyList<WaveDef> Build()
    {
        // Ordered, stable species pools by rarity band. Renamed to the ten-rung ladder's own
        // ids (seed-to-concrete T4.1) via the SAME band each old value migrated to
        // (ssot-rarity.md §4.3's forward map) — behaviour-preserving: the 84-species generated
        // catalog only populates these four rungs today, so the wave rosters are unchanged.
        // Widening these four bands to cover the six new intermediate rungs is a wave-content
        // authoring decision, out of scope for this migration.
        var commons = Band(DemonRarity.Chaff);
        var rares = Band(DemonRarity.Cultivated);
        var epics = Band(DemonRarity.Heirloom);
        var legendaries = Band(DemonRarity.Sunwoven);

        return new[]
        {
            // T15/B36 — expeditions and web matches run on `hybrid-atb` (decisions.md, "Battle engine
            // open questions (2026-09-04)", item 1): W=4, FixedIncrement, EarlyBoundWithFallback,
            // ActionPoints(2). Both surfaces move together because they share this roster. This is
            // what makes `turn.speed` / `turn.haste` live in production for the first time.
            //
            // No `RulesetVersion` bump accompanies it, and that was MEASURED, not assumed (B35): the
            // golden fixtures use "golden-*" wave ids that are not in this roster, and the expedition
            // tier hash covers the expedition *plan*, not resolved battle reports. The joint 4 -> 5
            // re-bless this task once predicted -- shared with B26's scaled clock -- has no remaining
            // cause: B26 is injector-side and Core cannot observe it either.
            new WaveDef("rift-skirmish", "Rift Skirmish", 1, Enemies(theta: 1, (commons, 4)), Profile: Timeline.BattleModeProfileCatalog.HybridAtbId),
            new WaveDef("rift-warband", "Rift Warband", 3, Enemies(theta: 3, (commons, 4), (rares, 2)), Profile: Timeline.BattleModeProfileCatalog.HybridAtbId),
            new WaveDef("rift-onslaught", "Rift Onslaught", 6, Enemies(theta: 6, (commons, 3), (rares, 3), (epics, 1)), Profile: Timeline.BattleModeProfileCatalog.HybridAtbId),
            new WaveDef("rift-tyrant", "Rift Tyrant", 10, Enemies(theta: 10, (rares, 3), (epics, 2), (legendaries, 1)), Profile: Timeline.BattleModeProfileCatalog.HybridAtbId)
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
