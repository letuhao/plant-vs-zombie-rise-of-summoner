using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// **Checkpoint 3 — wave-1 acceptance** (world-map-plan.md). Twenty turns that actually play: a
/// legion marches, clears two guards, claims the sector, meets a warband head-on in the middle of a
/// lane, pushes on to the frontier, and finally has its supply line cut by a third faction walking
/// into the sector behind it.
///
/// The scenario is scripted rather than improvised, so its value is in the invariants: one turn-log
/// row per turn, a stable hash sequence, and — the sharp one — the pure engine reproducing the
/// store's hashes from nothing but (seed, template, command log).
/// </summary>
public class WorldWaveOneAcceptanceTests : IDisposable
{
    const int Turns = 20;
    const ulong Seed = 4242;
    static readonly string[] Commanders = { "dave", "wild", "zomboss" };

    /// <summary>The one commander with no policy — the only one the auto-fill leaves alone.</summary>
    const string Human = "dave";

    // Re-blessed twice on 2026-08-22, both deliberately, for world-intel:
    //
    //   1. W19 — claiming a sector no longer rewrites its *authored* intel. That field is template
    //      input seeding the player's opening belief; live intel is per-faction now.
    //   2. W20 — the `Intel` phase landed and RulesetVersion went to 2, so every turn writes each
    //      faction's belief and the same commands produce a larger state than they did before.
    //
    //   3. W22 — every faction now starts believing what it can already see, not just the player,
    //      and a surveyed slot remembers which encounter dens there. Both were found by the
    //      projection's own tests: a warband could not describe the ground under its feet until a
    //      turn had been committed, and `guardState` alone cannot tell "cleared" from "never
    //      guarded".
    //
    //   4. A survey now records a sector's development level and each slot's state. Both were
    //      fields the wire already carried and nothing populated, so they read as a flat zero that
    //      looked exactly like a sector that genuinely had none — a trap set for whoever ships
    //      `sector-development`.
    //
    //   5. W37 — Zomboss's `PolicyId` became `frontier-rules`. `WorldCanonical` writes that field
    //      into the faction row, so the hash moves. **Nothing else did:** the scenario's command log
    //      is byte-identical before and after, verified by dumping it both ways, because
    //      `first-light` gives Zomboss no forces — a brain with nothing to command falls straight
    //      through to standing fast. That the map ships without an opponent army is a content gap,
    //      not an AI one; see WorldAiAcceptanceTests.
    //
    //   6. **A glimpse no longer erases a survey.** Found by playing twenty turns and reading them:
    //      a legion surveyed a sector, stepped one lane away, and forgot what was inside, because
    //      `IntelRecorder` wrote a whole new snapshot at whatever level it currently saw. Worse, the
    //      same bug destroyed the *template's authored intel* on the very first turn — Dave was
    //      supposed to start knowing Ember Hollow's insides and did not, past turn zero. Belief is
    //      hashed state, so keeping what a survey taught moves this.
    //
    //   7. **The map changed.** Verdant Shelf now hangs off Black Gate instead of Ash Waste. Ash
    //      Waste used to touch four of six sectors, so one march to the middle lit the whole map
    //      permanently and the fog was a three-turn opening rather than a condition — found by
    //      playing, not by testing. The map now has two chokepoints instead of one hub, and the far
    //      corner stays dark until somebody goes and looks.
    //
    //      (`Intel` also moved after `Snapshot` at RulesetVersion 3, which did *not* move this
    //      hash: belief converges within a turn, so only the intermediate ones changed.)
    //
    //   8. **loam-model (L2 + L4), 2026-08-23** — `WorldCanonical` gained three loam fields:
    //      `LoamStock`/`FractureIntensityMilli` on the sector row, `UpkeepHandicapMilli` on the
    //      faction row. This is the module's own budgeted golden move (its acceptance criteria call
    //      for exactly one), not a drift: `RulesetVersion` is unchanged, and `first-light`'s G-D
    //      minimum edit (a homeworld rootbed plus a starting stock) is what actually changes the
    //      sector row's content — the other two fields sit at their pre-loam defaults everywhere in
    //      this scenario. **Blessed once, after L4** (persistence), not at L2 alone: `Play(...)`
    //      commits each turn through `RpgStore`, so between L2 and L4 the homeworld's authored
    //      `LoamStock` was silently dropped on the first save (the new columns didn't exist yet),
    //      and a hash captured in that window would have blessed a lossy round trip rather than the
    //      real one. Caught by every store-vs-engine replay-parity test in this project going red
    //      at once — the same shape, not nine separate coincidences.
    //
    //   9. **loam-turn (L12-L15), 2026-08-23** — `Production` and `Pressure` stop being
    //      pass-throughs (`RulesetVersion` 4). Zomboss's warband still has nothing here to command
    //      (this scenario predates the AI stream giving him forces), so nothing about *combat* or
    //      *movement* changed; what moved is that `first-light`'s homeworld now seeps loam each turn,
    //      pays its own upkeep, and every other sector does too. This is the module's own budgeted
    //      golden move (its acceptance criteria call for exactly one) and the loam program's
    //      **second and last** — the first was `loam-model`'s field addition, which left
    //      `RulesetVersion` unchanged and needed no re-bless of its own reasoning here beyond entry
    //      #8 above.
    //
    // The plan expected one re-bless. Four were needed for world-intel, each for a behaviour change
    // rather than a drift, and each recorded here. Protecting the hash in any of them would have
    // meant shipping something known to be wrong.
    const string GoldenFinalHash = "0ac049f4522eeb8eaa98ab7f9271a2f63584c85c65d8ca99ff1dbb62142b0f68";

