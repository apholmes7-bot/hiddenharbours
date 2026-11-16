using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Tests.Support;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Pins <see cref="SceneSettle"/>'s contract: when it returns, what it destroyed is really gone, so no
    /// delayed destroy is left to run beside the scene load or unload that follows. The engine deadlock it
    /// prevents (a queued SpriteRenderer destroy against the scene preload thread) is in the class notes.
    /// </summary>
    public class SceneSettlePlayTests
    {
        static GameObject SpriteHierarchy(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<SpriteRenderer>();
            var child = new GameObject(name + "/child");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<SpriteRenderer>();
            return root;
        }

        [Test]
        public void DrainFrames_CoversADestroyQueuedAfterThePassAndOneCascade()
        {
            // Two frames is the floor (a destroy queued after frame N's PostLateUpdate pass runs in N+1's);
            // the class notes give the reasoning. Lowering this reopens the hang.
            Assert.That(SceneSettle.DrainFrames, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TheLoadHold_IsBoundedWellInsideTheHangWatchdog()
        {
            // The hold blocks the main thread inside one frame; past the watchdog's frozen-thread verdict it
            // would be reported as the very hang it prevents. And it stops holding after a few timeouts, so
            // a hold that cannot work on some editor costs a bounded amount of time per run.
            Assert.That(SceneSettle.HoldSeconds * 3f, Is.LessThan(MainThreadPulse.FrozenAfterSeconds));
            Assert.That(SceneSettle.MaxHoldTimeouts, Is.InRange(1, 5));
            // allowSceneActivation=false stops an async load's progress at 0.9 once its threaded read is done.
            Assert.That(SceneSettle.ReadDoneProgress, Is.EqualTo(0.9f));
        }

        [UnityTest]
        public IEnumerator DestroyAndSettle_LeavesEveryObjectUnityNull()
        {
            var a = SpriteHierarchy("SettleA");
            var b = SpriteHierarchy("SettleB");
            var child = a.transform.GetChild(0).gameObject;
            var mesh = new Mesh();

            yield return SceneSettle.DestroyAndSettle(a, b, mesh, null);

            Assert.That(a == null, Is.True, "the root is destroyed, not queued");
            Assert.That(child == null, Is.True, "its child went with it");
            Assert.That(b == null, Is.True);
            Assert.That(mesh == null, Is.True, "a runtime asset is destroyed too");
            Assert.That(ReferenceEquals(a, null), Is.False, "Unity-null, the managed wrapper is still here");
        }

        [UnityTest]
        public IEnumerator DestroyAndSettle_WaitsAtLeastDrainFrames()
        {
            var a = SpriteHierarchy("SettleFrames");
            int before = Time.frameCount;
            yield return SceneSettle.DestroyAndSettle(a);
            Assert.That(Time.frameCount - before, Is.GreaterThanOrEqualTo(SceneSettle.DrainFrames));
        }

        [UnityTest]
        public IEnumerator Frames_YieldsAtLeastDrainFrames()
        {
            int before = Time.frameCount;
            yield return SceneSettle.Frames();
            Assert.That(Time.frameCount - before, Is.GreaterThanOrEqualTo(SceneSettle.DrainFrames));
        }

        [Test]
        public void DestroyNow_IsImmediate()
        {
            var a = SpriteHierarchy("SettleNow");
            var child = a.transform.GetChild(0).gameObject;
            SceneSettle.DestroyNow(a);
            Assert.That(a == null, Is.True, "gone in the same frame: nothing is left queued");
            Assert.That(child == null, Is.True);
            Assert.DoesNotThrow(() => SceneSettle.DestroyNow((Object)null));
        }
    }
}
