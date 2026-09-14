using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// D5: soul-ledger tail-trim + archive (the P4 deferral — expedition volume makes it real).
/// XP-ledger pattern: only rows already folded into the watermarked balance trim, so the
/// balance is byte-identical before and after (spec-soul-economy success criterion 4).
///
/// <para>This class's subject IS the filesystem archive: <c>TrimSoulLedgerTails</c> is an archive
/// entry point that throws on a memory store (module <c>memory-storage-plan</c> §5), so the class uses
/// the leak-proof file-backed helper rather than the memory one. It joins the Tier-3 file-bound set
/// until module <c>archive-target</c> makes the archive target memory-capable.</para>
/// </summary>
public class SoulLedgerTrimTests : IDisposable
{
    readonly DataTestStore _testStore;
    readonly RpgStore _store;

    public SoulLedgerTrimTests()
    {
        _testStore = DataTestStore.CreateFileBacked();
        _store = _testStore.Store;
    }

    public void Dispose() => _testStore.Dispose();

    void Seed(int n)
    {
        for (var i = 0; i < n; i++)
            _store.AwardSouls(1, 10, "seed", "trim-" + i);
    }

    [Fact]
    public void Trim_archives_the_tail_and_keeps_the_balance_byte_identical()
    {
        Seed(25);
        var before = _store.GetSoulBalance(1);
        Assert.Equal(250, before.Balance);

        _store.TrimSoulLedgerTails(retainOverride: 10);

        var after = _store.GetSoulBalance(1);
        Assert.Equal(before.Balance, after.Balance);
        Assert.Equal(before.EarnedTotal, after.EarnedTotal);
        Assert.Equal(before.SpentTotal, after.SpentTotal);

        // Newest 10 remain; the archived overflow is on disk and cataloged.
        var ledger = _store.ListSoulLedger(1, 100);
        Assert.Equal(10, ledger.Items.Count);
        var archives = Directory.GetFiles(Path.Combine(_testStore.DataDir!, "archive"), "souls-a1-*.sqlite");
        Assert.Single(archives);

        // Second trim is a no-op.
        _store.TrimSoulLedgerTails(retainOverride: 10);
        Assert.Equal(10, _store.ListSoulLedger(1, 100).Items.Count);
        Assert.Single(Directory.GetFiles(Path.Combine(_testStore.DataDir!, "archive"), "souls-a1-*.sqlite"));
    }

    [Fact]
    public void Under_the_retain_limit_nothing_trims()
    {
        Seed(5);
        _store.TrimSoulLedgerTails(retainOverride: 10);
        Assert.Equal(5, _store.ListSoulLedger(1, 100).Items.Count);
        var archiveDir = Path.Combine(_testStore.DataDir!, "archive");
        Assert.True(!Directory.Exists(archiveDir)
                    || Directory.GetFiles(archiveDir, "souls-*.sqlite").Length == 0);
    }
}
