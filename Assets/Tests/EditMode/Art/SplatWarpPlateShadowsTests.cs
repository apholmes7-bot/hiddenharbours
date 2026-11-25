using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE SPLAT WARP PLATES' SPRITE SHADOWS, RE-TICKED</b> (terrain-fidelity 3b, seat ruling t20). The second
    /// re-plate caught Ginny's plot with its shadows missing twice (one launch's plate and its control, and another
    /// launch's first take): a
    /// <c>SpriteShadow</c> recomputes on a <c>Time.deltaTime</c> throttle, and under <c>Time.timeScale</c> 0 it stands
    /// at whatever its last tick read. After each pose settles and before its plate is read, the plate case now
    /// disables and enables every enabled shadow in the loaded scenes, so <c>OnEnable</c> resets the throttle and
    /// ticks each one once from the settled camera. The component is not changed and nothing is re-seeded; the
    /// retake stays the backstop and does not re-tick; the capture and the cost case do not touch the shadows.
    ///
    /// <para>Read from the sources: CI has no GPU, and whether the flash is gone is the push round's proof lease.
    /// <b>On the base</b> (fa8df4b9) the harness re-ticks nothing, and this test fails at its first assert.</para>
    /// </summary>
    public sealed class SplatWarpPlateShadowsTests
    {
        private const string PlatePath = "Assets/Tests/PlayMode/StPetersSplatWarpPlatePlayTests.cs";
        private const string PlateCase = "public IEnumerator TheSplatEdges_PlatedSettledAtNoon()";
        private const string BlendCase = "public IEnumerator TheBlends_PlatedSoftAndDrawn()";
        private const string ReTick ="static int ReTickTheSpriteShadows()";
        private const string Capture = "Color32[] ReadMedian()";
        private const string CostCase = "public IEnumerator TheGroundAndTheSea_CostAtThePlatePose()";

        [Test]
        public void ThePlateCase_ReTicksTheSpriteShadowsAfterEachPose_BeforeItsPlate()
        {
            string code = Code(PlatePath);
            const string call = "ReTickTheSpriteShadows();";

            // the re-tick: every enabled shadow in a loaded scene, disabled then enabled; nothing else touched
            string reTick = Squash(Body(code, ReTick));
            StringAssert.Contains("Resources.FindObjectsOfTypeAll<SpriteShadow>()", reTick, "every shadow loaded");
            StringAssert.Contains(".gameObject.scene.IsValid()", reTick, "a loaded prefab asset is not in the plate");
            StringAssert.Contains("!s.isActiveAndEnabled)continue;", reTick, "a shadow that is off stays off");
            StringAssert.Contains("s.enabled=false;s.enabled=true;", reTick, "OnDisable then OnEnable: one tick now");
            foreach (string word in new[] { "Tick(", "_timer", "Random", "SetActive" })
                Assert.IsFalse(reTick.Contains(word), "re-enabled, not ticked by hand, re-seeded or hidden: " + word);

            // the plate case: once, inside the pose loop, right after the pose settles, before the plate is read
            string plate = Squash(Body(code, PlateCase));
            Assert.AreEqual(1, Count(plate, Regex.Escape(call)), "the plate case re-ticks once per pose");
            int poses = plate.IndexOf("foreach(varfinPoses)", StringComparison.Ordinal);
            int settle = plate.IndexOf("yieldreturnFrameOn(f.Centre);intreTicked=ReTickTheSpriteShadows();",
                                       StringComparison.Ordinal);
            Assert.Greater(poses, -1);
            Assert.Greater(settle, poses, "inside the pose loop, right after the pose settles");
            Assert.Less(settle, plate.IndexOf("ReadMedian()", StringComparison.Ordinal), "before the plate is read");

            // the retake stays the backstop, as it was: no re-tick inside it
            int retake = plate.IndexOf("if(noisy>0){", StringComparison.Ordinal);
            Assert.Greater(retake, settle, "the retake comes after the first plate");
            Assert.IsFalse(Body(plate, "if(noisy>0)").Contains(call), "the retake does not re-tick");

            // the blend plate case (pixel-true blends) re-ticks at each settled camera, before that camera's first plate:
            // once in the pose loop right after each pose settles, and once in the pan right after each camera step
            // settles (the step moves the camera the shadows are read from). Two call sites, no more.
            string blends = Squash(Body(code, BlendCase));
            int blendCalls = Count(blends, Regex.Escape(call));
            Assert.AreEqual(2, blendCalls, "the blend plate case re-ticks once per settled pose and once per pan step");
            int blendPoses = blends.IndexOf("for(intp=0;p<BlendPoses.Length;p++)", StringComparison.Ordinal);
            int blendSettle = blends.IndexOf("yieldreturnFrameOn(pose.Centre);intreTicked=ReTickTheSpriteShadows();",
                                             StringComparison.Ordinal);
            Assert.Greater(blendPoses, -1);
            Assert.Greater(blendSettle, blendPoses, "the blends: inside the pose loop, right after the pose settles");
            Assert.Less(blendSettle, blends.IndexOf("ReadMedian()", StringComparison.Ordinal), "the blends: before the pose's first plate");
            string pan = Body(blends, "if(p==BlendPanPose)");
            Assert.AreEqual(1, Count(pan, Regex.Escape(call)), "the pan re-ticks once per camera step");
            const string panSettle = "yieldreturnFrameOn(home+newVector2(steps*step,steps*step));" +
                                     "Vector3camMoved=_cam.transform.position-camHome;ReTickTheSpriteShadows();";
            int panAt = pan.IndexOf(panSettle, StringComparison.Ordinal);
            Assert.Greater(panAt, -1, "the pan: right after the step settles");
            Assert.Less(panAt, pan.IndexOf("ReadMedian()", StringComparison.Ordinal), "the pan: before the step's plate");

            // neither the capture nor the cost case re-ticks, and the two named plate cases are the only callers
            foreach (string body in new[] { Body(code, Capture), Body(code, CostCase) })
                Assert.IsFalse(Squash(body).Contains("ReTickTheSpriteShadows"), "the capture and the cost case keep the shadows");
            Assert.AreEqual(1 + blendCalls, Count(Squash(code), Regex.Escape(call)),
                "the splat-warp and the blend plate cases are the only callers: a new caller is named here or fails");
        }
    }
}
