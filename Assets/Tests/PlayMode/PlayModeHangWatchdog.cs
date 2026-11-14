using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HiddenHarbours.Tests.Support;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(HiddenHarbours.Tests.PlayMode.PlayModeHangWatchdog))]

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Turns a silent PlayMode hang on CI into a red job that names the test, minutes after it stalls instead of
    /// at the job's 150-minute cap. See <see cref="TestHangWatch"/> for why and for the limits.
    ///
    /// <para><b>What it does.</b> On every test and suite start it records the full name (in
    /// <see cref="TestHangWatch"/>), writes <c>[HangWatchdog] START &lt;name&gt;</c> to the log and overwrites
    /// <c>playmode-current-test.txt</c> in <c>$HH_TEST_WATCHDOG_DIR</c>. A background thread (the main thread is
    /// the one that hangs) checks every <see cref="PollSeconds"/>; once the current test or gap has run past its
    /// limit it writes the <c>HANG:</c> verdict to <c>playmode-hang.txt</c>, stderr and the log, then ends the
    /// editor with <see cref="TestHangWatch.HangExitCode"/>. The workflow's "Name the last PlayMode test" step
    /// prints both files.</para>
    ///
    /// <para><b>Inert unless batchmode on CI.</b> It arms only when <c>Application.isBatchMode</c> and
    /// <c>GITHUB_ACTIONS=true</c> (or <c>HH_TEST_HANG_WATCHDOG=1</c> to opt in locally), so a developer's
    /// editor never has a thread that can end it. Test assembly only: nothing ships in the playable build.</para>
    ///
    /// <para><b>Stacks.</b> Mono cannot snapshot another thread's managed stack; the verdict says so instead of
    /// pretending. The START trail and the last log lines before the silence are the evidence.</para>
    /// </summary>
    public sealed class PlayModeHangWatchdog : ITestRunCallback
    {
        /// <summary>How often the background thread looks.</summary>
        const int PollSeconds = 10;

        /// <summary>The workflow sets this to its artifacts folder; unset, the files go to the working directory.</summary>
        const string DirVariable = "HH_TEST_WATCHDOG_DIR";

        const string OptInVariable = "HH_TEST_HANG_WATCHDOG";
        const string CurrentFile = "playmode-current-test.txt";
        const string HangFile = "playmode-hang.txt";

        // Static: UTF may construct the callback more than once; one watch and one thread per domain.
        static readonly TestHangWatch Watch = new TestHangWatch();
        static readonly object StartGate = new object();
        static Thread _thread;
        static RuntimePlatform _platform; // read on the main thread: Unity's API is not the watchdog thread's to call
        static volatile bool _running;
        static int? _armed;

        static bool Armed
        {
            get
            {
                if (_armed == null)
                {
                    bool ci = string.Equals(System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                              || System.Environment.GetEnvironmentVariable(OptInVariable) == "1";
                    _armed = Application.isBatchMode && ci ? 1 : 0;
                }
                return _armed == 1;
            }
        }

        static string Dir
        {
            get
            {
                string dir = System.Environment.GetEnvironmentVariable(DirVariable);
                return string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : dir;
            }
        }

        public void RunStarted(ITest testsToRun)
        {
            // isPlaying: the callback is an assembly attribute, and only a PlayMode run is in play mode.
            if (!Armed || !Application.isPlaying) return;
            Watch.RunStarted(DateTime.UtcNow);
            EnsureThread();
        }

        public void RunFinished(ITestResult testResults)
        {
            Watch.Stop();
            if (!_running) return; // never armed in this run
            _running = false;
            Note("[HangWatchdog] run finished; watchdog stopped");
        }

        public void TestStarted(ITest test)
        {
            if (!Armed || !Application.isPlaying) return;
            EnsureThread();
            Watch.Started(test.FullName, test.IsSuite, DeclaredTimeoutMs(test), DateTime.UtcNow);
            Trail("START " + (test.IsSuite ? "suite " : "") + test.FullName);
        }

        public void TestFinished(ITestResult result)
        {
            if (!_running) return;
            Watch.Finished(result.Test.FullName, result.Test.IsSuite, DateTime.UtcNow);
            if (!result.Test.IsSuite) WriteCurrent("FINISHED " + result.Test.FullName + " (" + result.ResultState + ")");
        }

        /// <summary>The test's own [Timeout], or its fixture's, in ms; 0 when neither declares one.</summary>
        static int DeclaredTimeoutMs(ITest test)
        {
            for (ITest t = test; t != null; t = t.Parent)
            {
                if (t.Properties.ContainsKey(PropertyNames.Timeout) && t.Properties.Get(PropertyNames.Timeout) is int ms) return ms;
            }
            return 0;
        }

        static void EnsureThread()
        {
            lock (StartGate)
            {
                if (_thread != null && _thread.IsAlive) return;
                _platform = Application.platform;
                _running = true;
                _thread = new Thread(Loop) { IsBackground = true, Name = "HH PlayMode hang watchdog" };
                _thread.Start();
                Note(string.Format(CultureInfo.InvariantCulture,
                    "[HangWatchdog] armed: default limit {0} s per test or gap, [Timeout] + {1} s where declared; exit code {2} on a hang",
                    TestHangWatch.DefaultLimitSeconds, TestHangWatch.DeclaredTimeoutGraceSeconds, TestHangWatch.HangExitCode));
            }
        }

        static void Loop()
        {
            try
            {
                while (_running)
                {
                    Thread.Sleep(PollSeconds * 1000);
                    string verdict = Watch.Check(DateTime.UtcNow);
                    if (verdict != null)
                    {
                        Fire(verdict);
                        return;
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // The domain is unloading (play mode exited or reloaded): nothing to watch any more.
            }
        }

        static void Fire(string verdict)
        {
            string report = verdict + "\n" + MainThreadStack() + "\n";
            // The file and stderr first: neither needs the main thread, which may hold whatever it hangs on.
            TryWrite(HangFile, report);
            try { Console.Error.WriteLine(report); Console.Error.Flush(); } catch (Exception) { }
            // Debug.Log is thread-safe, but give it its own thread so a log lock held by the hang cannot stop the exit.
            var log = new Thread(() => { try { Debug.LogError("[HangWatchdog] " + report); } catch (Exception) { } }) { IsBackground = true };
            log.Start();
            log.Join(5000);
            Thread.Sleep(2000);
            Exit(TestHangWatch.HangExitCode);
        }

        // .NET Standard 2.1 (this assembly's profile) has no API that reads another thread's stack, and Mono
        // cannot snapshot one; say so rather than print a stack that is the watchdog's own.
        static string MainThreadStack() =>
            "Main thread stack: not obtainable from managed code on this runtime. The last [HangWatchdog] START line " +
            "and the log lines just before the silence are the evidence.";

        static void Trail(string line)
        {
            Note("[HangWatchdog] " + line);
            WriteCurrent(line);
        }

        static void WriteCurrent(string line) =>
            TryWrite(CurrentFile, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + " " + line + "\n");

        static void Note(string line) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line);

        static void TryWrite(string name, string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, name), text);
            }
            catch (Exception)
            {
                // A watchdog must never fail a test; a lost line is the lesser harm.
            }
        }

        [DllImport("libc", EntryPoint = "_exit")]
        static extern void LinuxExit(int code);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern bool TerminateProcess(IntPtr process, uint code);

        /// <summary>Ends the process now with <paramref name="code"/>. Not EditorApplication.Exit or
        /// Environment.Exit: both want the main thread, which is the one that is stuck.</summary>
        static void Exit(int code)
        {
            try
            {
                switch (_platform)
                {
                    case RuntimePlatform.LinuxEditor: LinuxExit(code); break;
                    case RuntimePlatform.WindowsEditor: TerminateProcess(GetCurrentProcess(), (uint)code); break;
                }
            }
            catch (Exception)
            {
                // No native exit here (an unmapped libc, say): fall through to the managed one.
            }
            var managed = new Thread(() => System.Environment.Exit(code)) { IsBackground = true };
            managed.Start();
            if (!managed.Join(15000)) System.Diagnostics.Process.GetCurrentProcess().Kill(); // 137, but ended
        }
    }
}
