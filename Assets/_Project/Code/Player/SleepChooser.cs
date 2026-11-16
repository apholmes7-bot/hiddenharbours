using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The keys and the words for choosing how long to sleep</b> (owner ruling 2026-11-15: "Player
    /// chooses sleep duration. Time moves fast with player/s able to cancel with a button."). The minimal
    /// chooser: the rules live in Core's <see cref="SleepFlow"/>; this reads the keys, drives the flow's
    /// fast night with the real seconds of each frame, and says what is on offer through
    /// <see cref="DevNotice"/>, the greybox's one line of player-facing text. A proper bedside picture
    /// (a clock face by the pillow) reads the same <see cref="SleepFlowChanged"/> and replaces the line.
    ///
    /// <para><b>The keys.</b> Lying down: left/right (A/D, the arrows, the d-pad) shorter or longer; E,
    /// Enter, Space or the gamepad's south button to sleep; Backspace or the east button to get up.
    /// Asleep: any of those wakes you. Escape stays the pause menu's, and a paused game pauses the night.
    /// The press that lay you down is not read as the press that confirms: input is ignored on the frame
    /// the chooser opens.</para>
    ///
    /// <para><b>Only in a real session.</b> Installed before the first scene like the hit-stop host, but it
    /// presents itself to <see cref="SleepFlow"/> only once the shell has entered the world
    /// (<see cref="ShellPhaseChanged"/> to Playing). A test rig or a scene opened straight in the editor
    /// never runs the shell, so its rest stays the instant one-press night that its tests pin. Text is
    /// built on a change only, never per frame.</para>
    /// </summary>
    public sealed class SleepChooser : MonoBehaviour
    {
        private static bool _installed;
        private int _openedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("SleepChooser") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SleepChooser>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ShellPhaseChanged>(OnShellPhase);
            EventBus.Subscribe<SleepFlowChanged>(OnSleepChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ShellPhaseChanged>(OnShellPhase);
            EventBus.Unsubscribe<SleepFlowChanged>(OnSleepChanged);
            SleepFlow.ChooserPresent = false;
            SleepFlow.Abandon();
        }

        private void OnShellPhase(ShellPhaseChanged e)
        {
            if (e.Phase == ShellPhase.Playing) SleepFlow.ChooserPresent = true;
            else SleepFlow.Abandon();   // back at the title: nobody is in bed
        }

        private void OnSleepChanged(SleepFlowChanged e)
        {
            if (e.Phase == SleepPhase.Choosing && _openedFrame < 0) _openedFrame = Time.frameCount;
            if (e.Phase == SleepPhase.Awake) _openedFrame = -1;
            string line = PromptFor(e, GameServices.SecondsPerDay);
            if (line.Length > 0) EventBus.Publish(new DevNotice(line));
        }

        private void Update()
        {
            SleepPhase phase = SleepFlow.Phase;
            if (phase == SleepPhase.Awake) return;
            if (ShellFlow.WorldInputBlocked) return;               // the pause menu has the keys
            if (Time.frameCount == _openedFrame) return;          // the press that lay you down

            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool confirm = Pressed(kb, Key.E) || Pressed(kb, Key.Enter) || Pressed(kb, Key.Space)
                           || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = Pressed(kb, Key.Backspace) || (pad != null && pad.buttonEast.wasPressedThisFrame);

            if (phase == SleepPhase.Choosing)
            {
                if (back) { SleepFlow.Decline(); return; }
                if (confirm) { SleepFlow.Confirm(); return; }
                int dir = 0;
                if (Pressed(kb, Key.D) || Pressed(kb, Key.RightArrow) || (pad != null && pad.dpad.right.wasPressedThisFrame)) dir++;
                if (Pressed(kb, Key.A) || Pressed(kb, Key.LeftArrow) || (pad != null && pad.dpad.left.wasPressedThisFrame)) dir--;
                if (dir != 0) SleepFlow.Adjust(dir);
                return;
            }

            // Asleep: the button wakes you; otherwise the night runs on real (unscaled) seconds, so the
            // landing hit-stop's feel dip can never slow it.
            if (confirm || back) { SleepFlow.Cancel(); return; }
            SleepFlow.Tick(Time.unscaledDeltaTime);
        }

        private static bool Pressed(Keyboard kb, Key key) => kb != null && kb[key].wasPressedThisFrame;

        /// <summary>
        /// The line for <paramref name="e"/>: the offer while choosing, a short line on falling asleep, and
        /// nothing on waking (the save's own <see cref="GameSaved"/> says that). Pure, so the words are a test.
        /// </summary>
        public static string PromptFor(in SleepFlowChanged e, double secondsPerDay)
        {
            switch (e.Phase)
            {
                case SleepPhase.Choosing:
                    return $"Sleep {HoursText(e.Hours)}, till about {ClockText(e.WakeSeconds, secondsPerDay)}?  " +
                           "Left/right: shorter or longer.  E: sleep.  Backspace: get up.";
                case SleepPhase.Asleep:
                    return $"Asleep till about {ClockText(e.WakeSeconds, secondsPerDay)}.  E: wake up.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>"8 hours", "1 hour", "7½ hours" style, to the nearest quarter hour.</summary>
        public static string HoursText(float hours)
        {
            float q = Mathf.Round(hours * 4f) / 4f;
            if (Mathf.Approximately(q, 1f)) return "1 hour";
            int whole = Mathf.FloorToInt(q);
            float frac = q - whole;
            string tail = frac >= 0.74f ? "¾" : frac >= 0.49f ? "½" : frac >= 0.24f ? "¼" : string.Empty;
            return whole == 0 ? $"{tail} hour" : $"{whole}{tail} hours";
        }

        /// <summary>The clock face's reading of <paramref name="totalSeconds"/>, "06:00".</summary>
        public static string ClockText(double totalSeconds, double secondsPerDay)
        {
            if (!(secondsPerDay > 0d)) return "--:--";
            double day = totalSeconds % secondsPerDay;
            if (day < 0d) day += secondsPerDay;
            int minutes = (int)System.Math.Round(day / secondsPerDay * 24d * 60d) % (24 * 60);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }
}