    readonly string _dir;
    readonly RpgStore _store;

    public WorldWaveOneAcceptanceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-cp3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    /// <summary>
    /// `first-light` plus a mid-campaign roster: Dave's legion is a real force rather than the three
    /// starting conscripts, and Zomboss has a warband on the board. Both are authored here rather
    /// than played into existence — recruitment belongs to a later wave, and the scenario needs a
    /// board where every wave-1 verb can actually fire.
    /// </summary>
    static WorldState Scenario(string worldId)
    {
        var world = WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, Seed, worldId);

        var legion = world.Entities.Single(e => e.EntityId == "e-dave-legion-1") with
        {
            Members = Enumerable.Range(0, 5)
                .Select(_ => new WorldEntityMember { SpeciesId = "peashooterzombie", Level = 3, Hp = 260 })
                .ToList()
        };

        // Replaced rather than appended since 2026-08-22: the template ships this band now, and
        // appending a second one with the same id fails validation. The scenario still wants a
        // heavier version of it than the map gives a new player.
        var zomboss = new WorldEntity
        {
            EntityId = "e-zomboss-band-1",
            Kind = WorldEntityKind.Warband,
            OwnerFactionId = "zomboss",
            AtSectorId = "black-gate",
            Stance = "march",
            MovementRemaining = 1000,
            Members = Enumerable.Range(0, 4)
                .Select(_ => new WorldEntityMember { SpeciesId = "normalzombie", Level = 4, Hp = 300 })
                .ToList()
        };

