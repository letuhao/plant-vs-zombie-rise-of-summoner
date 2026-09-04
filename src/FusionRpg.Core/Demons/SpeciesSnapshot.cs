namespace FusionRpg.Core.Demons;

/// <summary>
/// `catalog-runtime` (T4.7 step 2 / T4.8, `spec-catalog-runtime.md` §3, demon-seed module 14, ⭐ "the
/// riskiest module in the program") — the seam a static class needs to read a roster it did not
/// compile in. Synthesises the two named precedents exactly as the spec asks: <see cref="Configure"/>
/// throws with no built-in default (<c>DerivedStatPolicy</c>'s own discipline — "there is nowhere to
/// pass a data directory" for a static class, so a missing call is a startup-ordering bug, never
/// papered over); <see cref="UseScoped"/> is an <c>AsyncLocal</c> override so one test's roster never
/// leaks into a test running beside it (<c>ElementTable</c>/<c>ChannelPolicyTable</c>'s own shape —
/// unlike those two, this hub has no safe empty default: an empty demon roster is exactly the loud
/// failure §4 asks for, not something to fall back through silently).
///
/// <para><b>Loaded once, immutable for the process lifetime (§3a).</b> No live reload — an import
/// requires a host restart, which is already how this repo deploys. The three downstream catalogs
/// (<c>WaveCatalog</c>, <c>DemonRecipeCatalog</c>, <c>DemonMaterialCatalog</c>) already converted to
/// lazy `_x ??= Build()` properties (T4.7 step 1) specifically so their own first touch happens after
/// <see cref="Configure"/> runs, not at an unpredictable point tied to class-load order.</para>
/// </summary>
public static partial class DemonSpeciesCatalog
{
    static IReadOnlyList<DemonSpeciesDef>? _configured;
    static readonly AsyncLocal<IReadOnlyList<DemonSpeciesDef>?> Scoped = new();

    /// <summary>`species-build` T1.2 — a non-throwing check for callers that must treat the roster as
    /// an optional enrichment rather than a hard requirement (`RpgXpAwardMap`'s species-placement
    /// award: most progression tests never configure a roster, and awarding type/player XP must keep
    /// working exactly as it always has when one isn't present).</summary>
    public static bool IsConfigured => Scoped.Value != null || _configured != null;

    /// <summary>
    /// Process-wide. What a host calls once, after loading the roster from its store
    /// (<c>RpgStore.BuildDemonSpeciesSnapshot()</c>). Validates and rejects a bad roster the same way
    /// <see cref="All"/> always has — a species with an unknown trait, a duplicate id, or a missing
    /// acquisition flag is a startup error here too, not a runtime surprise moved one layer later.
    /// </summary>
    /// <exception cref="InvalidOperationException">The roster is empty (§4: "today the catalog
    /// cannot be empty... after this change it can be — a fresh database, a failed import, a wrong
    /// data directory. Failing loudly at load beats failing later").</exception>
    public static void Configure(IReadOnlyList<DemonSpeciesDef> snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Count == 0)
            throw new InvalidOperationException(
                "DemonSpeciesCatalog.Configure received an empty species roster. A server that starts " +
                "with zero species reports healthy and fails later, untraceably, in SummonRoller. Run " +
                "'dotnet run --project tools/DemonSpeciesImport' against the data directory this host " +
                "points at, or point FUSIONRPG_DATA at one that already has an imported roster.");

        _configured = Validate(snapshot);
        _byId = null; // a fresh Configure invalidates any cached id map from a previous one
    }

    /// <summary>Swap the roster for THIS async context only, and put it back on dispose — the same
    /// isolation `ElementTable.UseScoped`/`ChannelPolicyTable.UseScoped` already give every other
    /// process-global table, so one test's roster is never visible to a test running beside it under
    /// xUnit's default cross-class parallelism.</summary>
    public static IDisposable UseScoped(IReadOnlyList<DemonSpeciesDef> snapshot)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        var validated = Validate(snapshot);
        var previous = Scoped.Value;
        Scoped.Value = validated;
        return new Restore(previous);
    }

    sealed class Restore : IDisposable
    {
        readonly IReadOnlyList<DemonSpeciesDef>? _previous;
        public Restore(IReadOnlyList<DemonSpeciesDef>? previous) => _previous = previous;
        public void Dispose() => Scoped.Value = _previous;
    }

    /// <summary>Reset the process-wide roster — test teardown only, mirroring
    /// `ChannelPolicyTable.ResetToEmpty`'s own role (never called by a host).</summary>
    public static void ResetToUnconfigured() { _configured = null; _byId = null; }

    /// <summary>
    /// ⛔ Transitional (T4.7 step 2 / T4.8 step 2-4): configure from the SAME compiled roster
    /// `All` always read before this module existed — behaviour-preserving by construction, since
    /// `Validate(GeneratedSpecies)` is exactly what the old lazy `All` computed. Every host calls
    /// this today, including the two live-game hosts (`Server/Program.cs`, `Injector/Host/RpgHost.cs`)
    /// — <b>NOT</b> <see cref="Configure"/> with a store-backed snapshot, which would silently shrink
    /// a live roster from the compiled 84 species to however many `species-import` has actually
    /// written (5, today — the full classification run, T2.11, is explicitly owner-run and has not
    /// happened). The real flip (step 5 of `spec-catalog-runtime.md` §7) is a SEPARATE, later,
    /// owner-gated change: it requires the diff test to pass AND a live lawn run exercising summon,
    /// fusion and expedition (Checkpoint 4's own line) — neither of which this call performs or
    /// substitutes for.
    /// </summary>
    public static void ConfigureFromCompiledDefault() => Configure(GeneratedSpecies);
}
