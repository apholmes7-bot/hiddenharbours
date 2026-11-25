using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where a boat's cabin-light switch hands its setting to her lights.</b> The switch owns the
    /// state; the lights own the look (which windows glow, how brightly, and the automatic rule when the
    /// switch is on <see cref="CabinLightSwitchSetting.Auto"/>). This is the only seam between them.
    /// </summary>
    public interface ICabinLightsOutlet
    {
        /// <summary>Is the cabin showing lit right now? False when the lights cannot say.</summary>
        bool TryReadLit(out bool lit);

        /// <summary>Set the lights to this switch setting.</summary>
        void Apply(CabinLightSwitchSetting setting);
    }

    /// <summary>
    /// Finds the outlet for a boat root.
    ///
    /// <para><b>Today it finds none.</b> The lights' side of this seam is a Core contract that has not
    /// reached main yet. When it does, <see cref="ForRoot"/> returns a small wrapper over the lights
    /// found under the root, and the switch starts showing; until then a press still sets the state,
    /// raises its event and is remembered, and the windows keep their automatic rule.</para>
    /// </summary>
    public static class CabinLightsOutlet
    {
        /// <summary>The outlet for the boat at <paramref name="boatRoot"/>, or null when she has none.</summary>
        public static ICabinLightsOutlet ForRoot(Transform boatRoot) => null;
    }
}
