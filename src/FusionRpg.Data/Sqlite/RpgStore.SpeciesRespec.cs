using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Aptitudes;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// species-build-todo.md T4.2 — spec-species-respec.md, read in full this session. A partial
/// <see cref="RpgStore"/> slice sharing the one connection/lock/<c>EnsureHotSchema</c>/<c>Reset()</c>
/// pipeline (the correction <c>RpgStore.Aptitudes.cs</c>'s own header already recorded for this exact
/// mistake) — not the standalone class a literal reading of the project structure might suggest.
///
/// <para><b>Free-vs-priced is decided HERE</b>, inside the same transaction as the spend and the
/// override write — the same shape <c>RpgStore.Patron.cs</c>'s <c>SetPatron</c> already uses for its
/// own "first designation free, every change spends" rule. Free: the species has no current override
/// (first override) or <paramref name="newOverride"/> is empty (revert to baseline). Priced: an
/// existing nonzero override is being replaced by a different nonzero one.</para>
///
/// <para><b>Ledger path, not <c>TrySpendSouls</c></b> (spec's own ⛔ callout, audit finding A4: that
/// method has zero production callers) — <see cref="RpgStore.AppendSoulLedgerUnlocked"/> and
/// <see cref="RpgStore.ReadSoulBalanceUnlocked"/>, the same private helpers <c>RpgStore.Souls.cs</c>'s
/// shipped sinks already call, reused here inside this method's own transaction.</para>
/// </summary>
public sealed record SpeciesRespecOutcome(
    bool Ok, string Reason, bool Priced, long PriceAmount, long RespecCount, SoulBalanceDto Balance);

