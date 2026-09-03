using System.Text.RegularExpressions;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ActorHub;

/// <summary>
/// spec-unbuilt-reconcile.md §5 (T6.1) — "the check that would have caught F3 and F5 automatically."
/// Converts the one-time reconcile sweep into a standing guard: every backtick-wrapped
/// combat./status./resource./progression. token in docs/architecture/**/*.md either names a real
/// registered channel (exact id, or a family-level prefix of one — `combat.power` is a legitimate
/// shorthand for `combat.power.omni`/`combat.power.fire`/...), or sits under a heading whose nearby
/// text says PROPOSED. Everything else fails, naming the file and the token.
///
/// The same lexical prefix is shared by at least two OTHER, unrelated namespaces this repo also uses
/// — atom kinds (spec-atom-kind-registry.md's closed vocabulary: `resource.delta`, `resource.economy`,
/// `status.apply`, `status.clear`) and event/trigger names (`combat.hit`, `combat.hitland`, decisions.md
/// / effect-funnel.md / atom-catalog-ssot.md). <see cref="KnownNonChannelTokens"/> is the curated,
/// individually-verified list of that collision — not a loophole, the honest alternative to a false
/// positive on every doc that legitimately talks about atom kinds or Unity re-entry.
/// </summary>
public class SpecChannelClaimTests
{
    static readonly Regex TokenPattern =
        new(@"`((?:combat|status|resource|progression)\.[a-zA-Z0-9_.{}]+)`", RegexOptions.Compiled);

    static readonly Regex HeadingPattern = new(@"^#{1,6}\s.*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Verified individually against the code before being added — see the file header.
    /// `status.apply`/`status.apply.duration`/`status.apply.target`/`status.clear`/`status.spread` are
    /// atom-kind/mechanism names (spec-atom-kind-registry.md); `resource.delta`/`resource.economy` are
    /// atom kinds too; `combat.hit`/`combat.hitland` are Unity re-entry / event names, not channels
    /// (effect-funnel.md, atom-catalog-ssot.md:140 — `combat.hitland` is explicitly "not shipped");
    /// `status.v2.json` is a tuning file name; `status.WithdrawEntity` is a method name;
    /// `status.resistance` is English prose ("the status resistance axis"), not `status.resist`;
    /// `status.probability` is explicitly documented as having no channel equivalent (spec-unbuilt-
    /// reconcile.md F5); `combat.something` is spec-stat-taxonomy.md's own abstract placeholder example.
    /// `combat.power.pierce` and `combat.power.overflow` are channel-SHAPED ids that authored item
    /// families name and the registry does NOT contain — `spec-kind-value-guard.md` §5.1 tables them as
    /// a defect, with the decision to scope E29's acceptance rather than mint 14 channels. Same category
    /// as `combat.hitland` above: named in a doc precisely BECAUSE it is not shipped. Added 2026-09-03;
    /// **remove these two if the families are ever registered**, so the guard resumes covering them.
    /// `combat.timer` is a host EVENT kind, verified in `EffectEventAdapterCore.TryMap` where it maps
    /// (alongside `effect.timer`) to the `OnTimer` atom trigger — the same category as `combat.hit`
    /// directly above, added 2026-09-03 when Wave 8's trigger-vocabulary audit named it in prose.
    /// `progression.tierPower` is a locked, shipped FORMULA name (`progression.power ×
    /// progression.realm`, actor-hub-ssot.md:121) computed where needed, not itself a stored channel.</summary>
    static readonly HashSet<string> KnownNonChannelTokens = new(StringComparer.Ordinal)
    {
        "combat.hit", "combat.hitland", "combat.something", "combat.timer",
        "combat.power.pierce", "combat.power.overflow",
        "resource.delta", "resource.economy",
        "status.apply", "status.apply.duration", "status.apply.target", "status.clear", "status.spread",
        "status.v2.json", "status.WithdrawEntity", "status.resistance", "status.probability",
        "progression.tierPower"
    };

    [Fact]
    public void NoSpecClaimsAnUnregisteredChannel()
    {
        var registry = DerivedStatRegistry.CreateDefault();
        var registeredIds = registry.AllRegistered.Select(d => d.ChannelId).ToHashSet(StringComparer.Ordinal);
        var docsRoot = Path.Combine(FindRepoRoot(), "docs", "architecture");
        var failures = new List<string>();

        foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var (token, index) in ExtractTokens(text))
            {
                if (Resolves(token, registeredIds, registry)) continue;
                if (IsNearProposedHeading(text, index)) continue;
                failures.Add($"{Path.GetRelativePath(docsRoot, file)}: `{token}`");
            }
        }

