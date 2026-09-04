using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Loadout;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

/// <summary>
/// Web-mode match producer (spec-match-source-core.md §WebMatchService): replay check →
/// log-before-ingest → pure BattleEngine resolve → DEDICATED single-transaction ingest
/// (explicit player, never the shared channel) → hub notifies. A crash between log and ingest
/// is healed by the boot sweep — resolution is deterministic from the logged setup + seed.
/// </summary>
public sealed class WebMatchService
{

    /// <summary>
    /// B21 — whether this logged match ran under a profile that expects a live human. Read from the
    /// wave the setup names, not stored on the row: the profile is content's choice and is looked up
    /// at resolve time, exactly as `WaveDef.Profile`'s own doc requires (a field on `BattleSetup`
    /// would move all four expedition hashes).
    ///
    /// <para>A row whose setup will not parse is NOT treated as interactive — it has its own refusal
    /// further down, and guessing here would refuse it for the wrong stated reason.</para>
    /// </summary>
    static bool IsInteractive(WebMatchLogEntry entry)
    {
        BattleSetup? setup;
        try { setup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson); }
        catch (JsonException) { return false; }

        return setup is not null && ProfileForWave(setup.WaveId)?.RequiresLiveInput == true;
    }

    /// <summary>
    /// B36 — the wave's own mode profile, the last link in "content chooses the profile"
    /// (battle-timeline-map.md decision 4). <c>WaveDef.Profile</c> is <c>null</c> for every shipped
    /// wave, so this resolves to <c>classic-round</c> and every battle is byte-identical today; the
    /// wiring exists so that setting a profile on a wave actually reaches the engine, which it did
    /// not before — <c>BattleEngine.Resolve</c> had no caller passing one at all.
    ///
    /// <para>An unknown wave id keeps the resolver's own loud behaviour rather than being defaulted
    /// here: "content did not choose" and "content chose wrong" are different failures and only the
    /// first has a default.</para>
    /// </summary>
    static FusionRpg.Core.Battle.Timeline.BattleModeProfile? ProfileForWave(string? waveId) =>
        FusionRpg.Core.Battle.WaveCatalog.IsKnown(waveId)
            ? FusionRpg.Core.Battle.WaveCatalog.ProfileFor(waveId!)
            : null;

    readonly RpgStore _store;
    readonly IHubContext<RpgHub> _hub;

    public WebMatchService(RpgStore store, IHubContext<RpgHub> hub)
    {
        _store = store;
        _hub = hub;
    }

    /// <param name="TurnOrder">
    /// `battle-tempo` `forecast-rail` FR3 (spec-forecast-rail.md §2, §6): the acting order this
    /// battle actually recorded, projected from the trace FR1 built — a RECORD of what happened, not
    /// a forecast (an expedition is resolved before the player sees it). Empty when no trace was
    /// built for this resolve (never null — "declared, not deferred", `game-gui-map.md`'s own
    /// contract discipline every other DTO in this repo already follows).
    /// </param>
    public sealed record WebMatchOutcome(
        bool Replayed, string MatchKey, long? RunId, BattleReport Report,
        IReadOnlyList<TurnOrderEntry> TurnOrder);

    public async Task<(bool Ok, string Reason, WebMatchOutcome? Outcome)> RunWebMatchAsync(
        long playerId, string correlationId, string waveId, IReadOnlyList<string>? squadInstanceIds)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        if (correlationId.Trim().Length > 64) return (false, "correlation.toolong", null);
        if (!WaveCatalog.IsKnown(waveId)) return (false, "wave.unknown", null);
        var corr = correlationId.Trim();

        // 1. Server-authoritative setup: squad snapshots from the roster (or a SIM synthetic
        //    squad when the roster is empty), wave from the code-authored catalog.
        var (squadOk, squadReason, squad, pickedIds) = BuildSquad(playerId, squadInstanceIds);
        if (!squadOk) return (false, squadReason, null);
        var setup = new BattleSetup
        {
            WaveId = waveId,
            Squad = squad!,
            Wave = WaveCatalog.Get(waveId).Enemies
        };
        var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        var matchKey = "web-" + Guid.NewGuid().ToString("N");

        // 2. Durable idempotency anchor BEFORE any ingest. The atomic append IS the replay gate:
        //    a pre-check would be a separate lock acquisition, and two concurrent requests with
        //    the same correlation could both pass it — the loser would ingest a duplicate battle
        //    (2026-08-21 review, Critical 1). Created=false means another request (now or ever)
        //    holds this correlation; validate against ITS stored row and replay it.
        var (created, entry) = _store.AppendWebMatchLog(playerId, corr, matchKey,
            JsonSerializer.Serialize(setup), seed,
            BattleRuleset.EngineVersion, BattleRuleset.RulesetVersion, SeededRng.RngAlgoVersion,
            BattleEnvironment.Stamp, _store.ComputeContentHash().ToCompact());
        if (!created)
        {
            var storedSetup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson);
            if (storedSetup == null || storedSetup.WaveId != waveId)
                return (false, "correlation.mismatch", null);
            // FR1: also a player-facing replay -- opts in, matching the fresh-resolve paths below.
            var replayTrace = new BattleTrace();
            var storedReport = BattleEngine.Resolve(storedSetup, entry.Seed, replayTrace, profile: ProfileForWave(storedSetup.WaveId), actionCatalog: _store.BuildActionCatalog(RungPolicy.Table));
            // FR3: BattleTrace is a class -- `replayTrace` reflects the resolve that just ran, no
            // second return path needed.
            var replayTurnOrder = TurnOrderRecord.FromTrace(replayTrace, storedSetup);
            return (true, "replay", new WebMatchOutcome(true, entry.MatchKey, entry.RunId, storedReport, replayTurnOrder));
        }

        // 3–4. Resolve + dedicated ingest; 5. notify.
        // FR1: player-facing -- opts in.
        var freshTrace = new BattleTrace();
        var (report, notify) = ResolveAndIngest(playerId, matchKey, setup, seed, freshTrace);
        // 4b. The squad lived it: contracted members gain or lose loyalty for the result. Replays
        //     return above, so a retry never credits a second time. Deliberately OUTSIDE the
        //     log-before-ingest envelope: a crash between ingest and here loses ±15 loyalty and the
        //     boot sweep will not replace it. That is the accepted trade — widening the exactly-once
        //     envelope for a loyalty point is not worth the coupling.
        _store.ApplyContractResults(playerId, pickedIds, report.Outcome == BattleOutcome.Victory);
        await BroadcastAsync(playerId, notify).ConfigureAwait(false);

        var linked = _store.TryGetWebMatchLog(playerId, corr);
        var turnOrder = TurnOrderRecord.FromTrace(freshTrace, setup);
        return (true, "ok", new WebMatchOutcome(false, matchKey, linked?.RunId, report, turnOrder));
    }

    /// <summary>
    /// Runs a pre-planned battle (expedition collect): the caller supplies the sealed setup +
    /// seed and a deterministic correlation, so retries replay instead of re-fighting. Same
    /// log-before-ingest discipline as ad-hoc matches.
    /// </summary>
    public async Task<(bool Ok, string Reason, WebMatchOutcome? Outcome)> RunPlannedMatchAsync(
        long playerId, string correlationId, string matchKey, BattleSetup setup, ulong seed)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        var corr = correlationId.Trim();

        // The atomic append doubles as the replay gate (no check-then-act window; see
        // RunWebMatchAsync). On replay the STORED setup is authoritative by design: a collect
        // retry may see drifted roster stats, and the sealed snapshot must win — so only the
        // cross-wiring sentinels (seed, matchKey) are compared, never the setup body.
        var (created, entry) = _store.AppendWebMatchLog(playerId, corr, matchKey,
            JsonSerializer.Serialize(setup), seed,
            BattleRuleset.EngineVersion, BattleRuleset.RulesetVersion, SeededRng.RngAlgoVersion,
            BattleEnvironment.Stamp, _store.ComputeContentHash().ToCompact());
        if (!created)
        {
            var storedSetup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson);
            if (storedSetup == null || entry.Seed != seed || entry.MatchKey != matchKey)
                return (false, "correlation.mismatch", null);
            // FR1: also a player-facing replay -- opts in, matching the fresh-resolve paths below.
            var replayTrace = new BattleTrace();
            var storedReport = BattleEngine.Resolve(storedSetup, entry.Seed, replayTrace, profile: ProfileForWave(storedSetup.WaveId), actionCatalog: _store.BuildActionCatalog(RungPolicy.Table));
            var replayTurnOrder = TurnOrderRecord.FromTrace(replayTrace, storedSetup);
            return (true, "replay", new WebMatchOutcome(true, entry.MatchKey, entry.RunId, storedReport, replayTurnOrder));
        }

        // FR1: player-facing (an expedition collect) -- opts in, same as RunWebMatchAsync.
        var freshTrace = new BattleTrace();
        var (report, notify) = ResolveAndIngest(playerId, matchKey, setup, seed, freshTrace);
        await BroadcastAsync(playerId, notify).ConfigureAwait(false);
        var linked = _store.TryGetWebMatchLog(playerId, corr);
        var turnOrder = TurnOrderRecord.FromTrace(freshTrace, setup);
        return (true, "ok", new WebMatchOutcome(false, matchKey, linked?.RunId, report, turnOrder));
    }

    /// <summary>Boot sweep: re-ingest logged matches whose ingest never landed. Version-guarded —
    /// a report from a different engine/ruleset must not be silently re-blessed.</summary>
    public int SweepUnresolved()
    {
        var healed = 0;
        // Hashed ONCE for the whole sweep. Per entry it would be a full read of every covered
        // content table per row — the content cannot change while this loop runs.
        var content = _store.ComputeContentHash();

        foreach (var entry in _store.ListUnresolvedWebMatches())
        {
            // A refusal is TERMINAL, not a skip: the row is marked so it leaves the unresolved
            // window for good. Left unmarked, refused rows are re-listed every boot and (since
            // the query is ORDER BY id ASC LIMIT n) enough of them would crowd out every newer
            // row — crash recovery dies silently while still reporting a clean sweep.
            if (entry.EngineVersion != BattleRuleset.EngineVersion
                || entry.RulesetVersion != BattleRuleset.RulesetVersion
                || entry.RngAlgoVersion != SeededRng.RngAlgoVersion)
            {
                var why = $"version {entry.EngineVersion}/{entry.RulesetVersion}/{entry.RngAlgoVersion} != " +
                          $"{BattleRuleset.EngineVersion}/{BattleRuleset.RulesetVersion}/{SeededRng.RngAlgoVersion}";
                Console.Error.WriteLine($"[web-match] sweep refused {entry.MatchKey}: {why}");
                _store.MarkWebMatchSweepRefused(entry.Id, why);
                continue;
            }

            // Owner decision 7: Math.Exp is not bit-identical across architecture/OS — a report
            // logged on another platform must not be silently re-resolved here.
            if (entry.EnvironmentStamp != null && entry.EnvironmentStamp != BattleEnvironment.Stamp)
            {
                var why = $"platform '{entry.EnvironmentStamp}' != '{BattleEnvironment.Stamp}'";
                Console.Error.WriteLine($"[web-match] sweep refused {entry.MatchKey}: {why}");
                _store.MarkWebMatchSweepRefused(entry.Id, why);
                continue;
            }

            // B21 (spec-interactive-turns.md §4): an INTERACTIVE match is only reproducible from its
            // decision trace, because with real input `(setup, seed)` stops describing the battle.
            // A missing or unparseable trace is refused TERMINALLY and never healed — re-resolving it
            // would substitute AI decisions for a player's and silently overwrite a real result, which
            // is the exact hole the trace exists to close. Inert today: no shipped profile sets
            // RequiresLiveInput, so no match reaches this branch.
            if (IsInteractive(entry) && FusionRpg.Core.Battle.Timeline.DecisionTrace.FromJson(entry.DecisionsJson) is null)
            {
                const string why = "interactive match with no decision trace — refused rather than re-resolved with AI decisions";
                Console.Error.WriteLine($"[web-match] sweep refused {entry.MatchKey}: {why}");
                _store.MarkWebMatchSweepRefused(entry.Id, why);
                continue;
            }

            // E8: effect content is rows, so a resolve is only reproducible against the content it
            // ran on. A REGISTRY change is not a refusal — a table joining the covered set is
            // expected and attributable — but an edited row under the same registry version is.
            var contentCheck = ContentHashComparison.Compare(entry.ContentHash, content);
            if (contentCheck.ShouldRefuse)
            {
                Console.Error.WriteLine($"[web-match] sweep refused {entry.MatchKey}: {contentCheck.Reason}");
                _store.MarkWebMatchSweepRefused(entry.Id, contentCheck.Reason);
                continue;
            }
            if (contentCheck.Verdict == ContentHashVerdict.RegistryChanged)
                Console.Error.WriteLine($"[web-match] sweep {entry.MatchKey}: {contentCheck.Reason}");

            try
            {
                var setup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson);
                if (setup == null)
                {
                    // Unparseable setup can never heal on a later boot either — terminal.
                    Console.Error.WriteLine($"[web-match] sweep refused {entry.MatchKey}: unreadable setup_json");
                    _store.MarkWebMatchSweepRefused(entry.Id, "unreadable setup_json");
                    continue;
                }
                // FR1: the boot sweep -- the ONE caller that must never trace. Nobody is watching a
                // crash-recovery re-ingest; tracing it would be exactly the bulk-path cost D3 scopes
                // this feature away from. `trace` defaults to null; left unpassed deliberately.
                ResolveAndIngest(entry.PlayerId, entry.MatchKey, setup, entry.Seed);
                healed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[web-match] sweep failed for {entry.MatchKey}: {ex.Message}");
            }
        }

        return healed;
    }

    /// <param name="trace">
    /// `battle-tempo` `forecast-rail` FR1 (spec-forecast-rail.md §2.2, D3): trace opt-in PER BATTLE —
    /// null (the default) traces nothing. `SweepUnresolved` (the boot-sweep bulk path) is the ONE
    /// caller that must always pass null; every other caller of this method is player-facing and
    /// opts in. `Turns` is excluded from `BattleTrace.Digest` by design, so passing one here moves no
    /// determinism hash regardless of which callers opt in.
    /// </param>
    (BattleReport Report, EventInsertNotify Notify) ResolveAndIngest(
        long playerId, string matchKey, BattleSetup setup, ulong seed, BattleTrace? trace = null)
    {
        // E12: the report carries WHICH CONTENT produced it. Stamped here, where the store is, and
        // never recomputed later — a power or a trait magnitude read back under a different
        // contentHash is a different number, so a recomputed stamp would describe a battle that did
        // not happen. Provenance only: it is excluded from the determinism hash, exactly as the


        // platform stamp is, or every added row would look like a determinism break.
        var report = BattleEngine.Resolve(setup, seed, trace, profile: ProfileForWave(setup.WaveId), actionCatalog: _store.BuildActionCatalog(RungPolicy.Table)) with
        {
            ContentHash = _store.ComputeContentHash().ToCompact(),
        };
        var events = BattleReportEmitter.Emit(report, matchKey).ToList();

        // class-system-todo.md P9.2's own prerequisite, found while building it: without this, no
        // later query can attribute a battle to the allocation that produced it — rpg_aptitude_
        // allocation is an upsert with no history, so a later re-allocation silently erases every
        // earlier battle's own provenance. Captured here, once, reading the SAME store/scope/key
        // AptitudeChannelMods already read to build this same squad (BuildSquad, above) — not
        // re-derived from a different assumption, and never affects combat math or goldens (a pure
        // additional event, not a resolver input).
        var aptitudeShares = _store.LoadAllocation(
            FusionRpg.Core.Stats.Aptitudes.AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId)).Shares();
        events.Add(new EventEnvelope
        {
            T = "",
            Game = RpgConstants.GameIdWebRpg,
            Kind = "aptitude.snapshot",
            MatchKey = matchKey,
            Payload = new Dictionary<string, object?> { ["scope"] = "commander", ["shares"] = aptitudeShares }
        });

        // The engine is clockless — stamp strictly monotonic t here, at ingest.
        var t0 = DateTime.UtcNow;
        for (var i = 0; i < events.Count; i++)
            events[i].T = t0.AddMilliseconds(i).ToString("o");

        var notify = _store.InsertWebMatchEvents(playerId, matchKey, events);
        return (report, notify);
    }

    /// <summary>Server-authoritative squad snapshots from the roster — shared with expeditions.</summary>
    public (bool Ok, string Reason, List<BattleActorSetup>? Squad, List<string> InstanceIds) BuildSquad(
        long playerId, IReadOnlyList<string>? squadInstanceIds)
    {
        // Own guards, not caller trust: every future battle producer inherits them.
        const int maxSquad = 6;
        var none = new List<string>();
        if (squadInstanceIds is { Count: > maxSquad })
            return (false, "squad.toolarge", null, none);
        if (squadInstanceIds != null
            && squadInstanceIds.Distinct(StringComparer.Ordinal).Count() != squadInstanceIds.Count)
            return (false, "squad.duplicate", null, none);

        // Contracts gate fielding (spec-demon-contracts.md). Settling first is what keeps an
        // un-migrated player from being refused everything; on any day already settled it is a
        // single read, so this is not a billing loop on the battle path.
        _store.SettleContracts(playerId);
        var contracts = _store.ListContracts(playerId)
            .ToDictionary(c => c.InstanceId, c => c, StringComparer.Ordinal);
        bool Bound(string id) => contracts.TryGetValue(id, out var c) && c.Bound;
        bool Deployable(string id) => contracts.TryGetValue(id, out var c) && c.Deployable;

        var roster = _store.ListDemonRoster(playerId).Items;
        List<DemonSpecimenDto> picked;
        if (squadInstanceIds is { Count: > 0 })
        {
            picked = new List<DemonSpecimenDto>();
            foreach (var id in squadInstanceIds)
            {
                var specimen = roster.FirstOrDefault(s =>
                    string.Equals(s.Profile.InstanceId, id, StringComparison.Ordinal));
                if (specimen == null) return (false, "squad.unknown-specimen", null, none);
                if (!Bound(id)) return (false, "squad.unbound", null, none);
                if (!Deployable(id)) return (false, "squad.insubordinate", null, none);
                picked.Add(specimen);
            }
        }
        else
        {
            // Auto-pick skips what cannot serve rather than refusing — the player asked for a
            // battle, not for a lecture about their roster.
            picked = roster.Where(s => Deployable(s.Profile.InstanceId)).Take(3).ToList();
        }

        if (picked.Count == 0)
        {
            // SIM convenience: an empty roster still gets a deterministic synthetic squad.
            return (true, "", Enumerable.Range(0, 2).Select(i => Synthetic(i)).ToList(), none);
        }

        var squad = new List<BattleActorSetup>(picked.Count);
        for (var i = 0; i < picked.Count; i++)
        {
            var s = picked[i];
            var species = FusionRpg.Core.Demons.DemonSpeciesCatalog.Get(s.Profile.SpeciesId);
            var level = (int)Math.Max(1, s.Actor.Level);
            squad.Add(new BattleActorSetup
            {
                Key = $"squad:{i}",
                Side = "squad",
                SpeciesId = species.SpeciesId,
                TypeId = species.DemonTypeId,
                Level = level,
                ElementPrimary = species.ElementPrimary,
                ElementSecondary = species.ElementSecondary,
                TraitIds = s.Profile.TraitIds,
                MaxHp = BattleRuleset.BaseHp(level),
                Atk = BattleRuleset.BaseAtk(level),
                Defense = BattleRuleset.BaseDefense(level),
                ChannelMods = StarChannelMods(s.Profile.Star, level)
                    .Concat(LoyaltyChannelMods(
                        contracts.TryGetValue(s.Profile.InstanceId, out var c) ? c.Loyalty : 0, level))
                    .Concat(AptitudeChannelMods(level, playerId, _store))
                    .ToList(),
                EquippedActionIds = EquippedActionIdsFor(s.Profile.InstanceId, _store),
            });
        }

        return (true, "", squad, picked.Select(p => p.Profile.InstanceId).ToList());
    }

    /// <summary>
    /// Star ranks reach battles ONLY here — flat per-mille shares of the level stats on the omni
    /// channels (spec-demon-fusion.md F8). The engine and its goldens never change; stars are
    /// ordinary ChannelMods in the setup. Floored at `star` so low-level stars still register.
    /// </summary>
    public static IReadOnlyList<BattleChannelMod> StarChannelMods(int star, int level)
    {
        if (star <= 0) return Array.Empty<BattleChannelMod>();
        // The per-mille bonus is now a CURVE indexed on the triangular sacrifice cost, not `star`
        // times a flat rate (StarPolicy.StarPowerMilli) -- so `star` must not be multiplied in again.
        var power = Math.Max(star,
            BattleRuleset.BaseAtk(level) * FusionRpg.Core.Demons.Fusion.StarPolicy.StarPowerMilli(star) / 1000);
        var defense = Math.Max(star,
            BattleRuleset.BaseDefense(level) * FusionRpg.Core.Demons.Fusion.StarPolicy.StarDefenseMilli(star) / 1000);
        return new[]
        {
            new BattleChannelMod(FusionRpg.Core.Stats.Derived.DerivedStatChannels.CombatPowerOmni, power),
            new BattleChannelMod(FusionRpg.Core.Stats.Derived.DerivedStatChannels.CombatDefenseOmni, defense)
        };
    }

    /// <summary>
    /// Loyalty reaches battles the same way stars do — flat per-mille shares of the level stats on
    /// the omni channels, never an engine change (spec-demon-contracts.md G7). The Bound band pays
    /// +0‰ by design, so a fresh contract cannot move a single golden hash.
    /// </summary>
    public static IReadOnlyList<BattleChannelMod> LoyaltyChannelMods(int loyalty, int level)
    {
        var rank = ContractPolicy.RankFor(loyalty);
        var milli = ContractPolicy.RankBonusMilli(rank);
        if (milli <= 0) return Array.Empty<BattleChannelMod>();
        // Floored at the rank step (Sworn 1 / Trusted 2 / Devoted 3) exactly like stars: at low
        // levels a per-mille share truncates to nothing, and every rank would look identical.
        var floor = (int)rank - 1;
        var power = Math.Max(floor, BattleRuleset.BaseAtk(level) * milli / 1000);
        var defense = Math.Max(floor, BattleRuleset.BaseDefense(level) * milli / 1000);
        return new[]
        {
            new BattleChannelMod(FusionRpg.Core.Stats.Derived.DerivedStatChannels.CombatPowerOmni, power),
            new BattleChannelMod(FusionRpg.Core.Stats.Derived.DerivedStatChannels.CombatDefenseOmni, defense)
        };
    }

    /// <summary>
    /// class-system-todo.md P2.5/P9.1 — aptitudes reach battle the same way stars/loyalty do: ordinary
    /// ChannelMods in the setup, adapted at this one seam, never an engine or composer change
    /// (spec-aptitude-resolve.md §2a — "this module emits one thing and it is adapted at two seams").
    /// **Reads the real commander-scope allocation now** (spec-aptitude-allocation-surface.md, 2026-08-27)
    /// — `point-economy`'s `AllocationStore` is the real per-actor source; a player who has never
    /// allocated still resolves against `AptitudeAllocation.Empty` (`LoadAllocation`'s own contract on
    /// an unset key), so this stays exactly as inert as before for every squad it already served.
    /// </summary>
    public static IReadOnlyList<BattleChannelMod> AptitudeChannelMods(int level, long playerId, RpgStore store)
    {
        var allocation = store.LoadAllocation(
            FusionRpg.Core.Stats.Aptitudes.AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId));
        var ladder = new FusionRpg.Core.Power.PowerLadder(FusionRpg.Core.Power.PowerTuningHub.Tuning);
        return FusionRpg.Core.Stats.Aptitudes.AptitudeResolver.ResolveForBattle(
            allocation, FusionRpg.Core.Stats.Aptitudes.AptitudeTuningHub.Tuning, ladder, level,
            FusionRpg.Core.Stats.Derived.DerivedStatRegistry.CreateDefault());
    }

    /// <summary>
    /// T22 (action-todo.md): "the auto-equipped set appears in the battle report." A real loadout row
    /// wins if the specimen has one; otherwise it is auto-equipped live from whatever skills the
    /// specimen currently holds, ranked by <see cref="RungPolicy.Table"/> — never persisted, so a
    /// later unlock/discard is reflected immediately with no stale cache to invalidate (the same
    /// contract `RpgStore.GetLoadoutOrAutoEquip`'s own doc comment states).
    ///
    /// <para>Keyed on <see cref="OwnerKind.Entity"/> + the specimen's own instance id, matching
    /// `LoadoutStoreTests.cs`'s own convention for "one demon's loadout, independent of who currently
    /// owns it" — never the player id, since two specimens of the same species held by one player can
    /// carry different loadouts.</para>
    /// </summary>
    static IReadOnlyList<string> EquippedActionIdsFor(string instanceId, RpgStore store)
    {
        var scope = new OwnerScope(OwnerKind.Entity, instanceId);

        var candidates = store.ListGrants(scope)
            .Select(g => store.GetAction(g.ActionId))
            .Where(a => a is { Kind: ActionKind.Skill })
            .Select(a => new AutoEquipCandidate(a!.ActionId, a.Rung))
            .ToList();

        return store.GetLoadoutOrAutoEquip(scope, candidates);
    }

    static BattleActorSetup Synthetic(int i) => new()
    {
        Key = $"squad:{i}",
        Side = "squad",
        SpeciesId = "sim-squad",
        TypeId = 10_000,
        Level = 5,
        MaxHp = BattleRuleset.BaseHp(5),
        Atk = BattleRuleset.BaseAtk(5),
        Defense = BattleRuleset.BaseDefense(5)
    };

    async Task BroadcastAsync(long playerId, EventInsertNotify notify)
    {
        try
        {
            foreach (var pid in notify.ActivityPlayers.Distinct())
            {
                var rollup = _store.GetPvzActivityRollup(pid);
                if (rollup != null)
                    await _hub.Clients.Group(RpgConstants.WebGroup)
                        .SendAsync("PvzActivityUpdated", rollup).ConfigureAwait(false);
            }

            foreach (var d in notify.Progression)
            {
                await _hub.Clients.Group(RpgConstants.WebGroup).SendAsync("RpgProgressionUpdated", new
                {
                    playerId = d.PlayerId,
                    kind = d.Kind,
                    typeId = d.TypeId,
                    revision = d.Revision
                }).ConfigureAwait(false);
            }

            await _hub.Clients.Group(RpgConstants.WebGroup)
                .SendAsync("SoulsUpdated", new { playerId }).ConfigureAwait(false);
        }
        catch
        {
            // notifies are best-effort; the match is durable regardless
        }
    }
}

