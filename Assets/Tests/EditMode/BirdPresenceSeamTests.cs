using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The bird-presence Core seam: the flock writes the birds near the view, the audio reads them, and
    /// neither names the other (rule 4). <see cref="GameServices.BirdPresence"/> is never null: with no
    /// flock it is <see cref="EmptyBirdPresence"/>, an empty sky, and an empty sky calls nothing.
    /// </summary>
    public class BirdPresenceSeamTests
    {
        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        /// <summary>A presence with one bird over the middle of the view.</summary>
        private sealed class OneBird : IBirdPresence
        {
            public Rect ViewWorld => new Rect(-14f, -8f, 28f, 16f);
            public int NearCount => 1;

            public int CopyNear(BirdAudible[] into)
            {
                if (into == null || into.Length == 0) return 0;
                into[0] = new BirdAudible(Vector2.zero, BirdActivity.Feeding, 1, "bird.herring_gull");
                return 1;
            }
        }

        [Test]
        public void BirdPresence_IsNeverNull()
        {
            Assert.IsNotNull(GameServices.BirdPresence, "the call player reads this WITHOUT a null check");
            Assert.AreSame(EmptyBirdPresence.Instance, GameServices.BirdPresence);
        }

        [Test]
        public void ClearingTheRegistration_FallsBackToTheEmptySky()
        {
            var presence = new OneBird();
            GameServices.BirdPresence = presence;
            Assert.AreSame(presence, GameServices.BirdPresence);

            GameServices.BirdPresence = null;   // what a flock does on disable
            Assert.AreSame(EmptyBirdPresence.Instance, GameServices.BirdPresence);
        }

        [Test]
        public void Reset_ClearsTheBirdPresence()
        {
            GameServices.BirdPresence = new OneBird();
            GameServices.Reset();
            Assert.AreSame(EmptyBirdPresence.Instance, GameServices.BirdPresence,
                           "scene teardown must leave an empty sky, not a stale flock");
        }

        [Test]
        public void ADestroyedProducer_ReadsAsEmpty()
        {
            // A flock is a MonoBehaviour: once destroyed its reference is Unity's fake null, which `??`
            // would read as live. The getter must see through it.
            var host = new GameObject("BirdPresenceSeamTests_Flock");
            try
            {
                GullFlock flock = host.AddComponent<GullFlock>();
                GameServices.BirdPresence = flock;
                Assert.AreSame(flock, GameServices.BirdPresence);

                Object.DestroyImmediate(host);
                host = null;
                Assert.AreSame(EmptyBirdPresence.Instance, GameServices.BirdPresence,
                               "a destroyed flock must read as an empty sky, never as a live one that throws");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TheEmptyPresenceIsSilent()
        {
            IBirdPresence sky = EmptyBirdPresence.Instance;
            Assert.AreEqual(0, sky.NearCount);
            Assert.AreEqual(default(Rect), sky.ViewWorld);
            var into = new BirdAudible[4];
            Assert.AreEqual(0, sky.CopyNear(into));
            Assert.AreEqual(0, sky.CopyNear(null), "a null array writes nothing");
        }
    }
}
