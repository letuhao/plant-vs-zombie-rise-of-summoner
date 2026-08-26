using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Tools.CombatSim;

/// <summary>
/// The DETERMINISTIC CORE — win probability in closed form, with no RNG and no trials.
///
/// <para><b>It contains no combat math.</b> Every shape it evaluates is the shipped function:
/// <see cref="CombatProbability.Sigmoid"/>, <see cref="OverlayCombatCalculator.PierceFactor"/>,
/// <see cref="OverlayCombatCalculator.DivisiveMitigation"/>,
/// <see cref="OverlayCombatCalculator.AmpFactorReciprocal"/>,
/// <see cref="OverlayCombatCalculator.CapAvoidanceBand"/>, <see cref="ClampedContest.Apply"/> —
/// read through <see cref="CombatPolicy.Default"/>, the same policy the dispatcher reads. The only
/// thing added here is the EXPECTATION over the outcome mixture. That is what makes a disagreement
/// with the simulator meaningful: both drive the same formulas, so a gap is a real modelling gap,
/// never two implementations drifting apart.</para>
///
/// <para><b>Why a closed form exists at all.</b> One swing has a FINITE outcome space — miss, parried,
/// blocked, clean, clean+crit. Five atoms with known probability and known damage. So the mean and
/// variance of a round are exact finite sums, and so is any nonlinear function downstream of them
/// (reflection, which is a whole second attack whose base is a fraction of the first, is enumerated
/// atom-by-atom rather than approximated at the mean).</para>
///
/// <para><b>The one approximation</b> is the last step: turning a per-round damage distribution into
/// a win probability uses the renewal first-passage result <c>E[T] = h/mu</c>,
/// <c>Var[T] = h·sigma^2/mu^3</c> and a normal race. That is asymptotic in the number of rounds, so
/// it degrades on very short fights — see <see cref="DuelPrediction.RoundsA"/>.</para>
///
/// <para><b>Shields ARE modelled</b>, as of 2026-08-25 — a depleting pool makes rounds non-identical,
/// which the closed form cannot assume, but it is a PHASE BOUNDARY rather than chaos. Two phases with
/// the same incoming distribution collapse into one effective-HP term
/// (<see cref="ShieldEffectiveHp"/>) plus one gate on reflection (a shield suppresses its owner's own
/// bounce, because nothing reaches HP to bounce). Measured residual with shields live and bought from
/// the distribution: <b>0.7% mean, 1.4% max</b>.</para>
///
/// <para><b>Still not modelled:</b> shield REGEN and resource regen. The duel runner does not tick
/// either, so the two agree — but both understate a regenerating pool, and neither can be trusted the
/// day the action layer starts spending <c>stamina</c> and <c>qi</c>.</para>
/// </summary>
public static class Analytic
{
    /// <summary>One possible result of one swing: how likely, and how much damage.</summary>
    public sealed record Atom(double P, double Damage);

    public sealed record StrikeStats(
        double Mean, double Variance,
        double PHit, double PParry, double PBlock, double PClean, double PCrit,
        double DClean, double DCrit, double DParry, double DBlock,
        IReadOnlyList<Atom> Atoms);

    public sealed record SideStats(double DealtMean, double DealtVar, double TakenMean, double TakenVar);

    public sealed record DuelPrediction(
        string A, string B,
        double WinShareA,
        double RateAgainstB, double RateAgainstA,
        double VarAgainstB, double VarAgainstA,
        double RoundsA, double RoundsB,
        StrikeStats StrikeA, StrikeStats StrikeB,
        double ReflectMeanToA, double ReflectMeanToB);

    // ── channel reads ────────────────────────────────────────────────────────────────────────────
    // Archetype.Load already rejects an unregistered channel id, so a typo here cannot reach a run
    // silently — it fails at load with the id named.

    static double V(Archetype a, string channel) =>
        a.Stats.TryGetValue(channel, out var r) ? (r.Min + r.Max) / 2.0 : 0.0;

