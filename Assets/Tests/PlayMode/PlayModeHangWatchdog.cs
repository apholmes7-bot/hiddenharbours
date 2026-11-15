using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HiddenHarbours.Tests.Support;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestRunner;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    ///
    /// <para><b>Stopped or turning (2026-11-15).</b> A main-thread hook (<see cref="MainThreadPulse"/>) records
    /// the last frame, the time scale and the scene events, and the verdict says whether the main thread had
    /// STOPPED (blocked inside one frame: an engine stall) or was still TURNING (a test waiting on something that
    /// never completes). When it had stopped on the Linux editor, the watchdog then sends SIGABRT to the main
    /// thread alone (tgkill), so the editor's own crash handler prints that thread's native stack to the log,
    /// gives it <see cref="NativeStackWaitSeconds"/> s, and ends the editor as before if it is still alive. The
    /// exit code may then be the crash handler's rather than 124; playmode-hang.txt is written first either way
    /// and is what the workflow reads. Before the abort it logs every thread's /proc state and, when the main
    /// thread stopped, every thread's gdb stack (the workflow installs gdb), also saved to
    /// playmode-hang-threads.txt: run 43754485394 showed the main thread waiting on a lock, and only the other
    /// threads' stacks name its holder.</para>
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

        /// <summary>How long the editor's crash handler gets to print the stopped main thread's native stack.</summary>
        const int NativeStackWaitSeconds = 30;

        /// <summary>The most gdb gets to attach and print every thread's stack before it is killed.</summary>
        const int GdbSeconds = 180;

        const string ThreadsFile = "playmode-hang-threads.txt";

        const int SigAbrt = 6;
        const long SysTgkillX64 = 234; // x86_64 syscall number, for a libc without the tgkill wrapper

        // Static: UTF may construct the callback more than once; one watch and one thread per domain.
        static readonly TestHangWatch Watch = new TestHangWatch();
        static readonly MainThreadPulse Pulse = new MainThreadPulse();
        static bool _pulseHooked; // main thread only
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
            HookPulse();
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

        /// <summary>Main thread, once per domain: a per-frame tick (no allocation) and the scene events.</summary>
        static void HookPulse()
        {
            if (_pulseHooked) return;
            _pulseHooked = true;
#if UNITY_EDITOR
            EditorApplication.update += TickPulse;
#endif
            SceneManager.sceneLoaded += (scene, mode) => NoteScene("loaded " + scene.name + " (" + mode + ")");
            SceneManager.sceneUnloaded += scene => NoteScene("unloaded " + scene.name);
            SceneManager.activeSceneChanged += (from, to) => NoteScene("activated " + to.name);
            NoteScene("watchdog armed");
        }

        static float _nextSceneRefresh;

        static void TickPulse()
        {
            Pulse.Tick(DateTime.UtcNow, Time.frameCount, Time.timeScale);
            // Every few seconds, re-read the scene list too, so a scene still loading (isLoaded false) shows up
            // even though no scene event has fired for it yet. A few small strings a minute, never per frame.
            if (Time.realtimeSinceStartup < _nextSceneRefresh) return;
            _nextSceneRefresh = Time.realtimeSinceStartup + 2f;
            NoteScene(null);
        }

        /// <summary>Records the loaded-scene list; <paramref name="what"/> null keeps the last scene event.</summary>
        static void NoteScene(string what)
        {
            try
            {
                var names = new System.Text.StringBuilder();
                Scene active = SceneManager.GetActiveScene();
                for (int i = 0; i < SceneManager.sceneCount; ++i)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    if (i > 0) names.Append(", ");
                    names.Append(s.name);
                    if (s == active) names.Append('*');
                    if (!s.isLoaded) names.Append(" (not loaded: loading or unloading)");
                }
                Pulse.SceneEvent(what, names.Length == 0 ? "(none)" : names.ToString(), DateTime.UtcNow);
            }
            catch (Exception)
            {
                // A watchdog must never fail a test.
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
            DateTime now = DateTime.UtcNow;
            bool askForStack = Pulse.IsFrozen(now) && _platform == RuntimePlatform.LinuxEditor;
            string report = verdict + "\n" + Pulse.Describe(now) + "\n" + MainThreadStack(askForStack) + "\n";
            // The file and stderr first: neither needs the main thread, which may hold whatever it hangs on.
            TryWrite(HangFile, report);
            try { Console.Error.WriteLine(report); Console.Error.Flush(); } catch (Exception) { }
            // Debug.Log is thread-safe, but give it its own thread so a log lock held by the hang cannot stop the exit.
            var log = new Thread(() => { try { Debug.LogError("[HangWatchdog] " + report); } catch (Exception) { } }) { IsBackground = true };
            log.Start();
            log.Join(5000);
            Thread.Sleep(2000);
            if (_platform == RuntimePlatform.LinuxEditor)
            {
                // Run 43754485394 (2026-11-15): the main thread's stack showed it waiting on PersistentManager's
                // lock; the thread HOLDING that lock is the other half of the answer. Every thread, before the abort.
                Report(ThreadStates(), ThreadsFile, append: false);
                if (askForStack) Report(AllThreadStacks(), ThreadsFile, append: true);
            }
            if (askForStack && SignalMainThread())
                Thread.Sleep(NativeStackWaitSeconds * 1000); // the crash handler usually ends the editor itself
            Exit(TestHangWatch.HangExitCode);
        }

        // .NET Standard 2.1 (this assembly's profile) has no API that reads another thread's stack, and Mono
        // cannot snapshot one; say so rather than print a stack that is the watchdog's own.
        static string MainThreadStack(bool askingTheCrashHandler) => askingTheCrashHandler
            ? "Main thread stack: the main thread has stopped, so SIGABRT goes to it alone; the editor's crash handler " +
              "prints its native stack below this line in the job log (look for 'Caught fatal signal' / 'stack frames')."
            : "Main thread stack: not obtainable from managed code on this runtime. The last [HangWatchdog] START line " +
              "and the log lines just before the silence are the evidence.";

        /// <summary>To stderr (the job log) and appended to <paramref name="file"/> in the artifacts folder.</summary>
        static void Report(string text, string file, bool append)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { Console.Error.WriteLine(text); Console.Error.Flush(); } catch (Exception) { }
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, file);
                if (append) File.AppendAllText(path, text + "\n"); else File.WriteAllText(path, text + "\n");
            }
            catch (Exception) { }
        }

        /// <summary>Linux: one line per thread of this process from /proc (name, state, kernel wait channel).
        /// Needs no tool and no privilege; names which threads are blocked and which are running.</summary>
        static string ThreadStates()
        {
            try
            {
                var sb = new System.Text.StringBuilder("[HangWatchdog] threads (tid name state wchan):\n");
                foreach (string task in Directory.GetDirectories("/proc/self/task"))
                {
                    string tid = Path.GetFileName(task);
                    string comm = ReadTrim(Path.Combine(task, "comm"));
                    string state = "?";
                    try
                    {
                        foreach (string line in File.ReadAllLines(Path.Combine(task, "status")))
                            if (line.StartsWith("State:", StringComparison.Ordinal)) { state = line.Substring(6).Trim(); break; }
                    }
                    catch (Exception) { }
                    sb.Append("  ").Append(tid).Append(' ').Append(comm).Append(" | ").Append(state)
                      .Append(" | ").Append(ReadTrim(Path.Combine(task, "wchan"))).Append('\n');
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "[HangWatchdog] threads: /proc/self/task unreadable (" + e.GetType().Name + ")";
            }
        }

        static string ReadTrim(string path)
        {
            try { return File.ReadAllText(path).Trim(); } catch (Exception) { return "?"; }
        }

        /// <summary>Linux: every thread's native stack from gdb, when the runner has it (the workflow installs it).
        /// Through `sudo -n` because Yama stops a child from tracing its own parent; bounded by
        /// <see cref="GdbSeconds"/> so a slow symbol load cannot stop the exit.</summary>
        static string AllThreadStacks()
        {
            try
            {
                int pid = LinuxGetPid();
                var info = new System.Diagnostics.ProcessStartInfo("sudo",
                    "-n timeout -s KILL " + GdbSeconds + " gdb -p " + pid +
                    " -batch -nx -ex \"set pagination off\" -ex \"thread apply all bt 40\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                };
                using (var gdb = System.Diagnostics.Process.Start(info))
                {
                    if (gdb == null) return "[HangWatchdog] all-thread stacks: gdb did not start";
                    gdb.StandardInput.Close();
                    var err = gdb.StandardError.ReadToEndAsync();
                    string output = gdb.StandardOutput.ReadToEnd(); // ends when gdb ends (or is killed by timeout)
                    gdb.WaitForExit((GdbSeconds + 10) * 1000);
                    string tail = err.Wait(5000) ? err.Result : "";
                    return "[HangWatchdog] all-thread stacks (gdb exit " + (gdb.HasExited ? gdb.ExitCode.ToString(CultureInfo.InvariantCulture) : "?") +
                           "):\n" + output + (string.IsNullOrEmpty(tail) ? "" : "\n[gdb stderr]\n" + tail);
                }
            }
            catch (Exception e)
            {
                return "[HangWatchdog] all-thread stacks: not taken (" + e.GetType().Name + ": " + e.Message + ")";
            }
        }

        /// <summary>Linux: SIGABRT to the main thread only (its tid is the pid), so the crash handler runs on it
        /// and prints its stack. False when no signal could be sent.</summary>
        static bool SignalMainThread()
        {
            try
            {
                int pid = LinuxGetPid();
                int rc;
                try { rc = LinuxTgkill(pid, pid, SigAbrt); }
                catch (EntryPointNotFoundException) { rc = (int)LinuxSyscall(SysTgkillX64, pid, pid, SigAbrt); }
                if (rc != 0) return false;
                try { Console.Error.WriteLine("[HangWatchdog] SIGABRT sent to the stopped main thread (tid " + pid + ") for its native stack"); Console.Error.Flush(); } catch (Exception) { }
                return true;
            }
            catch (Exception)
            {
                return false; // no libc entry points here: fall back to the plain exit
            }
        }

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

        [DllImport("libc", EntryPoint = "getpid")]
        static extern int LinuxGetPid();

        [DllImport("libc", EntryPoint = "tgkill")]
        static extern int LinuxTgkill(int tgid, int tid, int sig);

        [DllImport("libc", EntryPoint = "syscall")]
        static extern long LinuxSyscall(long number, long a, long b, long c);

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
