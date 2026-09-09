using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The fixture guard on the self-installing grade host</b> (juice PR 1). A
    /// <c>RuntimeInitializeOnLoadMethod</c> host runs in every scene and every PlayMode test in the suite,
    /// so the director may only GRADE — enable its Volume, switch a camera's post-processing on — for the
    /// persistent core's camera, in a region an anchor has reported, on a real graphics device. A fixture
    /// that builds its own camera and loads no region must see exactly the frame it saw before the host
    /// existed. Pinned here so a later tidy cannot turn the guard into the owner's kill switch.
    /// </summary>
    public class MoodGradeDirectorGuardTests
    {
        [Test]
        public void The_grade_runs_only_for_the_persistent_camera_in_a_reported_region_on_a_real_device()
        {
            Assert.IsTrue(MoodGradeDirector.MayGrade(true, true, true));
            Assert.IsFalse(MoodGradeDirector.MayGrade(false, true, true), "a fixture's own camera is never switched");
            Assert.IsFalse(MoodGradeDirector.MayGrade(true, false, true), "no region anchor has reported: pre-boot, or a bare scene");
            Assert.IsFalse(MoodGradeDirector.MayGrade(true, true, false), "the Null device CI runs on gets no post pass");
            Assert.IsFalse(MoodGradeDirector.MayGrade(false, false, false));
        }

        [Test]
        public void A_camera_a_fixture_builds_in_its_own_scene_is_not_the_persistent_one()
        {
            var go = new GameObject("FixtureCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                Assert.IsFalse(MoodGradeDirector.IsPersistentCamera(cam), "it lives in the fixture's scene, not DontDestroyOnLoad");
                Assert.AreNotEqual(MoodGradeDirector.PersistentSceneName, go.scene.name);
            }
            finally { Object.DestroyImmediate(go); }
            Assert.IsFalse(MoodGradeDirector.IsPersistentCamera(null));
        }

        [Test]
        public void The_tick_asks_the_guard_BEFORE_it_enables_the_volume_or_touches_a_camera()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Code/Art/MoodGradeDirector.cs");
            string src = File.ReadAllText(path);
            int guard  = src.IndexOf("if (!MayGrade(IsPersistentCamera(cam)", System.StringComparison.Ordinal);
            int enable = src.IndexOf("_volume.enabled = true", System.StringComparison.Ordinal);
            int postOn = src.IndexOf("SetCameraPost(true)", System.StringComparison.Ordinal);
            Assert.Greater(guard, 0, "the tick must ask MayGrade");
            Assert.Greater(enable, guard, "the volume is enabled only past the guard");
            Assert.Greater(postOn, guard, "the camera's post flag is switched on only past the guard");
            StringAssert.Contains("GameServices.CurrentRegionId", src.Substring(guard, 200), "the region fact is the anchor's report, read through Core");
        }
    }
}