    const string Power = "combat.power.omni";
    const string Defense = "combat.defense.omni";
    const string Pen = "combat.penetration.omni";
    const string Abs = "combat.absorption.omni";
    const string Amp = "combat.amplification.omni";
    const string Red = "combat.reduction.omni";
    const string Acc = "combat.accuracy.omni";
    const string Dodge = "combat.dodge.omni";
    const string CritRate = "combat.crit.rate.omni";
    const string CritResist = "combat.crit.resist.omni";
    const string CritDmg = "combat.crit.damage.omni";
    const string CritResistDmg = "combat.crit.resist.damage.omni";
    const string ParryRate = "combat.parry.rate.omni";
    const string ParryBreak = "combat.parry.break.omni";
    const string ParryStrength = "combat.parry.strength.omni";
    const string ParryShred = "combat.parry.shred.omni";
    const string BlockRate = "combat.block.rate.omni";
    const string BlockBreak = "combat.block.break.omni";
    const string BlockStrength = "combat.block.strength.omni";
    const string BlockShred = "combat.block.shred.omni";
    const string ReflectRate = "combat.reflect.rate.omni";
    const string ReflectDamage = "combat.reflect.damage.omni";
    const string ReflectResistRate = "combat.reflect.resist.rate.omni";
    const string ReflectResistDamage = "combat.reflect.resist.damage.omni";

    /// <summary>Fails loudly if a channel this model reads is not in the live registry — the same
    /// discipline Archetype/Scenario already apply, so a renamed channel cannot silently read 0.</summary>
    public static void AssertChannelsRegistered()
    {
        var registry = DerivedStatRegistry.CreateDefault();
        string[] all =
        [
            Power, Defense, Pen, Abs, Amp, Red, Acc, Dodge, CritRate, CritResist, CritDmg,
            CritResistDmg, ParryRate, ParryBreak, ParryStrength, ParryShred, BlockRate, BlockBreak,
            BlockStrength, BlockShred, ReflectRate, ReflectDamage, ReflectResistRate, ReflectResistDamage
        ];
        var bad = all.Where(c => !registry.TryResolveChannel(c, out _)).ToList();
        if (bad.Count > 0)
            throw new InvalidOperationException($"Analytic reads unregistered channel(s): {string.Join(", ", bad)}");
    }

