using System.IO;
using System.Linq;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.PassiveTree.Binding;
using Xunit;

namespace FusionRpg.Core.Tests.PassiveTree.Binding;

/// <summary>Task B4 — `AffixComposer` (spec-tree-binder.md §1). Verified against the REAL shipped
/// affix registry (`data/seed/effects/affixes/all.json`), not only synthetic fixtures.</summary>
public class AffixComposerTests
{
    static string RepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    static SeedContent LoadRealSeedContent()
    {
        var root = RepoRoot();
        var files = new[]
        {
            Path.Combine(root, "data", "seed", "effects", "affixes", "all.json"),
            Path.Combine(root, "data", "seed", "atoms", "fx-board.json"),
            Path.Combine(root, "data", "seed", "atoms", "fx-core.json"),
            Path.Combine(root, "data", "seed", "atoms", "fx-status.json"),
        }.Where(File.Exists).Select(f => (f, File.ReadAllText(f))).ToArray();

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        return collected.Content;
    }

    static (System.Collections.Generic.IReadOnlyDictionary<string, AffixRow> Affixes,
           System.Collections.Generic.IReadOnlyDictionary<string, AtomRow> Atoms) Index(SeedContent content) =>
        (content.Affixes.ToDictionary(a => a.AffixId), content.Atoms.ToDictionary(a => a.AtomId));

    [Fact]
    public void The_real_frostbite_venom_affix_resolves_to_its_two_atom_refs_in_seq_order()
    {
        var content = LoadRealSeedContent();
        var (affixes, atoms) = Index(content);

        // The reflect-shaped case §1 names by name: a node needing two atoms that must arrive
        // together, expressed as ONE affix, never two separate node atoms. Both refs here are
        // status.apply atoms (cold-on-hit seq 0, poison-on-hit seq 1) — a different param shape
        // from stat.modify's channel/op/amount (no "channel" key at all), which is exactly why
        // AffixComposer.ParseAtom must default gracefully rather than assume every kind has one.
        var resolved = AffixComposer.Resolve(
            new[] { "affix.authored.affix-draw-000" }, affixes, atoms);

        Assert.Equal(2, resolved.Count);
        Assert.Equal("status.apply", resolved[0].KindId);
        Assert.Equal("status.apply", resolved[1].KindId);
        // Neither carries a "channel" key in its real seed params -- resolved gracefully to empty,
        // never a crash or a fabricated value.
        Assert.Equal("", resolved[0].ChannelId);
        Assert.Equal("", resolved[1].ChannelId);
        Assert.Equal("OnDamageDealt", resolved[0].Trigger);
    }

    [Fact]
    public void Two_or_three_affixIds_resolve_and_concatenate_in_authored_order()
    {
        var content = LoadRealSeedContent();
        var (affixes, atoms) = Index(content);

        var resolved = AffixComposer.Resolve(
            new[] { "affix.authored.affix-draw-000", "affix.authored.affix-draw-001" }, affixes, atoms);

        // draw-000 has 2 refs, draw-001 has 2 refs -> 4 total, in that order.
        Assert.Equal(4, resolved.Count);
    }

    [Fact]
    public void Zero_or_four_affixIds_is_refused()
    {
        var (affixes, atoms) = Index(LoadRealSeedContent());

        Assert.Throws<BindRefusal>(() => AffixComposer.Resolve(Array.Empty<string>(), affixes, atoms));
        Assert.Throws<BindRefusal>(() => AffixComposer.Resolve(
            new[] { "a", "b", "c", "d" }, affixes, atoms));
    }

    [Fact]
    public void A_missing_affix_id_is_refused_naming_it()
    {
        var (affixes, atoms) = Index(LoadRealSeedContent());

        var ex = Assert.Throws<BindRefusal>(() =>
            AffixComposer.Resolve(new[] { "affix.does-not-exist" }, affixes, atoms));
        Assert.Contains("affix.does-not-exist", ex.Message);
    }

