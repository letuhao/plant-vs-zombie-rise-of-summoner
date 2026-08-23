using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Battle;

/// <summary>
/// Pure deterministic battle resolver (spec-match-source-core.md, combat-unification
/// battle-adoption). No I/O, no clock, no ambient state: same setup + seed + platform ⇒
/// byte-identical report. Round order is locked (RulesetVersion): status ticks →
/// initiative-ordered attacks → death cleanup → shield upkeep → round end.
/// v2: attack resolution runs the SSOT resolver (OverlayCombatCalculator — sigmoid hit/crit,
/// ElementHub matchup, battle profile min-chip); every HP delta applies through
/// DamageApplyPipeline (shield gate → battle funnel), so shields work exactly like overlay.
/// Per-system RNG streams (`initiative`, `crit`, `essence`, `status`, `proc`; the v1 `damage`
/// stream name stays reserved — its variance roll retired with the v1 curves).
/// </summary>
public static class BattleEngine
{
    sealed class ActorState : IBattleHpTarget
    {
        public ActorState(BattleActorSetup setup, int sideIndex)
        {
            Setup = setup;
            SideIndex = sideIndex;
            Hp = setup.MaxHp;
            Derived = BattleStatComposer.Compose(setup);
            ElementTypes = ActorElementTypes.Create(
                setup.ElementPrimary,
                setup.ElementSecondary == setup.ElementPrimary ? null : setup.ElementSecondary);
            AttackComponents = setup.ElementPrimary is { } elem
                ? new[] { new ElementPayloadComponent(elem, 1.0) }
                : Array.Empty<ElementPayloadComponent>();
            foreach (var traitId in setup.TraitIds)
                _traits.Add(TraitBattleCatalog.Get(traitId).TraitId);
            ImmortalCharges = Has("immortal") ? TraitBattleCatalog.Get("immortal").DeathRefusalCharges : 0;
        }

        readonly HashSet<string> _traits = new(StringComparer.Ordinal);

        public BattleActorSetup Setup { get; }
        public int SideIndex { get; }               // position within its own side (adjacency)
        public ActorDerivedSnapshot Derived { get; }
        public ActorElementTypes ElementTypes { get; }
        public ElementPayloadComponent[] AttackComponents { get; }
        public int Hp { get; set; }
        public int MaxHp => Setup.MaxHp;
        public int DamageDealt;
        public int Kills;
        public long ShieldAbsorbed;
        public bool Retreated;
        public int ImmortalCharges;
        public bool Alive => Hp > 0;

        /// <summary>Still fighting: alive and not retreated.</summary>
        public bool Active => Alive && !Retreated;

        public bool Has(string traitId) => _traits.Contains(traitId);
    }

    /// <summary>Owned-PRNG adapter for the L2b apply roll — `status` stream, never System.Random.</summary>
    sealed class BattleStatusRng : IStatusRng
    {
        readonly SeededRng _rng;
        readonly Timeline.BattleTrace? _trace;

        public BattleStatusRng(ulong seed, Timeline.BattleTrace? trace = null)
        {
            _rng = SeededRng.DeriveStream(seed, "status");
            _trace = trace;
        }

        public double NextUnit()
        {
            // Recorded so the parity ladder can see this stream at all. Asserting "the status
            // stream never draws" against an UNinstrumented stream would pass even if it drew a
            // thousand times — the assertion only means something because of this line.
            var raw = _rng.NextInt(1_000_000);
            _trace?.Draw("status", raw);
            return raw / 1_000_000.0;
        }
    }

    /// <summary>
    /// Routes DoT/regen pulses through the shared apply pipeline — shields absorb battle DoTs
    /// exactly like overlay ones (empty components + hitCount 1 is overlay parity: the overlay
    /// pulse sink sends no payload either).
    /// </summary>
    sealed class BattlePulseSink : IStatusPulseSink
    {
        readonly Func<string, long, string, DamageApplyResult> _apply;
        public BattlePulseSink(Func<string, long, string, DamageApplyResult> apply) => _apply = apply;