    /// <summary>
    /// One swing, as a distribution. Mirrors <c>OverlayCombatCalculator.Resolve</c> branch for branch
    /// for the single-element, neutral-matchup, effectiveness-1 case the duel runner drives.
    /// </summary>
    public static StrikeStats Strike(Archetype atk, Archetype def, double baseDamage)
    {
        var policy = CombatPolicy.Default;

        // ── probabilities: sigmoid contests and linear per-mille rate contests ───────────────────
        var pHit = CombatProbability.Sigmoid(V(atk, Acc) - V(def, Dodge), CombatProbabilityPolicy.AccuracyScale);
        var pCrit = CombatProbability.Sigmoid(V(atk, CritRate) - V(def, CritResist), CombatProbabilityPolicy.CritRateScale);
        var critMult = 1.0 + CombatProbability.Sigmoid(
            V(atk, CritDmg) - V(def, CritResistDmg), CombatProbabilityPolicy.CritDamageScale);

        var pParryRaw = Math.Max(0.0, V(def, ParryRate) - V(atk, ParryBreak)) / 1000.0;
        var pBlockRaw = Math.Max(0.0, V(def, BlockRate) - V(atk, BlockBreak)) / 1000.0;
        var (pParry, pBlock) = OverlayCombatCalculator.CapAvoidanceBand(
            pHit, pParryRaw, pBlockRaw, policy.AvoidanceBandCapPermille / 1000.0);
        var pClean = Math.Max(0.0, pHit - pParry - pBlock);

        // ── magnitudes: the mitigation chain, evaluated once ─────────────────────────────────────
        var pierce = OverlayCombatCalculator.PierceFactor(V(atk, Pen) - V(def, Abs), policy.PierceScale);
        var defense = V(def, Defense) * pierce;
        var power = V(atk, Power);
        var offense = baseDamage + power;

        var powerAdjusted = policy.DefenseShape == DefenseShape.Divisive
            ? OverlayCombatCalculator.DivisiveMitigation(offense, defense, policy.DefenseDivisorK, baseDamage + power)
            : offense - defense;

        var ampDelta = V(atk, Amp) - V(def, Red);
        var ampFactor = policy.AmpShape == AmpShape.Reciprocal
            ? OverlayCombatCalculator.AmpFactorReciprocal(ampDelta, policy.AmpScale)
            : OverlayCombatCalculator.AmpFactor(ampDelta, policy.AmpScale);

        var dClean = Math.Max(0.0, powerAdjusted) * ampFactor;
        var dCrit = Math.Max(0.0, powerAdjusted) * critMult * ampFactor;

        // Guarded hits end resolution before the mitigation chain (spec-evasion-chain.md §3), so
        // they read the authored hit, not powerAdjusted. Integer per-mille throughout, exactly as
        // ClampedContest is called at the real site.
        var baseLong = (long)Math.Round(baseDamage, MidpointRounding.AwayFromZero);
        var neutralBase = (long)Math.Round(
            baseDamage * (policy.ParryNeutralShareKPm / 1000.0), MidpointRounding.AwayFromZero);

        var removedParry = ClampedContest.Apply(
            neutralBase,
            (long)Math.Round(V(def, ParryStrength) - V(atk, ParryShred), MidpointRounding.AwayFromZero),
            1, baseLong, 0, policy.ParryCapPermille);
        var removedBlock = ClampedContest.Apply(
            neutralBase,
            (long)Math.Round(V(def, BlockStrength) - V(atk, BlockShred), MidpointRounding.AwayFromZero),
            1, baseLong, 0, policy.BlockCapPermille);

        var dParry = Math.Max(0.0, baseDamage - removedParry);
        var dBlock = Math.Max(0.0, baseDamage - removedBlock);

        // ── the mixture: five atoms partitioning the swing ───────────────────────────────────────
        var atoms = new List<Atom>
        {
            new(1.0 - pHit, 0.0),                 // miss
            new(pParry, dParry),
            new(pBlock, dBlock),
            new(pClean * (1.0 - pCrit), dClean),
            new(pClean * pCrit, dCrit)
        };

        var mean = atoms.Sum(a => a.P * a.Damage);
        var second = atoms.Sum(a => a.P * a.Damage * a.Damage);
        return new StrikeStats(mean, Math.Max(0.0, second - mean * mean),
            pHit, pParry, pBlock, pClean, pCrit, dClean, dCrit, dParry, dBlock, atoms);
    }

