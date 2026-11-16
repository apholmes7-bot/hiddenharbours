using System.Collections;
using System.Collections.Generic;
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
    /// <para>What this cannot do: it cannot see an async load the GAME started (a region streaming in
    /// additively through the region loader) and is still running. A destroy, deferred or immediate, during
    /// such a load can still deadlock in the editor.</para>
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
        /// <see cref="Object.Destroy(Object)"/> each object, exactly as a teardown always did, then yield
        /// until the delayed-destroy queue has drained and every one of them reads as destroyed
        /// (Unity-null). The destroy timing the components see is unchanged; only the wait is new.
        /// </summary>
        public static IEnumerator DestroyAndSettle(IEnumerable<Object> objects)
        {
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
            yield return SceneManager.LoadSceneAsync(scene, mode);
        }

        /// <summary>Settle, then <see cref="EditorSceneManager.LoadSceneAsyncInPlayMode"/> and wait.</summary>
        public static IEnumerator LoadInPlayMode(string path, LoadSceneParameters parameters)
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters);
        }

        /// <summary>Settle, then <see cref="SceneManager.UnloadSceneAsync(Scene)"/> and wait, as
        /// <c>yield return SceneManager.UnloadSceneAsync(scene)</c> did.</summary>
        public static IEnumerator Unload(Scene scene)
        {
            // A scene that was loaded when the caller checked and went away during the settle (something
            // else unloaded it) is not unloaded twice: that would log an engine error the caller never had.
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            for (int i = 0; i < DrainFrames; i++) yield return null;
            if (wasLoaded && !scene.isLoaded) yield break;
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>Settle, then <see cref="SceneManager.UnloadSceneAsync(string)"/> and wait.</summary>
        public static IEnumerator Unload(string scene)
        {
            for (int i = 0; i < DrainFrames; i++) yield return null;
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        static bool AnyAlive(List<Object> objects)
        {
            foreach (Object o in objects) if (o != null) return true;
            return false;
        }
    }
}