        // Math.Round, NOT a truncating cast — StatusEffectBridge (the overlay sink) rounds, and
        // EffectiveMagnitude is fractional whenever a status power/resist channel is non-zero.
        // Truncating cost battle 1 HP per pulse against the overlay, and turned a −0.6 pulse
        // into 0 — which fails the pipeline's `amount < 0` test and skips the shield gate.
        public void PulseHp(StatusInstance instance, double amount) =>
            _apply(instance.HostPtr, (long)Math.Round(amount), "battle.status." + instance.StatusId);
    }

    /// <param name="trace">
    /// Optional observation for the kernel-adoption parity ladder. Null in production, and every
    /// record site is null-conditional — tracing cannot change an outcome.
    /// </param>
    public static BattleReport Resolve(BattleSetup setup, ulong seed, Timeline.BattleTrace? trace = null)
    {
        if (setup.Squad.Count == 0) throw new ArgumentException("Squad is empty.");
        if (setup.Wave.Count == 0) throw new ArgumentException("Wave is empty.");

        // Loud validation over silent corruption (2026-08-21 review): the funnel lower-cases
        // target keys while the actor map is case-sensitive — a mixed-case key would make its
        // actor silently unhittable; a duplicate key would shadow an actor; MaxHp < 1 spawns a
        // corpse that never gets its die event. battle-adoption adds the prefix ban: keys
        // starting "entity:" or "0x" would be mangled by CombatPtr.Normalize at the shield
        // gate while the actor map keeps the original.
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in setup.Squad.Concat(setup.Wave))
        {
            if (string.IsNullOrWhiteSpace(a.Key) || a.Key != a.Key.Trim().ToLowerInvariant())
                throw new ArgumentException($"Actor key '{a.Key}' must be non-empty lower-case (funnel keys normalize).");
            if (a.Key.StartsWith("entity:", StringComparison.Ordinal) || a.Key.StartsWith("0x", StringComparison.Ordinal))
                throw new ArgumentException($"Actor key '{a.Key}' must not start with 'entity:' or '0x' (ptr-space prefixes).");
            if (!seenKeys.Add(a.Key))
                throw new ArgumentException($"Duplicate actor key '{a.Key}'.");
            if (a.MaxHp < 1)
                throw new ArgumentException($"Actor '{a.Key}' must have MaxHp >= 1.");
        }

        var initiativeRng = SeededRng.DeriveStream(seed, "initiative");
        ICombatRng critRng = new SeededRngCombatAdapter(SeededRng.DeriveStream(seed, "crit")); // hit + crit rolls
        if (trace != null) critRng = trace.WrapCombat("crit", critRng);
        var essenceRng = SeededRng.DeriveStream(seed, "essence"); // void/chaos rider procs
        var statusRng = new BattleStatusRng(seed, trace);
        var calculator = new OverlayCombatCalculator();

        // Stable ordered state — never dictionary-enumerated (determinism discipline).
        var actors = setup.Squad.Select((a, i) => new ActorState(a, i))
            .Concat(setup.Wave.Select((a, i) => new ActorState(a, i)))
            .ToList();
        var byKey = new Dictionary<string, ActorState>(StringComparer.Ordinal);
        foreach (var a in actors)
            byKey[a.Setup.Key] = a;

        // Battle-local effect stack: funnel → FA10 sink over engine state; statuses over the
        // composed derived profiles; the clock is the synthetic round clock.
        var host = new BattleEffectHost(key => byKey.TryGetValue(key, out var a) ? a : null, seed);
        var t0 = host.Clock.UtcNow;
        var status = new StatusRuntime(StatusCatalogBootstrap.CreateDefault(),
            (ptr, attackerLess) => attackerLess || ptr == null || !byKey.TryGetValue(ptr, out var a)
                ? ActorDerivedSnapshot.AttackerLess()
                : a.Derived);

