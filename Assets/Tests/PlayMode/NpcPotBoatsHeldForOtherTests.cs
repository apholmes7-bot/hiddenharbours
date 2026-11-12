using NUnit.Framework;
using HiddenHarbours.App;

/// <summary>
/// Holds the NPC pot boats at their berths for every other PlayMode test in this assembly. Those proofs load
/// a region and seek the clock to whatever hour suits them, and they count and photograph the moored fleet;
/// a resident that has gone to her pots at that hour is not what any of them are about.
/// <see cref="HiddenHarbours.Tests.PlayMode.NpcPotBoatsPlayTests"/> binds its boat by hand.
///
/// <para>Declared outside any namespace on purpose: NUnit then applies it to the whole assembly.</para>
/// </summary>
[SetUpFixture]
public class NpcPotBoatsHeldForOtherTests
{
    private bool _was;

    [OneTimeSetUp]
    public void HoldTheFleet()
    {
        _was = NpcPotBoats.AutoBind;
        NpcPotBoats.AutoBind = false;
    }

    [OneTimeTearDown]
    public void LetThemWork() => NpcPotBoats.AutoBind = _was;
}
