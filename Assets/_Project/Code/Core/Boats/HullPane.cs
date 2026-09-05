using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which wall of a deckhouse a window is in</b> — and therefore which way the light that
    /// leaves through it goes.
    ///
    /// <para><b>It is a GROUPING, not a direction.</b> The direction a pane faces is derived from the
    /// pane's own two in-plane vectors (<see cref="HullPane.Outward"/>) and cannot disagree with the
    /// geometry, because it IS the geometry. This enum exists so that the several panes in one wall
    /// can be washed by ONE spill rather than by one each — three windscreen panes 0.16 m apart do
    /// not throw three separate wedges of light, they throw one — and so that a printed table reads
    /// as a boat rather than as a list of triples.</para>
    /// </summary>
    public enum HullWall
    {
        /// <summary>The forward face — a windscreen or a bridge front, usually raked.</summary>
        Front = 0,

        /// <summary>The PORT side wall (outward is −x).</summary>
        Port = 1,

        /// <summary>The STARBOARD side wall (outward is +x).</summary>
        Starboard = 2,

        /// <summary>The aft face — the wall the door is usually in.</summary>
        Aft = 3,
    }

    /// <summary>
    /// <b>ONE window in ONE hull's deckhouse</b>, in her own rig metres (+x starboard, +y toward the
    /// bow, +z up from the keel) — the frame her lamps, her deck polygons and her interior shell
    /// already speak.
    ///
    /// <para><b>Why a rectangle and not a point.</b> The owner's ruling of 2026-09-03 is that a
    /// cabin's glow is "confined to the cabin with the glow only coming through the windows". A point
    /// with a radius is a disc, and a disc over a wheelhouse is precisely the blob that ruling
    /// retired. A window has a SHAPE, it is a shape the rigs already publish, and drawing it as its
    /// own shape is the difference between a lit room and a lamp parked on a roof.</para>
    ///
    /// <para><b>⭐ The outward direction is DERIVED, never declared.</b> The pane carries its two
    /// in-plane half-extents and nothing else; <see cref="Outward"/> is their cross product. A
    /// declared normal is a second source of truth for something the geometry already fixes, and the
    /// failure it invites is silent and severe: a pane whose normal is flipped throws its light INTO
    /// the cabin it is supposed to be lighting the sea from, which looks like nothing at all rather
    /// than like an error. Two vectors cannot disagree with themselves.</para>
    ///
    /// <para><b>Rig metres, not screen pixels</b> — the same argument <see cref="HullLamp"/> makes.
    /// A mesh hull's drawn child carries heading, roll, pitch and heave as a real transform, so one
    /// boat-local rectangle pushed through it lands correctly at every heading and rides every wave
    /// for free.</para>
    /// </summary>
    [Serializable]
    public struct HullPane
    {
        [Tooltip("Which wall this window is in. A grouping for the spill and for the printed " +
                 "table — the direction the pane faces comes from its own geometry, not from this.")]
        public HullWall Wall;

        [Tooltip("The centre of the glass, in HER OWN rig metres: +x starboard, +y toward the bow, " +
                 "+z up from the keel. Read off the rig's published HOUSE glazing, never eyeballed.")]
        public Vector3 CentreMetres;

        [Tooltip("Half the pane's WIDTH, as a vector along the wall: ±x on a front or aft face, " +
                 "±y on a side. Its length is half the glass's width in metres.")]
        public Vector3 HalfAcrossMetres;

        [Tooltip("Half the pane's HEIGHT, as a vector UP the wall — which follows the RAKE on a " +
                 "raked windscreen, so a leaning screen's panes lean with it. Its length is half " +
                 "the glass's height measured in the wall's own plane, not vertically.")]
        public Vector3 HalfUpMetres;

        public HullPane(HullWall wall, Vector3 centreMetres, Vector3 halfAcrossMetres, Vector3 halfUpMetres)
        {
            Wall = wall;
            CentreMetres = centreMetres;
            HalfAcrossMetres = halfAcrossMetres;
            HalfUpMetres = halfUpMetres;
        }

        /// <summary>
        /// <b>The way this pane faces</b>, as a unit vector in rig metres — up × across, which is
        /// outward for every wall provided the two are handed the way the probe hands them (a front
        /// and a starboard side count +x across; an aft face and a port side count −x and −y, because
        /// seen from OUTSIDE those walls run the other way).
        ///
        /// <para>Returns <see cref="Vector3.zero"/> for a degenerate pane rather than a NaN, so a row
        /// that was never filled in reads as "no direction" and is skipped, not drawn edge-on at
        /// random.</para>
        /// </summary>
        public Vector3 Outward
        {
            get
            {
                Vector3 n = Vector3.Cross(HalfUpMetres, HalfAcrossMetres);
                float m = n.magnitude;
                return m > 1e-6f ? n / m : Vector3.zero;
            }
        }

        /// <summary>The glass's width in metres (across the wall).</summary>
        public float WidthMetres => 2f * HalfAcrossMetres.magnitude;

        /// <summary>The glass's height in metres, measured in the wall's own plane.</summary>
        public float HeightMetres => 2f * HalfUpMetres.magnitude;

        /// <summary>Is this a pane that can actually be drawn — both extents real and not collapsed?
        /// An all-zero row (a def field left at its default) is not, and is skipped everywhere.</summary>
        public bool IsUsable =>
            HalfAcrossMetres.sqrMagnitude > 1e-12f &&
            HalfUpMetres.sqrMagnitude > 1e-12f &&
            Outward != Vector3.zero;

        /// <summary>
        /// One of the pane's four corners in rig metres, for <paramref name="acrossSign"/> and
        /// <paramref name="upSign"/> in {−1, +1}. The order a caller walks them in is the caller's;
        /// this only guarantees that the same pair always names the same corner.
        /// </summary>
        public Vector3 Corner(float acrossSign, float upSign) =>
            CentreMetres + acrossSign * HalfAcrossMetres + upSign * HalfUpMetres;
    }
}
