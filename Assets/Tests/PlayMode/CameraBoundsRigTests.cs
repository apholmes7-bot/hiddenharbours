using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The camera bounds rig END TO END, in play mode: a real orthographic camera with a real aspect,
    /// a real follow target, and a real <see cref="RegionAnchor"/> publishing a real extent — the
    /// whole chain <c>RegionDef → builder → anchor → GameServices → CameraFollow</c>.
    ///
    /// <para><b>Why this exists on top of the pure tests.</b> <c>CameraBoundsTests</c> proves the
    /// clamp arithmetic. It cannot prove the clamp is ever CALLED, that the extent actually reaches
    /// the persistent camera across a region hop, or that the clamp runs after the zoom settles. Those
    /// are rig facts, and a rig that is not scene-faithful lies about them (the punt-steer lesson).</para>
    ///
    /// <para><b>No frame-count-as-time assertions.</b> Headless <c>yield return null</c> is nowhere
    /// near a real frame, so nothing here asserts the camera CONVERGES within N frames. The claims are
    /// invariants — "never past the edge, on any frame" — which hold regardless of how much simulated
    /// time a frame turns out to be worth.</para>
    /// </summary>
    public class CameraBoundsRigTests
    {
        private const float RegionW = 760f;      // St Peters' authored extent — the case the rig exists for
        private const float RegionH = 520f;
        private const float Aspect = 16f / 9f;

        private GameObject _camGo;
        private GameObject _anchorGo;
        private GameObject _targetGo;
        private Camera _cam;
        private CameraFollow _follow;

        [SetUp]
        public void SetUp()
        {
            GameServices.CurrentRegionBounds = default;

            _camGo = new GameObject("TestCamera");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);
            _cam.aspect = Aspect;                 // pinned: batchmode's screen aspect is not the game's
            _follow = _camGo.AddComponent<CameraFollow>();

            _targetGo = new GameObject("Target");

            // The region reports itself exactly as a built region scene does.
            _anchorGo = new GameObject("TestRegionAnchor");
            _anchorGo.SetActive(false);           // configure BEFORE enable, like the builder
            var anchor = _anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure("region.test", _anchorGo.transform, null, null);
            anchor.ConfigureExtent(Vector2.zero, new Vector2(RegionW, RegionH));
            _anchorGo.SetActive(true);            // OnEnable publishes the extent
        }

        [TearDown]
        public void TearDown()
        {
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_anchorGo != null) Object.DestroyImmediate(_anchorGo);
            if (_targetGo != null) Object.DestroyImmediate(_targetGo);
            GameServices.CurrentRegionBounds = default;
            GameServices.Reset();
        }

        private float HalfW => _cam.orthographicSize * Aspect;
        private float HalfH => _cam.orthographicSize;

        // ===== the chain ==================================================================================

        [UnityTest]
        public IEnumerator TheAnchorsExtentReachesTheCamera()
        {
            yield return null;
            Assert.IsTrue(_follow.Bounds.IsBounded,
                "the anchor's extent must reach the persistent camera through the region seam");
            Assert.AreEqual(new Vector2(RegionW, RegionH), _follow.Bounds.SizeMeters,
                "…as the region's authored size, unmodified");
        }

        [UnityTest]
        public IEnumerator AnOffMapCamera_IsPulledBackWithinOneFrame()
        {
            // No follow target, so FollowTarget() returns early and this isolates the CLAMP: does
            // LateUpdate call it at all?
            _follow.Target = null;
            _camGo.transform.position = new Vector3(5000f, 5000f, -10f);

            yield return null;

            Assert.LessOrEqual(_camGo.transform.position.x, RegionW * 0.5f - HalfW + 1e-3f,
                "the clamp must run in LateUpdate — an off-map camera is pulled back inside");
            Assert.LessOrEqual(_camGo.transform.position.y, RegionH * 0.5f - HalfH + 1e-3f);
            Assert.AreEqual(-10f, _camGo.transform.position.z, 1e-4f,
                "the camera's DEPTH must never be touched by a 2D bounds clamp");
        }

        [UnityTest]
        public IEnumerator FollowingATargetOffTheMap_TheViewNeverLeavesIt()
        {
            // The real scenario: the boat sails past the map edge (it can — the sea mesh is bigger
            // than the painted map, and nothing stops the player heading out). The camera follows, and
            // must stop at the edge. Asserted as an INVARIANT on every frame, not a convergence claim.
            _targetGo.transform.position = new Vector3(4000f, -4000f, 0f);
            _follow.Target = _targetGo.transform;
            _camGo.transform.position = new Vector3(0f, 0f, -10f);

            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                Vector3 p = _camGo.transform.position;
                Assert.LessOrEqual(p.x, RegionW * 0.5f - HalfW + 1e-3f,
                    $"frame {frame}: the view's right edge left the map");
                Assert.GreaterOrEqual(p.y, -RegionH * 0.5f + HalfH - 1e-3f,
                    $"frame {frame}: the view's bottom edge left the map");
            }
        }

        [UnityTest]
        public IEnumerator WithNoRegionReporting_TheCameraIsUnclamped_AsBefore()
        {
            // Every scene that has not published an extent — a bare art scene, a region built before
            // this rig — must behave exactly as it did. This is the passthrough claim.
            Object.DestroyImmediate(_anchorGo);
            _anchorGo = null;
            GameServices.CurrentRegionBounds = default;

            _follow.Target = null;
            _camGo.transform.position = new Vector3(5000f, 5000f, -10f);

            yield return null;

            Assert.IsFalse(_follow.Bounds.IsBounded);
            Assert.AreEqual(5000f, _camGo.transform.position.x, 1e-3f,
                "unconfigured means UNCLAMPED — not clamped to some default box");
        }

        // ===== the zoom coupling, live ====================================================================

        [UnityTest]
        public IEnumerator ZoomingOutAtTheEdge_PushesTheCameraFurtherIn_TheSameFrame()
        {
            // ⚠️ The ordering claim the pure tests cannot make. The clamp's allowance depends on the
            // half-extents, so it must run AFTER the frame's zoom has settled. If it ran before, a
            // zoom-out at the map edge would leave the view hanging off the map for a frame — which is
            // exactly where a bounds rig gets noticed.
            _follow.Target = null;
            _camGo.transform.position = new Vector3(5000f, 0f, -10f);
            yield return null;

            float tightX = _camGo.transform.position.x;
            Assert.AreEqual(RegionW * 0.5f - HalfW, tightX, 1e-2f, "parked on the east edge at boat framing");

            // Zoom out hard (no tween — the endpoint is what matters here).
            _follow.SetFraming(60f, animate: false);
            yield return null;

            float wideX = _camGo.transform.position.x;
            Assert.Less(wideX, tightX - 1f,
                "a wider view must be pushed further from the edge in the SAME frame it widens");
            Assert.AreEqual(RegionW * 0.5f - HalfW, wideX, 1e-2f,
                "…and land exactly on the new allowance for the new zoom");
        }

        [UnityTest]
        public IEnumerator ARegionSmallerThanTheView_CentresRatherThanJittering()
        {
            // A pocket cove, or any region seen at a wide zoom. The camera must sit still in the
            // middle instead of flipping between edges as the target crosses.
            //
            // ⚠️ The region must be smaller than the view on BOTH axes for BOTH to centre, and the
            // view is not square: at the default 14 m framing it is 14 m tall but ~24.9 m wide (16:9).
            // This fixture first used 20 × 16 m — narrower than the view across, but TALLER than it up
            // — so y correctly CLAMPED (to 47) instead of centring, and the test failed on its own
            // arithmetic rather than on the code. 20 × 10 m is inside the view on both axes.
            GameServices.CurrentRegionBounds = new Rect(new Vector2(90f, 43f), new Vector2(20f, 10f)); // centre (100, 48)
            Assert.Less(20f, HalfW * 2f, "fixture: the region is narrower than the view");
            Assert.Less(10f, HalfH * 2f, "fixture: …and shorter than it");
            _follow.Target = _targetGo.transform;

            var seen = new Vector2[6];
            for (int i = 0; i < seen.Length; i++)
            {
                _targetGo.transform.position = new Vector3(i % 2 == 0 ? -500f : 500f, i % 2 == 0 ? -500f : 500f, 0f);
                yield return null;
                seen[i] = _camGo.transform.position;
            }

            for (int i = 0; i < seen.Length; i++)
            {
                Assert.AreEqual(100f, seen[i].x, 1e-3f, $"sample {i}: centred on x regardless of the target");
                Assert.AreEqual(48f, seen[i].y, 1e-3f, $"sample {i}: centred on y");
            }
        }

        // ===== travel =====================================================================================

        [UnityTest]
        public IEnumerator ArrivingInABiggerRegion_WidensTheClamp()
        {
            // The reason the bounds are NOT baked onto the camera: it is on the persistent core and
            // outlives every region. A rectangle serialized at build time would be the start region's
            // forever, and sailing to St Peters would trap the view in a small box in the middle of it.
            _follow.Target = null;
            _camGo.transform.position = new Vector3(5000f, 0f, -10f);
            yield return null;
            float inSmall = _camGo.transform.position.x;

            // The next region reports itself (its anchor enables).
            GameServices.CurrentRegionBounds = new Rect(new Vector2(-2000f, -2000f), new Vector2(4000f, 4000f));
            _camGo.transform.position = new Vector3(5000f, 0f, -10f);
            yield return null;

            Assert.Greater(_camGo.transform.position.x, inSmall + 100f,
                "arriving in a bigger region must widen the clamp — the camera cannot carry the start " +
                "region's rectangle across a hop");
            Assert.AreEqual(2000f - HalfW, _camGo.transform.position.x, 1e-2f,
                "…to exactly the new region's edge");
        }
    }
}
