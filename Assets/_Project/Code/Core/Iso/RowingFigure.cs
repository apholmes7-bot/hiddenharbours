using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rower's body follows the oars.</b> At the oars the figure is seated and still (its gait
    /// is Idle), so the ordinary state map would draw <c>idle_oars</c> while the oars stroke. While an
    /// <see cref="IOarStroke"/> under the figure is stroking, this picks the rowing clip instead and
    /// picks its frame FROM THE OAR'S PHASE, so that the hands, the lean and the blades are one motion
    /// rather than two clocks drifting apart. When the oars rest, nothing here applies and the state
    /// map's idle comes back on its own.
    ///
    /// <para><b>Two clips, one preferred.</b> <see cref="RowStrokeKey"/> (<c>row_oars</c>) is ONE
    /// stroke per loop, sampled finely from the rig's rowing clip by the row-stroke contract. Until a
    /// skin carries it, the rig's own <see cref="OarsRowKey"/> (<c>walk_oars</c>) plays: it holds TWO
    /// strokes in its loop (the rig's pose reads <c>frac(2*u)</c>), so only the first half of its
    /// frames is one stroke. Either way the frame is a pure function of the oar phase.</para>
    ///
    /// <para><b>Which way the figure faces decides which oar moment is "hands out".</b> The handle sits
    /// across the oarlock from the blade, so it is aft when the blade is forward. A figure facing aft
    /// (the usual rower) reaches out toward the stern at the catch; a figure facing the bow has his
    /// hands in at the catch and pushes them out through the drive. The rig's clip runs hands out at
    /// its phase 0 and hands in at <see cref="RigHandsInShare"/>; <see cref="BodyPhase"/> lines that up
    /// with the oar for either facing.</para>
    ///
    /// <para>Presentation only, static and allocation-free; no clock of its own (rule 5).</para>
    /// </summary>
    public static class RowingFigure
    {
        /// <summary>The anim name of the one-stroke rowing clip.</summary>
        public const string RowAnim = "row";

        /// <summary>The state key of the one-stroke rowing clip (<c>row_oars</c>), written by the
        /// row-stroke contract's bake.</summary>
        public static readonly string RowStrokeKey =
            CharacterSkinStateMap.CarryKey(RowAnim, CharacterSkinStateMap.OarsCarry);

        /// <summary>The state key of the rig's own rowing clip (<c>walk_oars</c>, the rig's
        /// <c>oars_row</c> carry clip), which every rig-9 and rig-10 skin already carries.</summary>
        public static readonly string OarsRowKey =
            CharacterSkinStateMap.CarryKey(CharacterSkinStateMap.Walk, CharacterSkinStateMap.OarsCarry);

        /// <summary>Strokes in one loop of <see cref="RowStrokeKey"/>: the contract samples exactly one.</summary>
        public const int RowStrokeStrokesPerLoop = 1;

        /// <summary>Strokes in one loop of <see cref="OarsRowKey"/>. A RIG FACT, not a tunable: the
        /// rig's <c>oars_row</c> pose reads <c>ph = frac(2*u)</c>. A text guard holds it to the rig.</summary>
        public const int OarsRowStrokesPerLoop = 2;

        /// <summary>The share of the rig's stroke that brings the hands in. A RIG FACT, not a tunable:
        /// the rig's <c>oars_row</c> pose reads <c>drive = ph &lt; 0.55 ? ... : ...</c>, hands out at
        /// <c>ph = 0</c> and in at <c>ph = 0.55</c>. A text guard holds it to the rig.</summary>
        public const float RigHandsInShare = 0.55f;

        /// <summary>The figure's deck bearing faces the bow (0 degrees is the bow, per
        /// <see cref="ICharacterFigureStand.FigureDeckBearingDegrees"/>) when it is within a quarter
        /// turn of it.</summary>
        public static bool FacesBow(float deckBearingDegrees) =>
            Mathf.Cos(deckBearingDegrees * Mathf.Deg2Rad) >= 0f;

        /// <summary>The stroke phase at which the figure's hands are furthest out: the release for a
        /// figure facing the bow, the catch for one facing aft.</summary>
        public static float HandsOutStrokePhase(bool facesBow) =>
            facesBow ? OarStrokeMath.ReleasePhase : 0f;

        /// <summary>
        /// The rig's body phase (in its own one-stroke turns: 0 hands out, <paramref name="handsInShare"/>
        /// hands in) for an oar at <paramref name="strokePhase"/>. Each half of the oar's stroke maps onto
        /// one of the rig's two segments, so the hands are in exactly when the handles are and out exactly
        /// when they are.
        /// </summary>
        public static float BodyPhase(float strokePhase, bool facesBow, float handsInShare = RigHandsInShare)
        {
            float share = Mathf.Clamp01(handsInShare);
            float tau = OarStrokeMath.Wrap01(strokePhase - HandsOutStrokePhase(facesBow));
            float half = OarStrokeMath.ReleasePhase;
            float body = tau < half
                ? tau / half * share
                : share + (tau - half) / half * (1f - share);
            return OarStrokeMath.Wrap01(body);
        }

        /// <summary>
        /// The frame of a clip of <paramref name="frameCount"/> frames holding
        /// <paramref name="strokesPerLoop"/> strokes that shows <paramref name="bodyPhase"/> of the first
        /// stroke: the NEAREST sample, so a frame is centred on the phase it was sampled at. Only the
        /// first stroke's frames are ever chosen.
        /// </summary>
        public static int FrameFor(float bodyPhase, int frameCount, int strokesPerLoop)
        {
            if (frameCount <= 0) return 0;
            int perStroke = Mathf.Max(1, frameCount / Mathf.Max(1, strokesPerLoop));
            int frame = Mathf.FloorToInt(OarStrokeMath.Wrap01(bodyPhase) * perStroke + 0.5f);
            return frame >= perStroke ? 0 : frame;
        }

        /// <summary>True when <paramref name="stroke"/> is a live source that is stroking. A destroyed
        /// component reads as no source.</summary>
        public static bool IsLiveStroke(IOarStroke stroke)
        {
            if (stroke == null) return false;
            if (stroke is UnityEngine.Object unityObject && unityObject == null) return false;
            return stroke.IsStroking;
        }

        /// <summary>
        /// The rowing state and frame for a figure in <paramref name="stance"/> above
        /// <paramref name="stroke"/>, or false when the ordinary state map should decide: not at the
        /// oars, the oars resting, or no rowing clip the skin can draw as a mesh. Prefers
        /// <see cref="RowStrokeKey"/> and falls back to <see cref="OarsRowKey"/>.
        /// </summary>
        public static bool TryResolve(CharacterSkinDef skin, CharacterStance stance, IOarStroke stroke,
                                      float deckBearingDegrees, out string stateKey, out int frame)
        {
            stateKey = null;
            frame = 0;
            if (skin == null || stance != CharacterStance.Oars || !IsLiveStroke(stroke)) return false;

            int strokesPerLoop;
            CharacterSkinDef.SkinClip clip;
            if (Drawable(skin, RowStrokeKey, out clip)) { stateKey = RowStrokeKey; strokesPerLoop = RowStrokeStrokesPerLoop; }
            else if (Drawable(skin, OarsRowKey, out clip)) { stateKey = OarsRowKey; strokesPerLoop = OarsRowStrokesPerLoop; }
            else return false;

            float body = BodyPhase(stroke.StrokePhase, FacesBow(deckBearingDegrees));
            frame = FrameFor(body, clip.FrameCount, strokesPerLoop);
            return true;
        }

        private static bool Drawable(CharacterSkinDef skin, string key, out CharacterSkinDef.SkinClip clip)
        {
            if (!skin.DrawsAsMesh(key))
            {
                clip = default;
                return false;
            }
            return skin.TryGetClip(key, out clip) && clip.FrameCount > 0;
        }
    }

    /// <summary>
    /// <b>One figure's way to its boat's oars.</b> Finds the <see cref="IOarStroke"/> on or above the
    /// hull the figure stands on, caches it per hull, and asks <see cref="RowingFigure"/> for the pose.
    /// It searches only while the figure is at the oars, and again only when the hull changes or the
    /// source it held has gone, so a figure that is not rowing costs one stance comparison. The search
    /// fills a reused list (no allocation once warm).
    /// </summary>
    public sealed class RowingFigureReader
    {
        private const int SearchCapacity = 4;

        private readonly List<IOarStroke> _found = new List<IOarStroke>(SearchCapacity);
        private Transform _hull;
        private IOarStroke _stroke;

        /// <summary>The source last found (null when none). Exposed for tests and the inspector.</summary>
        public IOarStroke Source => _stroke;

        /// <summary>The rowing pose for the figure on <paramref name="stand"/>, or false when the
        /// ordinary state map should decide.</summary>
        public bool TryPose(CharacterSkinDef skin, ICharacterFigureStand stand, CharacterStance stance,
                            out string stateKey, out int frame)
        {
            stateKey = null;
            frame = 0;
            if (stance != CharacterStance.Oars || stand == null) return false;
            IOarStroke stroke = StrokeUnder(stand.FigureHull);
            return RowingFigure.TryResolve(skin, stance, stroke, stand.FigureDeckBearingDegrees,
                                           out stateKey, out frame);
        }

        /// <summary>The <see cref="IOarStroke"/> on <paramref name="hull"/> or one of its parents.</summary>
        public IOarStroke StrokeUnder(Transform hull)
        {
            if (hull == null)
            {
                Forget();
                return null;
            }
            bool sameHull = ReferenceEquals(hull, _hull);
            bool lost = _stroke == null || (_stroke is UnityEngine.Object unityObject && unityObject == null);
            if (sameHull && !lost) return _stroke;

            _hull = hull;
            _stroke = null;
            hull.GetComponentsInParent(false, _found);
            if (_found.Count > 0) _stroke = _found[0];
            _found.Clear();
            return _stroke;
        }

        /// <summary>Drops the cached hull and source.</summary>
        public void Forget()
        {
            _hull = null;
            _stroke = null;
        }
    }
}