public sealed partial class RpgStore
{
    void EnsureSpeciesRespecSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_species_respec (
              player_id       INTEGER NOT NULL,
              species_id      TEXT    NOT NULL,
              count           INTEGER NOT NULL,
              last_respec_utc TEXT    NOT NULL,
              PRIMARY KEY (player_id, species_id)
            );
            """);
    }

    /// <summary><paramref name="Exists"/> is the "has this species EVER been touched by this economy"
    /// marker (T4.3's own fix: a revert clears the OVERRIDE but must never clear this row, or
    /// revert-then-reoverride becomes a free, unlimited respec-cost bypass) — distinct from
    /// <paramref name="Count"/> being zero, which decay alone can also produce.</summary>
    (bool Exists, long Count, string LastRespecUtc) ReadSpeciesRespecRowUnlocked(SqliteConnection db, long playerId, string speciesId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT count, last_respec_utc FROM rpg_species_respec WHERE player_id=$p AND species_id=$s;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$s", speciesId);
        using var r = cmd.ExecuteReader();
        // No row yet == never respecced -- count 0, and "last respec" is undefined, so decay has
        // nothing to measure from (DecayedCount only ever lowers a stored count, never invents one).
        return r.Read() ? (true, r.GetInt64(0), r.GetString(1)) : (false, 0L, DateTimeOffset.UtcNow.ToString("o"));
    }

    /// <summary>Bounded counter, decayed ON READ — never a timer, never a background job
    /// (spec-species-respec.md's own design). A PS-8 exemption BY NATURE, not a magnitude: this counts
    /// recent respecs, it does not measure progression. Day-quantised in UTC (a minute past midnight is
    /// a new day, matching <c>ContractPolicy.ElapsedDays</c>'s established convention) and floors at
    /// zero; a stamp in the future (never expected, but never trusted either) decays nothing rather
    /// than counting backwards.</summary>
    static long DecayedRespecCount(long storedCount, DateTimeOffset lastRespecUtc, DateTimeOffset now, int decayDays)
    {
        if (storedCount <= 0) return 0;
        var elapsedDays = Math.Max(0, (now.UtcDateTime.Date - lastRespecUtc.UtcDateTime.Date).Days);
        var decayTicks = decayDays > 0 ? elapsedDays / decayDays : 0;
        return Math.Max(0L, storedCount - decayTicks);
    }

    /// <summary>
    /// species-build-todo.md T5.1 — whether this species has EVER been touched by this economy
    /// (the same persistent marker <see cref="TryRespecSpecies"/> itself reads to decide free-vs-
    /// priced), exposed so a CLIENT can predict which one the next save will be before attempting it.
    /// Deliberately NOT <c>GetSpeciesRespecCount(...) &gt; 0</c> — the count legitimately DECAYS back
    /// to zero over time even for a species touched long ago, which would make a client wrongly
    /// predict "first override" (free) for a species the server will actually price. This is the
    /// bug T4.2's own "revert-then-reoverride" fix exists to prevent from happening server-side;
    /// exposing the SAME signal here prevents the client from re-introducing an equivalent UI-level
    /// mispredict — showing no price confirmation for a change the server then charges for anyway.
    /// </summary>
    public bool HasEverRespecced(long playerId, string speciesId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadSpeciesRespecRowUnlocked(db, playerId, speciesId).Exists;
        }
    }

    /// <summary>The effective (decayed) respec count right now — for pricing a preview before the
    /// player commits to spending. Read-only; does not itself decay the stored row (that only ever
    /// happens as part of an actual <see cref="TryRespecSpecies"/> call, which is the only path that
    /// also has a reason to rewrite <c>last_respec_utc</c>).</summary>
    public long GetSpeciesRespecCount(long playerId, string speciesId, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            throw new ArgumentException("speciesId must not be empty", nameof(speciesId));

        lock (_gate)
        {
            using var db = OpenUnlocked();
            var (_, count, lastUtc) = ReadSpeciesRespecRowUnlocked(db, playerId, speciesId);
            return DecayedRespecCount(count, DateTimeOffset.Parse(lastUtc), utcNow ?? DateTimeOffset.UtcNow,
                SpeciesBuildTuningHub.Tuning.RespecDecayDays);
        }
    }

    /// <summary>
    /// The respec feature's own entry point (spec's own callout: "spends are never a generic endpoint,
    /// each with its own reason") — <c>SpeciesBuildEndpoints.cs</c> (T4.3) is the HTTP surface over
    /// this. Spend, counter increment, and the override write happen in ONE transaction: a crash
    /// between them would otherwise charge a player for a build they did not get, or hand out a build
    /// for free (spec's own ⛔ on the spend path).
    ///
    /// <para>Never refused for being a respec (PS-8) — every path below either succeeds (fresh or
    /// replayed) or refuses for the one named reason that actually applies here (insufficient balance),
    /// never because a respec count is "too high."</para>
    /// </summary>
    public SpeciesRespecOutcome TryRespecSpecies(
        long playerId, string speciesId, AptitudeAllocation newOverride, string correlationId, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(speciesId))
            throw new ArgumentException("speciesId must not be empty", nameof(speciesId));
        if (newOverride is null) throw new ArgumentNullException(nameof(newOverride));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("correlationId must not be empty", nameof(correlationId));

        var corr = correlationId.Trim();
        var scopeKey = Core.Stats.Aptitudes.SpeciesAllocation.ScopeKey(playerId, speciesId);
        var tuning = SpeciesBuildTuningHub.Tuning;

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var now = utcNow ?? DateTimeOffset.UtcNow;
            var nowText = now.ToString("o");

            var (everTouched, storedCount, lastUtc) = ReadSpeciesRespecRowUnlocked(db, playerId, speciesId);
            var effectiveCount = DecayedRespecCount(storedCount, DateTimeOffset.Parse(lastUtc), now, tuning.RespecDecayDays);

            var isRevert = newOverride.TotalForScope(AllocationScope.DemonType) == 0;
            // "First override" means this species has NEVER been touched by this economy before --
            // NOT merely "the override happens to read empty right now." Reading the latter off
            // LoadAllocation would let revert-then-reoverride bypass every future price forever (revert
            // clears the override but must never look like "never overridden").
            var isFirstOverride = !everTouched && !isRevert;
            var free = isRevert || isFirstOverride;

            if (free)
            {
                if (isFirstOverride)
                {
                    // Mark this species touched (count 0) so a LATER revert-then-reoverride is priced,
                    // not free again -- the row's mere existence is the "ever overridden" memory, kept
                    // even though its count is still zero.
                    using var mark = db.CreateCommand();
                    mark.CommandText = """
                        INSERT INTO rpg_species_respec(player_id, species_id, count, last_respec_utc)
                        VALUES ($p, $s, 0, $t);
                        """;
                    mark.Parameters.AddWithValue("$p", playerId);
                    mark.Parameters.AddWithValue("$s", speciesId);
                    mark.Parameters.AddWithValue("$t", nowText);
                    mark.ExecuteNonQuery();
                }
                // No spend, no counter movement -- a free action leaves the churn clock untouched.
                SaveAllocationUnlocked(db, tx, AllocationScope.DemonType, scopeKey, newOverride);
                tx.Commit();
                return new SpeciesRespecOutcome(true, "", false, 0, effectiveCount, ReadSoulBalanceUnlocked(db, playerId));
            }

            // Replay check FIRST, before pricing off the current (possibly already-advanced) count --
            // unlike TrySpendSouls's own dedupe (a fixed caller-supplied amount), this price is a
            // function of the counter the very same call increments, so a stale "recompute and
            // compare" would reject a legitimate replay the moment the count it was originally priced
            // at has moved. Any hit under (reason, dedupe_key) is treated as the full original
            // outcome -- a correlation id is the caller's promise that repeats mean "the same request."
            using (var check = db.CreateCommand())
            {
                check.CommandText = "SELECT delta FROM rpg_soul_ledger WHERE player_id=$p AND reason=$r AND dedupe_key=$dk;";
                check.Parameters.AddWithValue("$p", playerId);
                check.Parameters.AddWithValue("$r", SoulEarnPolicy.Reasons.Respec);
                check.Parameters.AddWithValue("$dk", corr);
                if (check.ExecuteScalar() is long storedDelta)
                {
                    tx.Commit();
                    return new SpeciesRespecOutcome(true, "replay", true, -storedDelta, effectiveCount, ReadSoulBalanceUnlocked(db, playerId));
                }
            }

            var price = RespecPolicy.PriceOf(tuning, effectiveCount);
            var balance = ReadSoulBalanceUnlocked(db, playerId);
            if (balance.Balance < price.Amount)
            {
                tx.Rollback();
                return new SpeciesRespecOutcome(false, "souls.insufficient", true, price.Amount, effectiveCount, balance);
            }

            AppendSoulLedgerUnlocked(db, playerId, 0, -price.Amount, SoulEarnPolicy.Reasons.Respec,
                "spend", corr, corr, nowText);

            var newCount = effectiveCount + 1;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO rpg_species_respec(player_id, species_id, count, last_respec_utc)
                    VALUES ($p, $s, $c, $t)
                    ON CONFLICT(player_id, species_id)
                    DO UPDATE SET count = $c, last_respec_utc = $t;
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$s", speciesId);
                cmd.Parameters.AddWithValue("$c", newCount);
                cmd.Parameters.AddWithValue("$t", nowText);
                cmd.ExecuteNonQuery();
            }

            SaveAllocationUnlocked(db, tx, AllocationScope.DemonType, scopeKey, newOverride);

            tx.Commit();
            return new SpeciesRespecOutcome(true, "", true, price.Amount, newCount, ReadSoulBalanceUnlocked(db, playerId));
        }
    }
}
