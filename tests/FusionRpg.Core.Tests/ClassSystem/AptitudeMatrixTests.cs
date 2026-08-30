using System.Text.Json;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>
/// aura-skill-todo.md Phase 5 / <b>TC1</b> — the twelve aptitudes over the <b>real shipped edge set</b>.
///
/// <para><b>The gap this closes.</b> Every other unit-level aptitude test builds a synthetic one- or
/// two-edge tuning and funds <c>Might</c> (<see cref="AptitudeResolverTests"/>'s own
/// <c>MinimalTuning</c>; <c>AptitudeSubsystemTests</c>' <c>MightOnlyTuning</c>). The twelve are covered
/// elsewhere only by <i>balance</i> instruments — <c>DominanceGuardTests</c>' twelve-corner shape,
/// <c>CombatSimJsonEmitTests</c>' twelve gradient rows — which measure win-share, never a channel value.
/// Before this file, <b>nothing asserted that edge N of 486 resolves at all</b>, and the "twelve
/// aptitude matrix" this program cited as proof was a live manual probe recorded as prose. That is the
/// same shape of defect as the write-gate bug: composed correctly, dropped silently, found by a human
/// playing the game rather than by the suite.</para>
///
/// <para><b>Why this is an independent oracle and not a tautology.</b> These tests re-parse
/// <c>aptitudes.v2.json</c> with <see cref="JsonDocument"/> and recompute each edge's expected value
/// from the raw file, then compare against <see cref="AptitudeResolver"/>. They deliberately do NOT
/// call <see cref="AptitudeTuningLoader"/> to decide an edge's read mode or which dial applies — those
/// two selections are exactly what can silently go wrong for one family out of forty-nine, and asking
/// the loader to confirm the loader would prove nothing. Shares of <c>1.0</c> are used wherever
/// possible so <c>share^γ</c> is exactly 1 and the expected value collapses to plain integer
/// arithmetic (<c>round(k · P(Θ) / 1000)</c>) that this file states directly, independent of
/// <see cref="AptitudeReadFunctions"/>' own decimal path. <see cref="Fractional_shares_hold_too"/>
/// then exercises the real <c>share^γ</c> branch.</para>
///
/// <para><b>Not duplicated here:</b> channel registration (<c>AptitudeTuningTests
/// .EveryEdgeChannel_isRegistered_inDerivedStatRegistry</c>) and the reader-less-family census
/// (<c>ReaderCensusTests</c>) already exist and are cited rather than re-implemented.</para>
/// </summary>
public class AptitudeMatrixTests
{
    // ── locating and independently parsing the shipped file ──────────────────────────────────────
    // Same repo-root walk AptitudeTuningTests uses: Core owns no file I/O (tunables-ssot.md §7.2), so
    // a test that wants the real file reads it itself.

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-class-system.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "could not locate repo root (scripts/guard-class-system.ps1 not found above " + AppContext.BaseDirectory + ")");
    }

    static string ShippedPath() => Path.Combine(FindRepoRoot(), "data", "tuning", "aptitudes.v2.json");
    static string ShippedJson() => File.ReadAllText(ShippedPath());

    /// <summary>One edge as this test reads it — raw coefficient, plus the two selections
    /// (<paramref name="Mode"/>, <paramref name="EffectiveKMilli"/>) recomputed here rather than taken
    /// from the loader.</summary>
    sealed record RawEdge(string Channel, string Source, long KMilli, string Mode, long EffectiveKMilli, string DialApplied);

    /// <summary>The whole file, re-derived. Mirrors <c>AptitudeTuning.FamilyOf</c>'s documented rule
    /// (exact match first, then strip exactly one axis suffix) and <c>AptitudeResolver
    /// .EffectiveKMilli</c>'s documented dial order (recovery wins, then mitigation, else raw) — as an
    /// independent restatement, so a change to either rule that nobody intended shows up as a
    /// disagreement here instead of passing silently on both sides.</summary>
    static (IReadOnlyList<RawEdge> Edges, long ContestSpanPointsMilli, long ContestGammaMilli, long MagnitudeGammaMilli) ParseIndependently()
    {
        using var doc = JsonDocument.Parse(ShippedJson());
        var root = doc.RootElement;

        var familyRead = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in root.GetProperty("familyRead").EnumerateObject())
        {
            if (p.Name.StartsWith('_')) continue; // notes, never data
            familyRead[p.Name] = p.Value.GetString()!;
        }

        var recovery = root.GetProperty("recovery");
        var recoveryScale = recovery.GetProperty("scaleMilli").GetInt64();
        var recoveryFamilies = recovery.GetProperty("families").EnumerateArray().Select(x => x.GetString()!).ToList();

        var mitigation = root.GetProperty("mitigation");
        var mitigationScale = mitigation.GetProperty("scaleMilli").GetInt64();
        var mitigationFamilies = mitigation.GetProperty("families").EnumerateArray().Select(x => x.GetString()!).ToList();

        var read = root.GetProperty("read");
        var contestSpanMilli = (long)Math.Round(read.GetProperty("contest").GetProperty("spanPoints").GetDouble() * 1000.0);
        var contestGamma = read.GetProperty("contest").GetProperty("shareExponentMilli").GetInt64();
        var magnitudeGamma = read.GetProperty("magnitude").GetProperty("shareExponentMilli").GetInt64();

        var edges = new List<RawEdge>();
        foreach (var el in root.GetProperty("edges").EnumerateArray())
        {
            // `_group` divider rows carry no channel — data comments, skipped exactly as the loader
            // skips them (AptitudeTuningTests.GroupDividersAreSkipped_486RealEdgesNot490RawEntries).
            if (!el.TryGetProperty("channel", out var chEl)) continue;

            var channel = chEl.GetString()!;
            var source = el.GetProperty("source").GetString()!;
            var kMilli = el.GetProperty("kMilli").GetInt64();

            // FamilyOf's rule, restated: whole id first (move.range, progression.xpRate carry no axis),
            // then strip exactly one suffix (combat.power.omni -> combat.power).
            string family;
            if (familyRead.ContainsKey(channel)) family = channel;
            else
            {
                var dot = channel.LastIndexOf('.');
                var stripped = dot > 0 ? channel[..dot] : channel;
                Assert.True(familyRead.ContainsKey(stripped),
                    $"edge channel '{channel}' has no familyRead row under either its whole id or '{stripped}'");
                family = stripped;
            }

            // EffectiveKMilli's rule, restated: recovery wins, then mitigation, else raw. Both are
            // StartsWith over the channel, not over the family.
            long effective;
            string dial;
            if (recoveryFamilies.Any(f => channel.StartsWith(f, StringComparison.Ordinal)))
            {
                effective = checked(kMilli * recoveryScale) / 1000;
                dial = "recovery";
            }
            else if (mitigationFamilies.Any(f => channel.StartsWith(f, StringComparison.Ordinal)))
            {
                effective = checked(kMilli * mitigationScale) / 1000;
                dial = "mitigation";
            }
            else
            {
                effective = kMilli;
                dial = "none";
            }

            edges.Add(new RawEdge(channel, source, kMilli, familyRead[family], effective, dial));
        }

        return (edges, contestSpanMilli, contestGamma, magnitudeGamma);
    }

    static PowerLadder Ladder() => new(PowerTuningHub.Tuning);
    static DerivedStatRegistry Registry() => DerivedStatRegistry.CreateDefault();
    static AptitudeTuning Shipped() => AptitudeTuningLoader.Parse(ShippedJson());

    /// <summary>Fund exactly one aptitude, so its share is 1.0 and every OTHER aptitude's edges are
    /// correctly absent. Twelve of these cover all 486 edges with no share arithmetic in the way.</summary>
    static AptitudeAllocation SoleAllocation(string aptitudeId) =>
        AptitudeAllocation.Single(AllocationScope.Commander, aptitudeId, 100);

    // ── 1. the matrix itself ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The test the owner asked for.</b> Twelve aptitudes × their declared edges = all 486, each
    /// resolved and compared against a value computed here from the raw file, at two different Θ so a
    /// magnitude edge misread as a contest edge (or the reverse) cannot pass.
    ///
    /// <para>At share = 1.0, <c>share^γ = 1</c> exactly, so the expected magnitude collapses to
    /// <c>round(kEff · P(Θ) / 1000)</c> and the expected contest to <c>kEff · span / 10⁶</c> — plain
    /// arithmetic stated here, not a second call into <see cref="AptitudeReadFunctions"/>.</para>
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(74)]
    public void Every_one_of_the_486_shipped_edges_resolves_to_its_independently_computed_value(int theta)
    {
        var (raw, spanMilli, _, _) = ParseIndependently();
        var tuning = Shipped();
        var registry = Registry();
        var pTheta = Ladder().Value(theta);

        var checkedEdges = 0;
        var failures = new List<string>();

        foreach (var aptitude in AptitudeCatalog.All.Select(a => a.Id))
        {
            var mine = raw.Where(e => e.Source == aptitude).ToList();
            Assert.True(mine.Count > 0, $"aptitude '{aptitude}' sources no edge in the shipped file");

            var mods = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), theta, registry);

            // Exactly this aptitude's edges fired — no more, no fewer. A dropped edge is the defect
            // this whole file exists to catch, and it is invisible if we only spot-check values.
            Assert.Equal(mine.Count, mods.Count);

            foreach (var edge in mine)
            {
                var mod = mods.SingleOrDefault(m => m.ChannelId == edge.Channel && m.SourceId == $"aptitude.{aptitude}");
                if (mod is null)
                {
                    failures.Add($"{aptitude} -> {edge.Channel}: no modifier emitted");
                    continue;
                }

                double expected = edge.Mode == "magnitude"
                    // round-half-away-from-zero, matching the resolver's single documented rounding step
                    ? (double)(long)Math.Round((decimal)edge.EffectiveKMilli * pTheta / 1000m, MidpointRounding.AwayFromZero)
                    : edge.EffectiveKMilli / 1000.0 * (spanMilli / 1000.0);

                if (Math.Abs(mod.Value - expected) > 1e-6)
                    failures.Add($"{aptitude} -> {edge.Channel} [{edge.Mode}, dial={edge.DialApplied}, kEff={edge.EffectiveKMilli}]: expected {expected}, got {mod.Value}");

                checkedEdges++;
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} edge(s) disagreed with the independently computed value at Theta={theta}:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Take(25)));

        // The whole shipped set was actually exercised — not a subset that silently shrank.
        Assert.Equal(486, raw.Count);
        Assert.Equal(486, checkedEdges);
    }

    /// <summary>All twelve aptitude ids are computed from the file and cross-checked against the
    /// catalog, never a roster typed into this test — a thirteenth aptitude, a renamed one, or one that
    /// loses its last edge all turn this red.</summary>
    [Fact]
    public void All_twelve_aptitudes_source_at_least_one_edge_computed_from_the_file_not_a_hardcoded_roster()
    {
        var (raw, _, _, _) = ParseIndependently();

        var sourcesInFile = raw.Select(e => e.Source).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        var catalogIds = AptitudeCatalog.All.Select(a => a.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.Equal(catalogIds, sourcesInFile);
        Assert.Equal(AptitudeCatalog.Count, sourcesInFile.Count);
        Assert.Equal(12, sourcesInFile.Count); // AptitudeCatalog.Count is 3 postures x 4; pinned here too

        // Every source is a real catalog id — catches a typo'd source that would otherwise just never
        // be funded and so never resolve, silently costing the player that edge forever.
        foreach (var s in sourcesInFile)
            Assert.True(AptitudeCatalog.IsAptitudeId(s), $"edge source '{s}' is not a real aptitude id");
    }

    // ── 2. the read function is selected per edge, and it matters ────────────────────────────────

    /// <summary>
    /// The falsifier for mode selection: <b>magnitude edges scale with P(Θ), contest edges are Θ-free</b>
    /// (PS-3). Asserted for <i>every</i> edge, not a sample — so one family misclassified in
    /// <c>familyRead</c>, or a channel whose axis-suffix strip picks the wrong row, fails here.
    /// </summary>
    [Fact]
    public void Magnitude_edges_scale_with_pTheta_and_contest_edges_do_not_across_the_whole_shipped_set()
    {
        var (raw, _, _, _) = ParseIndependently();
        var tuning = Shipped();
        var registry = Registry();

        const int lowTheta = 10;
        const int highTheta = 74;
        var pLow = Ladder().Value(lowTheta);
        var pHigh = Ladder().Value(highTheta);
        Assert.True(pHigh > pLow, "the two Theta values must produce different P(Theta) or this test proves nothing");

        var magnitudeSeen = 0;
        var contestSeen = 0;

        foreach (var aptitude in AptitudeCatalog.All.Select(a => a.Id))
        {
            var low = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), lowTheta, registry);
            var high = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), highTheta, registry);

            foreach (var edge in raw.Where(e => e.Source == aptitude))
            {
                var lo = low.Single(m => m.ChannelId == edge.Channel).Value;
                var hi = high.Single(m => m.ChannelId == edge.Channel).Value;

                if (edge.Mode == "contest")
                {
                    Assert.True(lo == hi,
                        $"contest edge {aptitude} -> {edge.Channel} moved with Theta ({lo} -> {hi}) — it must be Theta-free (PS-3)");
                    contestSeen++;
                }
                else
                {
                    // A zero coefficient would be Theta-invariant and would falsely look like a contest
                    // edge; the shipped file has none, and this asserts that stays true.
                    Assert.True(edge.EffectiveKMilli > 0,
                        $"magnitude edge {aptitude} -> {edge.Channel} has a zero effective coefficient — it cannot demonstrate Theta scaling");
                    Assert.True(hi > lo,
                        $"magnitude edge {aptitude} -> {edge.Channel} did not grow with Theta ({lo} -> {hi})");
                    // Exact proportionality, not merely "bigger": the ratio must track P(Theta)'s own,
                    // within the one-unit rounding each value carries.
                    var expectedHigh = (double)(long)Math.Round((decimal)edge.EffectiveKMilli * pHigh / 1000m, MidpointRounding.AwayFromZero);
                    Assert.Equal(expectedHigh, hi, 6);
                    magnitudeSeen++;
                }
            }
        }

        // Both branches were genuinely exercised — a file that drifted to all-contest or all-magnitude
        // would otherwise pass this test vacuously.
        Assert.True(magnitudeSeen > 0 && contestSeen > 0,
            $"expected both read modes in the shipped set (magnitude={magnitudeSeen}, contest={contestSeen})");
        Assert.Equal(486, magnitudeSeen + contestSeen);
    }

    /// <summary>The <c>share^γ</c> branch, which share = 1.0 deliberately bypasses everywhere else in
    /// this file. Two aptitudes funded equally → share 0.5 each → every value is exactly half its
    /// sole-allocation value at γ = 1.0, within the per-edge rounding.</summary>
    [Fact]
    public void Fractional_shares_hold_too()
    {
        var (raw, _, _, magnitudeGamma) = ParseIndependently();
        Assert.Equal(1000, magnitudeGamma); // gamma = 1.0 in the shipped file; halving share halves the value

        var tuning = Shipped();
        var registry = Registry();
        const int theta = 74;

        var soleMight = AptitudeResolver.Resolve(SoleAllocation("Might"), tuning, Ladder(), theta, registry);

        var split = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100)
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 100);
        Assert.Equal(0.5, split.Share("Might"), 9);

        var splitMods = AptitudeResolver.Resolve(split, tuning, Ladder(), theta, registry);

        foreach (var edge in raw.Where(e => e.Source == "Might"))
        {
            var full = soleMight.Single(m => m.ChannelId == edge.Channel).Value;
            var half = splitMods.Single(m => m.ChannelId == edge.Channel && m.SourceId == "aptitude.Might").Value;

            // Allow one unit of slack: each side rounds once, independently.
            Assert.True(Math.Abs(half - full / 2.0) <= 1.0,
                $"Might -> {edge.Channel}: half-share value {half} is not ~half of the sole-share value {full}");
        }

        // Vigor's own edges are present too — a split allocation must fund BOTH sides, not just the
        // first one the resolver happens to walk.
        Assert.Contains(splitMods, m => m.SourceId == "aptitude.Vigor");
    }

    // ── 3. the two dials reach exactly the declared families ─────────────────────────────────────

    /// <summary>
    /// <c>recovery.scaleMilli</c> (374) and <c>mitigation.scaleMilli</c> (300) are the termination-
    /// invariant dials. The regression they guard is real and already happened once: the resolver
    /// shipped without the recovery dial and every recovery edge silently read its raw, undamped
    /// coefficient (class-system-todo.md, "Regression found and fixed", 2026-08-27). This asserts each
    /// dial reaches exactly the channels its own <c>families</c> list names — no more, no fewer — and
    /// that <b>recovery wins</b> where the two overlap.
    /// </summary>
    [Fact]
    public void Recovery_and_mitigation_dials_apply_to_exactly_their_declared_families()
    {
        var (raw, _, _, _) = ParseIndependently();
        var tuning = Shipped();
        var registry = Registry();
        const int theta = 74;
        var pTheta = Ladder().Value(theta);

        Assert.Equal(374, tuning.Recovery.ScaleMilli);
        Assert.Equal(300, tuning.Mitigation.ScaleMilli);

        var damped = raw.Where(e => e.DialApplied != "none").ToList();
        Assert.True(damped.Count > 0, "no edge takes either dial — the dials would then be dead config");

        foreach (var edge in damped)
        {
            // The dial genuinely changed the coefficient (a scale of 1000 would be a no-op and would
            // make this test pass while proving nothing).
            Assert.True(edge.EffectiveKMilli < edge.KMilli,
                $"{edge.Channel} is on the {edge.DialApplied} dial but its effective coefficient did not shrink");

            var mods = AptitudeResolver.Resolve(SoleAllocation(edge.Source), tuning, Ladder(), theta, registry);
            var actual = mods.Single(m => m.ChannelId == edge.Channel).Value;

            // The undamped value the resolver would emit if the dial were dropped — the exact
            // regression shape. It must NOT equal what we got.
            double undamped = edge.Mode == "magnitude"
                ? (double)(long)Math.Round((decimal)edge.KMilli * pTheta / 1000m, MidpointRounding.AwayFromZero)
                : edge.KMilli / 1000.0 * (tuning.Read.Contest.SpanPointsMilli / 1000.0);

            Assert.True(Math.Abs(actual - undamped) > 1e-9,
                $"{edge.Source} -> {edge.Channel}: resolved to the UNDAMPED value {undamped} — the {edge.DialApplied} dial was dropped");
        }

        // Recovery wins where both lists could match. Stated as a property of the shipped data so a
        // future overlap does not silently pick the other dial.
        foreach (var edge in raw.Where(e => e.DialApplied == "recovery"))
            Assert.True(tuning.Recovery.Families.Any(f => edge.Channel.StartsWith(f, StringComparison.Ordinal)),
                $"{edge.Channel} was classified as a recovery edge but matches no recovery family");
    }

    // ── 4. the RPG / PvZ boundary, as a test rather than as prose ────────────────────────────────

    /// <summary>
    /// <b>The owner's "include rpg layer stats and pvz engine stats" question, made mechanical.</b>
    ///
    /// <para><c>ActorHub.MergeAppliedCombat</c> reads exactly five derived channels when building the
    /// <c>AppliedCombat</c> view the injector's <c>EntityStatWriter</c> consumes — the five
    /// <c>progression.bonus.*</c> ids. Everything else an aptitude funds resolves and composes in the
    /// RPG layer and is read there (<c>OverlayCombatCalculator</c>, <c>ResistanceEvaluator</c>,
    /// <c>ActorResourcePools</c>, the skill/action layer) without ever touching a Unity field.</para>
    ///
    /// <para>So this asserts the split <b>behaviourally</b>, by driving <c>ActorHub.Resolve</c> once per
    /// distinct channel the shipped edges target and observing whether <c>AppliedCombat</c> actually
    /// diverges from <c>RuntimePrimary</c>. An earlier draft of this test hardcoded the five ids and
    /// claimed a sixth bridge channel would surface here — <b>it would not have</b>, because a
    /// hardcoded list agrees with itself no matter what <c>MergeAppliedCombat</c> does. Corrected
    /// during review, before this file was reported as closing anything.</para>
    /// </summary>
    [Fact]
    public void Progression_bonus_is_the_only_edge_family_that_can_reach_a_pvz_unity_field()
    {
        var (raw, _, _, _) = ParseIndependently();

        // The five ids the bridge is DOCUMENTED to carry. Below, every one is confirmed to really
        // bridge, and every other channel is confirmed not to — so this list is an expectation under
        // test, never the thing doing the deciding.
        var bridgeChannels = new HashSet<string>(StringComparer.Ordinal)
        {
            DerivedStatChannels.ProgressionBonusMaxHp,
            DerivedStatChannels.ProgressionBonusAtk,
            DerivedStatChannels.ProgressionBonusDefense,
            DerivedStatChannels.ProgressionBonusArm1,
            DerivedStatChannels.ProgressionBonusArm2,
        };

        // ── the behavioural half: ask ActorHub, do not assume ────────────────────────────────────
        // MergeAppliedCombat returns the primary INSTANCE unchanged when no bridge channel carries a
        // value, so reference identity is an exact, allocation-free reading of "did this channel reach
        // the Writer input?".
        var actuallyBridged = new List<string>();
        foreach (var channel in raw.Select(e => e.Channel).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal))
        {
            var hub = new FusionRpg.Core.Stats.Derived.ActorHub(StatSystemBootstrap.CreateDefault());
            hub.Register(new SingleChannelSubsystem(channel, 1000));

            var result = hub.Resolve(new StatContext { Side = StatSide.Plant, TypeId = 1, EntityKey = "0xTEST" });

            if (!ReferenceEquals(result.AppliedCombat, result.RuntimePrimary))
                actuallyBridged.Add(channel);
        }

        // The measured set and the documented set are the same set. A sixth bridge channel fed by any
        // aptitude edge, or one of these five quietly dropped from MergeAppliedCombat, fails here.
        Assert.Equal(
            bridgeChannels.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            actuallyBridged.OrderBy(s => s, StringComparer.Ordinal).ToList());

        var pvzReaching = raw.Where(e => bridgeChannels.Contains(e.Channel)).ToList();
        var rpgOnly = raw.Where(e => !bridgeChannels.Contains(e.Channel)).ToList();

        Assert.True(pvzReaching.Count > 0,
            "no aptitude edge reaches the progression.bonus.* bridge — allocation could not change a plant's stats at all");

        // Every PvZ-reaching edge is under progression.bonus, and every progression.bonus edge is a
        // bridge channel. Both directions, so neither a stray family nor an orphan bridge id slips by.
        foreach (var e in pvzReaching)
            Assert.StartsWith("progression.bonus.", e.Channel, StringComparison.Ordinal);
        foreach (var e in raw.Where(e => e.Channel.StartsWith("progression.bonus.", StringComparison.Ordinal)))
            Assert.Contains(e.Channel, bridgeChannels);

        // Not a single RPG-layer edge is a bridge channel — the partition is total.
        Assert.Equal(raw.Count, pvzReaching.Count + rpgOnly.Count);
        Assert.DoesNotContain(rpgOnly, e => bridgeChannels.Contains(e.Channel));

        // The families the RPG-layer remainder falls into, computed rather than assumed. This is the
        // categorisation the 2026-08-30 audit reported as prose; asserting it keeps the answer true.
        var families = rpgOnly.Select(e => e.Channel.Split('.')[0]).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "combat", "move", "progression", "resource", "skill", "status" }, families);
    }

    // ── 5. overflow — throws, never wraps ────────────────────────────────────────────────────────

    /// <summary>CLAUDE.md's numeric-overflow rule applied to the real edge set: at a Θ far past
    /// anything play reaches, every shipped edge either produces an exact <c>long</c> or throws. What
    /// it must never do is wrap into a negative or truncated magnitude — a silently wrong stat is the
    /// failure mode the whole long-magnitude rule exists to prevent.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(100_000)]
    public void No_shipped_edge_wraps_at_extreme_theta_it_resolves_exactly_or_throws(int theta)
    {
        var tuning = Shipped();
        var registry = Registry();

        foreach (var aptitude in AptitudeCatalog.All.Select(a => a.Id))
        {
            IReadOnlyList<DerivedModifier> mods;
            try
            {
                mods = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), theta, registry);
            }
            catch (OverflowException)
            {
                continue; // throwing is the correct, documented outcome — never wrapping
            }

            foreach (var m in mods)
                Assert.True(m.Value >= 0,
                    $"{aptitude} -> {m.ChannelId} resolved NEGATIVE ({m.Value}) at Theta={theta} — a wrapped magnitude");
        }
    }

    // ── 6. the compose op is a property of the target channel, not the read mode ─────────────────

    /// <summary>The resolver picks <c>Increased</c> for a <c>SumIncreased</c> channel and <c>Flat</c>
    /// otherwise, and those are independent axes from the read mode. Checked per edge against the
    /// registry, so a channel whose compose kind changes without its consumers being revisited fails
    /// here rather than quietly composing the wrong way.</summary>
    [Fact]
    public void Every_edges_compose_op_matches_its_target_channels_registered_compose_kind()
    {
        var (raw, _, _, _) = ParseIndependently();
        var tuning = Shipped();
        var registry = Registry();

        foreach (var aptitude in AptitudeCatalog.All.Select(a => a.Id))
        {
            var mods = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), theta: 74, registry);

            foreach (var edge in raw.Where(e => e.Source == aptitude))
            {
                Assert.True(registry.TryResolveChannel(edge.Channel, out var def),
                    $"{edge.Channel} is not registered — AptitudeTuningTests.EveryEdgeChannel_isRegistered_inDerivedStatRegistry owns this, but the resolve path must agree");

                var expected = def.Compose == DerivedComposeKind.SumIncreased
                    ? DerivedModifierOp.Increased
                    : DerivedModifierOp.Flat;

                Assert.Equal(expected, mods.Single(m => m.ChannelId == edge.Channel).Op);
            }
        }
    }

    // ── 7. the battle seam sees the same matrix ──────────────────────────────────────────────────

    /// <summary>
    /// <c>ProveAptitudeJsonEmitTests</c> proves the two engines agree for <b>one</b> edge
    /// (<c>might → combat.power.omni</c>). This widens that to the whole shipped set: for all twelve
    /// aptitudes, <see cref="AptitudeResolver.ResolveForBattle"/> emits the same channels as the
    /// overlay path, with amounts that match after the battle side's documented narrowing to
    /// <c>long</c>.
    /// </summary>
    [Fact]
    public void The_battle_seam_emits_the_same_486_edges_as_the_overlay_seam()
    {
        var tuning = Shipped();
        var registry = Registry();
        const int theta = 74;
        var total = 0;

        foreach (var aptitude in AptitudeCatalog.All.Select(a => a.Id))
        {
            var overlay = AptitudeResolver.Resolve(SoleAllocation(aptitude), tuning, Ladder(), theta, registry);
            var battle = AptitudeResolver.ResolveForBattle(SoleAllocation(aptitude), tuning, Ladder(), theta, registry);

            Assert.Equal(overlay.Count, battle.Count);
            Assert.Equal(
                overlay.Select(m => m.ChannelId).OrderBy(s => s, StringComparer.Ordinal),
                battle.Select(m => m.ChannelId).OrderBy(s => s, StringComparer.Ordinal));

            foreach (var b in battle)
            {
                var o = overlay.Single(m => m.ChannelId == b.ChannelId);
                var narrowed = (long)Math.Round(o.Value, MidpointRounding.AwayFromZero);
                Assert.True(b.Amount == narrowed,
                    $"{aptitude} -> {b.ChannelId}: battle {b.Amount} vs overlay {o.Value} (narrowed {narrowed})");
            }

            total += battle.Count;
        }

        Assert.Equal(486, total);
    }

    // ── 8. the reader-less edges, cross-checked from a real resolve ──────────────────────────────

    /// <summary>
    /// TC1's last acceptance box. <c>ReaderCensusTests</c> proves the reader-less count by running
    /// <c>scripts/audit-reader-census.py</c> — a <b>static scan</b> of <c>src/FusionRpg.Core</c> for
    /// reader call sites. This is the same claim approached from the opposite side: those 18 edges are
    /// driven through a real <see cref="AptitudeResolver"/> call and shown to produce <b>live, nonzero
    /// values</b>.
    ///
    /// <para><b>Why that matters and is not a duplicate.</b> A static scan can tell you nothing reads
    /// <c>skill.cooldown</c>. It cannot tell you whether the player's points are nonetheless being
    /// <i>spent</i> on it. They are — these edges resolve and compose exactly like every other, they
    /// simply reach no consumer. That is the honest shape of the gap (<c>_meta.measurable</c>:
    /// coefficients here are <i>"DESIGNED, not measured, and must not be reported as balanced"</i>),
    /// and pinning it stops the count from drifting silently in either direction: a family quietly
    /// gaining a reader, or an edge quietly being added to one that has none.</para>
    /// </summary>
    [Fact]
    public void The_eighteen_reader_less_edges_still_resolve_and_the_count_matches_meta_measurable()
    {
        var (raw, _, _, _) = ParseIndependently();

        // The six families audit-reader-census.py reports with no shipped reader at all. Named here so
        // that a family GAINING a reader (good news) also turns this red and forces the number to be
        // re-stated rather than silently diverging from _meta.measurable.
        var readerLessFamilies = new[]
        {
            "resource.efficiency", "skill.cooldown", "skill.effectiveness",
            "move.range", "progression.xpRate", "progression.breakthroughSuccess",
        };

        var readerLess = raw.Where(e => readerLessFamilies.Any(f => e.Channel.StartsWith(f, StringComparison.Ordinal))).ToList();

        // The count the shipped file's own prose claims, parsed from it rather than retyped.
        using var doc = JsonDocument.Parse(ShippedJson());
        var measurable = doc.RootElement.GetProperty("_meta").GetProperty("measurable").GetString()!;
        var m = System.Text.RegularExpressions.Regex.Match(measurable, @"(\d+)\s+of\s+(\d+)\s+edges");
        Assert.True(m.Success, "_meta.measurable no longer states its reader-less edge count as 'N of M edges' — update this cross-check with it");

        Assert.Equal(int.Parse(m.Groups[1].Value), readerLess.Count);
        Assert.Equal(int.Parse(m.Groups[2].Value), raw.Count);
        Assert.Equal(18, readerLess.Count);

        // ...and every one of them genuinely resolves. Reader-less is NOT the same as inert: the points
        // are spent, the value is composed, and nothing downstream consumes it.
        var tuning = Shipped();
        var registry = Registry();
        foreach (var group in readerLess.GroupBy(e => e.Source))
        {
            var mods = AptitudeResolver.Resolve(SoleAllocation(group.Key), tuning, Ladder(), theta: 74, registry);
            foreach (var edge in group)
            {
                var mod = mods.SingleOrDefault(x => x.ChannelId == edge.Channel);
                Assert.True(mod is not null, $"reader-less edge {edge.Source} -> {edge.Channel} did not resolve at all");
                Assert.True(mod!.Value > 0, $"reader-less edge {edge.Source} -> {edge.Channel} resolved to {mod.Value} — expected a real, live value");
            }
        }
    }

    /// <summary>Contributes one flat value to exactly one channel — the probe
    /// <see cref="Progression_bonus_is_the_only_edge_family_that_can_reach_a_pvz_unity_field"/> uses to
    /// ask ActorHub which channels actually cross into the Writer input.</summary>
    sealed class SingleChannelSubsystem : IActorStatSubsystem
    {
        readonly string _channel;
        readonly double _value;
        public SingleChannelSubsystem(string channel, double value) { _channel = channel; _value = value; }
        public string SubsystemId => "test.single-channel";
        public int Order => 999;
        public void ContributeDerived(StatContext ctx, ICollection<DerivedModifier> mods) =>
            mods.Add(new DerivedModifier(_channel, DerivedModifierOp.Flat, _value, SourceId: "test"));
    }
}
