using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One light station: where it stands in its key scene and what its lantern shows.</b> The buildings are
    /// drawn from the station rig's baked sprites; this is the part a keeper would write in the light list:
    /// the character, how fast the lens turns, how far the beams reach. Everything the lantern does is worked
    /// out from the clock and these numbers (rule 5), so nothing about it is saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Light Station", fileName = "LightStation_")]
    public sealed class LightStationDef : ScriptableObject
    {
        [Tooltip("Stable id, lightstation.snake_case. Append-only.")]
        public string Id = "lightstation.example";

        [Tooltip("The name on the chart.")]
        public string DisplayName = "";

        [Tooltip("The key scene this station belongs to.")]
        public string KeySceneId = "";

        [Tooltip("The key scene's row that says where the tower stands.")]
        public string TowerPieceId = "";

        [Header("The light")]
        [Tooltip("The light's character as charted, e.g. 'Fl W 6s'. A label; the numbers below are what is shown.")]
        public string Character = "Fl W 6s";

        [Tooltip("Seconds for the lens to turn once. With two beams, a flash is seen every half turn.")]
        [Min(0.1f)] public float TurnSeconds = 12f;

        [Tooltip("Beams the lens throws, evenly spaced round the turn.")]
        [Range(1, 8)] public int Beams = 2;

        [Tooltip("Seconds into the turn at game time zero, so two stations need not turn together.")]
        public float PhaseSeconds;

        [Tooltip("How far either side of pointing straight at the viewer a beam still reads as the flash, degrees.")]
        [Range(1f, 90f)] public float FlashHalfAngleDegrees = 14f;

        [Header("How it is drawn")]
        [Tooltip("How far a beam reaches over the ground and water, metres.")]
        [Min(1f)] public float BeamReachMetres = 46f;

        [Tooltip("Half the spread of the beam's lamp, degrees. The lamp draws about half of this at a long reach.")]
        [Range(1f, 45f)] public float BeamHalfAngleDegrees = 11f;

        [Tooltip("The beam's brightness.")]
        [Min(0f)] public float BeamIntensity = 0.2f;

        [Tooltip("The steady glow of the lit lantern room, metres across its halo.")]
        [Min(0.1f)] public float LanternGlowMetres = 1.8f;

        [Tooltip("The lantern room's steady brightness.")]
        [Min(0f)] public float LanternIntensity = 0.7f;

        [Tooltip("The halo of the flash when a beam crosses the viewer, metres.")]
        [Min(0.1f)] public float FlashGlowMetres = 7f;

        [Tooltip("The flash's brightness.")]
        [Min(0f)] public float FlashIntensity = 1.5f;

        [Tooltip("The colour of the light.")]
        [ColorUsage(false, true)] public Color Colour = new Color(0.965f, 0.839f, 0.518f, 1f);

        [Header("The keeper's lamp")]
        [Tooltip("The lamp the keeper lights at the house door, from the tower's ground: x across, y away (ground metres), z up.")]
        public Vector3 KeepersLampAt = new Vector3(-5.7f, -3.6f, 2f);

        [Tooltip("How far the keeper's lamp reaches on the ground, metres.")]
        [Min(0.1f)] public float KeepersLampReachMetres = 3.4f;

        [Header("How soft it is")]
        [Tooltip("How gently a beam fades along its length: 0 a hard end, 1 a long fade to nothing.")]
        [Range(0f, 1f)] public float BeamEdgeSoftness = 0.85f;

        [Tooltip("How far a beam's sides feather in, as a share of its half-angle: 0 a hard edge, 1 soft all the way across.")]
        [Range(0f, 1f)] public float BeamAngularSoftness = 0.4f;

        [Tooltip("How gently the flash's halo fades to its rim.")]
        [Range(0f, 1f)] public float FlashEdgeSoftness = 0.9f;

        [Tooltip("The hot core of the lit lamp, metres across its radius. 0 draws the lantern as a plain glow.")]
        [Min(0f)] public float LanternCoreMetres;

        [Tooltip("How bright the glow is beside that core (core = 1). Read only when the core is above 0.")]
        [Range(0f, 1f)] public float LanternHaloStrength = 0.35f;

        [Tooltip("How far the core is pulled toward white.")]
        [Range(0f, 1f)] public float LanternCoreWhiten = 0.3f;

        [Header("The lantern's glass")]
        [Tooltip("The lantern room's panes as the tower picture draws them, metres from the tower's foot: x across, " +
                 "y up the picture. Each is drawn as lit glass from dusk to dawn. None leaves the glass dark.")]
        public Rect[] GlassPanes = new Rect[0];

        [Tooltip("How brightly the lit glass burns.")]
        [Min(0f)] public float GlassIntensity = 0.9f;

        [Tooltip("The glass's brightness at the head of a pane, as a share.")]
        [Range(0f, 1.5f)] public float GlassHeadLevel = 0.75f;

        [Tooltip("The glass's brightness at the sill of a pane, as a share.")]
        [Range(0f, 1.5f)] public float GlassSillLevel = 1f;

        [Tooltip("How much brighter a pane is in its middle, where the lens stands behind it. Bright enough and the " +
                 "middle burns toward white: the hot core.")]
        [Range(0f, 1.5f)] public float GlassCentreLift = 0.6f;

        [Tooltip("How much dimmer a pane is at its sides: 0 even across, 1 dark at the edges.")]
        [Range(0f, 1f)] public float GlassAcrossFalloff = 0.35f;

        [Tooltip("Steps of brightness across a pane, for the pixel look: 0 smooth.")]
        [Range(0, 8)] public int GlassBands = 3;

        [Header("The glass's spill on the deck, rail and tower top")]
        [Tooltip("Where the lit glass throws its light round the lantern, metres from the tower's foot as the tower " +
                 "picture draws it: x left edge, y right edge, z the top edge (by the glass, where it is strongest), " +
                 "w the reach: how far down it falls off to nothing. The light is an ellipse filling the span, so it " +
                 "fades out before the sides, top and foot: size a span to the gallery's deck and rail. None: the " +
                 "deck stays dark.")]
        public Vector4[] GlassSpill = new Vector4[0];

        [Tooltip("How bright the spill is at its strongest: light added over the night, so small numbers (0.05 to " +
                 "0.15) read as a soft glow on a dark deck.")]
        [Min(0f)] public float GlassSpillStrength = 0.06f;

        [Tooltip("Where the spill peaks, as a share of a span's height down from its top edge: 0 at the top edge (it " +
                 "fades only sideways and down); above 0 it fades upward too, so the top edge curves like the foot.")]
        [Range(0f, 0.9f)] public float GlassSpillPeakDepth = 0.3f;

        [Tooltip("Thin the spill's last (dimmest) band to every other art pixel, so its outer edge breaks up.")]
        public bool GlassSpillDitherLastBand = true;

        [Tooltip("Steps the spill falls off in, for the pixel look.")]
        [Range(1, 8)] public int GlassSpillBands = 3;
    }
}
