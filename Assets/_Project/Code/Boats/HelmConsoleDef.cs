using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Data definition for a hull's HELM CONSOLE — the diegetic dashboard rig it carries and the
    /// equipment slots the player can upgrade. Content is data, not code (ADR 0003): a boat earns a
    /// console by pointing its <see cref="BoatHullDef.Helm"/> at one of these assets, never by a new class.
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Helm Console, save in Data/Boats/Helms.
    ///
    /// <para>Mirrors the existing <c>BoatVisualDef</c> / <c>DeckContainerDef</c> pointer pattern on
    /// <see cref="BoatHullDef"/>. Unlike <c>BoatVisualDef</c> (which carries a finite baked compass
    /// <c>Sprite[]</c>), this def carries NO baked art — it only NAMES the runtime renderer
    /// (<see cref="ConsoleRigKind"/>), because helm instruments are continuous-state and how they render is
    /// the open question of ADR 0025. The equipment fields are pure data the sim and UI read.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Helm Console", fileName = "HelmConsole")]
    public class HelmConsoleDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, type.snake_case (e.g. helm.console_skiff). Append-only & stable (rule 2).")]
        public string Id = "helm.console";

        [Header("Renderer")]
        [Tooltip("Which helm rig draws this dash (Console/Sport/Novi/Cape). Names the renderer only — " +
                 "carries no baked art (ADR 0025).")]
        public ConsoleRigKind Rig = ConsoleRigKind.Console;

        [Tooltip("The binnacle lever's housing finish on this console (LeverRig.SPECS): graphite = the " +
                 "matte centre-console housing, chrome = polished stainless (sport/novi/cape). Data, " +
                 "not code — the S1 lever overlay draws whichever this console says.")]
        public HelmLeverFinish Lever = HelmLeverFinish.Graphite;

        [Header("Default fit — what this hull ships with")]
        [Tooltip("The sounder fitted out of the box (Depth = the basic read; None = bare brow).")]
        public SounderKind DefaultSounder = SounderKind.Depth;
        [Tooltip("The compass fitted out of the box (usually None until the player buys one).")]
        public CompassMount DefaultCompass = CompassMount.None;
        public bool DefaultRadar = false;
        public bool DefaultGps = false;

        [Header("Slot capabilities — what this helm CAN be upgraded to")]
        [Tooltip("This helm's brow can take the colour fish-finder upgrade (else depth sounder only).")]
        public bool SupportsFishFinder = true;
        [Tooltip("This helm can mount a bracket DOME compass on the crown.")]
        public bool SupportsDomeCompass = true;
        [Tooltip("This helm can sink a FLUSH Ritchie compass into the dash (Novi & Cape only, per the rig set).")]
        public bool SupportsFlushCompass = false;
        [Tooltip("This helm has a RADAR brow slot (Novi & Cape). Placeholder MFD until the radar rig ships.")]
        public bool SupportsRadar = false;
        [Tooltip("This helm has a GPS brow slot (Novi & Cape). Placeholder until the gps rig ships.")]
        public bool SupportsGps = false;

        /// <summary>Can this helm mount the given compass form? (None is always allowed.)</summary>
        public bool SupportsCompassMount(CompassMount mount)
            => mount == CompassMount.None
            || (mount == CompassMount.Dome  && SupportsDomeCompass)
            || (mount == CompassMount.Flush && SupportsFlushCompass);
    }
}
