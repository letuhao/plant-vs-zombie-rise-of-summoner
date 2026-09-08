using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats.Aptitudes;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// class-system-todo.md P6.2 — <c>AllocationStore</c> (spec-point-economy.md, read in full this
/// session). Persists P6.1's <see cref="AptitudeAllocation"/>, one <c>(scope, scopeKey)</c> at a time
/// — INPUTS only, never a resolved channel value (spec-point-economy.md §6: "Save inputs... not
/// computed totals," `stat-system.md`'s own invariant, applied here — a stored channel value would be
/// a second SSOT that goes stale the moment a coefficient moves).
///
/// <para><b>Joins the existing <see cref="RpgStore"/> partial-class convention</b>
/// (<c>RpgStore.ChannelPolicy.cs</c> is the template this file follows) rather than the standalone
/// class spec-point-economy.md §5 literally names. That file listing predates this session's own
/// survey of `FusionRpg.Data`'s real conventions: every other feature — souls, unique actors, demons,
/// contracts — is a partial-class slice sharing ONE connection/lock (<c>_gate</c>), ONE
/// <c>EnsureHotSchema</c> dispatch, and ONE <c>Reset()</c>. A standalone class with its own connection
/// would fork that pipeline and silently drop out of <c>Reset()</c> — corrected in the spec in place,
/// not a silent rewrite, matching this session's own established "code beats docs" precedent.</para>
///
/// <para><b>Scope key shape</b> mirrors the existing <c>effect_binding</c> precedent
/// (<c>owner_kind</c>+<c>owner_key</c>, `src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs`): a
/// <c>scope</c> TEXT column plus a <c>scope_key</c> TEXT column, since the four
/// <see cref="AllocationScope"/> values key on four different things (spec-point-economy.md §2) — a
/// bare <c>player_id</c> or <c>instance_id</c> column alone fits none of the four uniformly. Commander
/// stringifies its `long` player id; the others carry their own natural string key
/// (<c>typeId</c> / <c>typeId:element</c> / <c>instanceId</c>) — this store does not interpret which,
/// only persists what the caller passes.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureAptitudeAllocationSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_aptitude_allocation (
              scope        TEXT    NOT NULL,
              scope_key    TEXT    NOT NULL,
              aptitude_id  TEXT    NOT NULL,
              points       INTEGER NOT NULL,
              PRIMARY KEY (scope, scope_key, aptitude_id)
            );
            """);
    }

    /// <summary>class-system-todo.md P6.2's own "unknown scope rejects" (§7 test 7) — the
    /// TEXT&lt;-&gt;<see cref="AllocationScope"/> boundary every row crosses in both directions. Throws
    /// naming the bad value rather than defaulting; tunables-ssot.md §7.2's "no built-in default"
    /// discipline, applied to a persistence key rather than a tuning value.</summary>
    public static string ScopeToText(AllocationScope scope) => scope switch
    {
        AllocationScope.Commander => "commander",
        AllocationScope.DemonType => "demonType",
        AllocationScope.Aspect => "aspect",
        AllocationScope.UniqueDemon => "uniqueDemon",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "unknown AllocationScope"),
    };

    public static AllocationScope ScopeFromText(string text) => text switch
    {
        "commander" => AllocationScope.Commander,
        "demonType" => AllocationScope.DemonType,
        "aspect" => AllocationScope.Aspect,
        "uniqueDemon" => AllocationScope.UniqueDemon,
        _ => throw new ArgumentException(
            $"'{text}' is not a known allocation scope (commander/demonType/aspect/uniqueDemon)", nameof(text)),
    };

    /// <summary>Persists ONLY this <c>(scope, scopeKey)</c>'s own points — one row per aptitude with a
    /// nonzero spend. A full upsert-and-prune (deletes this key's prior rows first, inside the same
    /// transaction), not an additive merge: the store holds the CURRENT allocation, not a change log,
    /// so a respec that zeroes an aptitude must actually remove its row, not leave a stale one behind.</summary>
    public void SaveAllocation(AllocationScope scope, string scopeKey, AptitudeAllocation allocation)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
            throw new ArgumentException("scopeKey must not be empty", nameof(scopeKey));
        if (allocation is null) throw new ArgumentNullException(nameof(allocation));

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            SaveAllocationUnlocked(db, tx, scope, scopeKey, allocation);
            tx.Commit();
        }
    }

    /// <summary>species-build-todo.md T4.2 — extracted so <c>TryRespecSpecies</c>
    /// (<c>RpgStore.SpeciesRespec.cs</c>) can write the override in the SAME transaction as its soul
    /// spend and counter update; a public <see cref="SaveAllocation"/> call would open its own
    /// connection/transaction and break the "neither applied on failure" atomicity that module needs.
    /// Same delete-then-insert-nonzero-only shape as before this extraction — no behavior change.</summary>
    void SaveAllocationUnlocked(SqliteConnection db, SqliteTransaction tx, AllocationScope scope, string scopeKey, AptitudeAllocation allocation)
    {
        var scopeText = ScopeToText(scope);

        ExecIn(db, tx, "DELETE FROM rpg_aptitude_allocation WHERE scope = $scope AND scope_key = $key;",
            ("$scope", scopeText), ("$key", scopeKey));

        foreach (var apt in AptitudeCatalog.All)
        {
            var points = allocation.PointsAt(scope, apt.Id);
            if (points == 0) continue; // no row for an unspent aptitude -- nothing to persist.

            ExecIn(db, tx, """
                INSERT INTO rpg_aptitude_allocation (scope, scope_key, aptitude_id, points)
                VALUES ($scope, $key, $apt, $points);
                """,
                ("$scope", scopeText), ("$key", scopeKey), ("$apt", apt.Id), ("$points", points));
        }
    }

    /// <summary>Reconstructs an <see cref="AptitudeAllocation"/> from ONLY this
    /// <c>(scope, scopeKey)</c>'s own persisted rows — empty (not null, not a thrown error) if nothing
    /// was ever saved for it, matching <see cref="AptitudeAllocation"/>'s own "empty means all-zero
    /// shares, never invent a default" contract.</summary>
    public AptitudeAllocation LoadAllocation(AllocationScope scope, string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
            throw new ArgumentException("scopeKey must not be empty", nameof(scopeKey));

        lock (_gate)
        {
            using var db = OpenUnlocked();
            return LoadAllocationUnlocked(db, scope, scopeKey);
        }
    }

    /// <summary>species-build-todo.md T4.2 — extracted for the same reason as
    /// <see cref="SaveAllocationUnlocked"/>: <c>TryRespecSpecies</c> needs to read the CURRENT override
    /// inside its own transaction (to decide free-vs-priced) without opening a second connection.</summary>
    AptitudeAllocation LoadAllocationUnlocked(SqliteConnection db, AllocationScope scope, string scopeKey)
    {
        var scopeText = ScopeToText(scope);

        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "SELECT aptitude_id, points FROM rpg_aptitude_allocation " +
            "WHERE scope = $scope AND scope_key = $key;";
        cmd.Parameters.AddWithValue("$scope", scopeText);
        cmd.Parameters.AddWithValue("$key", scopeKey);
        using var r = cmd.ExecuteReader();

        var allocation = AptitudeAllocation.Empty;
        while (r.Read())
            allocation += AptitudeAllocation.Single(scope, r.GetString(0), r.GetInt64(1));
        return allocation;
    }

    /// <summary>
    /// `demon-type-allocation` (module 5) — THE named entry point for a species' effective allocation
    /// (spec-demon-type-allocation.md: "composition lives behind a single named entry point... and
    /// `LoadAllocation` is not called directly by any consumer of species allocation" —
    /// `SpeciesAllocationSeamTests` guards this). Override REPLACES the baseline wholesale, never
    /// layers (spec's own "Override semantics"): a nonzero DemonType override wins outright; otherwise
    /// the baseline is computed fresh from the committed plan and the species' CURRENT level — never
    /// persisted (audit finding A9). A species the player has never overridden reads its baseline, not
    /// zero — the exact silent-zero risk this module's own design section calls out by name.
    ///
    /// <para>An override whose OWN total happens to be zero is indistinguishable from "no override" —
    /// not a new gap: `SaveAllocation`'s delete-then-insert-nonzero-only shape already gives Commander
    /// this same property (an all-zero save leaves no rows, identical to never having allocated), so
    /// DemonType inherits it rather than inventing a new "explicitly zero" state nothing else in this
    /// codebase tracks.</para>
    ///
    /// <para><b>Best-effort on the committed plan</b> (found running the real `battle-allocation`
    /// call site for real, `AuraDerivedEndpointsTests`): callers reached from `SpeciesAllocationSource`
    /// — battle setup, the derived-stat inspection endpoint — resolve species for EVERY actor, some of
    /// which have no reason to expect `SpeciesBuildPlanCatalog` configured (test fixtures that predate
    /// this module, matching the exact shape `RpgXpAwardMap.WithSpeciesPlacement` already treats as
    /// optional enrichment for the same reason). An un-configured plan catalog here returns
    /// <see cref="AptitudeAllocation.Empty"/> for the baseline half — an existing override still wins —
    /// rather than throwing and taking the WHOLE resolve down with it. The real server always
    /// configures this at startup (`Program.cs`), so production never reaches this fallback.</para>
    /// </summary>
    public AptitudeAllocation EffectiveSpeciesAllocation(long playerId, string speciesId, AptitudeTuning tuning)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            throw new ArgumentException("speciesId must not be empty", nameof(speciesId));

        var overrideAllocation = LoadAllocation(AllocationScope.DemonType,
            Core.Stats.Aptitudes.SpeciesAllocation.ScopeKey(playerId, speciesId));
        if (overrideAllocation.TotalForScope(AllocationScope.DemonType) > 0)
            return overrideAllocation;

        return SpeciesBaselineAllocation(playerId, speciesId, tuning);
    }

    /// <summary>
    /// species-build-todo.md T5.1 — the shipped plan's own baseline, ALWAYS, regardless of whether an
    /// override exists (unlike <see cref="EffectiveSpeciesAllocation"/>, which returns whichever of the
    /// two currently applies). `spec-allocation-surface.md`'s own design needs BOTH numbers at once —
    /// "the shipped baseline... the player's override, if any, shown as a deviation FROM the
    /// baseline" — so the two are exposed as separate values here rather than only ever the winner.
    /// Same best-effort-on-an-unconfigured-plan-catalog contract as `EffectiveSpeciesAllocation`.
    /// </summary>
    public AptitudeAllocation SpeciesBaselineAllocation(long playerId, string speciesId, AptitudeTuning tuning)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            throw new ArgumentException("speciesId must not be empty", nameof(speciesId));
        if (!SpeciesBuildPlanCatalog.IsConfigured)
            return AptitudeAllocation.Empty;

        var demonTypeId = DemonSpeciesCatalog.Get(speciesId).DemonTypeId;
        var level = GetRpgActor(playerId, RpgActorKinds.Species, demonTypeId)?.Level ?? 1;
        var shares = SpeciesBuildPlanCatalog.SharesFor(speciesId);
        return Core.Stats.Aptitudes.SpeciesAllocation.Baseline(shares, level, tuning);
    }

    /// <summary>Whether the player has ever set a DemonType override for this species — the exact
    /// signal <c>spec-allocation-surface.md</c>'s "shown as a deviation" UI needs to decide whether to
    /// render the override state at all, distinct from the override happening to equal the baseline.</summary>
    public bool HasSpeciesOverride(long playerId, string speciesId) =>
        LoadAllocation(AllocationScope.DemonType, Core.Stats.Aptitudes.SpeciesAllocation.ScopeKey(playerId, speciesId))
            .TotalForScope(AllocationScope.DemonType) > 0;
}
