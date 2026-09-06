using FusionRpg.Core.Battle;
using FusionRpg.Core.Match;
using Xunit;
using Xunit.Abstractions;

namespace FusionRpg.Core.Tests.Match;

public class ScratchRollCheck
{
    readonly ITestOutputHelper _out;
    public ScratchRollCheck(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData("50dbecad-3e73-4c74-bb88-4febdbd9a1c7")]
    [InlineData("e1fcb6a3-2b52-49ad-827f-8d0283c7a48e")]
    [InlineData("8dceda95-7ed3-4c93-8336-2b7ef6c9f49c")]
    [InlineData("90ffb546-f52d-46d8-aad1-f89c9bdbb57e")]
    [InlineData("06f5de79-bf5c-48b5-95eb-621fb90cb21b")]
    [InlineData("411c5987-a491-4d10-8766-1423b9c3cdc1")]
    [InlineData("a5456845-7c37-4107-9c78-a064b0bc1250")]
    [InlineData("d0ff7716-c5c0-4af3-a45a-0429b41dc5b0")]
    public void ComputeRealMatchRoll(string matchKey)
    {
        var seed = SeededRng.DeriveStream(0, matchKey).NextULong();
        var roll = SeededRng.DeriveStream(seed, "lawn-deploy-event:zombie-swarm").NextPerMille();
        var rollThin = SeededRng.DeriveStream(seed, "lawn-deploy-event:thin-defense").NextPerMille();
        _out.WriteLine($"matchKey={matchKey} seed={seed} zombie-swarm roll={roll} (needs <300) thin-defense roll={rollThin} (needs <300)");
    }
}
