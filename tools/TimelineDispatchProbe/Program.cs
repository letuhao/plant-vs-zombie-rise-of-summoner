// battle-tempo timeline-dispatch (TD1, D14's fix), executed standalone (Core.Tests blocked by
// pre-existing, unrelated WIP in other streams -- see PoiseProbe's header). Proves the three
// additive, zero-blast-radius pieces spec-timeline-dispatch.md §2.1/§2.3 describe are correct:
// the capability flag defaults false everywhere shipped, and ActionRunner.CurrentTarget reflects
// commitment-binding re-selection.

using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Shield;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;

var failures = 0;
void Check(string name, bool condition)
{
    if (condition) { Console.WriteLine($"PASS  {name}"); return; }
    Console.WriteLine($"FAIL  {name}");
    failures++;
}

var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "tuning", "derived-stats.v2.json")))
    dir = dir.Parent;
if (dir == null) throw new InvalidOperationException("could not locate data/tuning by walking up from " + AppContext.BaseDirectory);
string Load(string rel) => File.ReadAllText(Path.Combine(dir.FullName, "data", "tuning", rel));

DerivedStatPolicy.Configure(DerivedStatTuningLoader.Parse(Load("derived-stats.v2.json")));
FusionRpg.Core.Power.PowerTuningHub.Configure(FusionRpg.Core.Power.PowerTuningLoader.Parse(Load("power-scale.v2.json")));
StatsTuningHub.Configure(StatsTuningLoader.Parse(Load("stats.v1.json")));
ShieldPolicy.Configure(ShieldTuningLoader.Parse(Load("shield.v1.json")));
CombatPolicy.Configure(CombatTuningLoader.Parse(Load("combat.v1.json")));
StatusPolicy.Configure(StatusTuningLoader.Parse(Load("status.v1.json")));
BattleTuningHub.Configure(BattleTuningLoader.Parse(Load("battle.v4.json")));
// battle-tempo battle-resources (2026-09-05): the pool-share projection BattleStatComposer's own
// resource seeding reads. Its own file rather than a battle.v{n}.json section because publish.py's
// `set` path refuses to invent keys (spec-battle-resources.md S2.2a) -- and its own Configure, which
// every host must remember, exactly like ActionTimingPolicy already needs.
BattleRuleset.ConfigureResources(BattleResourceTuningLoader.Parse(Load("battle-resources.v1.json")));
FusionRpg.Core.Actions.ActionTimingPolicy.Configure(FusionRpg.Core.Actions.ActionTimingTuningLoader.Parse(Load("action-timing.v1.json")));
ReactionLanePolicy.Configure(ReactionLaneTuningLoader.Parse(Load("reaction-lane.v1.json")));

// ---- §2.1 UPDATED 2026-09-05: the flag LANDED on two profiles, and the invariant inverted ----
//
// These three used to assert "no catalog row sets the flag" -- correct while dispatch was unlanded,
// and deliberately retired by the owner-approved landing (decisions.md "Battle timeline dispatch --
// landed on two profiles"). The replacement is a STRONGER guard, because the thing that now needs
// protecting is the EXCLUSION rather than the absence.
Check("BareRecordDefaultsFalse", new BattleModeProfile().UsesTimelineDispatch == false);
Check("GalaxySyncDispatchesAfterTheLanding", BattleModeProfileCatalog.GalaxySync.UsesTimelineDispatch);
Check("HybridAtbDispatchesAfterTheLanding", BattleModeProfileCatalog.HybridAtb.UsesTimelineDispatch);

