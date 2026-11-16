using System;
using System.Globalization;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>
    /// The PlayMode hang watchdog's view of the main thread: when it last turned a frame, which frame, at what
    /// time scale, and the last scene event it saw. The watchdog's main-thread hook calls <see cref="Tick"/> once a
    /// frame and <see cref="SceneEvent"/> on every scene load, unload and activation; its background thread asks
    /// <see cref="Describe"/> when a test overruns. Pure and clock-free, so EditMode proves the wording.
    ///
    /// WHY (2026-11-15): two CI hangs on one day (runs 43731633266 and 43745942123, attempt 1 of each) went
    /// silent right after a LoadSceneAsync was issued, and the verdict could not say whether the main thread had
    /// stopped (a native stall) or was still turning frames while a test waited on something that never came.
    /// That is the first question of any hang; this answers it in the verdict.
    ///
    /// <para><see cref="Tick"/> allocates nothing: it runs every frame of the PlayMode run.</para>
    /// </summary>
    public sealed class MainThreadPulse
    {
        /// <summary>No frame for this long while a test is overrunning reads as a stopped main thread. The
        /// slowest single frame on CI is a scene integration of about a second; this is far past that.</summary>
        public const int FrozenAfterSeconds = 60;

        readonly object _gate = new object();
        DateTime _lastTick;
        int _frame;
        float _timeScale;
        int _ticks;
        string _lastSceneEvent;
        DateTime _lastSceneEventAt;
        string _scenes;

        /// <summary>One main-thread frame. No allocation.</summary>
        public void Tick(DateTime nowUtc, int frame, float timeScale)
        {
            lock (_gate)
            {
                _lastTick = nowUtc;
                _frame = frame;
                _timeScale = timeScale;
                if (_ticks < int.MaxValue) ++_ticks;
            }
        }

        /// <summary>A scene was loaded, unloaded or activated; <paramref name="loadedScenes"/> is the list after it.
        /// A null <paramref name="what"/> is a plain refresh of the list and keeps the last event.</summary>
        public void SceneEvent(string what, string loadedScenes, DateTime nowUtc)
        {
            lock (_gate)
            {
                if (what != null)
                {
                    _lastSceneEvent = what;
                    _lastSceneEventAt = nowUtc;
                }
                _scenes = loadedScenes;
            }
        }

        /// <summary>True when frames had been seen and none has come for <see cref="FrozenAfterSeconds"/>.</summary>
        public bool IsFrozen(DateTime nowUtc)
        {
            lock (_gate) return _ticks > 0 && (nowUtc - _lastTick).TotalSeconds >= FrozenAfterSeconds;
        }

        /// <summary>One line for the hang verdict: frozen or turning, the last frame, the scenes, the last scene event.</summary>
        public string Describe(DateTime nowUtc)
        {
            lock (_gate)
            {
                string scenes = string.Format(CultureInfo.InvariantCulture, " Loaded scenes: {0}. Last scene event: {1}.",
                    _scenes ?? "(none seen)",
                    _lastSceneEvent == null
                        ? "(none seen)"
                        : _lastSceneEvent + " at " + _lastSceneEventAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                if (_ticks == 0)
                    return "Main thread: no frame was recorded (the pulse hook never ran), so stopped vs turning is unknown." + scenes;
                double ago = Math.Floor((nowUtc - _lastTick).TotalSeconds);
                string state = ago >= FrozenAfterSeconds
                    ? "STOPPED: no frame for {0:0} s (last frame {1} at {2:yyyy-MM-ddTHH:mm:ssZ}, timeScale {3}). The main thread is blocked inside one frame; a waiting test would still turn frames."
                    : "TURNING: last frame {1} was {0:0} s ago (timeScale {3}). Frames still advance, so the test is waiting on something that never completes.";
                return "Main thread " + string.Format(CultureInfo.InvariantCulture, state, ago, _frame, _lastTick, _timeScale) + scenes;
            }
        }
    }
}
