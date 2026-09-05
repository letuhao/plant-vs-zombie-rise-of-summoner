namespace FusionRpg.Core.World.Turn;

/// <summary>
/// The wave-1 stand-in for combat. It compares weight, applies the loss, and goes home.
///
/// It is deliberately crude: the world module needs *an* answer so it can exercise who holds the
/// ground, and any balance written here would be thrown away the moment the real combat seam lands
/// at `combat-handoff`. What it does owe the world is determinism and symmetry — the same request
/// must give the same answer, and it must not matter which side the caller listed first.
///
/// Everything is integer per-mille and there is no RNG at all in wave 1; the seed is accepted so
/// the interface does not change when a real resolver starts rolling.
/// </summary>
public sealed class PlaceholderBattleResolver : IBattleResolver
{
    public static readonly IBattleResolver Instance = new PlaceholderBattleResolver();

    /// <summary>Standing on the ground when someone arrives is worth a quarter again.</summary>
    public static int DefenderBonusMilli => World.WorldTuningHub.Tuning.PlaceholderBattle.DefenderBonusMilli;

    /// <summary>A losing side this much lighter than the winner is wiped out rather than routed.</summary>
    public static int WipeoutRatioMilli => World.WorldTuningHub.Tuning.PlaceholderBattle.WipeoutRatioMilli;

    /// <summary>What a rout costs each surviving member, as a fraction of its health.</summary>
    public static int RoutWoundMilli => World.WorldTuningHub.Tuning.PlaceholderBattle.RoutWoundMilli;

    /// <summary>What clearing a guard costs each member, as a fraction of its health.</summary>
    public static int GuardWoundMilli => World.WorldTuningHub.Tuning.PlaceholderBattle.GuardWoundMilli;

    public BattleOutcome Resolve(BattleRequest request, IReadOnlyList<WorldEntity> combatants, ulong seed) =>
        request.Kind == BattleKinds.Guard
            ? ResolveGuard(request, combatants)
            : ResolveForces(request, combatants);

    /// <summary>Effective weight: health still in the fight, scaled by experience.</summary>
    public static long Strength(WorldEntity entity)
    {
        long total = 0;
        foreach (var m in entity.Members)
            total += Math.Max(0, m.Hp - m.Wounds) * (long)Math.Max(1, m.Level);
        return total;
    }

    static BattleOutcome ResolveGuard(BattleRequest request, IReadOnlyList<WorldEntity> combatants)
    {
        var attacker = combatants.FirstOrDefault(e =>
            string.Equals(e.EntityId, request.AttackerEntityId, StringComparison.Ordinal));
        if (attacker is null) return new BattleOutcome { BattleId = request.BattleId };

        // A force with nothing left standing cannot take a slot, and saying otherwise would let a
        // dead legion unlock a sector.
        if (Strength(attacker) <= 0)
            return new BattleOutcome
            {
                BattleId = request.BattleId,
                Sides = new[] { new BattleSideOutcome { EntityId = attacker.EntityId, Survivors = attacker.Members, Routed = true } }
            };

        return new BattleOutcome
        {
            BattleId = request.BattleId,
            WinnerEntityId = attacker.EntityId,
            GuardCleared = true,
            Sides = new[] { Wounded(attacker, GuardWoundMilli) }
        };
    }

    static BattleOutcome ResolveForces(BattleRequest request, IReadOnlyList<WorldEntity> combatants)
    {
        var attacker = combatants.FirstOrDefault(e =>
            string.Equals(e.EntityId, request.AttackerEntityId, StringComparison.Ordinal));
        var defender = combatants.FirstOrDefault(e =>
            string.Equals(e.EntityId, request.DefenderEntityId, StringComparison.Ordinal));
        if (attacker is null || defender is null) return new BattleOutcome { BattleId = request.BattleId };

        var attackerWeight = Strength(attacker);
        var defenderWeight = Strength(defender);

        // A dug-in garrison counts as holding the ground whether or not anybody moved this turn —
        // that is what it gave up its mobility for.
        var entrenched = request.DefenderStationary
                         || string.Equals(defender.Stance, Movement.MovementPolicy.Hold, StringComparison.Ordinal);
        // base-defense `siege-objective` §7 (spec-siege-objective.md): a district assault reads this
        // bonus as 1000 (no-op), never the placeholder's own 1250. `structure-state`/`siege-cover`
        // model real fortifications on the board itself — stacking this flat "standing still" bonus on
        // top would pay the defender twice for the same thing. Every non-district battle (guard-clear
        // is handled separately above and never reaches this branch; sector/lane contact battles) is
        // untouched — still reads the real, tunable 1250.
        var defenderBonusMilli = request.Kind == BattleKinds.District
            ? Battle.Board.SiegeTuningPolicy.Objective.DistrictDefenderBonusMilli
            : DefenderBonusMilli;
        if (entrenched) defenderWeight = defenderWeight * defenderBonusMilli / 1000;

        if (attackerWeight == defenderWeight)
            return new BattleOutcome
            {
                BattleId = request.BattleId,
                Sides = new[] { Wipe(attacker), Wipe(defender) }
                    .OrderBy(s => s.EntityId, StringComparer.Ordinal)
                    .ToList()
            };

        var attackerWon = attackerWeight > defenderWeight;
        var (winner, winnerWeight) = attackerWon ? (attacker, attackerWeight) : (defender, defenderWeight);
        var (loser, loserWeight) = attackerWon ? (defender, defenderWeight) : (attacker, attackerWeight);

        // The winner bleeds in proportion to what it was up against — a walkover costs nothing, a
        // near-run thing costs half the force's health.
        var winnerWound = (int)(loserWeight * 500 / Math.Max(1, winnerWeight));
        var loserSide = loserWeight * 1000 / Math.Max(1, winnerWeight) <= WipeoutRatioMilli
            ? Wipe(loser)
            : Wounded(loser, RoutWoundMilli) with { Routed = true };

        return new BattleOutcome
        {
            BattleId = request.BattleId,
            WinnerEntityId = winner.EntityId,
            Sides = new[] { Wounded(winner, winnerWound), loserSide }
                .OrderBy(s => s.EntityId, StringComparer.Ordinal)
                .ToList()
        };
    }

    /// <summary>Adds a share of each member's health as wounds; anyone past their health is gone.</summary>
    static BattleSideOutcome Wounded(WorldEntity entity, int woundMilli)
    {
        var survivors = new List<WorldEntityMember>(entity.Members.Count);
        foreach (var m in entity.Members)
        {
            var wound = Math.Max(1, (int)((long)m.Hp * Math.Max(0, woundMilli) / 1000));
            var wounds = m.Wounds + wound;
            if (wounds < m.Hp) survivors.Add(m with { Wounds = wounds });
        }

        return new BattleSideOutcome
        {
            EntityId = entity.EntityId,
            Survivors = survivors,
            Destroyed = survivors.Count == 0
        };
    }

    static BattleSideOutcome Wipe(WorldEntity entity) => new()
    {
        EntityId = entity.EntityId,
        Survivors = Array.Empty<WorldEntityMember>(),
        Destroyed = true
    };
}
