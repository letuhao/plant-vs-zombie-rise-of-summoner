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
public static partial class BattleEngine
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
        public long Hp { get; set; }
        public long MaxHp => Setup.MaxHp;
        public long DamageDealt;
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
    /// <param name="onEffectHostReady">
    /// T14 (action-todo.md, spec-basic-attack-adoption.md): a test-only seam proving the "grant
    /// path" two atom `D6` comments wait on — <c>resource.delta</c> and <c>shield.grant</c> can now
    /// reach <see cref="BattleEffectHost.Bag"/>'s sink and this method's own <see cref="ShieldGate"/>
    /// once <see cref="EffectBag.ShieldGate"/> is wired (done below, unconditionally). Null in every
    /// production and golden call site — the callback fires once, before round 1, with the fully
    /// wired host handed to it; nothing else about <see cref="Resolve"/> changes when it is null.
    /// </param>
    /// <param name="profile">
    /// B14 (spec-kernel-adoption.md): a 5th optional trailing parameter, not a new overload — every
    /// existing call site (positional 2-arg, or naming <paramref name="trace"/>/
    /// <paramref name="onEffectHostReady"/>) compiles unchanged. <c>null</c> means "content did not
    /// choose," resolving to <see cref="Timeline.BattleModeProfileCatalog.ClassicRound"/>, mirroring
    /// how B12's <see cref="WaveDef.Profile"/> resolution already works. <b>Scope of what this
    /// actually changes today:</b> only the round-boundary tick sequencing runs through the
    /// profile's chosen advance mechanism — <see cref="Resolve"/> is a batch resolver, not a live
    /// per-frame loop, so <see cref="Timeline.AdvancePolicyKind.NextEvent"/> is used regardless of
    /// which profile is passed (a <see cref="Timeline.FixedIncrementAdvance"/> has no meaning
    /// without a caller supplying per-frame ticks, which nothing here does). The profile's other
    /// fields (`W`, `WScope`, `Commitment`, `Economy`) are accepted and available for future
    /// enrichment but are inert here — this gate proves the kernel can carry the round skeleton
    /// byte-identically, not that combat routes through the per-actor turn FSM, which is explicitly
    /// out of scope (`battle-timeline-map.md`: "E2 skills... respec after T5").
    /// </param>
    public static BattleReport Resolve(BattleSetup setup, ulong seed, Timeline.BattleTrace? trace = null,
        Action<BattleEffectHost>? onEffectHostReady = null, Timeline.BattleModeProfile? profile = null)
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

        // B13 (spec-kernel-adoption.md): every local this method used to hold — actors, byKey,
        // host, shields, gate, sink, events, RNG streams — plus the eight closures over them, now
        // live on BattleRunState (BattleRunState.cs, nested in this partial class). Zero behavior
        // change: every line below is the same statement sequence as before extraction, reading
        // through `state.` instead of a captured local.
        var state = new BattleRunState(setup, seed, trace, onEffectHostReady);

        // B14: the round boundary runs on the kernel's own EventQueue/SimulationClock — the same
        // primitives every other Timeline module uses — instead of a raw integer counter. `Resolve`
        // is a batch resolver, so `NextEventAdvance` drives it regardless of the chosen profile's
        // `AdvancePolicy` (see the `profile` parameter's own doc comment for why).
        //
        // B16 (spec-kernel-adoption.md hazard 5, the deliberate fix): status pulses now share this
        // SAME queue as their own event kind, scheduled at each instance's true `NextPulse` tick
        // instead of being folded into the once-per-1000ms round-open call. The round-open call to
        // `Status.Tick` is GONE — round-open now only runs the regenerator trait pulse (step 1's
        // OTHER half); status delivery is fully event-driven. Exactly one event of EACH kind is ever
        // pending at a time, recomputed and rescheduled after it fires — the same "does the queue
        // still hold a scheduled X" pattern B14 already established for rounds, now applied twice.
        var roundQueue = new Timeline.EventQueue(expectedEvents: 4);
        var roundClock = new Timeline.SimulationClock();
        var roundAdvance = new Timeline.NextEventAdvance();
        var roundEventBuffer = new List<Timeline.ScheduledEvent>(2);
        const int RoundEventKind = 0;
        const int StatusPulseEventKind = 1;
        const string RoundEventOwnerKey = "round";
        const string StatusPulseEventOwnerKey = "status-pulse";

        void ScheduleNextStatusPulse()
        {
            if (state.NextStatusPulseAt() is not { } at) return;
            var tick = (long)Math.Round((at - state.T0).TotalMilliseconds);
            roundQueue.Schedule(Math.Max(tick, roundClock.Now), StatusPulseEventOwnerKey, StatusPulseEventKind, 0);
        }

        var rounds = 0;
        if (rounds < BattleRuleset.MaxRounds && state.AnyActive("squad") && state.AnyActive("wave"))
            roundQueue.Schedule(BattleRuleset.RoundDurationMs, RoundEventOwnerKey, RoundEventKind, 0);
        ScheduleNextStatusPulse();   // initial statuses may already carry a due pulse in flight

        while (roundQueue.Count > 0)
        {
            if (!state.AnyActive("squad") || !state.AnyActive("wave")) break;   // decided by an earlier sub-round pulse; a stale scheduled event is never acted on

            roundClock.TryAdvance(roundAdvance, roundQueue);
            roundEventBuffer.Clear();
            roundQueue.PopDue(roundClock.Now, roundEventBuffer);

            foreach (var ev in roundEventBuffer)
            {
                if (!state.AnyActive("squad") || !state.AnyActive("wave")) break;   // decided by an earlier event in THIS same batch
                var now = state.T0.AddMilliseconds((double)roundClock.Now);
                state.Host.Clock.UtcNow = now;

                if (ev.Kind == StatusPulseEventKind)
                {
                    trace?.Phase(rounds, "status-pulse");
                    state.Status.Tick(now, state.PulseSink, board: null, spreadRng: state.StatusRng);
                    state.Host.Flush();
                    state.PostFlush(rounds);
                    if (state.AnyActive("squad") && state.AnyActive("wave"))
                        ScheduleNextStatusPulse();
                    continue;
                }

                // RoundEventKind — steps 1 (regen only — status delivery moved off this call in
                // B16), 2 (initiative + attacks), 3/4 (death cleanup + shield upkeep).
                rounds++;
                state.RunRegeneratorPulses();
                state.Host.Flush();
                trace?.Phase(rounds, "post-flush");
                state.PostFlush(rounds);
                if (!state.AnyActive("squad") || !state.AnyActive("wave"))
                    break;

                // 2) Initiative-ordered attacks: stable order, per-round jitter from the initiative
                //    stream; swift subtracts a full band so it always acts before non-swift kin.
                trace?.Phase(rounds, "initiative");
                var order = state.Actors
                    .Where(a => a.Active)
                    .OrderBy(a =>
                    {
                        // Key selectors run once per element in SOURCE order, so the draw sequence
                        // is "actors list order, filtered to Active" — T5 hazard 1, and note that
                        // CC-locked actors are Active and therefore DO draw (hazard 4).
                        var roll = state.InitiativeRng.NextInt(1000);
                        trace?.Draw("initiative", roll);
                        return roll - (a.Has("swift") ? TraitBattleCatalog.Get("swift").InitiativeBonusMilli : 0);
                    })
                    .ToList();

                foreach (var attacker in order)
                {
                    // action-todo.md T13 (spec-basic-attack-adoption.md §1): the first four steps —
                    // active check, CC-lock, target, calculator.Compute — are the declared action
                    // `act.attack` (BasicAttack.cs). Everything below this call is EngineBehavior
                    // trait tail (E12), extracted to BattleRunState.DispatchHit (B13) but otherwise
                    // unchanged.
                    var step = RunBasicAttackStep(attacker, state.Actors, state.Status, now, state.Calculator, state.CritRng, trace, rounds);
                    if (step.Outcome == AttackStepOutcome.Continue) continue;
                    if (step.Outcome == AttackStepOutcome.Break) break;

                    state.DispatchHit(attacker, step.Target!, step.SignedDelta, rounds);
                }

                // 3) Death cleanup happens inline (Hp gate); 4) shield upkeep AFTER dispatch —
                // an expiring shield still absorbed this round's damage (shield spec order).
                foreach (var a in state.Actors)
                {
                    if (!a.Alive)
                    {
                        state.Status.WithdrawEntity(a.Setup.Key);
                        state.Shields.RemoveAll(Contracts.EffectOwnerKeys.Entity(a.Setup.Key));
                    }
                }

                trace?.Phase(rounds, "shield-upkeep");
                state.Shields.Tick(rounds, BattleRuleset.RoundDurationMs, ownerKey =>
                {
                    var key = ownerKey.StartsWith("entity:", StringComparison.Ordinal)
                        ? ownerKey.Substring("entity:".Length)
                        : ownerKey;
                    return state.ByKey.TryGetValue(key, out var a) ? a.Derived : ActorDerivedSnapshot.AttackerLess();
                });
                state.DrainShieldEvents(rounds);
                if (trace != null)
                    foreach (var a in state.Actors)
                        trace.State(rounds, a.Setup.Key, a.Hp, a.ShieldAbsorbed);

                // Schedule the NEXT round only now — mirrors the original while-header's
                // re-evaluated `rounds < Max && bothActive` exactly, just checked once per round
                // instead of once per loop entry, since `rounds` here already holds the round that
                // just finished.
                if (rounds < BattleRuleset.MaxRounds && state.AnyActive("squad") && state.AnyActive("wave"))
                    roundQueue.Schedule(roundClock.Now + BattleRuleset.RoundDurationMs, RoundEventOwnerKey, RoundEventKind, 0);
            }
        }

        state.DrainShieldEvents(rounds);   // trailing grants/absorbs when the loop exits mid-round

        var outcome = !state.AnyActive("wave") ? BattleOutcome.Victory
            : !state.AnyActive("squad") ? BattleOutcome.Defeat
            : BattleOutcome.Stalemate;

        var greedyDef = TraitBattleCatalog.Get("greedy");
        var geniusDef = TraitBattleCatalog.Get("genius");
        var greedySurvivors = state.Actors.Count(a =>
            a.Setup.Side == "squad" && a.Alive && a.Has("greedy"));

        return new BattleReport
        {
            Seed = seed,
            WaveId = setup.WaveId,
            Outcome = outcome,
            Rounds = rounds,
            SoulLootMilli = 1000 + greedyDef.SoulLootBonusMilli * greedySurvivors,
            Events = state.Events,
            Actors = state.Actors.Select(a => new BattleActorResult(
                a.Setup.Key, a.Setup.Side, a.Setup.SpeciesId, a.Setup.TypeId,
                a.Hp, a.DamageDealt, a.Kills, a.Alive, a.Retreated,
                a.Has("genius") ? 1000 + geniusDef.SpecimenXpBonusMilli : 1000,
                a.ShieldAbsorbed)
            { EquippedActionIds = a.Setup.EquippedActionIds }).ToList()
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
