using FusionRpg.Core.Battle;
using FusionRpg.Core.Match;
using Xunit;
using Xunit.Abstractions;

namespace FusionRpg.Core.Tests.Match;

public class ScratchRollCheck
{
    readonly ITestOutputHelper _out;
    public ScratchRollCheck(ITestOutputHelper output) => _out = output;

    [Fact]
    public void ComputeRealMatchRoll()
    {
        const string matchKey = "e01bc590-7b27-4fea-a992-71057b5a21c8";
        var seed = SeededRng.DeriveStream(0, matchKey).NextULong();
        var roll = SeededRng.DeriveStream(seed, "lawn-deploy-event:zombie-swarm").NextPerMille();
        var rollThin = SeededRng.DeriveStream(seed, "lawn-deploy-event:thin-defense").NextPerMille();
        _out.WriteLine($"seed={seed} zombie-swarm roll={roll} (needs <300) thin-defense roll={rollThin} (needs <300)");
    }
}
