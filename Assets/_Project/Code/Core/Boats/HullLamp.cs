using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The KIND of lamp a hull carries</b> — what it is FOR, never what it looks like. The look
    /// (colour, reach, softness, flicker, and how bright it burns) is one fixed preset per kind in
    /// the Art lane's light-preset library, for the same reason a window glow looks like a window
    /// glow on every cottage: a port sidelight must read as a port sidelight on every boat in the
    /// creek, or the colours stop meaning anything.
    ///
    /// <para><b>Why the colours are not data here.</b> Red to port and green to starboard is not a
    /// tunable — it is the rule of the road, the same in every harbour on earth, and a hull that
    /// could declare it the other way round is a hull that can lie about which way she is heading.
    /// The <em>positions</em> are per-hull data (every boat wears her lamps somewhere different);
    /// the <em>meanings</em> are not.</para>
    /// </summary>
    public enum HullLampKind
    {
        /// <summary>The PORT sidelight — red, and shown over the port bow.</summary>
        PortSidelight = 0,

        /// <summary>The STARBOARD sidelight — green, over the starboard bow.</summary>
        StarboardSidelight = 1,

        /// <summary>The STERN light — white, at the transom.</summary>
        SternLight = 2,

        /// <summary>The MASTHEAD/steaming light — white, high up, and the one that says
        /// "under power" rather than merely "here".</summary>
        Masthead = 3,

        /// <summary>The CABIN glow — the warm spill out of a lit wheelhouse or a lit cuddy. Not a
        /// navigation light: nobody takes a bearing off it, it just says somebody is aboard.</summary>
        CabinGlow = 4,

        /// <summary>
        /// The SEARCHLIGHT — the steerable beam that works the water ahead of her, and the only lamp
        /// here that is not a glow. It is drawn by the bespoke boat-spotlight component (a cone that
        /// follows the bow and lights the sea from inside the water's own shader), so this kind
        /// declares only WHERE the lamp is mounted; everything else about a beam is that component's.
        /// <b>Only x and y are read</b> — a searchlight is aimed in the boat's plane, and its height
        /// above the keel changes nothing about where the pool of light falls.
        /// </summary>
        Spotlight = 5,
    }

    /// <summary>
    /// <b>ONE lamp on ONE hull</b>: what kind it is, and WHERE on her it is bolted — in the hull's
    /// own rig metres (+x starboard, +y toward the bow, +z up from the keel), the frame the deck
    /// polygons, every fitting pivot and the interior shell already speak.
    ///
    /// <para><b>Rig metres, not screen pixels, and that distinction is the whole point.</b> The boat
    /// rigs publish <c>navMounts(dir)</c> — the same four points, PROJECTED, one answer per facing —
    /// because that is what a sprite bake needs. A mesh hull needs the triple behind the projection:
    /// her drawn child carries heading, roll, pitch and heave as a real transform, so one boat-local
    /// point pushed through it lands correctly at every heading and rides every wave for free. A
    /// per-facing pixel table would have to be re-derived for the intermediate headings a mesh hull
    /// actually sails at, and would not ride at all.</para>
    /// </summary>
    [Serializable]
    public struct HullLamp
    {
        [Tooltip("What this lamp is for. The kind fixes the colour and the feel; only the position " +
                 "below is per-hull.")]
        public HullLampKind Kind;

        [Tooltip("Where she wears it, in HER OWN rig metres: +x starboard, +y toward the bow, " +
                 "+z up from the keel. Measured from the hull's rig, never eyeballed off a " +
                 "screenshot — a lamp guessed in pixels is right at one heading and wrong at seven.")]
        public Vector3 RigLocalMetres;

        [Tooltip("Trim for THIS placement only: 1 = exactly the kind's preset. Lets one boat's " +
                 "masthead be dimmer than the fleet's without editing the shared preset every other " +
                 "hull reads. The night-gate and the flicker still scale it.")]
        public float IntensityScale;

        public HullLamp(HullLampKind kind, Vector3 rigLocalMetres, float intensityScale = 1f)
        {
            Kind = kind;
            RigLocalMetres = rigLocalMetres;
            IntensityScale = intensityScale;
        }

        /// <summary>
        /// The trim, made safe to multiply by. A struct deserialised out of a def that predates the
        /// field — or authored with the box left empty — carries <c>0</c>, and a lamp silently
        /// scaled to zero is a lamp that does not light with nothing to say why. Zero therefore
        /// means "unset, use the preset", and only a deliberate negative is clamped away.
        /// </summary>
        public float SafeIntensityScale => IntensityScale <= 0f ? 1f : IntensityScale;
    }
}
