using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Where a polar's numbers came from — carried as DATA on the asset, never inferred from the
    /// numbers themselves.
    ///
    /// <para>The sail rig kit ships a polar and flags it, in its own file, as
    /// <c>"status": "REFERENCE, art-side. Gameplay owns the polar and drive … replace the model,
    /// keep the glue."</c> That sentence is the whole reason this enum exists: a table of speeds
    /// looks identical whether it was measured, modelled, or guessed, so the provenance has to
    /// travel with it or it silently becomes truth on the first read.</para>
    /// </summary>
    public enum SailPolarStatus
    {
        /// <summary>
        /// A stated model over rig-derived inputs, shipped so that a first physics pass and the
        /// sprite agree. NOT a claim about how fast the boat is.
        /// </summary>
        Reference = 0,

        /// <summary>Produced by the game's own VPP or measured in play — the polar of record.</summary>
        Authored = 1,
    }

    /// <summary>
    /// <b>A boat speed polar as DATA</b> (ADR 0003, CLAUDE.md rule 2 and rule 6): true wind in, boat
    /// speed out, on a grid, with its provenance attached.
    ///
    /// <para>Created by <c>SailPolarImporter</c> from a hull's <c>boat-sailing@1</c> sidecar. Nothing
    /// here is typed: every number is read from the file the art director generated, and
    /// <see cref="DerivedFromRigSha256"/> pins which rig it was cut from, so a reshaped hull cannot
    /// leave a stale polar looking current.</para>
    ///
    /// <para><b>⚠️ The grid is the reference model's, not a law.</b> <see cref="Status"/> says which
    /// kind of table this is; a drive that treats a <see cref="SailPolarStatus.Reference"/> polar as
    /// the boat's real performance is reading a placeholder as a measurement.</para>
    ///
    /// <para><b>⚠️ Two different no-go angles live in this file and they DISAGREE by design.</b>
    /// <see cref="NoGoTrueWindDeg"/> is the polar's (below it the model returns zero speed);
    /// <see cref="SpriteIronsApparentDeg"/> is the angle below which the RIG draws flogging sails.
    /// They are in different frames — true and apparent — so they cannot be reconciled by picking a
    /// number, and <see cref="IronsRowCount"/> counts exactly how many cells of this grid fall
    /// between them. Which one the player feels is an owner ruling, not a default.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Sail Polar", fileName = "SailPolar")]
    public class SailPolarDef : ScriptableObject
    {
        /// <summary>Stable id, <c>sailpolar.snake_case</c> — append-only (rule 2).</summary>
        public string Id = "sailpolar.unnamed";

        /// <summary>Human-readable, for inspectors and log lines. Never parsed.</summary>
        public string DisplayName = "";

        // ---- provenance -------------------------------------------------------------------------

        /// <summary>Which kind of table this is. See <see cref="SailPolarStatus"/>.</summary>
        public SailPolarStatus Status = SailPolarStatus.Reference;

        /// <summary>The sidecar's own <c>POLAR_REFERENCE.status</c> sentence, verbatim.</summary>
        [TextArea(2, 6)] public string StatusNote = "";

        /// <summary>The model expression the sidecar states, e.g.
        /// <c>min( v_hull * (1 - exp(-tws / tws_h)) * G(twa),  R(twa) * tws )</c>.</summary>
        public string Model = "";

        /// <summary>Repo-relative path of the sidecar this was read from.</summary>
        public string SourceSidecarPath = "";

        /// <summary>The sidecar's <c>derivedFromRigSha256</c> — the full 64 hex characters. A prefix
        /// is not a hash check (a corrupt stamp has shared 16 leading digits with the truth).</summary>
        public string DerivedFromRigSha256 = "";

        // ---- the grid ---------------------------------------------------------------------------

        /// <summary>True wind speeds, knots, ascending — the grid's columns.</summary>
        public float[] TrueWindKn = Array.Empty<float>();

        /// <summary>True wind angles, degrees off the bow, ascending — the grid's rows.</summary>
        public float[] TrueWindAngleDeg = Array.Empty<float>();

        /// <summary>
        /// Boat speed in knots, ROW-MAJOR over (<see cref="TrueWindAngleDeg"/> × <see cref="TrueWindKn"/>):
        /// index <c>a * TrueWindKn.Length + w</c>. Length is the product of the two, or the asset is
        /// malformed (<see cref="IsUsable"/>).
        /// </summary>
        public float[] BoatSpeedKn = Array.Empty<float>();

        /// <summary>Apparent wind angle, same indexing as <see cref="BoatSpeedKn"/> — the sidecar's
        /// own <c>awa</c> column, kept so a consumer can check the sprite's band without re-deriving
        /// the true→apparent glue and disagreeing with the file by rounding.</summary>
        public float[] ApparentWindAngleDeg = Array.Empty<float>();

        /// <summary>Apparent wind speed, knots, same indexing.</summary>
        public float[] ApparentWindKn = Array.Empty<float>();

        /// <summary>Heel the rig's own law reports at that cell, degrees (magnitude), same indexing.</summary>
        public float[] HeelDeg = Array.Empty<float>();

        // ---- the two no-go angles, and the disagreement between them -----------------------------

        /// <summary>The polar's own no-go: below this TRUE wind angle the model returns zero.</summary>
        [Min(0f)] public float NoGoTrueWindDeg = 0f;

        /// <summary>The angle below which the RIG draws both sails flogging, in APPARENT degrees.</summary>
        [Min(0f)] public float SpriteIronsApparentDeg = 0f;

        /// <summary>
        /// How many cells of this grid the polar says are sailable while the sprite says she is in
        /// irons — <b>counted from the shipped rows, not estimated</b>. Zero would mean the two
        /// frames happen to agree everywhere on this grid; they do not.
        /// </summary>
        [Min(0)] public int IronsRowCount = 0;

        /// <summary>Total cells in the grid, so <see cref="IronsRowCount"/> reads as a fraction
        /// without the reader having to multiply the axes.</summary>
        [Min(0)] public int RowCount = 0;

        // ---- hull facts the model was built on ----------------------------------------------------

        /// <summary>Displacement-hull speed limit, knots (1.34·√LWL_ft) — the model's ceiling.</summary>
        [Min(0f)] public float HullSpeedKn = 0f;

        /// <summary>Sail area / displacement ratio, from the sidecar's <c>SAIL_PLAN</c>.</summary>
        [Min(0f)] public float SailAreaDisplacementRatio = 0f;

        /// <summary>Upwind sail area, m² — main plus headsail at full hoist.</summary>
        [Min(0f)] public float UpwindSailAreaM2 = 0f;

        /// <summary>
        /// True when the axes and every column are present and consistently sized. A malformed polar
        /// must refuse rather than index out of a short array at the helm.
        /// </summary>
        public bool IsUsable()
        {
            if (TrueWindKn == null || TrueWindAngleDeg == null || BoatSpeedKn == null) return false;
            if (TrueWindKn.Length == 0 || TrueWindAngleDeg.Length == 0) return false;
            int cells = TrueWindKn.Length * TrueWindAngleDeg.Length;
            if (BoatSpeedKn.Length != cells) return false;
            if (ApparentWindAngleDeg != null && ApparentWindAngleDeg.Length != 0 &&
                ApparentWindAngleDeg.Length != cells) return false;
            if (ApparentWindKn != null && ApparentWindKn.Length != 0 &&
                ApparentWindKn.Length != cells) return false;
            if (HeelDeg != null && HeelDeg.Length != 0 && HeelDeg.Length != cells) return false;
            return true;
        }

        /// <summary>Boat speed at a grid node, knots. Indices are NOT clamped by the caller's
        /// convenience: an out-of-range ask returns 0 rather than the nearest edge, because a silent
        /// nearest-edge read is how a polar lookup starts lying at its boundary.</summary>
        public float SpeedAt(int angleIndex, int windIndex)
        {
            if (!IsUsable()) return 0f;
            if (angleIndex < 0 || angleIndex >= TrueWindAngleDeg.Length) return 0f;
            if (windIndex < 0 || windIndex >= TrueWindKn.Length) return 0f;
            return BoatSpeedKn[angleIndex * TrueWindKn.Length + windIndex];
        }
    }
}