        Assert.True(failures.Count == 0,
            "Unregistered channel claim(s) in docs/architecture/** — resolve, mark PROPOSED, or add to " +
            "KnownNonChannelTokens with a verified reason:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void NoSpecClaimsAnUnregisteredChannel_failsOnAPlantedDrift()
    {
        // A guard never proven to fail is not evidence (established precedent: guard-stat-pairs.ps1's
        // planted violations, ElementHubDocDriftTests' own _failsOnAPlantedDrift sibling). Runs the REAL
        // extraction + resolution against synthetic text naming a channel that will never exist.
        var registry = DerivedStatRegistry.CreateDefault();
        var registeredIds = registry.AllRegistered.Select(d => d.ChannelId).ToHashSet(StringComparer.Ordinal);
        const string plantedDrift = """
            # Some doc

            ## 1. A section, still just an idea

            This claims `combat.totallyInventedChannel.omni` exists, which it never will.
            """;

        var found = ExtractTokens(plantedDrift).Select(t => t.Token).ToList();
        Assert.Contains("combat.totallyInventedChannel.omni", found);
        Assert.False(Resolves("combat.totallyInventedChannel.omni", registeredIds, registry));
        Assert.False(IsNearProposedHeading(plantedDrift, plantedDrift.IndexOf("combat.totallyInventedChannel", StringComparison.Ordinal)));

        const string sameClaimButProposed = """
            # Some doc

            ## 1. A future idea (PROPOSED)

            This claims `combat.totallyInventedChannel.omni` exists, which it never will.
            """;
        Assert.True(IsNearProposedHeading(sameClaimButProposed,
            sameClaimButProposed.IndexOf("combat.totallyInventedChannel", StringComparison.Ordinal)));
    }

    static IEnumerable<(string Token, int Index)> ExtractTokens(string text)
    {
        foreach (System.Text.RegularExpressions.Match m in TokenPattern.Matches(text))
        {
            var token = m.Groups[1].Value.TrimEnd('.');
            if (token.Length == 0) continue;
            yield return (token, m.Index);
        }
    }

    static bool Resolves(string token, HashSet<string> registeredIds, DerivedStatRegistry registry)
    {
        if (token.Contains('{')) return true; // template placeholder ({id}, {element}, ...) -- a pattern, not a concrete claim
        if (KnownNonChannelTokens.Contains(token)) return true;
        if (registeredIds.Contains(token)) return true;
        // Family-level shorthand: `combat.power` legitimately stands for `combat.power.omni` / `.fire` / ...
        if (registeredIds.Any(id => id.StartsWith(token + ".", StringComparison.Ordinal))) return true;
        // Some status families are OPEN PREFIXES resolved sparsely at read time
        // (DerivedStatRegistry.TryResolveChannel -- status.power./resist./duration(Reduction)./
        // intensity(Reduction)./immune(Reduction)./expose.), never statically pre-registered, so they
        // never appear in AllRegistered at all. A complete id resolves directly; a bare family mention
        // (`status.expose`) is checked via a plausible `.omni` member instead.
        if (registry.TryResolveChannel(token, out _)) return true;
        return registry.TryResolveChannel(token + ".omni", out _);
    }

    static bool IsNearProposedHeading(string text, int matchIndex)
    {
        var headingEnd = -1;
        var headingText = "";
        foreach (System.Text.RegularExpressions.Match h in HeadingPattern.Matches(text))
        {
            if (h.Index > matchIndex) break;
            headingEnd = h.Index + h.Length;
            headingText = h.Value;
        }
        if (headingEnd < 0) return false;
        if (headingText.Contains("PROPOSED", StringComparison.OrdinalIgnoreCase)) return true;

        // Also check the prose immediately under the heading (a "**Status:** PROPOSED" line, or a
        // "(PROPOSED)" note in the paragraph right after it) -- up to the next heading or 400 chars,
        // whichever is nearer, so a PROPOSED marker anywhere later in a long section cannot exempt an
        // unrelated claim near the top of that same section.
        var windowEnd = Math.Min(text.Length, headingEnd + 400);
        // Cap the window at the next heading too, so a PROPOSED marker later in a long section cannot
        // exempt an unrelated claim near the top of that same section.
        foreach (System.Text.RegularExpressions.Match h in HeadingPattern.Matches(text))
        {
            if (h.Index > headingEnd) { windowEnd = Math.Min(windowEnd, h.Index); break; }
        }
        var window = text[headingEnd..windowEnd];
        return window.Contains("PROPOSED", StringComparison.OrdinalIgnoreCase);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
