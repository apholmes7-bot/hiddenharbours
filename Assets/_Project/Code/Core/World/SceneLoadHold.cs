using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Editor-only guard against an engine lock-order deadlock in async scene loads.</b> Shared by the game's
    /// region loader (inside <c>#if UNITY_EDITOR</c>) and the test-side <c>SceneSettle</c>.
    ///
    /// <para><b>The deadlock (2026-11-16, seen on CI as a silent PlayMode hang).</b> The main thread runs a
    /// queued <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> of a SpriteRenderer hierarchy in
    /// PostLateUpdate (<c>DelayedDestroyCallback → SpriteRenderer::MainThreadCleanup →
    /// AssetChangedNotifier::UnsubscribeFromAll → PersistentManager::GetPathName</c>) while the
    /// <c>Loading.Preload</c> thread is inside <c>LoadSceneOperation::Perform →
    /// PersistentManager::LoadFileCompletelyThreaded</c>. Each holds the lock the other wants. The asset-change
    /// notifier exists only in the editor, so players cannot hit it.</para>
    ///
    /// <para><b>The guard.</b> <see cref="HoldUntilRead"/> holds the main thread, inside the frame that issued the
    /// load, until the preload thread has finished reading the scene file (progress reaches
    /// <see cref="ReadDoneProgress"/> with activation held back), then lets the load activate as normal. While the
    /// main thread is held no PostLateUpdate runs, so no delayed destroy can meet the threaded read. The hold is
    /// bounded by <see cref="CapSeconds"/>; after <see cref="MaxTimeouts"/> holds run out, the rest of the play
    /// session stops holding, so the worst case is a bounded stall, never a hang.</para>
    ///
    /// <para><b>Queued loads are not held.</b> Unity runs scene operations one at a time. A load issued while
    /// another tracked operation is still in flight waits for that one, which needs main-thread frames to
    /// finish, so holding the main thread could only run out the cap. Every game and test scene operation that
    /// goes through here is counted (<see cref="Track"/>), and a load issued behind one is left plain async.</para>
    ///
    /// <para><b>Unloads are not held.</b> The deadlock needs the preload thread inside a threaded FILE READ;
    /// an unload reads no file, and its work (destroying the scene's objects) runs on the main thread across
    /// frames, so a hold would only freeze it. Unloads are tracked, so a load behind one is not held.</para>
    /// </summary>
    public static class SceneLoadHold
    {
        /// <summary>The <c>progress</c> an async load reports once its threaded read is done and it is waiting
        /// for activation (documented engine behaviour with <c>allowSceneActivation</c> false).</summary>
        public const float ReadDoneProgress = 0.9f;

        /// <summary>Longest one hold may keep the main thread. A region's read is a few hundred milliseconds on
        /// CI (the job log's "Deserialize" line); this is far past that, and <see cref="MaxTimeouts"/> of them
        /// stay well inside the PlayMode hang watchdog's 60 s frozen-thread verdict.</summary>
        public const float CapSeconds = 10f;

        /// <summary>Holds that may run out before the rest of the session stops holding. One can run out
        /// honestly (a load queued behind an untracked scene operation); several mean the hold is not doing
        /// what it is for on this editor.</summary>
        public const int MaxTimeouts = 3;

        /// <summary>How the hold ended.</summary>
        public enum Outcome
        {
            /// <summary>No hold: null or finished operation, another operation in flight, or budget spent.</summary>
            NotHeld,
            /// <summary>Held until the threaded read was done.</summary>
            ReadDone,
            /// <summary>Held for the cap; the read had not finished.</summary>
            TimedOut,
        }

        static int s_inFlight;

        /// <summary>Tracked scene operations still in flight.</summary>
        public static int InFlight => s_inFlight;

        /// <summary>Loads held to the end of their read this session.</summary>
        public static int HeldLoads { get; private set; }

        /// <summary>Holds that ran out the cap this session.</summary>
        public static int Timeouts { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_inFlight = 0;
            HeldLoads = 0;
            Timeouts = 0;
        }

        /// <summary>PURE: whether a new load should be held, given the tracked operations already in flight and
        /// the holds that have run out so far.</summary>
        public static bool ShouldHold(int inFlight, int timeoutsSoFar, int maxTimeouts)
        {
            return inFlight <= 0 && timeoutsSoFar < maxTimeouts;
        }

        /// <summary>
        /// PURE: wait until <paramref name="progress"/> reports at least <see cref="ReadDoneProgress"/> or
        /// <paramref name="elapsedSeconds"/> passes <paramref name="capSeconds"/>, calling
        /// <paramref name="pause"/> between checks. A negative cap is treated as zero (one check, no wait).
        /// </summary>
        public static Outcome WaitForRead(Func<float> progress, Func<double> elapsedSeconds, Action pause,
                                          float capSeconds)
        {
            double cap = Math.Max(0f, capSeconds);
            while (progress() < ReadDoneProgress && elapsedSeconds() < cap)
                pause();
            return progress() >= ReadDoneProgress ? Outcome.ReadDone : Outcome.TimedOut;
        }

        /// <summary>
        /// Count a scene operation (load or unload) as in flight until it completes, so a load issued behind it
        /// is not held. Compiled out of player builds, and because it is <c>[Conditional]</c> its ARGUMENT
        /// expressions are compiled out with it: issue the operation on its own line, never inside the call.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void Track(AsyncOperation op)
        {
            if (op == null || op.isDone) return;
            s_inFlight++;
            op.completed += _ => { if (s_inFlight > 0) s_inFlight--; };
        }

        /// <summary>
        /// Hold the main thread in this frame until <paramref name="op"/>'s threaded read is done (see the class
        /// notes), then allow it to activate and track it. Returns the same operation for the caller to keep;
        /// activation, its frame and its <c>completed</c> callbacks are the engine's as before. For a load just
        /// issued from main-thread game or test code, never from a physics callback or <c>OnDestroy</c>.
        /// Call sites in game code sit inside <c>#if UNITY_EDITOR</c>.
        /// </summary>
        public static AsyncOperation HoldUntilRead(AsyncOperation op)
        {
            HoldUntilRead(op, out _);
            return op;
        }

        /// <summary><see cref="HoldUntilRead(AsyncOperation)"/>, also saying how the hold ended.</summary>
        public static AsyncOperation HoldUntilRead(AsyncOperation op, out Outcome outcome)
        {
            outcome = Outcome.NotHeld;
            if (op == null) return null;
            if (op.isDone || !ShouldHold(s_inFlight, Timeouts, MaxTimeouts))
            {
                Track(op);
                return op;
            }

            // Holding activation back is what makes progress stop at ReadDoneProgress once the read is done, so
            // "read done" is observable; it is released before the caller ever sees the operation.
            op.allowSceneActivation = false;
            Stopwatch clock = Stopwatch.StartNew();
            outcome = WaitForRead(() => op.progress, () => clock.Elapsed.TotalSeconds,
                                  () => System.Threading.Thread.Sleep(1), CapSeconds);
            if (outcome == Outcome.ReadDone)
            {
                HeldLoads++;
            }
            else
            {
                Timeouts++;
                Debug.LogWarning("[SceneLoadHold] a scene load's threaded read did not finish within " + CapSeconds +
                                 " s while the main thread was held (progress " + op.progress.ToString("0.00") +
                                 "); after " + MaxTimeouts + " of these, later loads this session are not held");
            }
            op.allowSceneActivation = true;
            Track(op);
            return op;
        }
    }
}
