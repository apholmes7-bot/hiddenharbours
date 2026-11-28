using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One frame of what the player asks of his camera</b> (ADR 0043; owner 2026-11-13: the camera is
    /// on P). The <c>Camera</c> map of the bindings asset: <see cref="Raise"/> lifts or lowers it (an edge,
    /// P / the pad's Y), <see cref="Shutter"/> takes the picture (an edge, Space / the pad's right trigger)
    /// and <see cref="Aim"/> slides the frame while it is up (the move keys / the left stick).
    ///
    /// <para>The reader is the camera itself, which only answers on foot and on a deck: at the helm P is
    /// the autopilot's (the Helm map), so the two never both hear one press.</para>
    /// </summary>
    public readonly struct CameraIntents
    {
        public readonly bool Raise;
        public readonly bool Shutter;
        public readonly Vector2 Aim;

        public CameraIntents(bool raise, bool shutter, Vector2 aim)
        {
            Raise = raise;
            Shutter = shutter;
            Aim = aim;
        }

        public static CameraIntents None => default;
    }

    /// <summary>The scripted camera source. Presses are edges: set for one frame, then
    /// <see cref="HeldIntents{TIntents}.Release"/>.</summary>
    public sealed class HeldCameraIntents : HeldIntents<CameraIntents>
    {
        public void PressRaise() => Set(new CameraIntents(true, false, Vector2.zero));
        public void PressShutter() => Set(new CameraIntents(false, true, Vector2.zero));
        public void HoldAim(Vector2 aim) => Set(new CameraIntents(false, false, aim));
    }

    /// <summary>
    /// <b>Where the camera's device-backed source is installed</b> (the <see cref="AutopilotInput"/>
    /// shape). The device layer (Player) installs it before the first scene; the camera takes it from
    /// here. A test hands its own source to the camera, never to this slot.
    /// </summary>
    public static class CameraInput
    {
        private static IControlIntentSource<CameraIntents> _device;

        /// <summary>The bindings asset's camera source, or null before the device layer installed one.</summary>
        public static IControlIntentSource<CameraIntents> Device => _device;

        public static void InstallDevice(IControlIntentSource<CameraIntents> source) => _device = source;
    }
}