public static class WebMatchEndpoints
{
    /// <summary>SIM-only trigger — mapped inside the /api/test group (no player-facing battle
    /// API in this module; expeditions owns the player surface).</summary>
    public static void MapWebMatchTest(this RouteGroupBuilder test)
    {
        test.MapPost("/web-match", async (WebMatchRequest body, WebMatchService svc, RpgStore store) =>
        {
            var pid = body.PlayerId ?? store.GetCurrentPlayerId();
            if (!store.PlayerExists(pid)) return Results.NotFound();
            var (ok, reason, outcome) = await svc.RunWebMatchAsync(
                pid, body.CorrelationId ?? "", body.WaveId ?? "rift-skirmish", body.Squad);
            if (!ok) return Results.BadRequest(new { reason });
            return Results.Ok(new
            {
                replayed = outcome!.Replayed,
                matchKey = outcome.MatchKey,
                runId = outcome.RunId,
                outcome = outcome.Report.Outcome.ToString().ToLowerInvariant(),
                rounds = outcome.Report.Rounds,
                soulLootMilli = outcome.Report.SoulLootMilli,
                actors = outcome.Report.Actors
            });
        });
    }

    public sealed class WebMatchRequest
    {
        public long? PlayerId { get; set; }
        public string? CorrelationId { get; set; }
        public string? WaveId { get; set; }
        public List<string>? Squad { get; set; }
    }
}
