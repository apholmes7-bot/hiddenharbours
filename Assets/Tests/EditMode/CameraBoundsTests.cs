using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The camera's world-bounds clamp (scene-sizing §6 item 4) — pure, so the whole rig is provable
    /// without a camera, a scene or play mode (the <c>CameraZoomPolicy</c> convention).
    ///
    /// <para>At the greybox's 160 m nobody noticed there was no bounds rig. St Peters is authored at
    /// <b>760 × 520 m</b>, where an unclamped camera sails off the painted map — past the seabed
    /// bake, past the sea mesh, into whatever the backdrop clears to.</para>
    /// </summary>
    public class CameraBoundsTests
    {
        // A 760 × 520 m region centred on the origin — St Peters' authored extent, the case this exists for.
        private static CameraBounds StPeters => new CameraBounds(Vector2.zero, new Vector2(760f, 520f));

        // Half-extents of the DEFAULT boat framing (14 m of world height) at 16:9.
        private const float HalfH = 7f;                    // ortho size = worldHeight / 2
        private const float HalfW = 7f * 16f / 9f;         // ≈ 12.44 m

        // ===== the clamp =================================================================================

        [Test]
        public void InsideTheRegion_ThePositionIsUntouched()
        {
            var desired = new Vector2(120f, -80f);
            Assert.AreEqual(desired, CameraBounds.Clamp(desired, StPeters, HalfW, HalfH),
                "well inside the map the clamp must be invisible — it is a fence, not a leash");
        }

        [Test]
        public void PastAnEdge_TheVIEWStopsAtTheEdge_NotTheCameraCentre()
        {
            // The whole point: the clamp holds the camera back by its HALF-EXTENT, so the visible
            // rectangle's edge lands on the map's edge. Clamping the centre to the map edge instead
            // would still show half a screen of nothing.
            Vector2 clamped = CameraBounds.Clamp(new Vector2(9999f, 0f), StPeters, HalfW, HalfH);
            Assert.AreEqual(380f - HalfW, clamped.x, 1e-4f, "east edge: centre held back by half the view");
            Assert.AreEqual(380f, clamped.x + HalfW, 1e-3f, "…so the view's right edge sits ON the map edge");

            clamped = CameraBounds.Clamp(new Vector2(0f, -9999f), StPeters, HalfW, HalfH);
            Assert.AreEqual(-260f + HalfH, clamped.y, 1e-4f, "south edge");
            Assert.AreEqual(-260f, clamped.y - HalfH, 1e-3f, "…view's bottom edge on the map edge");
        }

        [Test]
        public void AnOffCentreRegion_ClampsAboutItsOwnCentre()
        {
            // WorldCenter is authored, not assumed to be the origin — a region whose rectangle sits
            // off the origin must clamp about ITS centre, or the fence lands in the wrong place.
            var offset = new CameraBounds(new Vector2(1000f, -500f), new Vector2(200f, 200f));
            Vector2 clamped = CameraBounds.Clamp(new Vector2(99999f, -99999f), offset, HalfW, HalfH);
            Assert.AreEqual(1100f - HalfW, clamped.x, 1e-4f);
            Assert.AreEqual(-600f + HalfH, clamped.y, 1e-4f);
        }

        // ===== the zoom coupling =========================================================================

        [Test]
        public void ZoomingOut_HoldsTheCameraFurtherFromTheEdge()
        {
            // The allowance is a function of the half-extents, so it must shrink as the view grows.
            // This is what keeps the clamp correct across the discrete pixel-perfect tiers AND during
            // an upgrade/deck zoom tween — nothing here knows about tiers, which is why nothing here
            // can go stale when one is added.
            float tightX = CameraBounds.Clamp(new Vector2(9999f, 0f), StPeters, 5f, 3f).x;
            float wideX = CameraBounds.Clamp(new Vector2(9999f, 0f), StPeters, 40f, 22.5f).x;
            Assert.Greater(tightX, wideX,
                "a wider view must be held further back — otherwise zooming out shows off-map");
            Assert.AreEqual(380f, tightX + 5f, 1e-3f, "tight view's edge still lands on the map edge");
            Assert.AreEqual(380f, wideX + 40f, 1e-3f, "…and so does the wide view's");
        }

        // ===== the degenerate case the naive clamp gets WRONG =============================================

        [Test]
        public void AViewWiderThanTheRegion_CENTRES_RatherThanJitteringBetweenEdges()
        {
            // ⚠️ THE CASE A NAIVE CLAMP BREAKS ON. With the view wider than the world there is no
            // valid range: Mathf.Clamp(x, min, max) with min > max returns whichever endpoint it
            // tests last, so the camera would snap to one edge and flip to the other as the target
            // crossed — a visible shudder. Centring is the only stable answer, and it is what a
            // player expects of a small region: it sits in the middle of the screen.
            var small = new CameraBounds(new Vector2(50f, 20f), new Vector2(10f, 8f));

            foreach (float x in new[] { -9999f, 40f, 50f, 60f, 9999f })
            {
                Vector2 c = CameraBounds.Clamp(new Vector2(x, 20f), small, HalfW, HalfH);
                Assert.AreEqual(50f, c.x, 1e-4f, $"x={x}: a too-narrow region must CENTRE, not pick an edge");
            }

            // And it must be stable — the same answer regardless of which side you approach from.
            Vector2 fromLeft = CameraBounds.Clamp(new Vector2(-1000f, -1000f), small, HalfW, HalfH);
            Vector2 fromRight = CameraBounds.Clamp(new Vector2(1000f, 1000f), small, HalfW, HalfH);
            Assert.AreEqual(fromLeft, fromRight, "no jitter: both approaches land on the same point");
            Assert.AreEqual(new Vector2(50f, 20f), fromLeft, "…which is the region's centre");
        }

        [Test]
        public void OneAxisTooNarrow_CentresThatAxisAndStillClampsTheOther()
        {
            // The mixed case a per-axis clamp gets right and a whole-rect one does not: a long thin
            // channel is wider than the view along its length and narrower across it.
            var channel = new CameraBounds(Vector2.zero, new Vector2(600f, 10f));
            Vector2 c = CameraBounds.Clamp(new Vector2(9999f, 9999f), channel, HalfW, HalfH);
            Assert.AreEqual(300f - HalfW, c.x, 1e-4f, "the long axis still clamps");
            Assert.AreEqual(0f, c.y, 1e-4f, "the narrow axis centres");
        }

        // ===== the unconfigured state ====================================================================

        [Test]
        public void UnboundedIsTheIdentity_SoNothingChangesUntilARegionPublishesAnExtent()
        {
            // The shipped state everywhere until a builder wires the extent, and the state of any bare
            // test scene. It must be EXACTLY a passthrough, not "a very big box".
            var desired = new Vector2(123456f, -98765f);
            Assert.AreEqual(desired, CameraBounds.Clamp(desired, CameraBounds.Unbounded, HalfW, HalfH));
            Assert.IsFalse(CameraBounds.Unbounded.IsBounded);
            Assert.IsFalse(new CameraBounds(Vector2.zero, new Vector2(100f, 0f)).IsBounded,
                "a half-configured rect (one axis zero) is NOT bounded — it would clamp to a line");
            Assert.AreEqual(desired,
                CameraBounds.Clamp(desired, new CameraBounds(Vector2.zero, new Vector2(100f, 0f)), HalfW, HalfH),
                "…and passes through rather than collapsing the camera onto that line");
        }

        [Test]
        public void NegativeSizes_AreTreatedAsUnconfigured_NotAsAnInvertedBox()
        {
            var silly = new CameraBounds(Vector2.zero, new Vector2(-500f, -500f));
            Assert.IsFalse(silly.IsBounded);
            var desired = new Vector2(42f, 42f);
            Assert.AreEqual(desired, CameraBounds.Clamp(desired, silly, HalfW, HalfH));
        }

        // ===== sabotage ===================================================================================

        [Test]
        public void Sabotage_IgnoringTheHalfExtents_WouldShowOffMap()
        {
            // Proves the half-extent term is doing work: clamping the CENTRE to the map edge (the
            // easy mistake) leaves half a screen of void on show. The delta is exactly the half-extent.
            Vector2 correct = CameraBounds.Clamp(new Vector2(9999f, 0f), StPeters, HalfW, HalfH);
            const float centreOnlyClamp = 380f;
            Assert.AreEqual(HalfW, centreOnlyClamp - correct.x, 1e-3f,
                "the clamp must hold back by the half-extent — if these were equal the rig would be " +
                "showing half a viewport of off-map on every edge");
        }
    }
}
