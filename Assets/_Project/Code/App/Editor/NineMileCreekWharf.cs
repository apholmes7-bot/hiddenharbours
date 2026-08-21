#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain / SortingBands
using HiddenHarbours.Art;                 // YSortSprite — a bollard is something you walk around
using HiddenHarbours.Art.Editor;          // WharfKitCatalog — the kit's published semantic map

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE WORKING QUAY AT NINE MILE CREEK</b> — the squared-U wharf on the made spit at the creek's
    /// mouth: an 84 × 10 m north wall the fleet moors along, a 10 × 48 m west wall carrying the unloading
    /// apron and the winch, and a 92 m timber-crib breakwater sheltering the basin from the south.
    ///
    /// <para><b>⭐ THE QUAY IS GROUND NOW, NOT A SPRITE SET.</b> It used to be 48 cells of the baked wharf
    /// tile kit — one sprite per square metre, drawn back to front, six rows against a six-order band —
    /// over an 8 × 6 m deck. Three things make that the wrong shape for this wharf, and the ruling
    /// (owner, 2026-08-07) is to stop drawing it here:</para>
    /// <list type="number">
    /// <item><description><b>It does not scale.</b> The two walls are 84 × 10 m and 10 × 48 m — 1 320
    /// cells against the old 48, and 1 320 GameObjects for a static quay is a perf budget spent on
    /// nothing (rule 7). The per-row sorting scheme breaks too: the band is six orders wide and the north
    /// wall alone is ten rows deep.</description></item>
    /// <item><description><b>The kit is already ruled for migration</b> to the ISO wharf pack, so drawing
    /// 1 320 cells of it would be building something scheduled for replacement.</description></item>
    /// <item><description><b>The deck is genuinely terrain here.</b> A-1 authors both walls as FILLS in
    /// <see cref="NineMileCreekMainland"/> — made ground at +3.0 m standing on the harbour shoal — so the
    /// height field already knows the quay is there. A sprite deck on top of it would be a second copy of
    /// a shape the terrain owns.</description></item>
    /// </list>
    ///
    /// <para><b>So what this file still does — and it is the load-bearing half.</b> A quay you cannot
    /// stand on, tie to, or run aground against is scenery whatever it is drawn with:</para>
    /// <list type="bullet">
    /// <item><description>registers both decks as <see cref="StandablePlatform"/>s at the MEASURED deck
    /// height, so the on-foot sim stands on the concrete;</description></item>
    /// <item><description>places the mooring fittings, and gives every one of them a
    /// <see cref="ShoreCleat"/> from the SAME table — so the bollard you can see is the bollard you can
    /// tie to (#451's guarantee, which the A-2 handoff requires this slice to carry forward);</description></item>
    /// <item><description>lays the breakwater's collision line, because a breakwater a boat sails through
    /// is scenery.</description></item>
    /// </list>
    /// <para>The DRAWN quay is Phase B's, from the ISO wharf pack, and the plan's §7 already puts it
    /// there with these exact walls positioned for it.</para>
    ///
    /// <para><b>⚠ THE QUAY FACE IS 4.6 m TALL</b> (deck +3.0 m over the −1.6 m basin), and 5.2 m of it
    /// stands exposed at spring low. <b>CLOSED.</b> A-1's flag read "the ISO kit bakes a 24 px
    /// overhanging face — 0.75 m at PPU 32 — so Phase B must tile the face vertically"; that figure was
    /// the retired near-plan tile kit's and had nothing to do with the ISO pack, which baked 2.80 m.
    /// #471 re-measured the real gap at 2.40 m and ordered it as two numbers on one bake call; #477
    /// re-parameterised the rig at this coast's tide and #478 re-baked all 17 sheets. The face is drawn
    /// in ONE course — see <see cref="NineMileCreekQuayFace"/> for the arithmetic and
    /// <c>NineMileCreekDressing.FacePieces</c> for the placement. <b>No vertical tiling, and the deck did
    /// not have to come down toward the water.</b></para>
    /// </summary>
    public static class NineMileCreekWharf
    {
        /// <summary>The root every piece of the wharf hangs under.</summary>
        public const string RootName = "NineMileCreekWharf";

        // =====================================================================================
        //  THE WALLS — read off the plan's fills, never restated
        // =====================================================================================
        // A fill is authored as centre + half-size; a footprint is a Rect. Converting in ONE place is
        // what stops the wharf and the terrain under it from ever disagreeing about where the wharf is.

        /// <summary>The world rectangle a plan fill occupies (its FLAT part — the falloff is the ground
        /// easing back to the bay outside it).</summary>
        public static Rect FootprintOf(HiddenHarbours.World.MainlandZone zone) =>
            new Rect(zone.Center - zone.HalfSize, zone.HalfSize * 2f);

        /// <summary>
        /// THE NORTH WALL — 84 × 10 m, and the one that means "the quay" to everything else in the
        /// region. The fleet moors along its SOUTH face, you step ashore onto it, and the houses behind
        /// the creek turn their doors to it.
        /// </summary>
        public static Rect DeckFootprint() => FootprintOf(NineMileCreekMainland.NorthWallFill);

        /// <summary>THE WEST WALL — 10 × 48 m, carrying the unloading apron, the winch and a truck. Its
        /// water side faces EAST and the kit gives that edge only a curb, which is why the plan wants the
        /// winch to be a tall legible object rather than a detail on a wall.</summary>
        public static Rect ApronFootprint() => FootprintOf(NineMileCreekMainland.WestWallFill);

        /// <summary>Both decks, for anything that has to ask "is this on the quay at all?".</summary>
        public static IEnumerable<Rect> Decks()
        {
            yield return DeckFootprint();
            yield return ApronFootprint();
        }

        /// <summary>The MOORING EDGE: the north wall's south face, which is the wall the fleet lies
        /// against and the only edge the kit gives a tall face to.</summary>
        public static float MooringEdgeY => DeckFootprint().yMin;

        /// <summary>
        /// The compass heading the mooring face LOOKS ALONG (0 = North, 90 = East, clockwise) — out from
        /// the wall, over the water. What a hung fitting hangs into, and the way a ladder on that face is
        /// turned; a climber faces the reciprocal, back to the sea.
        ///
        /// <para>DERIVED, not typed: the mooring edge is the deck's <c>yMin</c>
        /// (<see cref="MooringEdgeY"/>), so the water lies south of it and the face looks along −Y — which
        /// is compass 180. Re-pointing the wall re-points this with it.</para>
        /// </summary>
        public static float MooringFaceHeadingDegrees => 180f;

        // --- the deck as FLOOR ------------------------------------------------------------------------

        /// <summary>Stable id of the north wall as a standable surface.</summary>
        public const string SurfaceId = "wharf.nine_mile_creek";
        /// <summary>…and of the west wall. Its own id because they are two structures, and a surface that
        /// claims ground it does not have is how a player ends up standing on the basin.</summary>
        public const string ApronSurfaceId = "wharf.nine_mile_creek.apron";

        /// <summary>
        /// The deck's standing height (m above chart datum), <b>MEASURED off the terrain</b> at the middle
        /// of the north wall — never a literal. A deck is LEVEL, so one number does the whole wall, and
        /// measuring means that if a terrain edit ever lowered the fill the deck follows it down instead
        /// of silently lying about where your feet are.
        /// </summary>
        public static float DeckElevationFrom(ITidalTerrain terrain)
            => terrain.ElevationAt(DeckFootprint().center);

        /// <inheritdoc cref="DeckElevationFrom"/>
        public static float ApronElevationFrom(ITidalTerrain terrain)
            => terrain.ElevationAt(ApronFootprint().center);

        // =====================================================================================
        //  THE BERTHS, AND THE FITTINGS THAT SERVE THEM
        // =====================================================================================
        // ⭐ THE FITTINGS ARE DERIVED FROM THE BERTH LINE. #451 made shore cleats real, and made them out
        // of this same table — so the bollard drawn on the quay and the bollard a rope may be made fast to
        // are one object. A-1 authored 14 berths at 5.5 m along the north wall's south face; giving each
        // one a bollard on the face beside it is what "there is always one where you come alongside"
        // means, and it cannot drift from the berths because it is computed from them.

        /// <summary>Berths along the mooring edge — the fleet in the owner's photograph (12–14 boats,
        /// rafted two deep in places).</summary>
        public static int BerthCount => NineMileCreekMainland.BerthCount;

        /// <summary>Centre of berth <paramref name="index"/> (0-based) — the plan's line, in the water off
        /// the wall's face.</summary>
        public static Vector2 BerthPos(int index) => NineMileCreekMainland.BerthPos(index);

        /// <summary>One placed piece of the wharf — a deck fitting or a block of breakwater armour: which
        /// kit slice, and where it goes.</summary>
        public struct Piece
        {
            public string Name;
            public Vector2 Position;
        }

        /// <summary>Every <paramref name="n"/>th berth gets a tyre fender hung off the face between its
        /// bollard and the next, so the gear reads as two runs rather than a matched pair at every post.</summary>
        public const int TyreEveryNthBerth = 2;

        /// <summary>
        /// The quay's fittings, all on the mooring edge.
        ///
        /// <para>⚠ Fitting pivots mean DIFFERENT things and were measured, not assumed, when the kit
        /// landed: standers (bollard/pilehead) sit 1 px above their base, hangers (ladder/tyre) pivot at
        /// the TOP and fall away from what they attach to. So a stander goes at the deck point it stands
        /// ON — half a metre in from the lip — and a hanger at the lip itself.</para>
        /// </summary>
        public static List<Piece> Fittings()
        {
            var list = new List<Piece>();
            Rect deck = DeckFootprint();
            float lip = MooringEdgeY;
            float stand = lip + 0.5f;          // half a metre in from the edge, on the concrete

            // A bollard per berth — the guarantee that a rope always has something to go round.
            for (int i = 0; i < BerthCount; i++)
                list.Add(new Piece { Name = "bollard", Position = new Vector2(BerthPos(i).x, stand) });

            // Tyres hung off the face, midway between every second pair of berths.
            for (int i = 0; i + 1 < BerthCount; i += TyreEveryNthBerth)
                list.Add(new Piece
                {
                    Name = "tyre",
                    Position = new Vector2((BerthPos(i).x + BerthPos(i + 1).x) * 0.5f, lip),
                });

            // A ladder mid-wall — how you get down to a punt at low water, and the one fitting a creek
            // that takes punts as well as lobster boats genuinely requires. Against a 4.6 m face that
            // bares 5.2 m at spring low, it is not a nicety.
            //
            // ⚠ It goes in a gap the TYRE RUN does not use, and that is derived rather than eyeballed:
            // the middle gap of an even berth count is an even-numbered one, which is exactly where a
            // fender already hangs. (NoTwoFittings_ShareAMetreOfLip caught this — the first draft stacked
            // the ladder on a tyre, and two hung fittings on one metre of wall read as a single broken
            // object.) Step one gap along and it is clear of both neighbours by half the berth spacing.
            int ladderGap = BerthCount / 2;
            if (ladderGap % TyreEveryNthBerth == 0) ladderGap++;
            ladderGap = Mathf.Clamp(ladderGap, 0, BerthCount - 2);
            list.Add(new Piece
            {
                Name = "ladder",
                Position = new Vector2((BerthPos(ladderGap).x + BerthPos(ladderGap + 1).x) * 0.5f, lip),
            });

            // The quay's own structure showing at the wall's exposed seaward corners.
            list.Add(new Piece { Name = "pilehead", Position = new Vector2(deck.xMax - 0.5f, stand) });
            list.Add(new Piece { Name = "pilehead", Position = new Vector2(deck.xMax - 0.5f, deck.yMax - 0.5f) });

            // A cleat at the apron's seaward corner — a painter line for whatever is small enough to lie
            // against the west wall rather than take a bollard on the working face.
            Rect apron = ApronFootprint();
            list.Add(new Piece { Name = "cleat", Position = new Vector2(apron.xMax - 0.5f, apron.yMin + 0.5f) });

            return list;
        }

        // =====================================================================================
        //  THE FLOATING DOCK — the one deck here whose height is the TIDE's
        // =====================================================================================
        // ⭐ THE SURFACE THIS SECTION WAS WAITING FOR HAS LANDED, and it cost Core nothing.
        // The note below used to end "when a tide-following surface lands (a Core change, and
        // lead-architect's call) the layout is already authored and tested here". It landed as
        // World.FloatingPlatform / World.GangwayPlatform / World.FloatCleat and Core did not move a
        // line: IStandableSurface is a QUERY ("am I over you, and if so how high is your deck?"), and
        // its own doc always claimed a moving deck was an implementation of that contract rather than a
        // change to it. This is the first registrant to prove the claim.

        /// <summary>Stable id of the floating dock as a standable surface.</summary>
        public const string FloatSurfaceId = "wharf.nine_mile_creek.float";
        /// <summary>…and of the brow you walk down to reach it. Its own id because it is its own
        /// structure, with its own rule for how high it is (a ramp, not a height).</summary>
        public const string GangwaySurfaceId = "wharf.nine_mile_creek.gangway";

        /// <summary>
        /// The float's plan outline: the authored run (y = 70, x = 104 → 152) given the width the ART
        /// draws it at — <see cref="NineMileCreekQuayFace.BakedRigFloatWidthMetres"/>, the wharf rig's
        /// own float-family default. Taken from the kit rather than picked, because a walkable footprint
        /// wider than the drawn dock is a player standing on water that merely looks solid.
        /// </summary>
        public static Rect FloatFootprint()
        {
            float width = NineMileCreekQuayFace.BakedRigFloatWidthMetres;
            return new Rect(NineMileCreekMainland.FloatRunWestX,
                            NineMileCreekMainland.FloatRunY - width * 0.5f,
                            NineMileCreekMainland.FloatRunLengthMetres, width);
        }

        /// <summary>How far apart the bed is sampled when measuring what the float grounds on: one metre.
        /// <para>Not a tunable — a numerical resolution, and the resolution the geometry asks for. The
        /// bank of the harbour channel climbs from the −3.90 m thalweg to the −1.60 m shoal across a 12 m
        /// half-width, i.e. under 0.2 m per metre at its steepest, so a one-metre grid cannot step over a
        /// shoal deep enough to matter against a 0.31 m draught.</para></summary>
        public const float FloatBedSampleStepMetres = 1f;

        /// <summary>
        /// ⭐ <b>WHAT THE FLOAT GROUNDS ON, MEASURED</b> — the SHALLOWEST ground anywhere under her, not
        /// the deepest and not the middle. A raft is rigid: the water surface is flat so every bay rides
        /// at the same height, and a rigid thing rests on the highest point beneath it. Sampling the
        /// centre-line alone would miss a shoal under one edge and float her through it.
        ///
        /// <para>Measured off the terrain for the same reason <see cref="DeckElevationFrom"/> is: a
        /// terrain edit that shoals this berth must ground the float, not leave it hovering over ground
        /// the region no longer has.</para>
        /// </summary>
        public static float FloatBedElevationFrom(ITidalTerrain terrain)
        {
            if (terrain == null) return float.NegativeInfinity;   // no terrain, no claim about grounding

            Rect box = FloatFootprint();
            float step = Mathf.Max(0.01f, FloatBedSampleStepMetres);
            float shallowest = float.NegativeInfinity;
            for (float x = box.xMin; x <= box.xMax + 1e-4f; x = Mathf.Min(x + step, box.xMax))
            {
                for (float y = box.yMin; y <= box.yMax + 1e-4f; y = Mathf.Min(y + step, box.yMax))
                {
                    float bed = terrain.ElevationAt(new Vector2(x, y));
                    if (bed > shallowest) shallowest = bed;
                    if (y >= box.yMax) break;      // Min() pins the last row ON the edge; break after it
                }
                if (x >= box.xMax) break;
            }
            return shallowest;
        }

        /// <summary>The brow's hinge and landing, straight off the plan — the apron's east face and the
        /// float's west end, so re-siting either takes the gangway with it.</summary>
        public static Vector2 GangwayShoreEnd => NineMileCreekMainland.GangwayShoreEnd;
        /// <inheritdoc cref="GangwayShoreEnd"/>
        public static Vector2 GangwayFloatEnd => NineMileCreekMainland.GangwayFloatEnd;

        /// <summary>How long the brow is: <b>12 m</b>, and DERIVED — the gap the geography leaves between
        /// the apron's face and the float's west end.
        ///
        /// <para>⚠ <b>The art would have chosen 13.68 m</b> — the rig's own <c>gRun</c> auto for a 4.4 m
        /// range (<see cref="NineMileCreekQuayFace.RigAutoGangwayRunMetres"/>). So this brow is
        /// 1.68 m short of the rig's rule and correspondingly steeper — 1:2.5 at dead low spring against
        /// the rig's 1:2.85, both inside its hard 3–14 m clamp. Lengthening it means moving the float
        /// east, and the harbour channel's meander is pinned to the float run's present ends. REPORTED,
        /// not taken.</para></summary>
        public static float GangwayRunMetres => Vector2.Distance(GangwayShoreEnd, GangwayFloatEnd);

        /// <summary>
        /// Where the floating dock's fittings go — a cleat run along it at the berth spacing, from half a
        /// pitch in so the first one is not hanging off the end. Pure and public, so the layout can be
        /// asserted without a scene.
        ///
        /// <para><b>⚠ STILL DELIBERATELY NOT IN <see cref="Fittings"/>.</b> Everything in that table is a
        /// QUAY fitting: it stands on one of the two walls, its cleat is registered at the deck's
        /// MEASURED elevation, and <c>EveryFitting_StandsOnTheQuay_NotOverTheBasin</c> holds it to exactly
        /// that. A float is none of those things — <b>its deck height is the TIDE's</b>, rising 4.4 m over
        /// the cycle — so its cleats are placed by <see cref="PlaceFloat"/> as
        /// <see cref="HiddenHarbours.World.FloatCleat"/>s, which read that height off the float itself
        /// instead of carrying a fixed one that would be wrong at every hour but two.</para>
        /// </summary>
        public static List<Vector2> FloatCleatPositions()
        {
            var list = new List<Vector2>();
            float spacing = NineMileCreekMainland.BerthSpacingMetres;
            float run = NineMileCreekMainland.FloatRunLengthMetres;
            int count = Mathf.Max(1, Mathf.FloorToInt(run / spacing));
            for (int i = 0; i < count; i++)
                list.Add(new Vector2(NineMileCreekMainland.FloatRunWestX + spacing * (i + 0.5f),
                                     NineMileCreekMainland.FloatRunY));
            return list;
        }

        // -------------------------------------------------------------------------------------
        //  …AND THE BOATS THAT LIE ALONGSIDE HER — the photograph's small craft
        // -------------------------------------------------------------------------------------
        // ⭐ THE PHOTOGRAPH IS TWO MOORINGS, NOT ONE. Small craft lie on the float fingers in the middle
        // of the bullpen; working boats lie against the tall quay walls. The wall's berth line has been
        // authored since A-1 (14 at 5.5 m, NineMileCreekMainland.BerthPos); this is the OTHER table, and
        // it is DERIVED from the float rather than typed beside it, so re-siting or re-cutting the float
        // takes its boats with it instead of leaving them lying in open water where a dock used to be.

        /// <summary>How many boats the float run berths: <b>one per cleat</b>, from
        /// <see cref="FloatCleatPositions"/>. Not a second count that could disagree with the fittings —
        /// a berth here IS the place a rope goes round, which is #451's guarantee carried onto the one
        /// deck that moves.</summary>
        public static int FloatBerthCount => FloatCleatPositions().Count;

        /// <summary>
        /// How far off a mooring face a moored hull's centre-line lies — <b>2 m</b>, and READ OFF THE
        /// WALL rather than picked again here: the region authored its berth line at
        /// <see cref="NineMileCreekMainland.FirstBerthPos"/>, two metres in front of
        /// <see cref="MooringEdgeY"/>. One convention for "alongside", so the float's boats stand off
        /// their dock exactly as far as the fleet stands off the quay, and moving the berth line moves
        /// both.
        /// </summary>
        public static float MooredStandoffMetres => MooringEdgeY - NineMileCreekMainland.FirstBerthPos.y;

        /// <summary>How far off the float's CENTRE-LINE a boat alongside her lies: half the dock's own
        /// width, plus the standoff. Derived from <see cref="FloatFootprint"/>, so a re-baked float of a
        /// different width pushes her boats out with her.</summary>
        public static float FloatBerthOffsetMetres =>
            FloatFootprint().height * 0.5f + MooredStandoffMetres;

        /// <summary>
        /// The two places a boat can lie at float berth <paramref name="index"/> — one off each side of
        /// the finger. Pure, so the layout can be asserted without a terrain or a scene.
        ///
        /// <para>A float has water on both sides and a real one is worked from both; which side a given
        /// berth actually uses is not a taste question here but a depth one, and
        /// <see cref="FloatBerthPos(int, ITidalTerrain)"/> measures it.</para>
        /// </summary>
        public static (Vector2 North, Vector2 South) FloatBerthSides(int index)
        {
            var cleats = FloatCleatPositions();
            if (cleats.Count == 0) return (Vector2.zero, Vector2.zero);
            Vector2 cleat = cleats[Mathf.Clamp(index, 0, cleats.Count - 1)];
            float off = FloatBerthOffsetMetres;
            return (new Vector2(cleat.x, cleat.y + off), new Vector2(cleat.x, cleat.y - off));
        }

        /// <summary>
        /// ⭐ <b>WHICH SIDE OF THE FINGER SHE LIES ON, MEASURED</b> — the side of float berth
        /// <paramref name="index"/> with more water under it.
        ///
        /// <para><b>It cannot be a constant, and the meander is why.</b> S1's lane does not run down the
        /// float's own latitude — it wanders across it (y = 70 at the head, 67 at the southern swing,
        /// 71 at the northern one), so the deeper side of the dock CHANGES ALONG HER LENGTH. Picking one
        /// side for the whole run would berth some boats on the lane and some on its bank, and the ones
        /// on the bank would be the ones that took the ground. Measured per berth off the same terrain
        /// the float's own bed is measured off, this puts every boat on the water there actually is —
        /// and puts them on BOTH sides of the finger, which is what the photograph shows.</para>
        ///
        /// <para>Ties go SOUTH, arbitrarily and harmlessly: with equal water either side the choice is
        /// free, and a deterministic tie-break is what stops a rebuild from reshuffling the fleet.</para>
        /// </summary>
        public static Vector2 FloatBerthPos(int index, ITidalTerrain terrain)
        {
            (Vector2 north, Vector2 south) = FloatBerthSides(index);
            if (terrain == null) return south;                  // no terrain, no claim: the authored side
            return terrain.ElevationAt(north) < terrain.ElevationAt(south) ? north : south;
        }

        /// <summary>The water over the bed at float berth <paramref name="index"/> at <b>spring low</b> —
        /// the worst hour of the month, which is the only hour worth gating a mooring on. Measured on the
        /// side the boat actually lies (<see cref="FloatBerthPos(int, ITidalTerrain)"/>).</summary>
        public static float FloatBerthDepthAtSpringLow(int index, ITidalTerrain terrain)
        {
            if (terrain == null) return float.PositiveInfinity;   // no terrain, no claim about grounding
            return NineMileCreekMainland.SpringLowWater
                   - terrain.ElevationAt(FloatBerthPos(index, terrain));
        }

        /// <summary>
        /// The deepest-draughted boat float berth <paramref name="index"/> can carry: the spring-low
        /// depth there, less the region's own keel clearance
        /// (<see cref="NineMileCreekMainland.ChannelKeelClearanceMetres"/> — the same 0.20 m the channel
        /// was cut to give the Cape Islander, so a berth and the lane that reaches it are gated by one
        /// number rather than two).
        ///
        /// <para>⚠ <b>DO NOT QUOTE THE DOCK'S OWN GUARANTEE HERE — it is a different measurement, and
        /// it does not bound this one in either direction.</b> <see cref="FloatBedElevationFrom"/> returns
        /// the SHALLOWEST ground anywhere under the float, because a raft is rigid and rests on the
        /// highest thing beneath it; that is a worst-point-over-a-rectangle. A berth is a single point
        /// beside her, on whichever side measured deeper. Measured on today's terrain the berths come
        /// out DEEPER than the dock's controlling number, not shallower — the dock is gated by one
        /// corner of her footprint while the berths are each free to take the better side. On another
        /// cut it could go the other way. Either way the number that gates a hull is measured where the
        /// HULL is, and quoting the dock's is simply a different question.</para>
        /// </summary>
        public static float DeepestDraughtAFloatBerthCarries(int index, ITidalTerrain terrain)
            => FloatBerthDepthAtSpringLow(index, terrain)
               - NineMileCreekMainland.ChannelKeelClearanceMetres;

        /// <summary>
        /// ⭐ <b>THE WIDEST HULL THE FLOAT CAN TAKE ALONGSIDE — her HALF-BEAM, against the standoff.</b>
        ///
        /// <para>The second gate on a float berth, and the one that is about SIZE rather than water. A
        /// boat lies with her centre-line <see cref="MooredStandoffMetres"/> off the dock's edge, so a
        /// hull whose half-beam exceeds that is not lying alongside the float at all — she is lying ON
        /// it, drawn overlapping the planking she is tied to, with her skipper standing in mid-air over
        /// the deck. Nothing about the water says so: at the meander's deepest reach the lane genuinely
        /// carries a working hull, and the depth gate alone would let her lie there.</para>
        ///
        /// <para>So this is what "the float is for SMALL CRAFT" actually means, measured rather than
        /// declared: the region's own standoff against the hull mesh's own watertight half-beam. Today
        /// that admits the punt (0.90 m) and the console skiff (1.25 m) and refuses the lobster boat
        /// (2.50 m) and the Cape Islander (2.40 m) — which is the fleet sorting itself into the
        /// photograph's two moorings without anybody typing a list of who goes where.</para>
        /// </summary>
        public static float WidestHalfBeamAFloatBerthCarries => MooredStandoffMetres;

        /// <summary>
        /// The compass heading a boat lying at the float points her bow.
        ///
        /// <para><b>DERIVED the same way the wall's is</b> — parallel to what she is tied to, pointing the
        /// way she leaves. The float runs east–west, and the end that is NOT the brow's landing
        /// (<see cref="GangwayFloatEnd"/>) is the seaward one, so that is the way her bow lies. Re-hinge
        /// the gangway on the other end and the fleet turns round with it.</para>
        /// </summary>
        public static float FloatMooredHeadingDegrees()
        {
            Rect box = FloatFootprint();
            float toEast = Mathf.Abs(box.xMax - GangwayFloatEnd.x);
            float toWest = Mathf.Abs(GangwayFloatEnd.x - box.xMin);
            return toEast > toWest ? 90f : 270f;
        }

        // =====================================================================================
        //  THE ROCK ARMOUR — the photograph's rubble line where made ground meets open water
        // =====================================================================================

        /// <summary>
        /// The armour blocks along one exposed run, laid the same way the breakwater's are: centres, from
        /// the run's start PLUS half a block, so a top-centre-pivoted sheet does not put half a crib on
        /// the beach. Pure.
        /// </summary>
        public static List<Piece> ArmourRun(Vector2 from, Vector2 to)
        {
            var list = new List<Piece>();
            float run = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.FloorToInt(run / ArmourWidthMetres));
            if (run <= 1e-3f) return list;

            Vector2 along = (to - from) / run;
            for (int i = 0; i < count; i++)
            {
                string variant = i == count - 1 ? "end" : "straight";
                list.Add(new Piece
                {
                    Name = variant,
                    Position = from + along * (ArmourWidthMetres * (i + 0.5f)),
                });
            }
            return list;
        }

        /// <summary>Every armour block on the region's two exposed faces — the wharf head and the spit's
        /// east edge. The breakwater keeps its own run (<see cref="BreakwaterBlocks"/>): it is a structure
        /// in its own right rather than an edge of made ground.</summary>
        public static List<Piece> ShoreArmourBlocks()
        {
            var list = new List<Piece>();
            list.AddRange(ArmourRun(NineMileCreekMainland.WharfHeadArmour[0],
                                    NineMileCreekMainland.WharfHeadArmour[1]));
            list.AddRange(ArmourRun(NineMileCreekMainland.SpitEastArmour[0],
                                    NineMileCreekMainland.SpitEastArmour[1]));
            return list;
        }

        /// <summary>How many of the fittings are things a rope may be made fast to — the kit decides, not
        /// this file (<see cref="WharfKitCatalog.IsMooringFitting"/>).</summary>
        public static int MooringFittingCount() => Fittings().Count(f => WharfKitCatalog.IsMooringFitting(f.Name));

        // =====================================================================================
        //  THE BREAKWATER
        // =====================================================================================
        // The owner's photographs show it unmistakably: log boxes filled with stone — `crib` armour, what
        // a small community wharf actually builds. `sheet` pile is saved for the commercial quay money and
        // machinery would build. A-1 authored it as a fill, so its line comes from the plan like the walls.

        /// <summary>Armour type. See the note above.</summary>
        public const string BreakwaterArmour = "crib";

        /// <summary>The arm, as ground.</summary>
        public static Rect BreakwaterFootprint() => FootprintOf(NineMileCreekMainland.BreakwaterFill);

        /// <summary>The arm's crest line (m) — south of the basin, sheltering it while leaving the
        /// approach open.</summary>
        public static float BreakwaterY => BreakwaterFootprint().center.y;
        /// <inheritdoc cref="BreakwaterY"/>
        public static float BreakwaterWestX => BreakwaterFootprint().xMin;
        /// <inheritdoc cref="BreakwaterY"/>
        public static float BreakwaterEastX => BreakwaterFootprint().xMax;

        /// <summary>Armour cell width in metres — 48 px at the locked 32 px = 1 m.</summary>
        public const float ArmourWidthMetres = 1.5f;

        /// <summary>How many armour blocks the arm takes — derived from its length, never typed in.</summary>
        public static int BreakwaterBlockCount =>
            Mathf.Max(1, Mathf.FloorToInt((BreakwaterEastX - BreakwaterWestX) / ArmourWidthMetres));

        /// <summary>
        /// Sorting order for the breakwater: the first order ABOVE the sea plane, or the harbour is drawn
        /// over its own armour. Read off the partition (ADR 0032) rather than typed, so a re-base moves it.
        /// </summary>
        public static int BreakwaterSortingOrder => SortingBands.WharfDeckMin;

        /// <summary>The band the wharf's own STRUCTURE draws in — below the decor band and above the sea.
        /// Nothing fills it now that the deck is ground; it is kept because it is the rung things standing
        /// ON the quay derive themselves against, and because the ISO quay Phase B draws belongs in it.</summary>
        public static int SortingOrderMin => SortingBands.WharfDeckMin;
        /// <inheritdoc cref="SortingOrderMin"/>
        public static int SortingOrderMax => SortingBands.WharfDeckMax;

        /// <summary>
        /// The armour blocks along the arm, west to east, with the seaward tip capped. Pure, so the run
        /// can be asserted without a scene.
        ///
        /// <para><b>The positions are CENTRES.</b> The armour sheet pivots TOP-CENTRE, so a block placed
        /// at <c>x</c> spans <c>x ± 0.75 m</c> and the run is laid out from the arm's west end PLUS half a
        /// block. Getting that wrong puts half a crib on the beach.</para>
        /// </summary>
        public static List<Piece> BreakwaterBlocks()
        {
            var list = new List<Piece>();
            int count = BreakwaterBlockCount;
            for (int i = 0; i < count; i++)
            {
                float x = BreakwaterWestX + ArmourWidthMetres * (i + 0.5f);
                string variant = i == count - 1 ? "end" : "straight";   // the seaward tip is battered
                list.Add(new Piece { Name = variant, Position = new Vector2(x, BreakwaterY) });
            }
            return list;
        }

        /// <summary>The arm's collision line — the crest, as the boat meets it.</summary>
        public static Vector2[] BreakwaterPoints => new[]
        {
            new Vector2(BreakwaterWestX, BreakwaterY),
            new Vector2(BreakwaterEastX, BreakwaterY),
        };

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Build the quay into the open scene. Returns the number of mooring cleats registered — the one
        /// count that matters, because it is the number of places a rope can be made fast, and it does not
        /// depend on any art being imported.
        /// </summary>
        /// <param name="terrain">The region's authored terrain, used to MEASURE the deck height
        /// (<see cref="DeckElevationFrom"/>). Required: a wharf whose deck height was guessed rather than
        /// measured is a wharf that floods without anything failing.</param>
        public static int Place(ITidalTerrain terrain)
        {
            var root = new GameObject(RootName);

            // THE DECKS ARE FLOOR. Both walls, each at its own measured height, so the on-foot sim stands
            // on the concrete rather than in the −1.6 m basin the walls are built out onto.
            float deckElevation = DeckElevationFrom(terrain);
            float apronElevation = ApronElevationFrom(terrain);
            AddPlatform(root, "NorthWall", SurfaceId, DeckFootprint(), deckElevation);
            AddPlatform(root, "WestWall", ApronSurfaceId, ApronFootprint(), apronElevation);

            int fittings = PlaceFittings(root);
            // WHERE A ROPE MAY BE MADE FAST (M2-38) — from the same table as the sprites, and OUTSIDE
            // PlaceFittings so mooring never depends on the overlay art having imported.
            int cleats = PlaceMooringCleats(root, deckElevation);
            // …and WHERE YOU CAN CLIMB DOWN, for exactly the same reason: the ladder is how you get aboard
            // once the ebb has dropped the boat below the planks, and that must not depend on art either.
            int ladders = PlaceLadders(root, deckElevation);
            // …and THE FLOAT, whose deck is the one here that is not a number at all but the tide. Built
            // after the apron because it hinges its brow off the apron's MEASURED height.
            float floatBed = FloatBedElevationFrom(terrain);
            int floatCleats = PlaceFloat(root, floatBed, apronElevation);
            int armour = PlaceBreakwater(root);
            int shoreArmour = PlaceShoreArmour(root);
            // …and THE WALLS, so the fisher stops walking off the quay (the owner's 2026-08-19 walk).
            // LAST, because the plan asks the terrain which edges are a fall and one of the answers is
            // "the seam where these two decks meet is not one" — a question only askable once both decks
            // exist. What is gated and what deliberately is not: see NineMileCreekWalls.
            float wallMetres = NineMileCreekWalls.Place(root, terrain);

            float lowDeck = HiddenHarbours.World.FloatingPlatform.DeckElevation(
                NineMileCreekMainland.SpringLowWater, floatBed,
                NineMileCreekQuayFace.BakedRigFloatDraughtMetres,
                NineMileCreekQuayFace.BakedRigFloatFreeboard);
            float highDeck = HiddenHarbours.World.FloatingPlatform.DeckElevation(
                NineMileCreekMainland.SpringHighWater, floatBed,
                NineMileCreekQuayFace.BakedRigFloatDraughtMetres,
                NineMileCreekQuayFace.BakedRigFloatFreeboard);

            Debug.Log(
                $"[NineMileCreekWharf] Built the squared-U quay as GROUND: the north wall " +
                $"{DeckFootprint().width:0.#} × {DeckFootprint().height:0.#} m and the west wall " +
                $"{ApronFootprint().width:0.#} × {ApronFootprint().height:0.#} m, both registered as " +
                $"standable floor with their decks MEASURED at {deckElevation:0.00} / {apronElevation:0.00} m " +
                $"above datum. {BerthCount} berths along the mooring edge at y={MooringEdgeY:0.#}, " +
                $"{fittings} fitting sprite(s) placed, {cleats} of them real mooring cleats (the " +
                $"bollard you see IS the bollard you tie to) and {ladders} real climbable ladder(s) at " +
                $"deck +{deckElevation:0.00} m (the ladder you see IS the ladder you climb). " +
                $"THE FLOAT RIDES THE TIDE: a {FloatFootprint().width:0.0} × " +
                $"{FloatFootprint().height:0.0} m dock over a MEASURED bed of {floatBed:0.00} m, her deck " +
                $"swinging {lowDeck:0.00} → {highDeck:0.00} m over the cycle and taking the ground only " +
                $"if the water ever fell to {floatBed + NineMileCreekQuayFace.BakedRigFloatDraughtMetres:0.00} m " +
                $"(draught {NineMileCreekQuayFace.BakedRigFloatDraughtMetres:0.00} m), reached by a " +
                $"{GangwayRunMetres:0.#} m brow hinged on the apron at +{apronElevation:0.00} m, with " +
                $"{floatCleats} float cleat(s) that ride the planks they are bolted to. " +
                $"{armour} block(s) of '{BreakwaterArmour}' " +
                $"breakwater along y={BreakwaterY:0.#}, and {shoreArmour} block(s) of shore armour on the " +
                "two faces of made ground the photograph shows exposed (the wharf head and the spit's " +
                $"east edge). AND IT IS WALLED: {wallMetres:0.#} m of blocking edge on the faces that are " +
                "a fall, through the one wall system (World.PropertyBoundary), gated at the brow alone. " +
                "THE DRAWN QUAY IS PHASE B's, from the ISO wharf " +
                "pack — the old tile kit does not scale to an 84 m wall and is ruled for migration.");
            return cleats;
        }

        static void AddPlatform(GameObject root, string name, string id, Rect footprint, float elevation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: false);
            go.transform.position = new Vector3(footprint.center.x, footprint.center.y, 0f);
            go.AddComponent<HiddenHarbours.World.StandablePlatform>().Configure(id, footprint, elevation);
        }

        static int PlaceFittings(GameObject root)
        {
            var sprites = LoadNamedSheet(WharfKitCatalog.OverlaysPath);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekWharf] no fitting slices at {WharfKitCatalog.OverlaysPath} — " +
                                 "the quay is bare (no bollards, ladder or fenders). The MOORING still " +
                                 "works: the cleats are placed from the same table and need no art.");
                return 0;
            }

            var fittingRoot = new GameObject("Fittings");
            fittingRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var f in Fittings())
            {
                string slice = WharfKitCatalog.FittingSlice(f.Name);
                if (!sprites.TryGetValue(slice, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Fitting_{f.Name}");
                go.transform.SetParent(fittingRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // A bollard is something you walk AROUND, and the quay under it is ground rather than a
                // sprite deck now — so it layers by world Y with the rest of the world instead of taking
                // a fixed order one above a deck band that no longer draws anything.
                go.AddComponent<YSortSprite>();
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Give every mooring fitting a <see cref="HiddenHarbours.World.ShoreCleat"/> — the Core-side
        /// tie-off point M2-38's ropes are made fast to. Derived from <see cref="Fittings"/> so placement
        /// has exactly one source, and filtered by <see cref="WharfKitCatalog.IsMooringFitting"/> so the
        /// KIT decides what counts as a tie-off.
        ///
        /// <para>The elevation is the quay's MEASURED deck height — the fixed end of every line, against
        /// which the tide moves the other one. ⚠ It is <b>+3.0 m</b> here against St Peters' +5.35 m, so
        /// the drop from cleat to boat is 0.8–5.2 m rather than 2.6–7.0 m: every line has more horizontal
        /// reach for the same scope, tying up is easier, and the falling-tide slip is less likely. That is
        /// the right way round — the working wharf should be the forgiving one — but it means <b>the ebb's
        /// teeth are a St Peters property, not a universal one</b>, and anyone tuning mooring against this
        /// wharf will read the mechanism as gentler than it is.</para>
        /// </summary>
        static int PlaceMooringCleats(GameObject root, float deckElevation)
        {
            var cleatRoot = new GameObject("MooringCleats");
            cleatRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int n = 0;
            foreach (var f in Fittings())
            {
                if (!WharfKitCatalog.IsMooringFitting(f.Name)) continue;

                var go = new GameObject($"Cleat_{f.Name}_{n}");
                go.transform.SetParent(cleatRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                go.AddComponent<HiddenHarbours.World.ShoreCleat>()
                  .Configure($"{SurfaceId}.{f.Name}_{n}", deckElevation);
                n++;
            }
            return n;
        }

        /// <summary>
        /// Give every ladder fitting a <see cref="HiddenHarbours.World.WharfLadder"/> — the Core-side
        /// climb the boarding move takes when the tide has opened a gap a step cannot cross. Derived from
        /// <see cref="Fittings"/> for the same reason the cleats are: <b>the ladder you can see is the
        /// ladder you can climb</b>, and placement has exactly one source.
        ///
        /// <para>The elevation is the quay's MEASURED deck height — the FIXED end of the tide gap, against
        /// which the floating boat moves. At this wharf's +3.0 m against a 2.2 m tide amplitude and a
        /// dory's 0.55 m sheer, the gap runs from about a quarter of a metre at high water to about two
        /// and a half at low: a step aboard for the top of the tide, a climb for the bottom of it. That
        /// spread is the whole point, and it is a property of THIS wharf's deck height — a higher quay
        /// (St Peters is +5.35 m) is a ladder wharf at almost every state of tide.</para>
        /// </summary>
        static int PlaceLadders(GameObject root, float deckElevation)
        {
            var ladderRoot = new GameObject("Ladders");
            ladderRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int n = 0;
            foreach (var f in Fittings())
            {
                if (!string.Equals(f.Name, "ladder", System.StringComparison.Ordinal)) continue;

                var go = new GameObject($"Ladder_{n}");
                go.transform.SetParent(ladderRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                go.AddComponent<HiddenHarbours.World.WharfLadder>()
                  .Configure($"{SurfaceId}.ladder_{n}", deckElevation, MooringFaceHeadingDegrees);
                n++;
            }
            return n;
        }

        /// <summary>
        /// ⭐ <b>BUILD THE FLOAT AS FLOOR</b> — the dock, the brow you reach it by, and the cleats you tie
        /// to, all three riding one number. Returns the count of float cleats registered, which is the
        /// same "places a rope can be made fast" measure <see cref="PlaceMooringCleats"/> returns and does
        /// not depend on any art having imported.
        ///
        /// <para><b>The order is load-bearing:</b> the float is configured FIRST because both of the other
        /// two read their height off it — the brow lerps its landing end from
        /// <see cref="HiddenHarbours.World.FloatingPlatform.DeckElevationNow"/> and every cleat IS that
        /// number. One float, one deck height, and nothing downstream that can hold a stale copy of
        /// it.</para>
        ///
        /// <para><b>What the numbers are and where each comes from:</b> the footprint is the plan's
        /// authored run at the ART's float width; the freeboard and draught are the wharf rig's committed
        /// defaults (<see cref="NineMileCreekQuayFace.BakedRigFloatFreeboard"/> /
        /// <see cref="NineMileCreekQuayFace.BakedRigFloatDraughtMetres"/>, the second derived from the
        /// billets the first is drawn above); the bed is MEASURED off the terrain by
        /// <see cref="FloatBedElevationFrom"/> and passed in; and the abutment is the
        /// apron's own measured deck, so the brow meets the concrete rather than stepping above it — which
        /// is the rig's stated rule for a gangway hinge (<c>abut = T.R + s.clearance</c>).</para>
        /// </summary>
        static int PlaceFloat(GameObject root, float bedElevation, float apronElevation)
        {
            var floatRoot = new GameObject("FloatingDock");
            floatRoot.transform.SetParent(root.transform, worldPositionStays: false);

            Rect box = FloatFootprint();
            var floatGo = new GameObject("Float");
            floatGo.transform.SetParent(floatRoot.transform, worldPositionStays: false);
            floatGo.transform.position = new Vector3(box.center.x, box.center.y, 0f);
            var dock = floatGo.AddComponent<HiddenHarbours.World.FloatingPlatform>();
            dock.Configure(FloatSurfaceId, box, bedElevation,
                           NineMileCreekQuayFace.BakedRigFloatDraughtMetres,
                           NineMileCreekQuayFace.BakedRigFloatFreeboard);

            var browGo = new GameObject("Gangway");
            browGo.transform.SetParent(floatRoot.transform, worldPositionStays: false);
            browGo.transform.position = new Vector3((GangwayShoreEnd.x + GangwayFloatEnd.x) * 0.5f,
                                                    (GangwayShoreEnd.y + GangwayFloatEnd.y) * 0.5f, 0f);
            browGo.AddComponent<HiddenHarbours.World.GangwayPlatform>()
                  .Configure(GangwaySurfaceId, GangwayShoreEnd, GangwayFloatEnd,
                             NineMileCreekQuayFace.BakedRigGangwayWidthMetres * 0.5f,
                             apronElevation, dock);

            var cleatRoot = new GameObject("FloatCleats");
            cleatRoot.transform.SetParent(floatRoot.transform, worldPositionStays: false);

            int n = 0;
            foreach (Vector2 at in FloatCleatPositions())
            {
                var go = new GameObject($"FloatCleat_{n}");
                go.transform.SetParent(cleatRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(at.x, at.y, 0f);
                go.AddComponent<HiddenHarbours.World.FloatCleat>()
                  .Configure($"{FloatSurfaceId}.cleat_{n}", dock);
                n++;
            }
            return n;
        }

        static int PlaceBreakwater(GameObject root)
        {
            var armRoot = new GameObject("Breakwater");
            armRoot.transform.SetParent(root.transform, worldPositionStays: false);

            // Solid FIRST, and unconditionally: a breakwater a boat sails through is scenery, and that
            // must not depend on whether an art sheet imported.
            var edge = armRoot.AddComponent<EdgeCollider2D>();
            edge.points = BreakwaterPoints;

            var sprites = LoadIndexedSheet(WharfKitCatalog.BreakwatersPath);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekWharf] no armour slices at {WharfKitCatalog.BreakwatersPath} — " +
                                 "the arm is undrawn but still SOLID, and still standing in the terrain. " +
                                 "Re-run after the sheet imports.");
                return 0;
            }

            int placed = 0;
            foreach (var block in BreakwaterBlocks())
            {
                int index = WharfKitCatalog.BreakwaterIndex(BreakwaterArmour, block.Name);
                if (!sprites.TryGetValue(index, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Crib_{block.Position.x:0.#}");
                go.transform.SetParent(armRoot.transform, worldPositionStays: false);
                // TOP-CENTRE pivot (the kit's importer sets it): the block hangs BELOW its crest point.
                go.transform.position = new Vector3(block.Position.x, block.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = BreakwaterSortingOrder;
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Lay the shore armour. Same sheet and same top-centre pivot as the breakwater's, so the two runs
        /// are one material — which is what the photograph shows and what a community wharf can afford.
        ///
        /// <para>No collider: unlike the breakwater, these blocks armour an edge of ground the terrain
        /// already stands up. A hull is stopped there by DEPTH against the authored height field (the
        /// region's whole no-fence argument), and a second hand-laid line along ground that already stops
        /// her would be the duplicate coastline this recreation exists to remove.</para>
        /// </summary>
        static int PlaceShoreArmour(GameObject root)
        {
            var blocks = ShoreArmourBlocks();
            if (blocks.Count == 0) return 0;

            var sprites = LoadIndexedSheet(WharfKitCatalog.BreakwatersPath);
            if (sprites.Count == 0) return 0;   // the breakwater already warned about this sheet

            var armRoot = new GameObject("ShoreArmour");
            armRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var block in blocks)
            {
                int index = WharfKitCatalog.BreakwaterIndex(BreakwaterArmour, block.Name);
                if (!sprites.TryGetValue(index, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Armour_{block.Position.x:0.#}_{block.Position.y:0.#}");
                go.transform.SetParent(armRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(block.Position.x, block.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // Armour is something you walk past on the yard's edge, so it layers by world Y with the
                // rest of the world rather than taking the breakwater's fixed order out in the water.
                go.AddComponent<YSortSprite>();
                placed++;
            }
            return placed;
        }

        /// <summary>A grid-sliced sheet's sub-sprites keyed by their trailing slice INDEX (the slicer
        /// names them <c>&lt;stem&gt;_&lt;index&gt;</c>, row-major from the top-left).</summary>
        static Dictionary<int, Sprite> LoadIndexedSheet(string path)
        {
            var byIndex = new Dictionary<int, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is Sprite s)) continue;
                int u = s.name.LastIndexOf('_');
                if (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n)) byIndex[n] = s;
            }
            return byIndex;
        }

        /// <summary>A sidecar-sliced sheet's sub-sprites keyed by their full slice NAME (the overlays are
        /// named, not indexed, because each fitting has its own rect and its own kind of pivot).</summary>
        static Dictionary<string, Sprite> LoadNamedSheet(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                         .GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
    }
}
#endif
