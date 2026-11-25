using System;
using System.Globalization;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>
    /// The bookkeeping half of the PlayMode hang watchdog (<c>PlayModeHangWatchdog</c> in the PlayMode test
    /// assembly): which test (or gap between tests) is running, since when, and whether it has run past its
    /// limit. Pure and clock-free, so EditMode can prove it; the watchdog feeds it test events on the main
    /// thread and asks <see cref="Check"/> from a background thread, hence the lock.
    ///
    /// WHY (2026-11-14): CI's PlayMode step sometimes goes silent until the job's 150-minute cap and ends
    /// "cancelled" with no results file and no test named (run 43623934300 attempt 1: the log stops at
    /// 19:29:30 inside NmcWharfWaterSidePlayTests and nothing more is written for 58 minutes). UTF's
    /// [Timeout] cannot catch that: it is checked between yields on the main thread, and a blocked main
    /// thread never yields. Only another thread can notice.
    /// </summary>
    public sealed class TestHangWatch
    {
        /// <summary>
        /// The one place the limits live. A test with no [Timeout] of its own may run this long. The slowest
        /// PlayMode test in green run 43623934300 attempt 2 took 160 s (1,398 cases; slowest fixture 722 s), so
        /// 600 s is ~3.75x the slowest test and still turns a 2.5-hour hang into ~10 minutes.
        /// </summary>
        public const int DefaultLimitSeconds = 600;

        /// <summary>A test that declares [Timeout(ms)] is given that much plus this grace (UTF should fail it
        /// itself at its timeout if the main thread still turns; the watchdog only fires when it does not).</summary>
        public const int DeclaredTimeoutGraceSeconds = 120;

        /// <summary>The editor's exit code when the watchdog fires: coreutils' "timed out", unused by Unity's
        /// test runner (0 pass, 2 failures, 3 run error), so the red job is readable as a hang.</summary>
        public const int HangExitCode = 124;

        readonly object _gate = new object();
        string _what;
        DateTime _since;
        int _limitSeconds;
        bool _armed;
        bool _fired;

        /// <summary>The limit a test gets, from its declared [Timeout] in ms (0 or less = none declared).</summary>
        public static int LimitFor(int declaredTimeoutMs)
        {
            if (declaredTimeoutMs <= 0) return DefaultLimitSeconds;
            long declared = (declaredTimeoutMs + 999L) / 1000L + DeclaredTimeoutGraceSeconds;
            return (int)Math.Min(int.MaxValue, Math.Max(DefaultLimitSeconds, declared));
        }

        /// <summary>The run began (play mode is up); time spent before the first test counts too.</summary>
        public void RunStarted(DateTime nowUtc) => Set("the start of the run, before any test began", nowUtc, DefaultLimitSeconds);

        /// <summary>A test or suite began. Suites count too: a fixture's OneTimeSetUp runs after its start.</summary>
        public void Started(string fullName, bool isSuite, int declaredTimeoutMs, DateTime nowUtc) =>
            Set((isSuite ? "suite " : "test ") + fullName, nowUtc, isSuite ? DefaultLimitSeconds : LimitFor(declaredTimeoutMs));

        /// <summary>A test or suite ended; the gap until the next event (fixture teardown, scene unload) is timed.</summary>
        public void Finished(string fullName, bool isSuite, DateTime nowUtc) =>
            Set("the gap after " + (isSuite ? "suite " : "test ") + fullName + " ended", nowUtc, DefaultLimitSeconds);

        /// <summary>The run is over; nothing fires after this.</summary>
        public void Stop()
        {
            lock (_gate) _armed = false;
        }

        /// <summary>What is running now, for the log trail; null before the run starts.</summary>
        public string Current
        {
            get { lock (_gate) return _armed ? _what : null; }
        }

        /// <summary>
        /// Null while everything is within its limit. Once the current test or gap has run past its limit,
        /// returns the one-line verdict exactly once (later calls return null) so the watchdog acts once.
        /// </summary>
        public string Check(DateTime nowUtc)
        {
            lock (_gate)
            {
                if (!_armed || _fired) return null;
                double ran = (nowUtc - _since).TotalSeconds;
                if (ran <= _limitSeconds) return null;
                _fired = true;
                return string.Format(CultureInfo.InvariantCulture,
                    "HANG: {0} has run {1:0} s (limit {2} s, since {3:yyyy-MM-ddTHH:mm:ssZ})",
                    _what, Math.Floor(ran), _limitSeconds, _since);
            }
        }

        void Set(string what, DateTime nowUtc, int limitSeconds)
        {
            lock (_gate)
            {
                _what = what;
                _since = nowUtc;
                _limitSeconds = limitSeconds;
                _armed = true;
            }
        }
    }
}
