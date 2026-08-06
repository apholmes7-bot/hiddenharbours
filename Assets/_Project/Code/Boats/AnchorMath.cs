using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>What a drop attempt does — the three answers the depth gate can give.</summary>
    public enum AnchorDrop
    {
        /// <summary>The rode reaches bottom in water that floats her: the hook is down and she is brought up.</summary>
        Set,
        /// <summary>Too deep — the rode does not reach. The anchor "finds no bottom": a refusal, not a state
        /// change (nothing is dropped, nothing is lost).</summary>
        NoBottom,
        /// <summary>She is not afloat here (dry flats, or the keel is already on the bottom). You do not
        /// anchor aground — you ARE aground, which the existing grounding sim already owns.</summary>
        Aground,
    }

    /// <summary>Where the ground tackle stands right now.</summary>
    public enum AnchorState
    {
        /// <summary>Catted/stowed. The anchor does nothing and the hull handles exactly as it always has.</summary>
        Stowed,
        /// <summary>Down and holding: she lies to her swing circle around the drop point.</summary>
        Set,
        /// <summary>Down but no longer biting — the water rose past the rode and the hook lost the bottom.
        /// She is not held; she creeps off downwind/downtide while the tackle skips along the seabed.</summary>
        Dragging,
    }

    /// <summary>
    /// The <b>ground tackle's</b> pure rules — when the hook holds, how far she swings on it, and what a
    /// dragging anchor does (P1 "the sea has moods" / P5 "cozy, but with teeth"). Static, engine-light and
    /// deterministic, so the whole mechanic's decision-making is EditMode-testable without a scene, a
    /// physics loop or a boat.
    ///
    /// <para><b>"Can I anchor here" IS "does the rode reach the bottom".</b> The depth these rules take is
    /// the SAME single number the water render, the walkability sim, the boat-cross gate and the sounder
    /// all read — <see cref="BoatCrossing.DepthAt"/> over <see cref="ITidalTerrain.ElevationAt"/> and
    /// <see cref="IEnvironmentService.WaterLevelAt"/> (ADR 0014: paint = sail). Nothing here does its own
    /// depth arithmetic, so the anchor can never disagree with the sea the player is looking at, and the
    /// depth is recomputed every tick from <c>(worldSeed, gameTime)</c> rather than stored (rule 5).</para>
    ///
    /// <para><b>No magic numbers.</b> Every constant this maths needs is passed in from
    /// <see cref="AnchorSettings"/> (the owner's <c>GameConfig.Anchor</c>) or from the hull's own
    /// <see cref="BoatHullDef.RodeMeters"/> data.</para>
    /// </summary>
    public static class AnchorMath
    {
        /// <summary>
        /// The rode this hull actually carries (m): her own authored <see cref="BoatHullDef.RodeMeters"/>,
        /// or the shared dinghy-class default when she has none (0 — including every hull asset written
        /// before that field existed). ONE place resolves the fallback, so the gate, the swing circle and
        /// the drag check can never disagree about how much line she has. A null hull carries nothing.
        /// </summary>
        public static float RodeFor(BoatHullDef hull, in AnchorSettings settings)
        {
            if (hull == null) return 0f;
            return hull.RodeMeters > 0f ? hull.RodeMeters : Mathf.Max(0f, settings.DefaultRodeMeters);
        }

        /// <summary>
        /// <b>The drop gate.</b> The hook goes down only where the rode reaches the seabed under a hull
        /// that is genuinely afloat:
        /// <list type="bullet">
        ///   <item>Not afloat (<paramref name="depth"/> ≤ 0, i.e. dry ground, or shallower than the hull's
        ///   <paramref name="draughtMeters"/> so the keel is already on the bottom) →
        ///   <see cref="AnchorDrop.Aground"/>. The existing grounding sim owns that state; anchoring adds
        ///   nothing to it.</item>
        ///   <item>Deeper than <paramref name="rodeMeters"/> → <see cref="AnchorDrop.NoBottom"/>. She finds
        ///   no bottom: a refusal with no state change.</item>
        ///   <item>Otherwise → <see cref="AnchorDrop.Set"/>.</item>
        /// </list>
        /// <para>An <b>infinite</b> depth — what <see cref="BoatCrossing.DepthAt"/> returns where no
        /// seabed is authored ("open water") — falls out as <see cref="AnchorDrop.NoBottom"/>, which is
        /// the honest answer: with no height map there is no bottom for the hook to reach. That is the
        /// mirror of the crossing gate's safe default (a missing map never falsely BLOCKS a boat; here it
        /// never falsely CLAIMS to hold her).</para>
        /// Pure + static.
        /// </summary>
        public static AnchorDrop EvaluateDrop(float depth, float rodeMeters, float draughtMeters)
        {
            if (!(depth > 0f)) return AnchorDrop.Aground;          // NaN-safe: an unknown depth is not afloat
            if (depth < draughtMeters) return AnchorDrop.Aground;  // the existing "can she float here" rule
            if (depth > rodeMeters) return AnchorDrop.NoBottom;    // +Inf (no height map) lands here too
            return AnchorDrop.Set;
        }

        /// <summary>
        /// <b>The swing circle</b> (m) — how far she may lie from the spot the hook went down.
        ///
        /// <para><b>The one formula:</b> <c>√(rode² − depth²)</c>, floored at
        /// <paramref name="minSwingMeters"/>. It is the plain geometry of a taut rode: the line runs from
        /// the boat down to the anchor, so the horizontal leg is whatever is left of the rode once the
        /// vertical leg has taken the depth. Spare rode is swing — a short scope in deep water pins her
        /// almost over her anchor, the same rode in shallow water lets her range nearly its full length —
        /// which is exactly the "more spare rode, more swing" the mechanic asks for, with no invented
        /// constant. As the tide rises the depth grows and the circle SHRINKS, until at
        /// <c>depth == rode</c> it is nothing and she is up and down on her hook: the last moment before
        /// <see cref="HasLostBottom"/>.</para>
        ///
        /// <para>The floor exists because a circle of zero would weld her in place; a boat on her anchor
        /// always fidgets. Pure + static.</para>
        /// </summary>
        public static float SwingRadius(float rodeMeters, float depth, float minSwingMeters)
        {
            float rode = Mathf.Max(0f, rodeMeters);
            float d = Mathf.Max(0f, depth);
            float spare = rode * rode - d * d;
            float swing = spare > 0f ? Mathf.Sqrt(spare) : 0f;
            float floor = Mathf.Max(0f, minSwingMeters);
            return swing > floor ? swing : floor;
        }

        /// <summary>
        /// True when the water over the anchor has risen past the rode, so the hook can no longer reach
        /// the bottom and she DRAGS (P5's teeth: the anchor does not prevent a bad anchorage, it exposes
        /// one). The exact complement of the drop gate's depth test, so a spot she could just be anchored
        /// in is a spot she is just holding in. Pure + static.
        /// </summary>
        public static bool HasLostBottom(float depth, float rodeMeters) => !(depth <= rodeMeters);

        /// <summary>
        /// The <b>dragging-anchor brake</b> (design-unit force, before the physics feel scale): the hook is
        /// skipping along the seabed rather than biting, so it no longer HOLDS her — it only checks her.
        /// Below <paramref name="creepSpeed"/> the tackle does nothing and she creeps off downwind/downtide
        /// under the wind and current the sim is already applying; above it, a drag opposes only the
        /// EXCESS speed, so she settles onto a slow creep instead of sailing away. Symmetric in direction
        /// (a dragging anchor does not care which way she is going) and never a push. Returns zero when
        /// she is already at or under the creep, or when the brake is non-positive.
        ///
        /// <para>Note this is a settling speed, not a hard cap: a real blow holds her a little above the
        /// creep, which is the honest read.</para>
        /// Pure + static.
        /// </summary>
        /// <param name="velocity">Hull velocity over ground (m/s).</param>
        /// <param name="creepSpeed">Speed a dragging anchor lets her make (m/s) — the owner's tunable.</param>
        /// <param name="brakeStrength">Design-unit drag per m/s of speed above the creep.</param>
        public static Vector2 DragBrakeForce(Vector2 velocity, float creepSpeed, float brakeStrength)
        {
            if (brakeStrength <= 0f) return Vector2.zero;
            float speed = velocity.magnitude;
            float creep = Mathf.Max(0f, creepSpeed);
            if (speed <= creep || speed < 1e-5f) return Vector2.zero;
            Vector2 dir = velocity / speed;
            return -dir * ((speed - creep) * brakeStrength);
        }
    }
}
