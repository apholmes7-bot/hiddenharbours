using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Tests.Support;
using UnityEngine.SceneManagement;
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
            Assert.That(SceneSettle.PendingOperationTimeoutSeconds, Is.GreaterThan(0d));
            Assert.That(SceneSettle.PendingOperationTimeoutSeconds, Is.LessThan(TestHangWatch.DefaultLimitSeconds));
        }

        [UnityTest]
        public IEnumerator DestroyAndSettle_LeavesEveryObjectUnityNull()
        {
            yield return SceneSettle.WaitForPendingSceneOperations("the destruction premise");
            var a = SpriteHierarchy("SettleA");
            var b = SpriteHierarchy("SettleB");
            var child = a.transform.GetChild(0).gameObject;
            var mesh = new Mesh();

            var scene = SceneManager.CreateScene("SettleDestroyPending");
            var unload = SceneManager.UnloadSceneAsync(scene);
            SceneLoadHold.Track(unload);
            Assert.That(unload, Is.Not.Null);
            Assert.That(unload.isDone, Is.False);

            var destroy = SceneSettle.DestroyAndSettle(a, b, mesh, null);
            Assert.That(destroy.MoveNext(), Is.True);
            var pending = destroy.Current as IEnumerator;
            Assert.That(pending, Is.Not.Null, "destruction first yields the pending-operation gate");
            Assert.That(pending.MoveNext(), Is.True);
            Assert.That(pending.Current, Is.Null, "the pending operation needs a frame");
            yield return pending.Current;
            Assert.That(a != null && b != null && mesh != null, Is.True,
                "destruction is not queued while the gate owns the boundary");
            yield return pending;
            Assert.That(unload.isDone, Is.True);
            yield return destroy;

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

        [Test]
        public void TestLoadHoldAndWaits_AreBoundedInsideTheHangWatchdog()
        {
            // One test hold blocks the main thread inside one frame: it must stay under the frozen-thread verdict.
            Assert.That(SceneSettle.TestHoldSeconds, Is.LessThan(MainThreadPulse.FrozenAfterSeconds));
            // It exists because the game's cap can run out on a slow runner; a shorter one would buy nothing.
            Assert.That(SceneSettle.TestHoldSeconds, Is.GreaterThan(SceneSettle.HoldSeconds));
            Assert.That(SceneSettle.MaxTestHoldTimeouts, Is.InRange(1, 5));
            // The frame-turning waits fail, naming what they waited for, long before the watchdog ends the run.
            Assert.That(SceneSettle.PendingOperationTimeoutSeconds + SceneSettle.OperationTimeoutSeconds,
                Is.LessThan(TestHangWatch.DefaultLimitSeconds / 2.0));
        }

        [UnityTest]
        public IEnumerator Await_PastItsBound_FailsNamingTheOperation()
        {
            yield return SceneSettle.WaitForPendingSceneOperations("the premise");
            var scene = SceneManager.CreateScene("SettleAwaitBound");
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            SceneLoadHold.Track(unload);
            Assert.That(unload, Is.Not.Null);
            Assert.That(unload.isDone, Is.False, "premise: the unload needs a player-loop turn");

            var wait = SceneSettle.Await(unload, "unload of 'SettleAwaitBound'", 0d);
            var error = Assert.Throws<System.TimeoutException>(() => wait.MoveNext());
            StringAssert.Contains("unload of 'SettleAwaitBound' did not complete within 0 s", error.Message);
            yield return SceneSettle.Await(unload, "unload of 'SettleAwaitBound'");
            Assert.That(unload.isDone, Is.True);
        }

        [UnityTest]
        public IEnumerator WaitForPendingSceneOperations_WaitsForATrackedOperation_AndFailsPastItsBound()
        {
            yield return SceneSettle.WaitForPendingSceneOperations("the premise");
            var idle = SceneSettle.WaitForPendingSceneOperations("nothing", 0d);
            Assert.That(idle.MoveNext(), Is.False, "nothing in flight adds no frame");

            var scene = SceneManager.CreateScene("SettlePendingBound");
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            SceneLoadHold.Track(unload);
            Assert.That(unload, Is.Not.Null);
            Assert.That(unload.isDone, Is.False, "premise: the unload needs a player-loop turn");
            Assert.That(SceneLoadHold.InFlight, Is.EqualTo(1), "premise: exactly one unload is tracked");

            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                var late = SceneSettle.WaitForPendingSceneOperations("loading 'Somewhere'", 0d);
                var error = Assert.Throws<System.TimeoutException>(() => late.MoveNext());
                StringAssert.Contains("before loading 'Somewhere'", error.Message);
                StringAssert.Contains("1 tracked scene operation(s) still in flight", error.Message);
                Assert.That(SceneLoadHold.InFlight, Is.EqualTo(1), "a timeout must not reset the tracker");

                var wait = SceneSettle.WaitForPendingSceneOperations("the next load");
                Assert.That(wait.MoveNext(), Is.True, "cleanup waits while the operation is pending");
                Assert.That(wait.Current, Is.Null, "yield a frame also at zero time scale");
                yield return wait.Current;
                yield return wait;
                Assert.That(SceneLoadHold.InFlight, Is.Zero);
                Assert.That(unload.isDone, Is.True);
            }
            finally { Time.timeScale = previousScale; }
        }
    }
}
