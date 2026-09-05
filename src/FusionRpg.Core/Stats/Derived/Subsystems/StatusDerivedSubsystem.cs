namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// The consumer for a STATUS's derived-channel writes — the fourth registered
/// <see cref="IActorStatSubsystem"/>, and the one whose absence made every mechanism node the passive
/// tree depends on compile, bind, and change no number.
///
/// <para><b>The gap this closes.</b> A status may author `stat.&lt;channel&gt;.&lt;op&gt;` payloads
/// (<see cref="Status.StatusStatPayload"/>), and <see cref="Status.StatusStatPayload.IsKnownChannel"/>
/// accepts DERIVED channels — `combat.*` and the status-family channels — alongside the 23 primary
/// ones. But the injector upserts every one of them into the PRIMARY session bag
/// (`EffectRuntime.cs`'s `CheatState.Stats.Upsert`), and no registered subsystem reads that bag for
/// derived values. A status raising `combat.defense.omni` therefore resolved to nothing at all. Two
/// shipped runtimes already produce such mods and compose nothing today for the same reason —
/// `StanceRuntime.Raise` and `ExhaustionPolicy.Sync`.</para>
///
/// <para><b>Why a subsystem, not another sink arm.</b> Same argument
/// <see cref="AtomDerivedSubsystem"/> makes: the lawn's derived compose already runs per resolve in
/// <see cref="ActorHub.ResolveDerived"/>, every registered subsystem contributes and
/// <see cref="DerivedComposer"/> folds. A second delivery path for a value the composer already owns
/// is how five features grew private derived-write paths, exactly as actor-hub-ssot.md §6.1 predicted
/// in writing.</para>
///
/// <para><b>Why a separate subsystem from <see cref="AtomDerivedSubsystem"/>.</b>
/// <see cref="ActorHub.Register"/> replaces by <see cref="SubsystemId"/>, so registering a second
/// <c>AtomDerivedSubsystem</c> would silently EVICT the first rather than add to it. Statuses and
/// bound atoms are different sources with different lifetimes — a status withdraws when its instance
/// expires — so they get their own id and their own delegate.</para>
///
/// <para><b>One behaviour change, and it is deliberate</b> (spec-mechanism-wiring.md §12 q1, closed by
/// the owner 2026-09-05). <c>ResistanceEvaluator</c> already reads the defender's
/// <see cref="ActorDerivedSnapshot"/>, so once a status can contribute `status.resist.*`, a host
/// carrying one rolls harder against the NEXT status applied. That is what a resist status means, and
/// it makes application order significant. It changes no shipped content — no status in `data/seed/`
/// authors a stat overlay — so the order-sensitivity is a constraint on future authoring, not a
/// regression. The read terminates: resolving a host's statuses is a dictionary lookup, never a nested
/// resolve.</para>
///
/// <para>Stateless and idempotent between calls, like every other arm of this seam. Instance-scoped
/// with a per-context delegate and no static cache — D21 gives every actor its own tree state, and a
/// static cache here would leak one scoped host's statuses into another (the `AptitudeTuningHub` race
/// this repo has already fixed once).</para>
/// </summary>
public sealed class StatusDerivedSubsystem : IActorStatSubsystem
{
    /// <summary>
    /// The derived-channel mods contributed by the host's currently active statuses. A delegate for
    /// the same reason <see cref="AptitudeSubsystem"/> and <see cref="AtomDerivedSubsystem"/> use one:
    /// this module resolves values into channels, it never owns where instances are stored. Production
    /// passes a projection over the live `StatusRuntime` instances; tests pass a list.
    /// </summary>
    readonly Func<StatContext, IReadOnlyList<StatusDerivedMod>> _modsFor;

    public StatusDerivedSubsystem(Func<StatContext, IReadOnlyList<StatusDerivedMod>>? modsFor = null) =>
        _modsFor = modsFor ?? (_ => Array.Empty<StatusDerivedMod>());

    /// <summary>Namespaced by OWNER, like `rpg.aptitude` and `atom.derived` — and deliberately NOT
    /// `status.derived`, because `status.*` is a live derived-channel family (`status.resist.*`,
    /// `status.immune.*`) so that id reads as a channel claim. `SpecChannelClaimTests` caught it.</summary>
    public string SubsystemId => "l2b.derived";

    /// <summary>After <see cref="AtomDerivedSubsystem"/>'s reserved 350. A status is the shortest-lived
    /// source on this seam, so it folds last among the derived contributors.</summary>
    public int Order => 400;

    public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods)
    {
        var live = _modsFor(ctx);
        if (live is null || live.Count == 0) return;

        foreach (var mod in live)
        {
            if (string.IsNullOrWhiteSpace(mod.Channel)) continue;
            mods.Add(new DerivedModifier(mod.Channel, mod.Op, mod.Amount, SourceId: mod.SourceId));
        }
    }

    /// <summary>
    /// Maps a status payload's op string to a composer op.
    ///
    /// <para><b><c>more</c> is refused, never coerced.</b> `StatusStatPayload.Ops` allows
    /// `flat | increased | more` because `more` is meaningful on a PRIMARY channel. There is no `More`
    /// on the derived side (effect-atom/definitions.md §14), so a status authoring `more` against a
    /// derived channel is a content error. Coercing it to `Flat` is how a wrong number ships looking
    /// correct — the same reasoning <see cref="AtomDerivedSubsystem.TryParseOp"/> gives.</para>
    /// </summary>
    public static bool TryParseOp(string? op, out DerivedModifierOp parsed)
    {
        switch (op)
        {
            case "flat": parsed = DerivedModifierOp.Flat; return true;
            case "increased": parsed = DerivedModifierOp.Increased; return true;
            default: parsed = default; return false;
        }
    }
}

/// <summary>
/// One derived-channel mod contributed by a live status instance, resolved to what the composer needs.
/// Deliberately not the raw <see cref="Status.StatusStatMod"/>: op parsing and the primary-vs-derived
/// split belong at projection time, not on a per-resolve hot path (perf SSOT — uncached resolves are
/// the measured cost, not scans alone).
///
/// <para><paramref name="SourceId"/> names the status INSTANCE, not the status id, so two coexisting
/// stacks withdraw independently when one expires.</para>
/// </summary>
public readonly record struct StatusDerivedMod(
    string Channel, DerivedModifierOp Op, double Amount, string SourceId);