        // Shield stack (battle-adoption): battle-local runtime + gate; every HP delta goes
        // through the shared pipeline so the one-key discipline holds (single FA10 slot per
        // actor per window) and shields absorb before HP — overlay-identical semantics.
        var shields = new ShieldRuntime();
        var shieldGate = new ShieldGate(shields, (ptr, attackerLess) =>
            attackerLess || ptr == null || !byKey.TryGetValue(ptr, out var a)
                ? CombatActorSnapshot.AttackerLess()
                : new CombatActorSnapshot(a.Derived, a.ElementTypes));
        var hpSink = new FunnelHpDeltaSink(host.Funnel);

        DamageApplyResult ApplyHp(
            ActorState owner, long amount, string effectId,
            ElementPayloadComponent[]? components = null, ActorState? attacker = null, string? grantId = null)
        {
            var result = DamageApplyPipeline.Apply(
                owner.Setup.Key, amount, hitCount: 1,
                components ?? Array.Empty<ElementPayloadComponent>(),
                attacker?.Derived, owner.Derived, shieldGate, hpSink,
                pluginId: "battle", effectId: effectId, grantId: grantId);
            owner.ShieldAbsorbed += result.AbsorbedAmount;
            return result;
        }

        var pulseSink = new BattlePulseSink((hostPtr, amount, effectId) =>
            byKey.TryGetValue(hostPtr, out var owner)
                ? ApplyHp(owner, amount, effectId)
                : new DamageApplyResult(DamageApplyOutcome.SinkRefused, 0, 0));