// ✅ `classic-round` LANDED in a second pass, once the W=1 starvation defect it exposed was fixed.
// The exclusion was correct while the bug existed: dispatch starved actors at this profile's width,
// turning a `Victory` into a `Stalemate`. `TimelineDispatch.TryCommitReady` now checks for a free slot
// BEFORE spending the actor's economy, and this profile measures 89.58% under dispatch -- exactly its
// atomic win rate, every outcome golden green, only the internal trace differing.
Check("ClassicRoundDispatchesAfterTheStarvationFix", BattleModeProfileCatalog.ClassicRound.UsesTimelineDispatch);
// Siege is not exercised here: this environment's battle.v4.json carries no 'siege' tuning row
// (unrelated to this probe -- MeasProbe/CommitmentProbe never call BattleModeProfileCatalog.Siege
// either), and it was explicitly out of scope for the landing.

// The field is real data, not hardcoded -- provable in BOTH directions now that every shipped row
// sets it. Opting OUT is the meaningful direction post-landing (it is what every falsifier below uses
// to build its flag-off control), and it must not disturb the cached catalog row.
var synthetic = BattleModeProfileCatalog.ClassicRound with { UsesTimelineDispatch = false };
Check("ASyntheticProfileCanOptOut", synthetic.UsesTimelineDispatch == false);
Check("OptingOutDoesNotMutateTheCachedCatalogRow", BattleModeProfileCatalog.ClassicRound.UsesTimelineDispatch);

// ---- §2.3: ActionRunner.CurrentTarget reflects commitment-binding re-selection ----
{
    var queue = new EventQueue(64);
    var clock = new SimulationClock();
    var slots = new ActionSlots(4, WScope.Global);
    var cooldowns = new CooldownLedger();
    var actors = new Dictionary<string, ActorTurnMachine>();
    var dead = new HashSet<string>();
    var advance = new NextEventAdvance();
    var buffer = new List<ScheduledEvent>(32);

    var runner = new ActionRunner(queue, slots, cooldowns, key => !dead.Contains(key),
        Commitment.LateBound, (actorKey, deadTarget) => "fallback");

    ActorTurnMachine Add(string key) { var m = new ActorTurnMachine(key); actors[key] = m; return m; }
    Add("a"); Add("victim"); Add("fallback");

    var a = actors["a"];
    a.TransitionTo(TurnState.Ready);
    var envelope = new ActionEnvelope
    {
        ActionId = "strike", WindupTicks = 100, RecoveryTicks = 50,
        ResolveOffsets = new long[] { 0 },
    };
    runner.TryCommit(a, "left", new ActionIntent("strike", "victim", envelope), clock.Now);

    // Before the target dies: CurrentTarget reflects the committed intent.
    Check("CurrentTargetReflectsTheCommittedIntentBeforeResolve", runner.CurrentTarget("a") == "victim");

    dead.Add("victim");
    string? targetAtResolve = null;
    for (var guard = 0; guard < 10_000; guard++)
    {
        var due = queue.PeekDueTick();
        if (due is null) break;
        clock.TryAdvance(advance, queue);
        buffer.Clear();
        queue.PopDue(clock.Now, buffer);
        foreach (var e in buffer)
        {
            var actor = actors[e.OwnerKey];
            if ((TimelineEventKind)e.Kind == TimelineEventKind.Resolve)
            {
                // The exact sequence a real caller (BattleEngine.Resolve's timeline-dispatch branch)
                // must follow: read CurrentTarget right after OnResolveDue returns Resolved, BEFORE
                // Recovery fires and clears the run -- Recovery is what makes CurrentTarget go null
                // (proven by the falsifier below), so reading it any later would be reading a run
                // that has already ended.
                if (runner.OnResolveDue(actor, e) == ActionOutcome.Resolved)
                    targetAtResolve = runner.CurrentTarget(e.OwnerKey);
            }
            else if ((TimelineEventKind)e.Kind == TimelineEventKind.Recovery) runner.OnRecoveryDue(actor, e);
        }
    }

    // After LateBound re-selection: CurrentTarget reflects the NEW target, not the dead one --
    // the exact fact ApplyBasicAttack would need to know who to actually hit.
    Check("CurrentTargetReflectsReselectionAfterTheOriginalTargetDied", targetAtResolve == "fallback");

    // Falsifier: an actor holding no active run (never committed, or already finished) reports null,
    // not a stale value -- confirms the accessor reads `run.Active`, not just `run.TargetKey`.
    Check("AnActorWithNoActiveRunReportsNull", runner.CurrentTarget("victim") is null);
    Check("TheSameActorAfterItsOwnActionFinishedAlsoReportsNull", runner.CurrentTarget("a") is null);
}