    /// <summary>
    /// How much HP a shield is worth — the phase decomposition, collapsed to one number.
    ///
    /// <para>While the shield stands, <c>ShieldMath.AbsorbLayer</c> takes the WHOLE hit (remainder 0)
    /// and spends <c>damageToShield</c> from the pool. So a shield of <c>S</c> survives
    /// <c>S / damageToShield</c> hits, each of which would otherwise have dealt <c>input</c> to HP:</para>
    ///
    /// <code>effective HP from a shield = S × input / damageToShield</code>
    ///
    /// <para><b>Why one term is enough rather than two solved phases.</b> Mitigation runs BEFORE the
    /// shield gate, so the shield phase and the HP phase face the identical per-round damage
    /// distribution — only the target changes. Two phases with the same rate sum to
    /// <c>(S_eff + hp)/mu</c>, which is what a single pool of that size already gives.</para>
    ///
    /// <para><b>What the ratio means, and it is the whole shield-tank mechanic:</b>
    /// <c>damageToShield</c> is <c>ClampedContest(input, pen − toughness, …)</c> bounded to
    /// <c>[100‰, 3000‰]</c> of the hit. At <c>pen = toughness</c> a shield point is worth exactly an
    /// HP point. Out-toughness the attacker and it is worth up to <b>10×</b>; get out-penetrated and
    /// it is worth as little as <b>1/3</b>. That spread — not the pool size — is why a shield tank is
    /// a different build from an HP tank rather than a reskin of one, and why <c>Pierce</c> is its
    /// counter.</para>
    ///
    /// <para><b>Not modelled:</b> shield regen. The duel runner never ticks it either, so the two
    /// agree; both understate a regenerating shield.</para>
    /// </summary>
    static double ShieldEffectiveHp(Archetype owner, Archetype attacker, double incomingPerSwing)
    {
        var shield = (owner.ShieldHp.Min + owner.ShieldHp.Max) / 2.0;
        if (shield <= 0 || incomingPerSwing <= 0) return 0;

        var input = (long)Math.Round(incomingPerSwing, MidpointRounding.AwayFromZero);
        if (input <= 0) return 0;

        var breakerDelta = (long)Math.Round(
            V(attacker, ShieldPen) - V(owner, ShieldToughness), MidpointRounding.AwayFromZero);
        var damageToShield = ClampedContest.Apply(
            input, breakerDelta, 1, input,
            FusionRpg.Core.Combat.Shield.ShieldPolicy.ChipFloorKPm,
            FusionRpg.Core.Combat.Shield.ShieldPolicy.PenCapKPm);

        return damageToShield <= 0 ? 0 : shield * (double)input / damageToShield;
    }

    const string ShieldPen = "combat.shield.pen.omni";
    const string ShieldToughness = "combat.shield.toughness.omni";

    /// <summary>Probability the defender bounces, and the share it bounces — both linear-from-zero
    /// contests, not sigmoids (<c>CombatDamageDispatcher.TryReflect</c>).</summary>
    static (double P, double Share) Reflect(Archetype reflector, Archetype attacker)
    {
        var policy = CombatPolicy.Default;
        var rate = Math.Clamp(
            Math.Max(0.0, V(reflector, ReflectRate) - V(attacker, ReflectResistRate)) / policy.ReflectRateScale, 0.0, 1.0);
        var share = Math.Clamp(
            Math.Max(0.0, V(reflector, ReflectDamage) - V(attacker, ReflectResistDamage)) / policy.ReflectShareScale, 0.0, 1.0);
        return (rate, share);
    }

