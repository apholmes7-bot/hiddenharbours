using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <see cref="GameServices.PlayerTransform"/> — the Core relay that lets REGION content find the
    /// person walking the world without referencing App (rule 4). The behaviour pinned here is the one
    /// the whole seam rests on and the one that is easiest to lose in a later "tidy-up": the accessor
    /// hands back a <b>real</b> null once its transform has been destroyed.
    ///
    /// <para><b>Why that is worth a test of its own.</b> A destroyed <c>UnityEngine.Object</c> is
    /// Unity's FAKE-null: <c>UnityEngine.Object</c>'s overloaded <c>==</c> reports it equal to null, but
    /// the reference is not actually null, so <c>??</c> and <c>?.</c> sail straight past it and the next
    /// dereference throws <c>MissingReferenceException</c>. This relay exists precisely BECAUSE the
    /// reference it replaces gets destroyed (a region's dev stand-in dies on the travel path), so a
    /// consumer being handed a fake-null here would rebuild the exact defect the relay was added to
    /// close — compile-clean, runtime-red, and invisible until someone sails to another region.</para>
    ///
    /// <para>The <see cref="BuildingInterior"/> end of the same seam is driven end-to-end across a real
    /// region hop in <c>InteriorTravelPlayTests</c>; this is the unit half.</para>
    /// </summary>
    public class PlayerTransformRelayTests
    {
        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        [Test]
        public void Unpublished_ReadsAsNull()
        {
            Assert.IsNull(GameServices.PlayerTransform,
                "nobody has published a player — EditMode, a bare art scene, a region played with no " +
                "persistent core. Consumers must be able to treat that as 'carry on', never a throw");
        }

        [Test]
        public void APublishedPlayer_ComesBackAsItself()
        {
            var go = new GameObject("Player");
            try
            {
                GameServices.PlayerTransform = go.transform;
                Assert.AreSame(go.transform, GameServices.PlayerTransform);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ADestroyedPlayer_ReadsAsARealNull_NotUnitysFakeOne()
        {
            // ⭐ THE ONE THAT MATTERS. The publisher is gone and never cleared the relay — a torn-down
            // core, a shell restart, a test rig. Everything downstream must see a null it can trust.
            var go = new GameObject("Player");
            GameServices.PlayerTransform = go.transform;
            Object.DestroyImmediate(go);

            Transform read = GameServices.PlayerTransform;

            Assert.IsTrue(read is null,
                "the accessor must LAUNDER Unity's fake-null into a real one. `Assert.IsNull` alone " +
                "would pass on a fake-null too (NUnit unwraps it), so this asserts on the C# `is null` " +
                "pattern, which does NOT use UnityEngine.Object's overloaded == and therefore sees the " +
                "difference. A consumer writing `?? fallback` against a fake-null would skip its " +
                "fallback and throw MissingReferenceException on the next dereference");
        }

        [Test]
        public void Reset_ClearsIt()
        {
            var go = new GameObject("Player");
            try
            {
                GameServices.PlayerTransform = go.transform;
                GameServices.Reset();
                Assert.IsNull(GameServices.PlayerTransform,
                    "a relay that survived Reset would leak one test's player into the next test's " +
                    "world — and one region's into the next region's, after a teardown");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