        return WorldValidation.Validate(world with
        {
            Entities = world.Entities
                .Select(e => e.EntityId == legion.EntityId ? legion : e)
                .Select(e => e.EntityId == zomboss.EntityId ? zomboss : e)
                .OrderBy(e => e.EntityId, StringComparer.Ordinal)
                .ToList()
        });
    }

    static WorldCommand Cmd(string commander, int turn, string kind) => new()
    {
        CommanderId = commander,
        CommandId = $"t{turn}-{commander}",
        Kind = kind
    };

    /// <summary>The script. Anything a commander is not told to do this turn, it stands fast.</summary>
    static IReadOnlyList<WorldCommand> ScriptFor(int turn)
    {
        var dave = Cmd("dave", turn, WorldCommandKinds.StandFast);
        var wild = Cmd("wild", turn, WorldCommandKinds.StandFast);
        var zomboss = Cmd("zomboss", turn, WorldCommandKinds.StandFast);

        dave = turn switch
        {
            0 => dave with { Kind = WorldCommandKinds.Move, EntityId = "e-dave-legion-1", LanePath = new[] { "l-home-ember" } },
            1 => dave with { Kind = WorldCommandKinds.Clear, EntityId = "e-dave-legion-1", SectorId = "ember-hollow", SlotIndex = 2 },
            2 => dave with { Kind = WorldCommandKinds.Clear, EntityId = "e-dave-legion-1", SectorId = "ember-hollow", SlotIndex = 3 },
            3 => dave with { Kind = WorldCommandKinds.Claim, EntityId = "e-dave-legion-1", SectorId = "ember-hollow" },
            // Two turns on the ley lane: the first ends in the crossing, the second finishes it.
            4 or 5 => dave with { Kind = WorldCommandKinds.Move, EntityId = "e-dave-legion-1", LanePath = new[] { "l-ember-ash" } },
            9 => dave with { Kind = WorldCommandKinds.Clear, EntityId = "e-dave-legion-1", SectorId = "ash-waste", SlotIndex = 2 },
            10 => dave with { Kind = WorldCommandKinds.Claim, EntityId = "e-dave-legion-1", SectorId = "ash-waste" },
            _ => dave
        };

        // The wild pack is dug in where the template left it, so it has to break camp before it can
        // march — which costs it the turn it gives the order (world-movement §What `hold` is for).
        if (turn == 3)
            wild = wild with { Kind = WorldCommandKinds.Stance, EntityId = "e-wild-pack-1", Stance = "march" };

        // Then it marches out to meet the legion head-on in the middle of the ley lane.
        if (turn == 4)
            wild = wild with { Kind = WorldCommandKinds.Move, EntityId = "e-wild-pack-1", LanePath = new[] { "l-ember-ash" } };

        // Zomboss comes up the rift behind them and squats on the frontier sector.
        if (turn is 11 or 12)
            zomboss = zomboss with { Kind = WorldCommandKinds.Move, EntityId = "e-zomboss-band-1", LanePath = new[] { "l-ash-black" } };

        return new[] { dave, wild, zomboss };
    }

    List<string> Play(string worldId)
    {
        var (ok, reason, _) = _store.CreateWorld(1, Scenario(worldId));
        Assert.True(ok, reason);

        var hashes = new List<string>();
        for (var turn = 0; turn < Turns; turn++)
        {
            _store.SubmitWorldCommands(worldId, ScriptFor(turn));

            // Every commander ends the *same* turn. The scripted factions commit **first**: an
            // explicit commit speaks for a faction and keeps `ai-commander`'s auto-fill out of it,
            // which is what lets a scenario script what the wild and Zomboss do. The human commits
            // last, and that is the one that releases the barrier.
            var open = _store.GetWorldHeader(worldId)!.CurrentTurn;
            WorldTurnCommitResult last = default!;
            foreach (var commander in Commanders.Where(c => c != Human).Concat(new[] { Human }))
                last = _store.CommitWorldTurn(worldId, commander, open);

            Assert.True(last.Advanced, $"turn {turn} did not advance");
            hashes.Add(last.StateHash!);
        }

        return hashes;
    }

    IEnumerable<TurnReportEntry> AllEntries(string worldId) =>
        Enumerable.Range(0, Turns).SelectMany(t => _store.GetWorldTurnReport(worldId, t)?.Entries ?? Array.Empty<TurnReportEntry>());

    [Fact]
    public void Every_wave_one_verb_fires_somewhere_in_the_twenty_turns()
    {
        Play("cp3-verbs");
        var entries = AllEntries("cp3-verbs").ToList();

        var verbs = new (string Name, Func<TurnReportEntry, bool> Fired)[]
        {
            ("march", e => e.Kind == TurnReportKinds.Event && e.Detail.StartsWith("arrival:")),
            ("clear", e => e.Kind == TurnReportKinds.Battle && e.Detail.StartsWith("guard:")),
            ("crossing", e => e.Kind == TurnReportKinds.Battle && e.Detail.StartsWith("lane:")),
            ("claim", e => e.Kind == TurnReportKinds.Event && e.Detail.StartsWith("claim.held:")),
            ("zone-of-control", e => e.Kind == TurnReportKinds.Event && e.Detail.StartsWith("halt:zoc:")),
            ("supply cut", e => e.Kind == TurnReportKinds.Event && e.Detail.StartsWith("supply.cut:")),
            ("attrition", e => e.Kind == TurnReportKinds.Event && e.Detail.StartsWith("attrition:"))
        };

        var missing = verbs.Where(v => !entries.Any(v.Fired)).Select(v => v.Name).ToList();
        Assert.True(missing.Count == 0, "never fired: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_scenario_leaves_one_turn_log_row_per_turn_and_nothing_beyond()
    {
        Play("cp3-log");

        Assert.Equal(Turns, _store.GetActiveWorld(1)!.CurrentTurn);
        for (var turn = 0; turn < Turns; turn++)
            Assert.NotNull(_store.GetWorldTurnLog("cp3-log", turn));
        Assert.Null(_store.GetWorldTurnLog("cp3-log", Turns));
    }

    [Fact]
    public void The_same_script_and_seed_replay_to_the_same_twenty_hashes()
    {
        Assert.Equal(Play("cp3-a"), Play("cp3-b"));
    }

    [Fact]
    public void The_pure_engine_reproduces_the_stored_hashes_from_the_command_log_alone()
    {
        var stored = Play("cp3-replay");

        var world = Scenario("cp3-replay");
        var replayed = new List<string>();
        for (var turn = 0; turn < Turns; turn++)
        {
            var result = TurnEngine.Step(world, _store.ListWorldCommands("cp3-replay", turn), Seed);
            world = result.World;
            replayed.Add(result.StateHash);
        }

        Assert.Equal(stored, replayed);
    }

    /// <summary>
    /// The golden. It has no meaning on its own — it is a tripwire: if the ruleset changes without
    /// anyone deciding to change it, this is what notices. Re-bless it deliberately, with the reason
    /// in the commit message, never by pasting whatever the run produced.
    /// </summary>
    [Fact]
    public void The_scenario_hashes_to_its_golden()
    {
        Assert.Equal(GoldenFinalHash, Play("cp3-golden").Last());
    }

    [Fact]
    public void The_campaign_ends_with_Dave_holding_what_he_took()
    {
        Play("cp3-end");
        var world = _store.LoadWorldState("cp3-end")!;

        var ember = world.Sectors.Single(s => s.SectorId == "ember-hollow");
        Assert.Equal("dave", ember.OwnerFactionId);
        Assert.Equal(SectorPhase.Held, ember.Phase);
        Assert.All(ember.Slots, sl => Assert.Equal(GuardState.Cleared, sl.GuardState));
    }
}