    /// <summary>
    /// What one swing of <paramref name="atk"/> costs each side.
    ///
    /// <para><b>The bounce lands RAW.</b> <c>CombatDamageDispatcher.TryReflect</c> builds the bounce
    /// packet without an <c>ElementPayload</c>, and <c>OverlayCombatMath.Finalize</c> early-returns
    /// on a payload-less packet (<c>OverlayCombatMath.cs:42-43</c>) — so the calculator never runs for
    /// it. Reflected damage is <b>unmitigated, unavoidable and uncritable</b>: no power is added, no
    /// defense subtracts, dodge and guard cannot stop it, and crit cannot multiply it. It is a flat
    /// share of what landed. (This is what the closed form got wrong on its first run, assuming the
    /// bounce re-resolved as a full attack; the cross-check against the simulator found it in one
    /// pass — 3,886 measured against 1,698 predicted.)</para>
    ///
    /// <para>Still enumerated atom by atom rather than at the mean, because a bounce base is a fixed
    /// share of a RANDOM strike outcome and the guard branches make that mapping piecewise.</para>
    /// </summary>
    static SwingStats Swing(Archetype atk, Archetype def, double baseDamage, ActionSet? actions = null)
    {
        var s = actions is null ? Strike(atk, def, baseDamage) : MixedStrike(atk, def, baseDamage, actions);
        var (pRef, share) = Reflect(def, atk);

        // Joint space: (strike atom) x (bounce fires?). Exact, 10 terms. Kept JOINT rather than
        // collapsed to two marginals because dealt and bounced are the SAME event seen twice — the
        // bounce is a share of what landed — and that covariance is what the race needs.
        var joint = new List<(double P, double Dealt, double Back)>();
        foreach (var a in s.Atoms)
        {
            if (a.P <= 0) continue;
            var bounced = Math.Round(a.Damage * share, MidpointRounding.AwayFromZero);
            if (pRef <= 0 || bounced <= 0) { joint.Add((a.P, a.Damage, 0.0)); continue; }
            joint.Add((a.P * (1.0 - pRef), a.Damage, 0.0));
            joint.Add((a.P * pRef, a.Damage, bounced));
        }

        // STATUS — the fourth axis. A landed hit may apply a DoT, whose whole tail is attributable to
        // the swing that caused it (StatusMath.ExpectedDotPerSwing). Added to the dealt side only:
        // a DoT is not reflected, because reflection reads the damage packet and a DoT tick is its
        // own packet from the status, not from the attacker's swing.
        // Per ROUND, from refresh uptime — not per swing. See ExpectedDotPerRound.
        var dotPerSwing = Status is null ? 0.0
            : StatusMath.ExpectedDotPerRound(atk, def, Status, baseDamage, s.PHit);

        // CC removes rounds from the side it lands ON. Here `atk` is the one swinging, so a CC that
        // `def` lands on `atk` is what would reduce this swing — but that belongs to the OTHER swing's
        // resolution, and applying it here would double-count. What this swing owns is the CC it
        // INFLICTS, which reduces the defender's output and is applied by Predict.
        _ = s.PHit;

        var backMean = joint.Sum(x => x.P * x.Back);
        var backVar = Math.Max(0.0, joint.Sum(x => x.P * x.Back * x.Back) - backMean * backMean);
        var cov = joint.Sum(x => x.P * (x.Dealt - s.Mean) * (x.Back - backMean));
        // The DoT tail raises the mean without adding per-round variance of its own worth modelling:
        // it is spread over several rounds, which is precisely what averages a random payload out.
        return new SwingStats(s, s.Mean + dotPerSwing, s.Variance, backMean, backVar, cov);
    }

    sealed record SwingStats(
        StrikeStats Strike, double DealtMean, double DealtVar,
        double BackMean, double BackVar, double CovDealtBack);

    /// <summary>
    /// The whole prediction: two stat blocks in, a win share out. No trials.
    /// </summary>
    public static DuelPrediction Predict(Archetype a, Archetype b) => Predict(a, b, null);

    /// <summary>Status profile in play, or null for none. Set by the caller before Predict — a field
    /// rather than a parameter because it threads through Swing/Strike unchanged otherwise, and the
    /// alternative is five signatures changed for one optional term.</summary>
    public static StatusProfile? Status { get; set; }

