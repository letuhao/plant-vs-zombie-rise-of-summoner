using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Battle;
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
    readonly RpgStore _store;
    readonly IHubContext<RpgHub> _hub;

    public WebMatchService(RpgStore store, IHubContext<RpgHub> hub)
    {
        _store = store;
        _hub = hub;
    }

    public sealed record WebMatchOutcome(
        bool Replayed, string MatchKey, long? RunId, BattleReport Report);

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
            var storedReport = BattleEngine.Resolve(storedSetup, entry.Seed);
            return (true, "replay", new WebMatchOutcome(true, entry.MatchKey, entry.RunId, storedReport));
        }

        // 3–4. Resolve + dedicated ingest; 5. notify.
        var (report, notify) = ResolveAndIngest(playerId, matchKey, setup, seed);
        // 4b. The squad lived it: contracted members gain or lose loyalty for the result. Replays
        //     return above, so a retry never credits a second time. Deliberately OUTSIDE the
        //     log-before-ingest envelope: a crash between ingest and here loses ±15 loyalty and the
        //     boot sweep will not replace it. That is the accepted trade — widening the exactly-once
        //     envelope for a loyalty point is not worth the coupling.
        _store.ApplyContractResults(playerId, pickedIds, report.Outcome == BattleOutcome.Victory);
        await BroadcastAsync(playerId, notify).ConfigureAwait(false);

        var linked = _store.TryGetWebMatchLog(playerId, corr);
        return (true, "ok", new WebMatchOutcome(false, matchKey, linked?.RunId, report));
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
            var storedReport = BattleEngine.Resolve(storedSetup, entry.Seed);
            return (true, "replay", new WebMatchOutcome(true, entry.MatchKey, entry.RunId, storedReport));
        }

        var (report, notify) = ResolveAndIngest(playerId, matchKey, setup, seed);
        await BroadcastAsync(playerId, notify).ConfigureAwait(false);
        var linked = _store.TryGetWebMatchLog(playerId, corr);
        return (true, "ok", new WebMatchOutcome(false, matchKey, linked?.RunId, report));
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

    (BattleReport Report, EventInsertNotify Notify) ResolveAndIngest(
        long playerId, string matchKey, BattleSetup setup, ulong seed)
    {
        var report = BattleEngine.Resolve(setup, seed);
        var events = BattleReportEmitter.Emit(report, matchKey).ToList();

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
                    .ToList()
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
        var power = Math.Max(star,
            BattleRuleset.BaseAtk(level) * star * FusionRpg.Core.Demons.Fusion.StarPolicy.PerStarPowerMilli / 1000);
        var defense = Math.Max(star,
            BattleRuleset.BaseDefense(level) * star * FusionRpg.Core.Demons.Fusion.StarPolicy.PerStarDefenseMilli / 1000);
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
