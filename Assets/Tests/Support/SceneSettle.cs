using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>
    /// <b>No deferred destroy may be pending when a scene load or unload starts.</b> Every PlayMode scene
    /// load, scene unload and test-teardown destroy goes through here.
    ///
    /// <para><b>WHY (2026-11-16): an editor-only lock-order deadlock inside the engine</b>, seen four times
    /// on CI as a silent PlayMode hang (one was in NmcWharfWaterSidePlayTests, whose teardown destroyed its
    /// spawned objects and unloaded the region in the same frame). The two threads, from the native stacks:
    /// <list type="bullet">
    ///   <item>main: <c>PostLateUpdate.ScriptRunDelayedDynamicFrameRate → DelayedCallManager::Update →
    ///     DelayedDestroyCallback → DestroyGameObjectRecursive → SpriteRenderer::MainThreadCleanup →
    ///     AssetChangedNotifier::UnsubscribeFromAll → PersistentManager::GetPathName</c>, waiting for the
    ///     PersistentManager lock while holding the object lock.</item>
    ///   <item>Loading.Preload: <c>LoadSceneOperation::Perform →
    ///     PersistentManager::LoadFileCompletelyThreaded → CheckInstanceIDsLoaded</c>, holding the
    ///     PersistentManager lock and waiting for the object lock.</item>
    /// </list>
    /// The trigger is an <see cref="Object.Destroy(Object)"/> of a hierarchy holding SpriteRenderers that is
    /// still queued when an async scene operation starts: the queued destroy runs on the main thread in
    /// PostLateUpdate while the preload thread is mid-file. The notifier is editor-only, so players are not
    /// affected; tests are, because they run in the editor.</para>
    ///
    /// <para><b>The frame count.</b> <see cref="Object.Destroy(Object)"/> does not destroy; it queues a
    /// <c>DelayedDestroyCallback</c> on the DelayedCallManager, whose dynamic-frame-rate pass runs in
    /// PostLateUpdate (the stack above names it). A test coroutine resumes in Update, so a destroy it queues
    /// in frame N runs in frame N's PostLateUpdate pass. A destroy queued AFTER that pass (from a
    /// WaitForEndOfFrame resume or a render callback), or queued from INSIDE the pass by an
    /// <c>OnDestroy</c> that destroys something else, runs in frame N+1's pass. Resuming in Update of frame
    /// N+2 is therefore past both, which is two <c>yield return null</c>; <see cref="DrainFrames"/> is one
    /// more than that so a second-level cascade (an OnDestroy whose victim's OnDestroy destroys again) also
    /// drains. One yield, which several fixtures used, only covers the first case.</para>
    ///
    /// <para><b>Settling is not enough on its own (2026-11-16, second slice).</b> The first CI run with the
    /// settle hung again in <c>StPetersEastDoorPlayTests.TheIslandAnswersTheKeyABoatComesHomeWith</c>, in a
    /// load issued through <see cref="Load"/> after the settle, with the same two stacks. Nothing the test
    /// destroyed was pending: the destroy was queued by GAME code still running on the persistent
    /// (DontDestroyOnLoad) hosts while the preload thread was reading the scene file, which takes a few
    /// hundred milliseconds of frames. So a test-issued load now also HOLDS the main thread, inside the
    /// frame that issues it, until the preload thread has finished reading
    /// (<see cref="StartLoad"/>): no PostLateUpdate, so no delayed destroy, can run while
    /// <c>LoadSceneOperation::Perform</c> is on the preload thread. The hold is bounded
    /// (<see cref="HoldSeconds"/>) and falls back to the plain async load.</para>
    ///
    /// <para><b>Third slice (2026-11-16).</b> The hold now lives in Core (<see cref="SceneLoadHold"/>) and the
    /// region loader applies it, editor-only, to the loads the GAME starts (a crossing, a passage, a preload),
    /// which this helper cannot see. Every load and unload here is tracked there too, so a load queued behind
    /// another operation is left plain async instead of running out the bound.</para>
    ///
    /// <para><b>Fourth slice (2026-11-24): a test load is always held, and nothing here waits forever.</b> CI
    /// hung again (run 45197233449, <c>SunCastersEverywherePlayTests.Audit_Cumberland_VillagersGainCasters</c>)
    /// with the same two native stacks, the read side inside a region scene load. The shared hold has a 10 s
    /// cap and a three-timeout budget for the whole session; a region read on a desktop already takes up to
    /// 7.7 s (Nine Mile Creek), so on the slower CI runner a hold can run out and release the main thread
    /// mid-read, and once the budget is spent no later load is held at all. A load issued while another
    /// tracked operation is still in flight is not held either. So test loads now (1) wait, by frames and
    /// within <see cref="PendingOperationTimeoutSeconds"/>, until no tracked scene operation is in flight, so
    /// the hold is not refused for a queued load; (2) hold with their own cap, <see cref="TestHoldSeconds"/>,
    /// kept inside the watchdog's frozen-thread verdict and not drawn from the game's budget; and (3) wait for
    /// completion within <see cref="OperationTimeoutSeconds"/> and then FAIL with the scene's name, so a stall
    /// that still turns frames costs minutes and names itself.</para>
    /// </summary>
    public static class SceneSettle
    {
        /// <summary>Frames to yield so every destroy queued before the call has run. See the class notes
        /// for why two is the floor and three is the value.</summary>
        public const int DrainFrames = 3;

        /// <summary>Upper bound on frames <see cref="DestroyAndSettle(IEnumerable{Object})"/> waits for its
        /// own objects to read as destroyed after <see cref="DrainFrames"/>. A fixture may fail; it may not
        /// hang.</summary>
        public const int MaxWaitFrames = 30;

        /// <summary>Yield <see cref="DrainFrames"/> frames: the blind settle, for a load or unload whose
        /// caller does not know what was destroyed before it.</summary>
        public static IEnumerator Frames()
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
        }

        /// <summary>Destroy now, with nothing left queued. Use for objects a test spawned, from test code
        /// (a test body, SetUp or TearDown) and never from inside a physics or trigger callback or an
        /// <c>OnDestroy</c>. An asset on disk is never destroyed.</summary>
        public static void DestroyNow(Object o)
        {
            if (o == null || EditorUtility.IsPersistent(o)) return;
            Object.DestroyImmediate(o);
        }

        /// <summary><see cref="DestroyNow(Object)"/> for each.</summary>
        public static void DestroyNow(IEnumerable<Object> objects)
        {
            if (objects == null) return;
            foreach (Object o in new List<Object>(objects)) DestroyNow(o);
        }

        /// <summary>
        /// Wait for tracked scene operations, <see cref="Object.Destroy(Object)"/> each object, then yield
        /// until the delayed-destroy queue has drained and every one of them reads as destroyed
        /// (Unity-null). A pending operation must finish before destruction is queued.
        /// </summary>
        public static IEnumerator DestroyAndSettle(IEnumerable<Object> objects)
        {
            yield return WaitForPendingSceneOperations("destroying objects before cleanup");
            var mine = new List<Object>();
            if (objects != null)
                foreach (Object o in objects)
                    if (o != null) mine.Add(o);
            foreach (Object o in mine) if (o != null) Object.Destroy(o);

            for (int i = 0; i < DrainFrames; i++) yield return null;
            for (int i = 0; i < MaxWaitFrames && AnyAlive(mine); i++) yield return null;
            if (AnyAlive(mine))
                Debug.LogWarning("[SceneSettle] objects survived " + (DrainFrames + MaxWaitFrames) +
                                 " frames after Destroy; a scene operation started now may deadlock in the editor");
        }

        /// <summary><see cref="DestroyAndSettle(IEnumerable{Object})"/> for a few objects.</summary>
        public static IEnumerator DestroyAndSettle(params Object[] objects)
        {
            return DestroyAndSettle((IEnumerable<Object>)objects);
        }

        /// <summary>Settle, then <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/> and wait
        /// for it, as <c>yield return SceneManager.LoadSceneAsync(...)</c> did.</summary>
        public static IEnumerator Load(string scene, LoadSceneMode mode)
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return WaitForPendingSceneOperations("loading '" + scene + "'");
            yield return Await(StartLoad(scene, mode), "load of '" + scene + "'");
        }

        /// <summary>Settle, then <see cref="EditorSceneManager.LoadSceneAsyncInPlayMode"/> and wait.</summary>
        public static IEnumerator LoadInPlayMode(string path, LoadSceneParameters parameters)
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return WaitForPendingSceneOperations("loading '" + path + "'");
            yield return Await(StartLoadInPlayMode(path, parameters), "load of '" + path + "'");
        }

        /// <summary>Longest a test waits for tracked scene operations already in flight to finish before it
        /// issues its own (real time, frames keep turning). Far below the watchdog's per-test limit.</summary>
        public const double PendingOperationTimeoutSeconds = 60d;

        /// <summary>Longest a test waits for one load or unload it issued to complete (real time, frames keep
        /// turning). A region load is seconds; this is far past that and far below the watchdog's limit.</summary>
        public const double OperationTimeoutSeconds = 120d;

        /// <summary>Longest a test load holds the main thread for its threaded read. Below the watchdog's
        /// frozen-thread verdict, so a hold is never reported as the hang it prevents.</summary>
        public const float TestHoldSeconds = 40f;

        /// <summary>Test holds that may run out before later test loads stop holding, so a hold that cannot
        /// work on some editor costs a bounded amount of time per run.</summary>
        public const int MaxTestHoldTimeouts = 3;

        static int s_testHeld, s_testHoldTimeouts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_testHeld = 0;
            s_testHoldTimeouts = 0;
        }

        /// <summary>Test loads held to the end of their read this session, and test holds that ran out.</summary>
        public static int TestHeldLoads => s_testHeld;
        public static int TestHoldTimeouts => s_testHoldTimeouts;

        /// <summary>
        /// Yield frames until no tracked scene operation (<see cref="SceneLoadHold.InFlight"/>) is in flight, so
        /// the load or unload issued next is not queued behind one (a queued load cannot be held). Throws
        /// <see cref="System.TimeoutException"/> naming <paramref name="before"/> past
        /// <paramref name="timeoutSeconds"/>. Adds no frame when nothing is in flight.
        /// </summary>
        public static IEnumerator WaitForPendingSceneOperations(string before,
            double timeoutSeconds = PendingOperationTimeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (SceneLoadHold.InFlight > 0)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new System.TimeoutException("[SceneSettle] before " + before + ": " + SceneLoadHold.InFlight +
                        " tracked scene operation(s) still in flight after " + timeoutSeconds + " s");
                yield return null;
            }
        }

        /// <summary>
        /// Yield frames until <paramref name="op"/> is done, as <c>yield return op</c> did, but throw
        /// <see cref="System.TimeoutException"/> naming <paramref name="what"/> and its progress past
        /// <paramref name="timeoutSeconds"/>. A null operation (the engine refused it) returns at once.
        /// </summary>
        public static IEnumerator Await(AsyncOperation op, string what, double timeoutSeconds = OperationTimeoutSeconds)
        {
            if (op == null) yield break;
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!op.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new System.TimeoutException("[SceneSettle] " + what + " did not complete within " +
                        timeoutSeconds + " s (progress " + op.progress.ToString("0.00") + ", " +
                        SceneLoadHold.InFlight + " tracked operation(s) in flight)");
                yield return null;
            }
        }

        // The hold itself is the shared Core guard (third slice, 2026-11-16), so test loads and the loads the
        // game starts through the region loader count against one budget and see each other in flight.

        /// <summary>Longest the main thread is held waiting for the preload thread to finish reading a
        /// scene (<see cref="SceneLoadHold.CapSeconds"/>).</summary>
        public const float HoldSeconds = SceneLoadHold.CapSeconds;

        /// <summary>The <c>progress</c> an async load reports once its threaded read is done
        /// (<see cref="SceneLoadHold.ReadDoneProgress"/>).</summary>
        public const float ReadDoneProgress = SceneLoadHold.ReadDoneProgress;

        /// <summary>Holds that may run out before the rest of the run stops holding
        /// (<see cref="SceneLoadHold.MaxTimeouts"/>).</summary>
        public const int MaxHoldTimeouts = SceneLoadHold.MaxTimeouts;

        /// <summary>How many GAME loads (the shared Core hold) were held to the end of their read, and how many
        /// ran out its bound. Test loads count in <see cref="TestHeldLoads"/> and <see cref="TestHoldTimeouts"/>.</summary>
        public static int HeldLoads => SceneLoadHold.HeldLoads;
        public static int HoldTimeouts => SceneLoadHold.Timeouts;

        /// <summary>
        /// <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/>, with the main thread held in this
        /// frame until the preload thread has finished reading the scene (see the class notes). Returns the
        /// operation, already allowed to activate, for the caller to wait on; null when the engine refused
        /// the load, exactly as <c>LoadSceneAsync</c> does. Call it from test code, never from a physics
        /// callback or <c>OnDestroy</c>.
        /// </summary>
        public static AsyncOperation StartLoad(string scene, LoadSceneMode mode)
        {
            return HoldUntilRead(SceneManager.LoadSceneAsync(scene, mode));
        }

        /// <summary><see cref="StartLoad"/> for <see cref="EditorSceneManager.LoadSceneAsyncInPlayMode"/>.</summary>
        public static AsyncOperation StartLoadInPlayMode(string path, LoadSceneParameters parameters)
        {
            return HoldUntilRead(EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters));
        }

        // The shared Core hold's cap and session budget are sized for the game's loads; a test load holds with
        // its own (fourth slice). A load queued behind a tracked operation is left plain async, as in Core:
        // it waits on that operation, which needs frames, so holding could only run out the cap. Load and
        // LoadInPlayMode drain those first; a caller of StartLoad that did not is told so in the log.
        static AsyncOperation HoldUntilRead(AsyncOperation op)
        {
            if (op == null) return null;
            if (op.isDone || SceneLoadHold.InFlight > 0 || s_testHoldTimeouts >= MaxTestHoldTimeouts)
            {
                if (!op.isDone)
                    Debug.LogWarning("[SceneSettle] a test scene load was not held (" + SceneLoadHold.InFlight +
                        " tracked operation(s) in flight, " + s_testHoldTimeouts + " test hold timeouts): its read " +
                        "overlaps frames, where a delayed sprite destroy can deadlock the editor");
                SceneLoadHold.Track(op);
                return op;
            }

            op.allowSceneActivation = false;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            SceneLoadHold.Outcome outcome = SceneLoadHold.WaitForRead(() => op.progress,
                () => clock.Elapsed.TotalSeconds, () => System.Threading.Thread.Sleep(1), TestHoldSeconds);
            if (outcome == SceneLoadHold.Outcome.ReadDone)
            {
                s_testHeld++;
            }
            else
            {
                s_testHoldTimeouts++;
                Debug.LogWarning("[SceneSettle] a test scene load's threaded read did not finish within " +
                    TestHoldSeconds + " s while the main thread was held (progress " + op.progress.ToString("0.00") +
                    "); after " + MaxTestHoldTimeouts + " of these, later test loads this session are not held");
            }
            op.allowSceneActivation = true;
            SceneLoadHold.Track(op);
            return op;
        }

        /// <summary>Settle, then <see cref="SceneManager.UnloadSceneAsync(Scene)"/> and wait, as
        /// <c>yield return SceneManager.UnloadSceneAsync(scene)</c> did.</summary>
        public static IEnumerator Unload(Scene scene)
        {
            // A scene that was loaded when the caller checked and went away during the settle (something
            // else unloaded it) is not unloaded twice: that would log an engine error the caller never had.
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            string name = scene.IsValid() ? scene.name : "(invalid scene)";
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return WaitForPendingSceneOperations("unloading '" + name + "'");
            if (wasLoaded && !scene.isLoaded) yield break;
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            SceneLoadHold.Track(op);   // a game load issued behind it is then not held (it would wait on this)
            yield return Await(op, "unload of '" + name + "'");
        }

        /// <summary>Settle, then <see cref="SceneManager.UnloadSceneAsync(string)"/> and wait.</summary>
        public static IEnumerator Unload(string scene)
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return WaitForPendingSceneOperations("unloading '" + scene + "'");
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            SceneLoadHold.Track(op);
            yield return Await(op, "unload of '" + scene + "'");
        }

        static bool AnyAlive(List<Object> objects)
        {
            foreach (Object o in objects) if (o != null) return true;
            return false;
        }
    }
}