    /// <summary>
    /// With <paramref name="actions"/> non-null, every swing must be PAID FOR out of a pool the
    /// aptitude distribution filled. See <see cref="ActionMix"/> for why that is still closed form
    /// and still `Θ`-free.
    /// </summary>
    public static DuelPrediction Predict(Archetype a, Archetype b, ActionSet? actions)
    {
        var baseA = (a.BaseDamage.Min + a.BaseDamage.Max) / 2.0;
        var baseB = (b.BaseDamage.Min + b.BaseDamage.Max) / 2.0;

        var swingA = Swing(a, b, baseA, actions);   // A hits B; B may bounce onto A
        var swingB = Swing(b, a, baseB, actions);   // B hits A; A may bounce onto B

        // Shields are a PHASE, and this is the phase decomposition — collapsed to one term because
        // both phases take the identical incoming distribution and differ only in what the damage
        // lands on. See ShieldEffectiveHp.
        var rawHpA = (a.Hp.Min + a.Hp.Max) / 2.0;
        var rawHpB = (b.Hp.Min + b.Hp.Max) / 2.0;
        var shieldA = ShieldEffectiveHp(a, b, swingB.DealtMean);
        var shieldB = ShieldEffectiveHp(b, a, swingA.DealtMean);
        var hpA = rawHpA + shieldA;
        var hpB = rawHpB + shieldB;

        // A SHIELD SUPPRESSES ITS OWNER'S OWN REFLECTION, and this is not a modelling choice — it
        // falls out of `reflectReadsPostShield: true` (decisions.md, Combat mitigation shapes).
        // `CombatDamageDispatcher` fires reflect only when `reflectSource < 0`, and a fully-absorbed
        // hit applies exactly zero to HP. So while the shield stands, the wearer bounces nothing.
        //
        // First-order: reflection operates only during the HP phase, whose share of the fight is
        // hp / (hp + shieldEhp). Shield and thorns are ANTI-SYNERGISTIC — worth knowing before
        // anyone authors a build around both.
        static double HpPhaseShare(double hp, double shieldEhp)
            => hp + shieldEhp <= 0 ? 1.0 : hp / (hp + shieldEhp);

        var reflectShareB = HpPhaseShare(rawHpB, shieldB);   // B reflects onto A, gated by B's shield
        var reflectShareA = HpPhaseShare(rawHpA, shieldA);

        // CC: a disabled actor does not swing. A lands cc on B at p×duration of B's rounds, so B's
        // output is scaled down by exactly that share, and vice versa. Applied ONCE, here, rather than
        // inside Swing — the effect belongs to the victim's rate, not to the attacker's swing.
        var ccOnB = Status is null ? 0.0 : StatusMath.CcDisabledShare(a, b, Status, baseA) * swingA.Strike.PHit;
        var ccOnA = Status is null ? 0.0 : StatusMath.CcDisabledShare(b, a, Status, baseB) * swingB.Strike.PHit;
        var actB = 1.0 - ccOnB;
        var actA = 1.0 - ccOnA;

        // Per round both sides swing once. B's HP falls by A's damage plus A's bounce of B's swing.
        var rateB = swingA.DealtMean * actA + swingB.BackMean * reflectShareA * actB;
        var rateA = swingB.DealtMean * actB + swingA.BackMean * reflectShareB * actA;
        var varB = swingA.DealtVar * actA + swingB.BackVar * reflectShareA * actB;
        var varA = swingB.DealtVar * actB + swingA.BackVar * reflectShareB * actA;

        // Renewal first passage: rounds to deplete h at mean mu with per-round variance sigma^2.
        // Var[T] = h·sigma^2/mu^3 is the standard Wald/delta-method result for a renewal counting
        // process; it is what converts "a small per-round edge" into "a large win rate over a long
        // fight" — the largest single lever measured, and it is not a stat.
        static (double T, double Var) FirstPassage(double hp, double rate, double variance)
        {
            if (rate <= 0) return (double.PositiveInfinity, double.PositiveInfinity);
            return (hp / rate, hp * variance / (rate * rate * rate));
        }

        var (tKillB, vKillB) = FirstPassage(hpB, rateB, varB);
        var (tKillA, vKillA) = FirstPassage(hpA, rateA, varA);

        double win;
        if (double.IsInfinity(tKillB) && double.IsInfinity(tKillA)) win = 0.5;
        else if (double.IsInfinity(tKillB)) win = 0.0;
        else if (double.IsInfinity(tKillA)) win = 1.0;
        else
        {
            // A wins iff it kills first. Two first-passage times race; both are sums over many
            // rounds, so both are approximately normal (CLT). Initiative alternates by trial in the
            // duel runner, so the half-round first-strike edge averages out and needs no correction.
            //
            // The two times are NOT independent when reflection is live: one swing damages the
            // defender AND the attacker, so a heavy round shortens both kill times together. That
            // positive correlation shrinks the variance of the DIFFERENCE, which is the only thing
            // the race reads. Var[T_A − T_B] = Var[T_A] + Var[T_B] − 2·rho·SD_A·SD_B, with rho
            // carried up unchanged from the per-round increments (T = h/rate is monotone in rate, so
            // the correlation survives the delta method). Dropping this term is worth ~5 points of
            // win rate on a reflect matchup and nothing at all on one without reflection.
            var covRounds = swingA.CovDealtBack * reflectShareB + swingB.CovDealtBack * reflectShareA;
            var rho = varA > 0 && varB > 0
                ? Math.Clamp(covRounds / Math.Sqrt(varA * varB), -1.0, 1.0)
                : 0.0;
            var varDiff = vKillA + vKillB - 2.0 * rho * Math.Sqrt(vKillA * vKillB);
            var sd = Math.Sqrt(Math.Max(0.0, varDiff));
            win = sd <= 0 ? (tKillB < tKillA ? 1.0 : tKillB > tKillA ? 0.0 : 0.5)
                          : Phi((tKillA - tKillB) / sd);
        }

        return new DuelPrediction(a.Name, b.Name, win, rateB, rateA, varB, varA, tKillA, tKillB,
            Strike(a, b, baseA), Strike(b, a, baseB), swingA.BackMean, swingB.BackMean);
    }

