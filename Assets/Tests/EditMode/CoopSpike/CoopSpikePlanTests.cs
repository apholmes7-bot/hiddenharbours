using HiddenHarbours.CoopSpike;
using HiddenHarbours.CoopSpike.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    public sealed class CoopSpikePlanTests
    {
        [Test]
        public void DevStartRequiresExactlyOneRoleAndTheIsolatedProduct()
        {
            string host = CoopDevStart.HostArgument, client = CoopDevStart.ClientArgument;
            Assert.That(CoopDevStart.ShouldStart(new[] { host }, CoopDevStart.SpikeProduct), Is.True);
            Assert.That(CoopDevStart.ShouldStart(new[] { client }, CoopDevStart.SpikeProduct), Is.True);
            Assert.That(CoopDevStart.ShouldStart(new[] { host, client }, CoopDevStart.SpikeProduct), Is.False);
            Assert.That(CoopDevStart.ShouldStart(new string[0], CoopDevStart.SpikeProduct), Is.False);
            Assert.That(CoopDevStart.ShouldStart(new[] { host }, "My project"), Is.False);
            Assert.That(CoopDevStart.ShouldStart(null, CoopDevStart.SpikeProduct), Is.False);
        }

        [TestCase(21, true)]
        [TestCase(22, false)]
        public void OnlyAMatchingSeedMaySeekTheClock(int seed, bool accepted)
        {
            var go = new GameObject("ClockFixture");
            var clock = go.AddComponent<GameClock>();
            try
            {
                clock.SeekTo(17);
                var state = new CoopSeaState { Seed = 21, Seconds = 987.25, Rate = 2, Paused = true, Ready = true };
                Assert.That(state.Apply(clock, seed), Is.EqualTo(accepted));
                Assert.That(clock.TotalSeconds, Is.EqualTo(accepted ? 987.25 : 17));
                if (accepted) Assert.That(clock.IsPaused && clock.TimeScale == 2, Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void UnreadyAndNonFiniteTimeCannotSeek()
        {
            var go = new GameObject("ClockFixture");
            var clock = go.AddComponent<GameClock>();
            try
            {
                clock.SeekTo(17);
                Assert.That(default(CoopSeaState).Apply(clock, 0), Is.False);
                var invalid = new CoopSeaState { Seed = 0, Seconds = double.NaN, Rate = 1, Ready = true };
                Assert.That(invalid.Apply(clock, 0), Is.False);
                Assert.That(clock.TotalSeconds, Is.EqualTo(17));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BranchAssetsCarryTheRosterAndAnOwnerAuthoritativePrefab()
        {
            // Validate the committed assets; tests must not rebuild or rewrite presentation assets.
            var settings = AssetDatabase.LoadAssetAtPath<CoopSpikeSettings>(CoopSpikeAssets.SettingsPath);
            Assert.That(settings.PlayerPrefab, Is.Not.Null);
            Assert.That(settings.Roster.Length, Is.GreaterThan(1));
            Assert.That(settings.Character, Is.Not.Null);
            Assert.That(settings.Rod, Is.Not.Null);
            Assert.That(settings.Scene, Is.EqualTo("EastWater"));
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(
                scene => scene.enabled && scene.path == "Assets/_Project/Scenes/EastWater.unity"));
            Assert.That(settings.PlayerPrefab.GetComponent<NetworkTransform>().AuthorityMode,
                Is.EqualTo(NetworkTransform.AuthorityModes.Owner));
            Assert.That(settings.PlayerPrefab.GetComponent<CoopBoatReplica>(), Is.Not.Null);
        }

        [Test]
        public void HostDefaultsAndClientLanEndpointAreParsed()
        {
            Assert.That(CoopLaunchOptions.TryParse(new[] { CoopDevStart.HostArgument }, 7777, out var host, out _), Is.True);
            Assert.That(host.Client, Is.False);
            Assert.That(host.Port, Is.EqualTo(7777));
            Assert.That(CoopLaunchOptions.TryParse(new[] { CoopDevStart.ClientArgument,
                CoopLaunchOptions.AddressArgument, "192.168.2.10", CoopLaunchOptions.PortArgument, "8123" },
                7777, out var client, out _), Is.True);
            Assert.That(client.Client, Is.True);
            Assert.That(client.Address, Is.EqualTo("192.168.2.10"));
            Assert.That(client.Port, Is.EqualTo(8123));
        }

        [TestCase("0")]
        [TestCase("65536")]
        [TestCase("-1")]
        [TestCase("udp")]
        public void InvalidPortsFailClosed(string port)
        {
            Assert.That(CoopLaunchOptions.TryParse(new[] { CoopDevStart.HostArgument,
                CoopLaunchOptions.PortArgument, port }, 7777, out _, out _), Is.False);
        }

        [TestCase("0.0.0.0")]
        [TestCase("255.255.255.255")]
        [TestCase("224.1.1.1")]
        [TestCase("192.168.1.999")]
        [TestCase("localhost")]
        [TestCase("127.1")]
        public void InvalidJoinAddressesFailClosed(string address)
        {
            Assert.That(CoopLaunchOptions.TryParse(new[] { CoopDevStart.ClientArgument,
                CoopLaunchOptions.AddressArgument, address }, 7777, out _, out _), Is.False);
        }

        [Test]
        public void MissingDuplicateAndContradictoryArgumentsFailClosed()
        {
            foreach (var args in new[] {
                new[] { CoopDevStart.ClientArgument },
                new[] { CoopDevStart.HostArgument, CoopDevStart.ClientArgument },
                new[] { CoopDevStart.HostArgument, CoopDevStart.HostArgument },
                new[] { CoopDevStart.HostArgument, CoopLaunchOptions.PortArgument },
                new[] { CoopDevStart.HostArgument, "--coop-spike-typo" },
                new[] { CoopDevStart.HostArgument, CoopLaunchOptions.PortArgument, "7777", CoopLaunchOptions.PortArgument, "8123" }
            }) Assert.That(CoopLaunchOptions.TryParse(args, 7777, out _, out _), Is.False);
        }
    }
}
