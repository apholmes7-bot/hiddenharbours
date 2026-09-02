using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The greybox keyboard, as a drive input source</b> — the read <c>ControlSwitcher</c> made
    /// inline before the seam existed, moved behind <see cref="IDriveInputSource"/> and otherwise
    /// untouched: W/S (and the arrows) are the throttle, A/D the wheel, Space the brake — the move axis
    /// the player already walks with, so there is nothing new to learn.
    ///
    /// <para><b>Byte-identical to what it replaces, and pinned.</b> The mapping is a pure function
    /// (<see cref="Map"/>) so a test can hold the keys in every combination without a device: ahead
    /// and astern cancel, left and right cancel, LEFT is +1 (the rig's own steering sense, so the A key,
    /// the drawn wheels and the yaw agree without a sign flip), and no keyboard at all is the same as
    /// no key held. The New-Input-System polling style is the one every other control in the Player
    /// lane uses.</para>
    /// </summary>
    public sealed class KeyboardDriveInputSource : IDriveInputSource
    {
        public DriveDemand Read()
        {
            var kb = Keyboard.current;
            if (kb == null) return DriveDemand.None;

            return Map(kb.wKey.isPressed || kb.upArrowKey.isPressed,
                       kb.sKey.isPressed || kb.downArrowKey.isPressed,
                       kb.aKey.isPressed || kb.leftArrowKey.isPressed,
                       kb.dKey.isPressed || kb.rightArrowKey.isPressed,
                       kb.spaceKey.isPressed);
        }

        /// <summary>The keys, as a demand. Pure, so the sense of every key is a testable claim rather
        /// than a thing read off a screen.</summary>
        public static DriveDemand Map(bool ahead, bool astern, bool left, bool right, bool brake)
        {
            float throttle = 0f, steer = 0f;
            if (ahead) throttle += 1f;
            if (astern) throttle -= 1f;
            if (left) steer += 1f;
            if (right) steer -= 1f;
            return new DriveDemand(throttle, steer, brake);
        }
    }
}