    /// <summary>
    /// THE ACTION ECONOMY, in closed form.
    ///
    /// <para>An actor can only swing if it can pay. Over a fight of <c>T</c> rounds, action <c>i</c>
    /// can be taken at most <c>(max_i + regen_i × T) / cost_i</c> times — the pool is the burst, the
    /// regen is the sustain. Taking them greedily by priority gives an exact count per action, and the
    /// swing becomes a MIXTURE over actions on top of the mixture over outcomes each one already has.
    /// A finite mixture of finite mixtures is still finite, so it is still exact.</para>
    ///
    /// <para><b>Why this stays `Θ`-free, which is the part that could easily have been lost.</b> Cost
    /// is priced on OUTPUT (class-system-ideal.md §7a.4), and output is <c>P(Θ)</c>-scaled; pools come
    /// from magnitude-read channels, so they are <c>P(Θ)</c>-scaled too. Every ratio in here —
    /// <c>max/cost</c>, <c>regen/cost</c> — is therefore a pure number. Pricing INVESTMENT instead
    /// would have made the economy drift with the ladder. §7a.4 chose output-pricing for design
    /// reasons; it turns out to be required for invariance.</para>
    ///
    /// <para><c>T</c> depends on the damage rate and the rate depends on <c>T</c>, so this is a fixed
    /// point. It is solved by iteration, not by sampling — deterministic, and it converges in a
    /// handful of passes because the map is monotone and bounded.</para>
    /// </summary>
    static StrikeStats MixedStrike(Archetype atk, Archetype def, double baseDamage, ActionSet actions)
    {
        var ordered = actions.Actions.OrderBy(x => x.Priority).ToList();

        // Per-action strike distributions, computed once — they do not depend on the schedule.
        var perAction = ordered.Select(x => (Def: x,
                                             Stats: x.DamageMultiplier <= 0
                                                 ? null
                                                 : Strike(atk, def, baseDamage * x.DamageMultiplier),
                                             Cost: ActionPolicy.CostOf(x, baseDamage))).ToList();
        var fallback = perAction.FirstOrDefault(x => x.Stats is not null).Stats
                       ?? Strike(atk, def, baseDamage);

        // Two passes: the first estimates how long the fight is, the second walks that many rounds.
        // A pool trajectory depends on the number of rounds, and the number of rounds depends on the
        // damage the trajectory produces.
        var target = EffectiveHpOf(def);
        var rounds = Walk(perAction, atk, baseDamage, target, 400).Rounds;
        var w = Walk(perAction, atk, baseDamage, target, (int)Math.Clamp(Math.Ceiling(rounds) + 2, 1, 4000));

        return fallback with { Mean = w.Mean, Variance = w.Variance, Atoms = w.Atoms };
    }

