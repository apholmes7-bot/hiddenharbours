using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>The kinds of device a player may be holding. Append-only: a Core contract, and the HUD's
    /// glyph tables will index it.</summary>
    public enum ControlDevice { KeyboardMouse = 0, Gamepad = 1 }

    /// <summary>
    /// Raised when the LAST-USED device changes — the player picked up the pad, or went back to the
    /// keys. The one signal a surface that draws a glyph rides (the interact affordance, the HUD's
    /// hints, the settings sheet's "active scheme" line), so a prompt never shows an E to a hand holding
    /// a pad. Published on a genuine change only, never per frame.
    /// </summary>
    public readonly struct ActiveControlDeviceChanged
    {
        public readonly ControlDevice Device;
        public readonly ControlDevice Previous;

        public ActiveControlDeviceChanged(ControlDevice device, ControlDevice previous)
        {
            Device = device;
            Previous = previous;
        }
    }

    /// <summary>
    /// <b>Which device the player last touched</b> — a Core fact so any module can ask it without
    /// naming the device layer (rule 4; ADR 0043 §"the device signal").
    ///
    /// <para><b>Who writes it.</b> Only a device-backed intent source, on the frame a bound control is
    /// actuated: the source that read the actuation is the one thing in the project that knows what
    /// device it came from, and the intent it hands on deliberately does not say. A held (scripted)
    /// source reports nothing — a test driving the fisher is not a hand on a pad.</para>
    ///
    /// <para><b>Keyboard-and-mouse until told otherwise.</b> The project is PC-first (ADR 0005), a box
    /// with no pad plugged in must show keyboard glyphs from the first frame, and there is no honest
    /// way to know a device is in a hand until it is used.</para>
    /// </summary>
    public static class ActiveControlDevice
    {
        /// <summary>The device the player last used.</summary>
        public static ControlDevice Current { get; private set; } = ControlDevice.KeyboardMouse;

        /// <summary>A device-backed source saw an actuation from <paramref name="device"/>. Publishes
        /// <see cref="ActiveControlDeviceChanged"/> only when the answer changes.</summary>
        public static void Report(ControlDevice device)
        {
            if (device == Current) return;
            ControlDevice previous = Current;
            Current = device;
            EventBus.Publish(new ActiveControlDeviceChanged(device, previous));
        }

        /// <summary>Back to the keyboard (teardown / tests). Publishes nothing.</summary>
        public static void Reset() => Current = ControlDevice.KeyboardMouse;

        // Statics survive a play session when the editor's domain reload is disabled (the ShellFlow
        // pattern): re-seed at subsystem registration so every launch starts on the keyboard.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