        var events = new List<BattleEventRec>();
        var recordedDeaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in actors)
            events.Add(new BattleEventRec(0, BattleEventKinds.Spawn, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));

        // Innate shields (battle-adoption): direct apply at setup — snapshots composed in the
        // ActorState ctor, so the shield spec's capacity barrier is satisfied by construction.
        // Content durations are ms; battle ticks are rounds.
        foreach (var a in actors)
        {
            if (a.Setup.InnateShield is not { } innate) continue;
            shields.Apply(new ShieldGrant
            {
                OwnerKey = Contracts.EffectOwnerKeys.Entity(a.Setup.Key),
                SourceId = "innate:" + a.Setup.TypeId,
                Element = innate.Element,
                BaseHp = innate.BaseHp,
                Priority = innate.Priority,
                DurationTicks = innate.DurationMs is { } ms
                    ? (ms + BattleRuleset.RoundDurationMs - 1) / BattleRuleset.RoundDurationMs
                    : null,
                RefillOnMerge = false,
                IsInnate = true
            }, a.Derived, nowTick: 0);
        }

        var shieldEventScratch = new List<ShieldEventRec>();
        void DrainShieldEvents(int round)
        {
            shieldEventScratch.Clear();   // caller owns the scratch on EVERY path (DrainEvents appends)
            if (shields.DrainEvents(shieldEventScratch) == 0) return;
            foreach (var rec in shieldEventScratch)
            {
                var key = rec.OwnerKey.StartsWith("entity:", StringComparison.Ordinal)
                    ? rec.OwnerKey.Substring("entity:".Length)
                    : rec.OwnerKey;
                if (!byKey.TryGetValue(key, out var owner)) continue;
                events.Add(new BattleEventRec(round, rec.Kind,
                    key, owner.Setup.TypeId, owner.Setup.Side, rec.Amount, rec.Element, rec.ShieldId));
            }

            shieldEventScratch.Clear();
        }

        void SweepDeaths(int round)
        {
            foreach (var a in actors)
            {
                if (!a.Alive && recordedDeaths.Add(a.Setup.Key))
                {
                    events.Add(new BattleEventRec(round, BattleEventKinds.Die, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));
                    shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                }
            }
        }

        // Initial statuses land attacker-less at t0 (trait/attack riders reuse this path later).
        // Scripted setup statuses apply deterministically — the L2b evaluator still blocks them
        // on immunity/potency floor (resist channels), but the apply roll is bypassed (0.0 roll).
        var scriptedApplyRng = new FixedStatusRng(0.0);
        foreach (var a in actors)
        {
            foreach (var spec in a.Setup.InitialStatuses)
            {
                status.Apply(new StatusApplyInput(
                    spec.StatusId,
                    HostPtr: a.Setup.Key,
                    AttackerPtr: null,
                    GrantId: "battle:init:" + a.Setup.Key + ":" + spec.StatusId,
                    BaseMagnitude: spec.MagnitudePerPulse,
                    BaseDuration: spec.DurationMs,
                    PeriodMs: spec.PeriodMs,
                    DurationMs: spec.DurationMs,
                    GrantChance: spec.GrantChanceMilli / 1000.0,
                    EffectId: "battle.status." + spec.StatusId,
                    PluginId: "battle",
                    AttackerLess: true), scriptedApplyRng, t0);
            }
        }

        // Immortal death refusal: a queued +1 through the pipeline turns the death into survive-at-1.
        void ReviveImmortals()
        {
            var queued = false;
            foreach (var a in actors)
            {
                if (!a.Alive && !a.Retreated && a.ImmortalCharges > 0 && !recordedDeaths.Contains(a.Setup.Key))
                {
                    a.ImmortalCharges--;
                    ApplyHp(a, 1, "battle.trait.immortal");
                    queued = true;
                }
            }

            if (queued)
                host.Flush();
        }

        // Coward retreat: below the threshold the actor leaves the battle alive (no die event).
        void CheckRetreats()
        {
            foreach (var a in actors)
            {
                if (!a.Active || !a.Has("coward")) continue;
                var def = TraitBattleCatalog.Get("coward");
                if ((long)a.Hp * 1000 < (long)a.MaxHp * def.RetreatBelowMilli)
                {
                    a.Retreated = true;
                    status.WithdrawEntity(a.Setup.Key);
                    shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                }
            }
        }

        void PostFlush(int round)
        {
            ReviveImmortals();
            SweepDeaths(round);
            CheckRetreats();
        }

        var rounds = 0;
        while (rounds < BattleRuleset.MaxRounds && AnyActive(actors, "squad") && AnyActive(actors, "wave"))
        {
            rounds++;
            var now = t0.AddMilliseconds((double)rounds * BattleRuleset.RoundDurationMs);
            host.Clock.UtcNow = now;

            // 1) Status ticks + regenerator pulses share one FA10 window per round.
            foreach (var a in actors)
            {
                if (a.Active && a.Has("regenerator"))
                {
                    var def = TraitBattleCatalog.Get("regenerator");
                    ApplyHp(a, Math.Max(1, a.MaxHp * def.RegenPerRoundMilli / 1000), "battle.trait.regenerator");
                }
            }

            trace?.Phase(rounds, "status");
            status.Tick(now, pulseSink, board: null, spreadRng: statusRng);
            host.Flush();
            trace?.Phase(rounds, "post-flush");
            PostFlush(rounds);
            if (!AnyActive(actors, "squad") || !AnyActive(actors, "wave"))
                break;

            // 2) Initiative-ordered attacks: stable order, per-round jitter from the initiative
            //    stream; swift subtracts a full band so it always acts before non-swift kin.
            trace?.Phase(rounds, "initiative");
            var order = actors
                .Where(a => a.Active)
                .OrderBy(a =>
                {
                    // Key selectors run once per element in SOURCE order, so the draw sequence
                    // is "actors list order, filtered to Active" — T5 hazard 1, and note that
                    // CC-locked actors are Active and therefore DO draw (hazard 4).
                    var roll = initiativeRng.NextInt(1000);
                    trace?.Draw("initiative", roll);
                    return roll - (a.Has("swift") ? TraitBattleCatalog.Get("swift").InitiativeBonusMilli : 0);
                })
                .ToList();

            foreach (var attacker in order)
            {
                if (!attacker.Active) continue;
                if (IsCcLocked(status, attacker.Setup.Key, now)) continue; // CC skips the turn
                var target = SelectTarget(actors, attacker, status, now);
                if (target is null) break;

                // SSOT resolution (combat-unification): base = Atk; typed power/defense,
                // sigmoid hit/crit, matchup, and the battle-profile min-chip all live in the
                // resolver. Natural rolls only — Force* would desync the crit stream.
                var (signedDelta, breakdown) = calculator.Compute(new OverlayCombatRequest
                {
                    BaseOverlayDamage = attacker.Setup.Atk,
                    Components = attacker.AttackComponents,
                    Attacker = new CombatActorSnapshot(attacker.Derived, attacker.ElementTypes),
                    Defender = new CombatActorSnapshot(target.Derived, target.ElementTypes),
                    Profile = CombatProfile.BattleSim
                }, critRng);
                if (!breakdown.Hit)
                    continue; // miss — no damage, no HP change

                var damage = (int)(-signedDelta);

                // Berserker ramp: battle mechanic on resolver OUTPUT, never inside the formula.
                if (attacker.Has("berserker"))
                    damage = damage * TraitBattleMath.BerserkerRampMilli(
                        TraitBattleCatalog.Get("berserker"), attacker.Hp, attacker.MaxHp) / 1000;

                // Essence riders (void-touched / chaos-marked): per-landed-hit proc on its own stream.
                var rider = 0;
                foreach (var essenceId in EssenceTraits)
                {
                    if (!attacker.Has(essenceId)) continue;
                    var def = TraitBattleCatalog.Get(essenceId);
                    var essenceRoll = essenceRng.NextPerMille();
                    trace?.Draw("essence", essenceRoll);
                    if (essenceRoll < def.EssenceProcMilli)
                        rider += Math.Max(1, damage * def.EssenceRiderMilli / 1000);
                }

                // Guardian: an adjacent active guardian pulls a share of the hit onto itself.
                // Each slice passes the gate separately — both actors' shields absorb their own
                // portion (spec: guardian two-slice semantics).
                var guardian = FindAdjacentWithTrait(actors, target, "guardian");
                var share = guardian != null
                    ? damage * TraitBattleCatalog.Get("guardian").GuardShareMilli / 1000
                    : 0;

                ApplyHp(target, -(damage - share + rider), "battle.attack",
                    attacker.AttackComponents, attacker);
                if (share > 0)
                    ApplyHp(guardian!, -share, "battle.trait.guardian",
                        attacker.AttackComponents, attacker);
                host.Flush();
                attacker.DamageDealt += damage + rider;   // resolver output, pre-absorb (spec)

                ReviveImmortals();
                var killsThisHit = 0;
                foreach (var victim in guardian == null ? new[] { target } : new[] { target, guardian })
                {
                    if (!victim.Alive && recordedDeaths.Add(victim.Setup.Key))
                    {
                        attacker.Kills++;
                        killsThisHit++;
                        events.Add(new BattleEventRec(rounds, BattleEventKinds.Die, victim.Setup.Key, victim.Setup.TypeId, victim.Setup.Side));
                        shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(victim.Setup.Key));
                    }
                }

                // Soul-eater: on-kill heal through the pipeline.
                if (killsThisHit > 0 && attacker.Has("soul-eater"))
                {
                    var def = TraitBattleCatalog.Get("soul-eater");
                    ApplyHp(attacker,
                        (long)killsThisHit * Math.Max(1, attacker.MaxHp * def.OnKillHealMilli / 1000),
                        "battle.trait.soul-eater");
                    host.Flush();
                }

                CheckRetreats();
            }

            // 3) Death cleanup happens inline (Hp gate); 4) shield upkeep AFTER dispatch —
            // an expiring shield still absorbed this round's damage (shield spec order).
            foreach (var a in actors)
            {
                if (!a.Alive)
                {
                    status.WithdrawEntity(a.Setup.Key);
                    shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                }
            }

            trace?.Phase(rounds, "shield-upkeep");
            shields.Tick(rounds, BattleRuleset.RoundDurationMs, ownerKey =>
            {
                var key = ownerKey.StartsWith("entity:", StringComparison.Ordinal)
                    ? ownerKey.Substring("entity:".Length)
                    : ownerKey;
                return byKey.TryGetValue(key, out var a) ? a.Derived : ActorDerivedSnapshot.AttackerLess();
            });
            DrainShieldEvents(rounds);
            if (trace != null)
                foreach (var a in actors)
                    trace.State(rounds, a.Setup.Key, a.Hp, a.ShieldAbsorbed);
        }

        DrainShieldEvents(rounds);   // trailing grants/absorbs when the loop exits mid-round

        var outcome = !AnyActive(actors, "wave") ? BattleOutcome.Victory
            : !AnyActive(actors, "squad") ? BattleOutcome.Defeat
            : BattleOutcome.Stalemate;

        var greedyDef = TraitBattleCatalog.Get("greedy");
        var geniusDef = TraitBattleCatalog.Get("genius");
        var greedySurvivors = actors.Count(a =>
            a.Setup.Side == "squad" && a.Alive && a.Has("greedy"));

        return new BattleReport
        {
            Seed = seed,
            WaveId = setup.WaveId,
            Outcome = outcome,
            Rounds = rounds,
            SoulLootMilli = 1000 + greedyDef.SoulLootBonusMilli * greedySurvivors,
            Events = events,
            Actors = actors.Select(a => new BattleActorResult(
                a.Setup.Key, a.Setup.Side, a.Setup.SpeciesId, a.Setup.TypeId,
                a.Hp, a.DamageDealt, a.Kills, a.Alive, a.Retreated,
                a.Has("genius") ? 1000 + geniusDef.SpecimenXpBonusMilli : 1000,
                a.ShieldAbsorbed)).ToList()
        };
    }

    static readonly string[] EssenceTraits = { "void-touched", "chaos-marked" };

    static bool IsCcLocked(StatusRuntime status, string actorKey, DateTimeOffset now)
    {
        foreach (var inst in status.ForHost(actorKey))
        {
            // Asks what the status DOES, not how it is delivered (E17). This tested `Kind`, and
            // `Kind` conflates semantic role with execution path — so every `UnityCc` status locked
            // the actor out of its turn, including `poison`, which is damage over time. Of the nine
            // statuses the old check caught, eight are categorised `cc` and exactly one is not.
            if (inst.IsCrowdControl && inst.ExpiresAt >= now)
                return true;
        }

        return false;
    }

    static bool AnyActive(List<ActorState> actors, string side) =>
        actors.Any(a => a.Active && a.Setup.Side == side);

    /// <summary>
    /// Target policy: first active opponent, unless the attacker is bloodthirsty (lowest-HP
    /// active opponent, ties by list order). A loyal actor adjacent to the chosen target then
    /// intercepts the hit entirely.
    /// </summary>
    static ActorState? SelectTarget(List<ActorState> actors, ActorState attacker, StatusRuntime status, DateTimeOffset now)
    {
        ActorState? target = null;
        if (attacker.Has("bloodthirsty"))
        {
            foreach (var a in actors)
                if (a.Active && a.Setup.Side != attacker.Setup.Side && (target == null || a.Hp < target.Hp))
                    target = a;
        }
        else
        {
            foreach (var a in actors)
            {
                if (a.Active && a.Setup.Side != attacker.Setup.Side)
                {
                    target = a;
                    break;
                }
            }
        }

        if (target == null)
            return null;

        var bodyguard = FindAdjacentWithTrait(actors, target, "loyal");
        if (bodyguard != null && !IsCcLocked(status, bodyguard.Setup.Key, now))
            return bodyguard;
        return target;
    }

    /// <summary>First active same-side setup-order neighbor (index ±1) carrying the trait.</summary>
    static ActorState? FindAdjacentWithTrait(List<ActorState> actors, ActorState around, string traitId)
    {
        foreach (var a in actors)
        {
            if (!a.Active || ReferenceEquals(a, around)) continue;
            if (a.Setup.Side != around.Setup.Side) continue;
            if (Math.Abs(a.SideIndex - around.SideIndex) != 1) continue;
            if (a.Has(traitId))
                return a;
        }

        return null;
    }
}