    /// <summary>
    /// Walk the pool trajectory round by round. **Deterministic, no RNG** — costs are priced on
    /// NOMINAL output (a miss pays in full, spec-action-costs.md §3), so the pool state at every
    /// round is a fixed number, not a distribution. Only the DAMAGE is random, and each round's
    /// damage is the closed-form mixture <see cref="Strike"/> already gives.
    ///
    /// <para><b>Why a walk instead of an average rate.</b> Pools start FULL, so an actor bursts at the
    /// full action rate until the pool is gone and then drops to <c>regen/cost</c> forever. Averaging
    /// those two rates over the fight is wrong in the direction that matters most: a race is decided by
    /// WHEN damage lands, and front-loaded damage wins races that averaged damage loses. Measured: the
    /// averaging version sat 9% mean / 17.9% max off the simulator, all of it on the short fights.</para>
    ///
    /// <para>This is the same treatment shields got — a depleting pool is a PHASE, and the fix is to
    /// respect the phase rather than smooth it away. Here there can be several phases (one per action
    /// running dry), so the schedule is walked rather than solved in two segments.</para>
    /// </summary>
    static (double Mean, double Variance, double Rounds, List<Atom> Atoms) Walk(
        List<(ActionSet.ActionDef Def, StrikeStats? Stats, double Cost)> perAction,
        Archetype atk, double baseDamage, double targetHp, int maxRounds)
    {
        var pools = new ActorPools(atk);
        double cumDamage = 0, cumMean = 0, cumVar = 0;
        var weight = new Dictionary<int, double>();
        var used = 0;

        for (var r = 0; r < maxRounds; r++)
        {
            var picked = -1;
            for (var i = 0; i < perAction.Count; i++)
            {
                var (d, _, cost) = perAction[i];
                if (d.Cost is null) { picked = i; break; }
                if (cost <= 0 || pools.Value(d.Cost.ResourceId) >= cost)
                {
                    if (d.Cost is not null && cost > 0)
                        pools.TryPay(new[] { (d.Cost.ResourceId, cost) });
                    picked = i;
                    break;
                }
            }
            pools.Tick();
            used++;
            if (picked < 0) continue;

            weight[picked] = weight.GetValueOrDefault(picked) + 1.0;
            var st = perAction[picked].Stats;
            if (st is null) continue;                    // pass: costs nothing, deals nothing
            cumMean += st.Mean;
            cumVar += st.Variance;
            cumDamage += st.Mean;
            if (cumDamage >= targetHp) break;            // the fight is over; later rounds never happen
        }

        if (used == 0) return (0, 0, maxRounds, new List<Atom>());

        // The mixture over the rounds that actually happened — so a build that bursts for three rounds
        // and then passes reads as the average of THOSE rounds, not of an imagined steady state.
        var atoms = new List<Atom>();
        foreach (var (idx, count) in weight)
        {
            var st = perAction[idx].Stats;
            if (st is null) { atoms.Add(new Atom(count / used, 0.0)); continue; }
            foreach (var at in st.Atoms) atoms.Add(new Atom(at.P * count / used, at.Damage));
        }
        return (cumMean / used, cumVar / used, used, atoms);
    }

    /// <summary>Rounds-to-kill needs a target size; the shield term needs a rate, which needs rounds.
    /// Breaking that knot with raw hp only is deliberate — the shield correction is applied later at
    /// full precision, and using it here would nest one fixed point inside another for a second-order
    /// effect on the ACTION MIX, not on the result.</summary>
    static double EffectiveHpOf(Archetype a) => (a.Hp.Min + a.Hp.Max) / 2.0;

    /// <summary>Standard normal CDF. Abramowitz &amp; Stegun 7.1.26 on erf — max error 1.5e-7,
    /// three orders below the sampling error of any trial count this tool can afford.</summary>
    public static double Phi(double x)
    {
        var z = x / Math.Sqrt(2.0);
        var sign = z < 0 ? -1.0 : 1.0;
        z = Math.Abs(z);
        const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741,
                     a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;
        var t = 1.0 / (1.0 + p * z);
        var erf = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-z * z);
        return 0.5 * (1.0 + sign * erf);
    }
}