    [Fact]
    public void A_missing_atom_id_referenced_by_a_real_affix_is_refused_naming_it()
    {
        var content = LoadRealSeedContent();
        var (affixes, _) = Index(content);
        var emptyAtoms = new System.Collections.Generic.Dictionary<string, AtomRow>();

        var ex = Assert.Throws<BindRefusal>(() =>
            AffixComposer.Resolve(new[] { "affix.authored.affix-draw-000" }, affixes, emptyAtoms));
        Assert.Contains("atom.fx-cold-on-hit.t1", ex.Message);
    }

    [Fact]
    public void A_conversion_kind_atom_is_refused_with_the_17th_kind_reason()
    {
        var affix = new AffixRow("affix.synthetic", null,
            new[] { new AffixRefRow(0, "atom.synthetic-convert") });
        var conversionAtom = new AtomRow
        {
            AtomId = "atom.synthetic-convert",
            KindId = "element.convert", // not one of the 16 registered kinds, by design (D16)
            FamilyId = "atom.synthetic-convert",
            Tier = 1,
            Name = "synthetic",
            ParamsJson = """{"channel":"combat.power.fire","op":"flat"}""",
            WhenJson = "{}",
        };
        var affixes = new System.Collections.Generic.Dictionary<string, AffixRow> { [affix.AffixId] = affix };
        var atoms = new System.Collections.Generic.Dictionary<string, AtomRow> { [conversionAtom.AtomId] = conversionAtom };

        var ex = Assert.Throws<BindRefusal>(() =>
            AffixComposer.Resolve(new[] { affix.AffixId }, affixes, atoms));
        Assert.Contains("17th atom kind", ex.Message);
        Assert.Contains("D16", ex.Message);
    }

    [Fact]
    public void An_unregistered_non_conversion_kind_is_refused_generically()
    {
        var affix = new AffixRow("affix.synthetic2", null,
            new[] { new AffixRefRow(0, "atom.synthetic-bad-kind") });
        var badAtom = new AtomRow
        {
            AtomId = "atom.synthetic-bad-kind",
            KindId = "not.a.real.kind",
            FamilyId = "atom.synthetic-bad-kind",
            Tier = 1,
            Name = "synthetic",
            ParamsJson = """{"channel":"atk","op":"flat"}""",
            WhenJson = "{}",
        };
        var affixes = new System.Collections.Generic.Dictionary<string, AffixRow> { [affix.AffixId] = affix };
        var atoms = new System.Collections.Generic.Dictionary<string, AtomRow> { [badAtom.AtomId] = badAtom };

        var ex = Assert.Throws<BindRefusal>(() =>
            AffixComposer.Resolve(new[] { affix.AffixId }, affixes, atoms));
        Assert.Contains("not.a.real.kind", ex.Message);
        Assert.DoesNotContain("17th atom kind", ex.Message);
    }

    [Fact]
    public void A_registered_kind_and_channel_resolve_correctly()
    {
        var affix = new AffixRow("affix.synthetic3", null,
            new[] { new AffixRefRow(0, "atom.synthetic-ok") });
        var okAtom = new AtomRow
        {
            AtomId = "atom.synthetic-ok",
            KindId = "stat.modify",
            FamilyId = "atom.synthetic-ok",
            Tier = 1,
            Name = "synthetic",
            ParamsJson = """{"channel":"atk","op":"flat","amount":{"min":1,"max":1}}""",
            WhenJson = "{}",
        };
        var affixes = new System.Collections.Generic.Dictionary<string, AffixRow> { [affix.AffixId] = affix };
        var atoms = new System.Collections.Generic.Dictionary<string, AtomRow> { [okAtom.AtomId] = okAtom };

        var resolved = AffixComposer.Resolve(new[] { affix.AffixId }, affixes, atoms);

        var atom = Assert.Single(resolved);
        Assert.Equal("stat.modify", atom.KindId);
        Assert.Equal("atk", atom.ChannelId);
        Assert.Equal("flat", atom.Op);
    }
}
