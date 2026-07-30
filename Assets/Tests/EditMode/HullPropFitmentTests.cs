using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Where a fitting's mesh actually sits on its hull</b> — the rotate-about-a-point behind every
    /// oar and outboard on the mesh path (ADR 0022 phase 7), proven headlessly.
    ///
    /// <para>It is four lines of arithmetic and it has already been wrong twice in this project: once
    /// loudly (an engine rotating about the hull ORIGIN, swinging clean through the boat) and once
    /// quietly, which is the interesting one. Folding the lateral clamp offset into the point the
    /// fitting turns about — <c>(P+m) − R·(P+m)</c> — is algebraically tidy and cancels the offset
    /// EXACTLY at dead ahead, then grows it as the engine swivels. A twin outboard therefore sat with
    /// both engines in the same place until the helm moved, which is precisely the pose nobody
    /// screenshots.</para>
    ///
    /// <para>These tests pin the contract the pose files always stated:
    /// <c>out = P + mount + R·(v − P)</c>.</para>
    /// </summary>
    public class HullPropFitmentTests
    {
        // A skiff outboard's clamp swivel, in hull-rig metres — the real shape of the numbers.
        static readonly Vector3 Swivel = new Vector3(0f, -3.57f, 0.72f);

        /// <summary>The transform's job, restated from the outside: where does a baked vertex end
        /// up? <c>v' = R·v + localPosition</c>, and that must equal the pose contract.</summary>
        static Vector3 Placed(Vector3 vertex, Vector3 pivot, float lateral, Vector3 fitment,
                              Quaternion rotation)
            => rotation * vertex
             + HullPropFitment.LocalPosition(pivot, lateral, fitment, rotation);

        static void AssertClose(Vector3 expected, Vector3 actual, string because = "")
        {
            Assert.AreEqual(expected.x, actual.x, 1e-5f, because);
            Assert.AreEqual(expected.y, actual.y, 1e-5f, because);
            Assert.AreEqual(expected.z, actual.z, 1e-5f, because);
        }

        [Test]
        public void AFittingOnItsOwnBoat_AtRest_IsExactlyWhereItWasBaked()
        {
            // The zero case, and the one every existing hull relies on: no rotation, no clamp offset,
            // no loan → the fitting must not move by so much as a millimetre from its baked place.
            AssertClose(Vector3.zero,
                HullPropFitment.LocalPosition(Swivel, 0f, Vector3.zero, Quaternion.identity));
            AssertClose(Vector3.zero, HullPropFitment.FixedLocalPosition(0f, Vector3.zero));
        }

        [Test]
        public void ARotatedFitting_TurnsAboutItsPivot_NotAboutTheHullOrigin()
        {
            // The vertex AT the pivot is the one that must not move: that is what "turns about" means.
            var r = Quaternion.AngleAxis(32f, Vector3.forward);
            AssertClose(Swivel, Placed(Swivel, Swivel, 0f, Vector3.zero, r));

            // …and the prop, a third of a metre below the clamp, swings — about that same point.
            Vector3 prop = Swivel + new Vector3(0f, -0.1f, -0.47f);
            Vector3 swung = Placed(prop, Swivel, 0f, Vector3.zero, r);
            Assert.AreEqual((prop - Swivel).magnitude, (swung - Swivel).magnitude, 1e-4f,
                "a rigid rotation about the clamp keeps every part the same distance from it.");
        }

        [Test]
        public void TheLateralClampOffset_SeparatesTheTwinsEngines_EVENDeadAhead()
        {
            // ⚠️ THE REGRESSION THIS FILE EXISTS FOR. The sport skiff's twin is one fitting at two
            // clamp positions (±0.34 m). At the identity rotation — the pose she idles at, the pose
            // she is drawn in most of the time — the two engines must be 0.68 m apart.
            Vector3 port = Placed(Swivel, Swivel, -0.34f, Vector3.zero, Quaternion.identity);
            Vector3 star = Placed(Swivel, Swivel, +0.34f, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(0.68f, star.x - port.x, 1e-5f,
                "a twin's two engines are 0.68 m apart at dead ahead. Folding the mount into the " +
                "rotated pivot cancels it exactly here and lets it grow with the helm, so the twin " +
                "reads as ONE engine that splits in two when she turns.");
            Assert.AreEqual(-0.34f, port.x - Swivel.x, 1e-5f, "…and each sits at its own clamp.");
        }

        [Test]
        public void EachTwinEngine_StillSwivelsAboutItsOwnClamp_NotTheCentreline()
        {
            var r = Quaternion.AngleAxis(30f, Vector3.forward);
            Vector3 clamp = Swivel + new Vector3(-0.34f, 0f, 0f);
            AssertClose(clamp, Placed(Swivel, Swivel, -0.34f, Vector3.zero, r),
                "the port engine turns about the PORT bracket. An engine that swivels about the " +
                "centreline swings its own leg through the transom on the way over.");
        }

        [Test]
        public void ABorrowedFitting_MovesWhole_PivotIncluded()
        {
            // The dory's case: the punt's outboard, shifted 0.28 m forward and 0.055 m up onto her
            // own transom. The whole part moves — and it still turns about its clamp AFTERWARDS, or
            // the borrowed engine would swing off its bracket the first time she put the helm over.
            var fitment = new Vector3(0f, 0.28f, 0.055f);
            var r = Quaternion.AngleAxis(32f, Vector3.forward);

            AssertClose(Swivel + fitment, Placed(Swivel, Swivel, 0f, fitment, r));

            // The bolted-down half (the clamp bracket) takes the same translation and no rotation.
            AssertClose(fitment, HullPropFitment.FixedLocalPosition(0f, fitment));
        }

        [Test]
        public void TheClampOffsetAndTheLoan_Compose_RatherThanFight()
        {
            var fitment = new Vector3(0.05f, 0.28f, 0.055f);
            AssertClose(new Vector3(-0.29f, 0.28f, 0.055f),
                        HullPropFitment.Mount(-0.34f, fitment));
        }
    }
}
