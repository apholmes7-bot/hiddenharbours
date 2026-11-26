using HiddenHarbours.CoopSpike;
using NUnit.Framework;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Session state is kept per connection. Nothing here may assume there are two.</summary>
    public sealed class CoopSessionRulesTests
    {
        [Test]
        public void EachConnectionTakesTheLowestFreePlace()
        {
            Assert.That(CoopSlots.LowestFree(new int[0], 4), Is.EqualTo(0));
            Assert.That(CoopSlots.LowestFree(new[] { 0 }, 4), Is.EqualTo(1));
            Assert.That(CoopSlots.LowestFree(new[] { 0, 1, 2 }, 4), Is.EqualTo(3));
            Assert.That(CoopSlots.LowestFree(null, 1), Is.EqualTo(0));
        }

        [Test]
        public void APlaceFreedByALeaverIsGivenToTheNextJoiner()
        {
            Assert.That(CoopSlots.LowestFree(new[] { 0, 2 }, 4), Is.EqualTo(1));
            Assert.That(CoopSlots.LowestFree(new[] { 1 }, 2), Is.EqualTo(0));
        }

        [Test]
        public void AFullSessionGivesNoPlaceAndAdmitsNobody()
        {
            Assert.That(CoopSlots.LowestFree(new[] { 0, 1 }, 2), Is.EqualTo(CoopSlots.None));
            Assert.That(CoopSlots.LowestFree(new int[0], 0), Is.EqualTo(CoopSlots.None));
            Assert.That(CoopSlots.HasRoom(0, 2) && CoopSlots.HasRoom(1, 2), Is.True);
            Assert.That(CoopSlots.HasRoom(2, 2), Is.False);
            Assert.That(CoopSlots.HasRoom(3, 4), Is.True, "The limit is a number, not a pair.");
            Assert.That(CoopSlots.HasRoom(4, 4), Is.False);
        }

        [Test]
        public void UnsetAndOutOfRangePlacesDoNotBlockARealOne()
        {
            Assert.That(CoopSlots.LowestFree(new[] { CoopSlots.None, 7, -3 }, 2), Is.EqualTo(0));
        }

        [Test]
        public void TheHostAndJoinEntryIsOffUnlessTheBuildOrTheLaunchAsksForIt()
        {
            string[] plain = { "game.exe" };
            string[] asked = { "game.exe", CoopTrialPanel.MenuArgument };
            Assert.That(CoopTrialPanel.BuiltWithFlag, Is.False, "The project itself never carries the trial flag.");
            Assert.That(CoopTrialPanel.MenuOffered(false, false, false, false, plain), Is.False, "the ordinary game");
            Assert.That(CoopTrialPanel.MenuOffered(false, true, false, false, plain), Is.False, "a development build");
            Assert.That(CoopTrialPanel.MenuOffered(false, false, false, false, asked), Is.False, "a release build, asked");
            Assert.That(CoopTrialPanel.MenuOffered(false, true, false, false, asked), Is.True);
            Assert.That(CoopTrialPanel.MenuOffered(true, true, false, false, plain), Is.True);
            Assert.That(CoopTrialPanel.MenuOffered(true, true, true, false, asked), Is.False, "never in the editor");
            Assert.That(CoopTrialPanel.MenuOffered(true, true, false, true, asked), Is.False, "never without a window");
            Assert.That(CoopTrialPanel.MenuOffered(true, true, false, false,
                new[] { "game.exe", CoopDevStart.HostArgument }), Is.False, "a session already chosen has no menu");
        }

        [Test]
        public void TheMenuStartsExactlyTheLaunchTheParserAccepts()
        {
            string[] host = CoopLaunchOptions.Arguments(false, null, 7777);
            Assert.That(CoopLaunchOptions.TryParse(host, 7777, out var hosting, out _), Is.True);
            Assert.That(hosting.Client, Is.False);
            string[] join = CoopLaunchOptions.Arguments(true, "192.168.1.20", 7777);
            Assert.That(CoopLaunchOptions.TryParse(join, 7777, out var joining, out _), Is.True);
            Assert.That(joining.Client, Is.True);
            Assert.That(joining.Address, Is.EqualTo("192.168.1.20"));
            Assert.That(CoopLaunchOptions.TryAddress("my-pc", out _), Is.False);
            Assert.That(CoopLaunchOptions.TryAddress(" 192.168.1.20", out _), Is.False);
            Assert.That(CoopLaunchOptions.TryAddress(null, out _), Is.False);
        }
    }
}