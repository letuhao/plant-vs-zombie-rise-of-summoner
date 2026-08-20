using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// Pure deterministic battle resolver (spec-match-source-core.md). No I/O, no clock, no ambient
/// state: same setup + seed ⇒ byte-identical report. Round order is locked (RulesetVersion):
/// status ticks → initiative-ordered attacks → death cleanup → round end. Integer-only state;
/// per-system RNG streams (`initiative`, `damage`); element matchup via the shipped ring matrix.
/// </summary>
public static class BattleEngine
{
    sealed class ActorState
    {
        public ActorState(BattleActorSetup setup)
        {
            Setup = setup;
            Hp = setup.MaxHp;
        }

        public BattleActorSetup Setup { get; }
        public int Hp;
        public int DamageDealt;
        public int Kills;
        public bool Alive => Hp > 0;
    }

    public static BattleReport Resolve(BattleSetup setup, ulong seed)
    {
        if (setup.Squad.Count == 0) throw new ArgumentException("Squad is empty.");
        if (setup.Wave.Count == 0) throw new ArgumentException("Wave is empty.");

        var initiativeRng = SeededRng.DeriveStream(seed, "initiative");
        var damageRng = SeededRng.DeriveStream(seed, "damage");

        // Stable ordered state — never dictionary-enumerated (determinism discipline).
        var actors = setup.Squad.Concat(setup.Wave)
            .Select(a => new ActorState(a))
            .ToList();
        var events = new List<BattleEventRec>();
        foreach (var a in actors)
            events.Add(new BattleEventRec(0, BattleEventKinds.Spawn, a.Setup.Key, a.Setup.TypeId, a.Setup.Side));

        var rounds = 0;
        while (rounds < BattleRuleset.MaxRounds && AnyAlive(actors, "squad") && AnyAlive(actors, "wave"))
        {
            rounds++;

            // 1) Status ticks — C2 wires StatusRuntime pulses here at t = rounds * RoundDurationMs.

            // 2) Initiative-ordered attacks: stable order, per-round jitter from the initiative stream.
            var order = actors
                .Where(a => a.Alive)
                .OrderBy(_ => initiativeRng.NextInt(1000))
                .ToList();

            foreach (var attacker in order)
            {
                if (!attacker.Alive) continue;
                var target = FirstLivingOpponent(actors, attacker.Setup.Side);
                if (target is null) break;

                var damage = RollDamage(attacker, target, damageRng);
                target.Hp = Math.Max(0, target.Hp - damage);
                attacker.DamageDealt += damage;
                if (!target.Alive)
                {
                    attacker.Kills++;
                    events.Add(new BattleEventRec(rounds, BattleEventKinds.Die, target.Setup.Key, target.Setup.TypeId, target.Setup.Side));
                }
            }

            // 3) Death cleanup happens inline (Hp gate); 4) round end.
        }

        var outcome = !AnyAlive(actors, "wave") ? BattleOutcome.Victory
            : !AnyAlive(actors, "squad") ? BattleOutcome.Defeat
            : BattleOutcome.Stalemate;

        return new BattleReport
        {
            Seed = seed,
            WaveId = setup.WaveId,
            Outcome = outcome,
            Rounds = rounds,
            Events = events,
            Actors = actors.Select(a => new BattleActorResult(
                a.Setup.Key, a.Setup.Side, a.Setup.SpeciesId, a.Setup.TypeId,
                a.Hp, a.DamageDealt, a.Kills, a.Alive)).ToList()
        };
    }

    static bool AnyAlive(List<ActorState> actors, string side) =>
        actors.Any(a => a.Alive && a.Setup.Side == side);

    static ActorState? FirstLivingOpponent(List<ActorState> actors, string attackerSide)
    {
        foreach (var a in actors)
            if (a.Alive && a.Setup.Side != attackerSide)
                return a;
        return null;
    }

    /// <summary>
    /// Integer damage: (atk − defense) floor 1, ±20% roll, plus the element matchup share from
    /// the shipped ring matrix (additive base share, dual-type product rule — element-hub-ssot).
    /// C2 replaces the flat atk/def read with ActorHub-composed combat channels.
    /// </summary>
    static int RollDamage(ActorState attacker, ActorState target, SeededRng damageRng)
    {
        var baseDamage = Math.Max(1, attacker.Setup.Atk - target.Setup.Defense);
        var rolled = baseDamage * (800 + damageRng.NextInt(401)) / 1000; // 80%..120%, integer math

        if (attacker.Setup.ElementPrimary is { } atkElem)
        {
            // Dual-type product rule in per-mille integers (element-hub-ssot §matrix; no floats).
            var combinedMilli = 1000;
            foreach (var defElem in DefenderElements(target.Setup))
                combinedMilli = combinedMilli * (1000 + ShareMilli(ElementRingMatrix.GetRelation(atkElem, defElem))) / 1000;
            rolled += baseDamage * (combinedMilli - 1000) / 1000;
        }

        return Math.Max(1, rolled);
    }

    /// <summary>Integer mirror of ElementMatchupPolicy.MatchupShareK (0.25) — asserted equal in tests.</summary>
    public static int ShareMilli(ElementMatchupRelation relation) => relation switch
    {
        ElementMatchupRelation.Strong => 250,
        ElementMatchupRelation.Weak => -250,
        _ => 0
    };

    static IEnumerable<ElementTypeId> DefenderElements(BattleActorSetup defender)
    {
        if (defender.ElementPrimary is { } p) yield return p;
        if (defender.ElementSecondary is { } s) yield return s;
    }
}
