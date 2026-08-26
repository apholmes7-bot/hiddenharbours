using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One character build's settled rest at one height: what the sheet was actually baked at, what
    /// was asked for, and whether the two differ.
    ///
    /// <para><b>Read <see cref="LiftM"/>, not <see cref="RequestedM"/>.</b> The first is the truth
    /// about the PIXELS — where the hand ended up in the art you are about to draw. The second is
    /// only there so <see cref="Clamped"/> means something you can act on: a small build reaching for
    /// a rack above their head cannot get there, so the rig lowers the reach to what they CAN touch
    /// and says so, rather than stretching the arm past the shoulder and hoping nobody looks. Placing
    /// a tool at <c>RequestedM</c> for a clamped build hangs it above an empty hand.</para>
    /// </summary>
    [Serializable]
    public struct CharacterReachRest
    {
        [Tooltip("The rest SURFACE height in world metres, as this build's sheet was baked. ⚠️ A " +
                 "world metre on the FURNITURE, never a body fraction — a rack is the same height " +
                 "whoever is standing at it (the workZ precedent).")]
        public float LiftM;

        [Tooltip("The height the bake ASKED for. Equal to LiftM unless Clamped.")]
        public float RequestedM;

        [Tooltip("The build could not reach RequestedM, so the rig lowered it to LiftM. True for the " +
                 "children at both racks, and for one small adult at the high one.")]
        public bool Clamped;
    }

    /// <summary>One preset's three settled rests, keyed by the visual def it belongs to.</summary>
    [Serializable]
    public struct CharacterReachRow
    {
        [Tooltip("The CharacterVisualDef id this row describes — visual.fisher_iso, visual.ginny_iso, …")]
        public string VisualId;

        [Tooltip("Setting a tool down on the GROUND. Never clamped: everyone can reach the floor.")]
        public CharacterReachRest Ground;

        [Tooltip("Standing a tool UPRIGHT in a rack, butt down.")]
        public CharacterReachRest StowV;

        [Tooltip("Laying a tool ACROSS a rack — the highest of the three, and the one the small " +
                 "builds reach a clamped version of.")]
        public CharacterReachRest StowH;
    }

    /// <summary>
    /// <b>The rig-6.6 REACH contract, imported once into an asset</b> — the consumer side of
    /// <c>Reach_sidecar.json</c>, which the character rig exports beside the reach sheets.
    ///
    /// <para><b>What a set-down needs that the sheets do not carry.</b> The three
    /// <see cref="CharacterClip"/>s draw a character reaching down and letting go. WHERE they let go
    /// — the height of the floor or the rack — and WHEN — the frame the hand opens — are neither
    /// pixels nor gameplay; they are art-contract geometry, changing when the rig re-bakes and at no
    /// other time. So they are DATA (CLAUDE.md rule 2 and rule 6), imported at edit time into a
    /// committed asset. Runtime never parses the JSON (ADR 0021 §4), the same arrangement as
    /// <see cref="CharacterOffDeckMountsDef"/>.</para>
    ///
    /// <para><b>The two numbers a tool hand-over actually turns on.</b> <see cref="ReleaseAt"/> is
    /// where the hand OPENS and <see cref="ArriveAt"/> is where the tool is HOME, and
    /// <c>arrive &lt; release</c> is the whole point: the tool lands, and only then does the hand let
    /// go. Releasing at the seam is what made the old rod rests read as teleports, and it is why
    /// these are published rather than left for a caller to guess from the frame count.</para>
    ///
    /// <para><b>There is no pick-up half.</b> A pick-up is this clip played in REVERSE — a 0.72
    /// release mirrors to a 0.28 grip-close, so the hand arrives empty, closes, and lifts. Ask
    /// <see cref="ReleaseAtReversed"/> rather than working it out at each call site.</para>
    ///
    /// <para><b>What is NOT here: the ART.</b> Sheets, frame counts and playback rates ride on
    /// <see cref="CharacterVisualDef"/> as ordinary <see cref="CharacterClip"/>s behind the same
    /// all-or-nothing gate as every other clip. <see cref="Frames"/> and
    /// <see cref="MillisecondsPerFrame"/> are carried here as the SIDECAR's statement of them, so a
    /// re-export that quietly retimed the clip goes red against the def's initialisers instead of
    /// drifting — <c>CharacterReachTests</c> is what compares them.</para>
    ///
    /// <para><b>Timing is MILLISECONDS PER FRAME, never fps.</b> 100 ms is 10 fps; reading it the
    /// other way runs the set-down a hundred times too fast.</para>
    ///
    /// Written by <c>CharacterReachBuilder</c>; do not hand-edit.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Reach", fileName = "CharacterReach")]
    public class CharacterReachDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5).")]
        public string Id = "reach.character_iso";

        [Tooltip("The rig revision these numbers were exported from, as the sidecar states it. " +
                 "Provenance, not a switch — nothing branches on it.")]
        public string Rig = "";

        [Header("The clip, as the sidecar states it")]
        [Tooltip("Frames in the set-down. The tool kit's own set-down length, to the frame.")]
        [Min(1)] public int Frames = 6;

        [Tooltip("MILLISECONDS per frame — not fps. 100 ms is 10 fps.")]
        [Min(0f)] public float MillisecondsPerFrame = 100f;

        [Tooltip("Where through the clip (0..1) the hand OPENS. Gripped below this, empty above it.")]
        [Range(0f, 1f)] public float ReleaseAt = 0.72f;

        [Tooltip("Where through the clip (0..1) the tool is HOME. Must be BELOW ReleaseAt — the tool " +
                 "arrives, THEN the hand lets go. Never release at the seam.")]
        [Range(0f, 1f)] public float ArriveAt = 0.62f;

        [Tooltip("How far the grip centre sits above the rest surface, world metres. The rod rig's " +
                 "own ground datum for a reeled rod is the same 0.095 m.")]
        public float GripRiseM = 0.095f;

        [Header("The rest surfaces the bake ASKED for, before any per-build clamp")]
        [Tooltip("The floor. 0, and not a placeholder: the ground is the ground.")]
        public float GroundLiftM = 0f;

        [Tooltip("⚠️ A PLACEHOLDER, and the drop says so. The height a rod stands upright in a rack. " +
                 "It is the art lane's reading of 'rack height, roughly standing reach', not a " +
                 "measurement of any furniture in the game — and the rod rig is NOT the oracle for " +
                 "it: RodIso.restLift('stowV') answers a different question (how far a settled rod " +
                 "holds its GRIP above whatever it rests on, 0.16 m), and the rod rig has no way to " +
                 "know how high the rack is. Whoever builds the rack owns this number; when they " +
                 "set it, the sheets re-bake at the new height.")]
        public float StowVLiftM = 0.95f;

        [Tooltip("⚠️ A PLACEHOLDER, same provenance as StowVLiftM. The height a rod lies across a " +
                 "rack. RodIso.restLift('stowH') is 0.62 m and again answers a different question.")]
        public float StowHLiftM = 1.05f;

        [Header("Per-character rest heights")]
        [Tooltip("One row per cast preset. A character with no row has no measured rest and the " +
                 "caller keeps whatever it did before — nothing here throws on a miss.")]
        public CharacterReachRow[] Rows = Array.Empty<CharacterReachRow>();

        /// <summary>The clip's natural rate, so a caller never divides by a millisecond.</summary>
        public float FramesPerSecond => MillisecondsPerFrame > 0f ? 1000f / MillisecondsPerFrame : 0f;

        /// <summary>
        /// A pick-up's grip-close point: this clip reversed. The hand arrives empty, closes on the
        /// tool at <c>1 − ReleaseAt</c>, and lifts — one number, computed once here rather than at
        /// each call site, because a pick-up that closes at the wrong moment grabs at thin air.
        /// </summary>
        public float ReleaseAtReversed => 1f - ReleaseAt;

        /// <summary>
        /// The frame → <c>u</c> mapping this clip is baked with: <c>u = f/(frames−1)</c>, so the LAST
        /// frame is the settled rest at <c>u = 1</c> rather than one step short of it.
        ///
        /// <para>⚠️ Every OTHER clip in the kit is cyclic (<c>f/frames</c>). This one settles, which
        /// is what makes its last frame holdable — see <see cref="CharacterVisualDef.ClipSettles"/>.
        /// An off-by-one here is invisible until a tool hangs in mid-air.</para>
        /// </summary>
        public float UAtFrame(int frame)
        {
            int span = Mathf.Max(1, Frames - 1);
            return Mathf.Clamp01(frame / (float)span);
        }

        /// <summary>True while the tool is still IN THE HAND at this frame.</summary>
        public bool IsGripped(int frame) => UAtFrame(frame) < ReleaseAt;

        /// <summary>How many of the clip's frames still have the tool in hand — counted off the same
        /// mapping the art was baked with, so a sidecar and the pixels cannot disagree about it.</summary>
        public int GrippedFrames
        {
            get
            {
                int n = 0;
                for (int f = 0; f < Frames; f++) if (IsGripped(f)) n++;
                return n;
            }
        }

        /// <summary>
        /// The rest heights for a visual def id, or <c>false</c> with an empty row when this asset
        /// carries none. Never throws and never invents a height: a caller that gets <c>false</c>
        /// keeps whatever placement it already had, the same degrade-per-element rule the rest of the
        /// character data follows.
        /// </summary>
        public bool TryGetRow(string visualId, out CharacterReachRow row)
        {
            row = default;
            if (string.IsNullOrEmpty(visualId) || Rows == null) return false;

            for (int i = 0; i < Rows.Length; i++)
            {
                if (string.Equals(Rows[i].VisualId, visualId, StringComparison.Ordinal))
                {
                    row = Rows[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The settled rest for a visual def id and one of the three REACH clips. Any other clip has
        /// no rest height at all and returns <c>false</c> — a haul or a swim rests on nothing.
        /// </summary>
        public bool TryGetRest(string visualId, CharacterClip clip, out CharacterReachRest rest)
        {
            rest = default;
            if (!CharacterVisualDef.ClipSettles(clip)) return false;
            if (!TryGetRow(visualId, out var row)) return false;

            rest = clip switch
            {
                CharacterClip.ReachStowV => row.StowV,
                CharacterClip.ReachStowH => row.StowH,
                _ => row.Ground,
            };
            return true;
        }

        /// <summary>
        /// Where the GRIP ends up in world metres above the floor once the tool is settled — the rest
        /// surface plus the grip rise. This is the number that places a tool sprite; the surface
        /// height alone places the furniture.
        /// </summary>
        public bool TryGetSettledGripZ(string visualId, CharacterClip clip, out float gripZ)
        {
            gripZ = 0f;
            if (!TryGetRest(visualId, clip, out var rest)) return false;
            gripZ = rest.LiftM + GripRiseM;
            return true;
        }
    }
}
