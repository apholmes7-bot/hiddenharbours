using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Where a piece of deck gear sits on a hull, where its operator stands, and which way that
    /// operator faces — in HULL-LOCAL METRES, pure, allocation-free and scene-free.
    ///
    /// <para><b>The frame</b> is the sidecars' frame, unchanged: origin amidships / keel-bottom /
    /// centreline, <b>+x starboard, +y bow, +z up</b>, metres, 32 px = 1 m. Every number a caller
    /// hands in and every number this hands back is in it. Nothing here knows about screens,
    /// sprites, sorting or facings-as-cells — that is a presenter's job.</para>
    ///
    /// <para><b>⚠️ THE FACING IS DERIVED, NEVER INHERITED</b> (#457). An operator's heading is
    /// <c>deck bearing + the gear's own bearing + the station's turn</c>, resolved fresh every time
    /// from the hull's DRAWN heading. Inheriting a facing from the boat — or worse, caching one —
    /// is the defect that lane has now shipped twice: the fisher keeps her old heading while the
    /// hull turns under her. <see cref="OperatorHeading"/> is the whole of the rule and it is a
    /// function, deliberately, so there is nowhere to cache it.</para>
    ///
    /// <para><b>⚠️ NO CONTENT LIVES HERE</b> (CLAUDE.md rule 2). There is not one hull dimension,
    /// mount position, stack step or capacity in this file. Every one of those is DATA the caller
    /// supplies: mounts come from the rig's own <c>HAULER</c>/<c>TUBS</c> tables, stack steps and
    /// per-hull capacities from <c>TrapIso.STACK</c>/<c>TrapIso.CAPS</c>, and stations from
    /// <c>DeckGear.station()</c>. The rig is the source; this is only the arithmetic. A hard-coded
    /// 1.34 here would be the same mistake as a hard-coded fish price.</para>
    ///
    /// <para><b>Why a POCO in Core.</b> Fishing needs this and Boats owns the hull, so it may not
    /// live in either (rule 4). It is a static maths class with no Unity lifetime, no service
    /// lookup and no state, so it is EditMode-testable without a scene and cannot drift into a
    /// module dependency.</para>
    /// </summary>
    public static class DeckGearPlacement
    {
        /// <summary>The rigs' facing count. Eight 45° steps — not a tunable, a property of the
        /// turntable every rig in the repo shares.</summary>
        public const int Facings = 8;

        /// <summary>Degrees per <c>dir</c> step.</summary>
        public const float DegreesPerFacing = 360f / Facings;

        /// <summary>
        /// A station: everything <c>DeckGear.station(kind, dir)</c> answers, in the sidecar frame.
        /// Built from rig data by the caller — this struct declares the SHAPE, never the values.
        /// </summary>
        public readonly struct Station
        {
            /// <summary>Where the operator stands, RELATIVE TO THE GEAR, in the gear's own local
            /// frame (metres) before the gear's bearing is applied.</summary>
            public readonly Vector3 StandLocal;

            /// <summary>Facing steps to add to the gear's own <c>dir</c> so the operator looks the
            /// right way. The hauler is the one station whose operator faces OUTBOARD — its turn is
            /// 4, a half turn — because the warp comes in over the rail and the drum is worked from
            /// inboard of it.</summary>
            public readonly int Turn;

            /// <summary>The surface the hands work at, in WORLD METRES above the hull frame's z=0.
            /// Handed to the clip as <c>opts.workZ</c>. Never a body fraction: a bench belongs to
            /// the deck, not to the person standing at it.</summary>
            public readonly float WorkZ;

            /// <summary>Deck a planner must reserve for the operator, metres (x = across, y =
            /// along), BEFORE it packs anything else onto the deck.</summary>
            public readonly Vector2 Clear;

            public Station(Vector3 standLocal, int turn, float workZ, Vector2 clear)
            {
                StandLocal = standLocal;
                Turn = turn;
                WorkZ = workZ;
                Clear = clear;
            }
        }

        /// <summary>
        /// The operator's heading in world degrees.
        ///
        /// <para>Derived, every call, from the hull's DRAWN heading — the whole point of the #457
        /// lesson. <paramref name="deckHeadingDegrees"/> is the hull's current heading as drawn (not
        /// a target, not a last-known value); <paramref name="gearDir"/> is the gear's own facing
        /// step on the deck; the station's turn is the operator's offset from it.</para>
        /// </summary>
        public static float OperatorHeading(float deckHeadingDegrees, int gearDir, in Station station)
            => Wrap360(deckHeadingDegrees + (WrapFacing(gearDir + station.Turn) * DegreesPerFacing));

        /// <summary>
        /// Where the operator's feet land, in hull-local metres.
        ///
        /// <para>The station's stand offset is expressed in the GEAR's local frame, so it is rotated
        /// by the gear's own bearing before being added to the mount — otherwise a bench turned
        /// broadside would still put its operator forward of it.</para>
        /// </summary>
        public static Vector3 OperatorStand(in Vector3 gearMount, int gearDir, in Station station)
        {
            Vector3 r = RotateOnDeck(station.StandLocal, gearDir);
            return new Vector3(gearMount.x + r.x, gearMount.y + r.y, gearMount.z + r.z);
        }

        /// <summary>
        /// Rotate a gear-local offset onto the deck by <paramref name="dir"/> 45° steps, about +z.
        ///
        /// <para>This is a rotation in the hull's GROUND PLANE — plain metres, no iso squash. The
        /// squash belongs to the projection and applying it here would foreshorten gameplay
        /// geometry, which is the un-squash mistake in reverse.</para>
        /// </summary>
        public static Vector3 RotateOnDeck(in Vector3 local, int dir)
        {
            float a = WrapFacing(dir) * DegreesPerFacing * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(local.x * c - local.y * s, local.x * s + local.y * c, local.z);
        }

        /// <summary>
        /// The hull-local position of tier <paramref name="tier"/> of a trap stack standing on
        /// <paramref name="basePos"/>.
        ///
        /// <para><paramref name="stepMetres"/> is the build's own tier height — cones NEST at 0.52 m
        /// where a wooden bow-top needs 0.40 — and <paramref name="slew"/> is the build's per-tier
        /// lateral jog, so a stack leans like a real one instead of being a perfect column. Both come
        /// from the rig's <c>STACK</c> table; neither is a constant here.</para>
        ///
        /// <para>The slew pattern repeats when the stack is taller than the pattern, which is what
        /// makes an over-tall stack look wrong rather than crash. Capacity is the caller's to
        /// enforce — see <see cref="StackFits"/>.</para>
        /// </summary>
        public static Vector3 StackSlot(in Vector3 basePos, int tier, float stepMetres,
                                        int[] slew, float slewMetres, int dir)
        {
            int t = tier < 0 ? 0 : tier;
            int jog = (slew == null || slew.Length == 0) ? 0 : slew[t % slew.Length];
            Vector3 offset = RotateOnDeck(new Vector3(jog * slewMetres, 0f, 0f), dir);
            return new Vector3(basePos.x + offset.x, basePos.y + offset.y, basePos.z + (t * stepMetres));
        }

        /// <summary>
        /// Does a stack of <paramref name="wanted"/> pots fit where it is being put?
        ///
        /// <para>Capacity is PER HULL and PER SURFACE — a lobster boat takes 5 on the deck and 2 on
        /// the washboard where a Cape Islander takes 3 and 2 — and those numbers are rig data the
        /// caller passes in. A wharf's washboard capacity is 0, and 0 is a real answer meaning "you
        /// cannot stack there", not a missing value to fall back from.</para>
        /// </summary>
        public static bool StackFits(int wanted, int capacity) => wanted >= 0 && wanted <= capacity;

        /// <summary>
        /// Is a point inside the reserved working clearance of a station? Used to keep a planner from
        /// packing pots into the space the operator needs to stand in.
        ///
        /// <para>Axis-aligned in the GEAR's frame, so the test rotates the point back rather than
        /// growing a box — a broadside bench reserves a broadside footprint, not a square.</para>
        /// </summary>
        public static bool WithinClearance(in Vector3 point, in Vector3 gearMount, int gearDir,
                                           in Station station)
        {
            Vector3 stand = OperatorStand(gearMount, gearDir, station);
            Vector3 d = new Vector3(point.x - stand.x, point.y - stand.y, 0f);
            Vector3 local = RotateOnDeck(d, -gearDir);          // back into the gear's own frame
            return Mathf.Abs(local.x) <= station.Clear.x * 0.5f
                && Mathf.Abs(local.y) <= station.Clear.y * 0.5f;
        }

        /// <summary>Facing step wrapped into 0…7, negative-safe.</summary>
        public static int WrapFacing(int dir)
        {
            int n = dir % Facings;
            return n < 0 ? n + Facings : n;
        }

        /// <summary>Degrees wrapped into [0, 360), negative-safe.</summary>
        public static float Wrap360(float degrees)
        {
            float d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }
    }
}