// ---- §2.5: the dispatch branch, end to end through the REAL BattleEngine.Resolve ----
// Mirrors MeasProbe's own Actor()/CloseSetup() exactly (mirrors BattleGoldenTests.cs), and reuses its
// staged-sweep methodology: measure a win rate, change one axis, measure again, report the delta.
// UsesTimelineDispatch is TRUE only on these synthetic profiles -- never on a BattleModeProfileCatalog
// row, so nothing here is reachable by any shipped battle/expedition/E2E test.
{
    BattleActorSetup Actor(string key, string side, int level, ElementTypeId? elem = null, params string[] traits) =>
        ActorWithHp(key, side, level, null, elem, traits);

    BattleActorSetup ActorWithHp(string key, string side, int level, long? hpOverride, ElementTypeId? elem = null, string[]? traits = null) => new()
    {
        Key = key, Side = side, SpeciesId = "golden-species", TypeId = 10_001, Level = level,
        ElementPrimary = elem, TraitIds = traits ?? Array.Empty<string>(),
        MaxHp = hpOverride ?? BattleRuleset.BaseHp(level), Atk = BattleRuleset.BaseAtk(level), Defense = BattleRuleset.BaseDefense(level),
    };

    BattleSetup CloseSetup() => new()
    {
        WaveId = "golden-close",
        Squad = new[]
        {
            Actor("squad:0", "squad", 5, ElementTypeId.Air, "regenerator"),
            Actor("squad:1", "squad", 5, ElementTypeId.Earth, "guardian", "loyal"),
        },
        Wave = new[]
        {
            Actor("wave:0", "wave", 5, ElementTypeId.Dark, "bloodthirsty"),
            Actor("wave:1", "wave", 5, ElementTypeId.Fire, "soul-eater"),
        },
    };

    // A setup deliberately shaped to make a mid-wind-up death LIKELY, not just possible -- iterated
    // empirically (a standalone traced single-battle run, not guesswork) until the exact race actually
    // fires: base HP (~106 at level 1) is far larger than one attacker's per-hit damage (~20 net), so
    // TWO concurrent attackers were never enough -- confirmed by measurement (successive drafts with
    // 2-on-1 and 6-on-1 both measured an honest 0.00% delta, because a target that dies from combined
    // fire always died either on the FIRST resolve in a batch (nothing else left in flight to observe
    // it) or only after accumulating damage across several ROUNDS, where every round's fresh
    // `DeclareBasicAttack` re-targets regardless of Commitment). THREE concurrent attackers on a
    // target with HP tuned between one and two hits' worth (hpOverride: 20) is what actually produces
    // "hit 1 doesn't kill, hit 2 kills, hit 3 observes a dead target" inside one batch -- verified via
    // a traced run showing `target=wave:0 targetActive=False` on the third resolve.
    BattleSetup CommitmentProbeSetup() => new()
    {
        WaveId = "golden-commitment",
        Squad = new[]
        {
            Actor("squad:0", "squad", 4, ElementTypeId.Air),
            Actor("squad:1", "squad", 4, ElementTypeId.Earth),
            Actor("squad:2", "squad", 4, ElementTypeId.Fire),
        },
        // wave:0 dies from two of the three squad hits landing in the same tick-150 batch, leaving the
        // third to observe a dead target; wave:1 is tanky enough to keep the fight going long enough
        // for that race to recur many times over a battle -- LateBound turns each such "wasted" swing
        // into a real extra hit against wave:1 instead of a fizzle.
        Wave = new[]
        {
            ActorWithHp("wave:0", "wave", 2, hpOverride: 20, elem: ElementTypeId.Air),
            Actor("wave:1", "wave", 7, ElementTypeId.Fire),
        },
    };

    const int Seeds = 240;
    double WinRate(BattleModeProfile profile, BattleSetup setup)
    {
        var wins = 0;
        for (var i = 0; i < Seeds; i++)
        {
            var report = BattleEngine.Resolve(setup, (ulong)(9_000 + i), profile: profile);
            if (report.Outcome == BattleOutcome.Victory) wins++;
        }
        return (double)wins / Seeds;
    }

    var setup = CloseSetup();
    var baseline = BattleModeProfileCatalog.ClassicRound with { UsesTimelineDispatch = true, W = 1 };

    // Smoke test first: does it even run to completion without throwing (the MaxLocalIterations
    // guard, in particular, must never trip for this ordinary a scenario)?
    Exception? thrown = null;
    try { WinRate(baseline, setup); } catch (Exception ex) { thrown = ex; }
    Check("TheDispatchBranchRunsToCompletionWithoutThrowing", thrown is null);
    if (thrown is not null) Console.WriteLine($"      (threw: {thrown})");

    // ⛔ RETRACTED 2026-09-05 — this probe's original claim was measuring a BUG, not the `W` axis.
    //
    // It used to assert `rNarrow != rWide` and reported "+12.92pp, the first non-zero measurement of
    // `W` in this program's history" (`TD3`, and `battle-tempo-todo.md` Checkpoint B/F both cite it).
    // That delta was **entirely an artifact of the W=1 starvation defect** fixed in
    // `TimelineDispatch.TryCommitReady` the same day: at `W = 1` the losing contender spent its whole
    // turn's economy on a doomed commit and could never retry, so the narrow profile was not
    // "concurrency-limited", it was **broken**. Once actors stop starving, W=1 and W=4 produce the
    // SAME win rate (89.58% both) on this fixture.
    //
    // ⭐ The honest position: `W` joins `AdvancePolicy` as **structurally unmeasurable on this
    // fixture** rather than measured-non-zero. `Commitment` (−0.725 rounds) is unaffected and remains
    // a real, independently falsified result. Asserting equality here rather than deleting the probe,
    // so the retraction is enforced and a future session cannot quietly re-inherit the old claim.
    var wide = baseline with { W = 4 };
    var rNarrow = WinRate(baseline, setup);
    var rWide = WinRate(wide, setup);
    Console.WriteLine($"      W=1 winRate={rNarrow:P2}  W=4 winRate={rWide:P2}  delta={rWide - rNarrow:+0.00%;-0.00%}");
    Check("WMeasuresZeroOnceStarvationIsFixed_theOldNonZeroWasTheBug", rNarrow == rWide);

    // Commitment: EarlyBound (locked, fizzles on a dead target) vs LateBound (re-targets), on a setup
    // shaped to make a mid-wind-up death likely (CommitmentProbeSetup, not the general CloseSetup
    // above) -- the second axis Checkpoint B names as unmet.
    // Win rate is the wrong metric here: six attackers against one fragile defender wins regardless
    // of Commitment. What Commitment changes is HOW MANY ROUNDS it takes -- EarlyBound wastes a swing
    // (fizzle) whenever a concurrently-committed attacker's target already died from an earlier hit
    // in the same batch; LateBound turns that same swing into extra, real damage against the wave's
    // survivor instead, finishing faster on average.
    double AvgRounds(BattleModeProfile profile, BattleSetup setup)
    {
        long total = 0;
        for (var i = 0; i < Seeds; i++) total += BattleEngine.Resolve(setup, (ulong)(9_000 + i), profile: profile).Rounds;
        return (double)total / Seeds;
    }

    var commitmentSetup = CommitmentProbeSetup();
    // W=6: wide enough for all three squad attackers plus wave's own two counter-attacks to commit
    // in the same tick -- narrower than this measured the honest 0.00% deltas noted above.
    var commitmentBase = baseline with { W = 6 };
    var earlyBound = commitmentBase with { DefaultCommitment = Commitment.EarlyBound };
    var lateBound = commitmentBase with { DefaultCommitment = Commitment.LateBound };
    var roundsEarly = AvgRounds(earlyBound, commitmentSetup);
    var roundsLate = AvgRounds(lateBound, commitmentSetup);
    var winEarly = WinRate(earlyBound, commitmentSetup);
    var winLate = WinRate(lateBound, commitmentSetup);
    Console.WriteLine($"      EarlyBound avgRounds={roundsEarly:F3} winRate={winEarly:P2}  LateBound avgRounds={roundsLate:F3} winRate={winLate:P2}");
    Console.WriteLine($"      deltaRounds={roundsLate - roundsEarly:+0.000;-0.000}  deltaWinRate={winLate - winEarly:+0.00%;-0.00%}");
    Check("CommitmentIsMeasurablyNonZeroUnderTimelineDispatch", roundsEarly != roundsLate || winEarly != winLate);

    // Falsifier for both headline checks: with the SAME axis changes applied to a flag=false profile
    // (today's atomic path), both deltas must be exactly zero -- proving the non-zero deltas above
    // come from timeline-dispatch actually mattering, not from some other unrelated effect of the
    // `with` expressions themselves.
    var atomicNarrow = baseline with { UsesTimelineDispatch = false };
    var atomicWide = wide with { UsesTimelineDispatch = false };
    var rAtomicNarrow = WinRate(atomicNarrow, setup);
    var rAtomicWide = WinRate(atomicWide, setup);
    Check("FalsifierWDeltaIsZeroWhenTheFlagIsOff", rAtomicNarrow == rAtomicWide);
    var atomicEarly = earlyBound with { UsesTimelineDispatch = false };
    var atomicLate = lateBound with { UsesTimelineDispatch = false };
    Check("FalsifierCommitmentDeltaIsZeroWhenTheFlagIsOff",
        AvgRounds(atomicEarly, commitmentSetup) == AvgRounds(atomicLate, commitmentSetup) &&
        WinRate(atomicEarly, commitmentSetup) == WinRate(atomicLate, commitmentSetup));

    // ---- RL2: the reaction lane, wired live for the first time ----
    // WReact=0 (every shipped profile) already keeps this inert -- proven structurally by ReactionLane
    // itself (`_slots is null`) and empirically above (every W/Commitment measurement already ran with
    // WReact=0 inherited from ClassicRound and reproduced MeasProbe's exact baseline). This section
    // proves the MECHANISM fires when WReact > 0, on a synthetic profile only.
    var reactingProfile = baseline with { W = 1, WReact = 1 };
    var noLaneProfile = baseline with { W = 1, WReact = 0 };
    var reactingSetup = CloseSetup();

    // `HpRemaining` sums directly comparable across runs of the SAME setup/seeds (MaxHp per actor is
    // fixed by the setup, so a lower sum means strictly more damage landed somewhere on that side) --
    // squad's own counter damages the ATTACKER (wave, in this direction), so a lower wave HpRemaining
    // sum under WReact=1 than under WReact=0 is exactly the counter's own extra damage landing.
    long SideHpRemaining(BattleModeProfile profile, BattleSetup setup, string side)
    {
        long total = 0;
        for (var i = 0; i < Seeds; i++)
        {
            var report = BattleEngine.Resolve(setup, (ulong)(9_000 + i), profile: profile);
            foreach (var a in report.Actors)
                if (a.Side == side) total += a.HpRemaining;
        }
        return total;
    }

    var waveHpNoLane = SideHpRemaining(noLaneProfile, reactingSetup, "wave");
    var waveHpReacting = SideHpRemaining(reactingProfile, reactingSetup, "wave");
    Console.WriteLine($"      wave HpRemaining: WReact=0 -> {waveHpNoLane}, WReact=1 (shipped-shape spend) -> {waveHpReacting}");

    // ⭐ CLOSED 2026-09-05 by `battle-resources` (`BR3`) — this probe used to assert the OPPOSITE.
    //
    // The gap it documented was real: NOTHING in BattleStatComposer.cs/BattleEffects.cs/
    // BattleDerivedModifierLedger.cs ever set a `resource.max.*`/`resource.regen.*` channel for ANY of
    // the six ids, so `ActorResourcePools.CreateFull` built a 0-capacity pool and `PoiseLedger.TryCommit`
    // correctly, honestly declined every counter at any spend > 0. The probe asserted that decline
    // (`waveHpReacting == waveHpNoLane`) and deliberately refused to "fix" it by inventing a channel
    // value, on the grounds that sizing a demon's poise was a balance decision out of RL2's scope.
    //
    // `BattleStatComposer` now seeds all six pools from a per-mille share of the shipped HP ladder
    // (spec-battle-resources.md §2.2), so the input exists and the counter FIRES. The assertion
    // inverts: a reacting lane must now take MORE wave HP than a closed one, because a counter that
    // commits poise deals `Riposte` damage back.
    //
    // ⛔ This is `RL2`'s own acceptance line — "a counter reduces the reactor's poise and its damage
    // tracks the spend" — observed true for the first time in this program's history.
    //
    // ⚠️ Asserts INEQUALITY, not a direction, and the reason is a correction worth keeping: an earlier
    // version of this line asserted `waveHpReacting < waveHpNoLane` on the intuition that a firing
    // counter means "the wave takes more damage". That intuition is wrong, and the starvation fix made
    // it obvious by flipping the sign (10346→8855 before, 2818→3358 after). The counter damages the
    // ATTACKING side, so a wave that successfully counters kills its attackers sooner and therefore
    // SURVIVES BETTER — more wave HP remaining, not less. Direction here is an emergent property of
    // the fixture, not a contract; what the lane guarantees is that it changes the outcome at all.
    Check("AReactionNowActuallyFiresBecauseBattleActorsCarryRealPoise", waveHpReacting != waveHpNoLane);

    // Falsifier, and the one that keeps the assertion above honest: an UNAFFORDABLE spend must go back
    // to declining exactly as before. If the new assertion passed for any reason other than a real
    // affordable counter, this would move too -- it must not.
    ReactionLanePolicy.Configure(new ReactionLaneTuning(1, 1, PoiseSpend: 1_000_000_000, RiposteShareCapMilli: 500));
    var waveHpUnaffordable = SideHpRemaining(reactingProfile, reactingSetup, "wave");
    Check("AnEvenLargerUnaffordableSpendStillDeclines", waveHpUnaffordable == waveHpNoLane);
    ReactionLanePolicy.Configure(ReactionLaneTuningLoader.Parse(Load("reaction-lane.v1.json"))); // restore

    // Falsifier: WReact=0 on the ATOMIC path (flag off entirely) is unaffected either way -- the whole
    // reaction mechanism is unreachable outside RunTimelineActionPhase.
    var atomicReacting = reactingProfile with { UsesTimelineDispatch = false };
    var atomicNoLane = noLaneProfile with { UsesTimelineDispatch = false };
    Check("FalsifierReactionLaneDeltaIsZeroWhenTheFlagIsOff",
        SideHpRemaining(atomicReacting, reactingSetup, "wave") == SideHpRemaining(atomicNoLane, reactingSetup, "wave"));
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PROBES PASSED" : $"{failures} PROBE(S) FAILED");
Environment.Exit(failures == 0 ? 0 : 1);
