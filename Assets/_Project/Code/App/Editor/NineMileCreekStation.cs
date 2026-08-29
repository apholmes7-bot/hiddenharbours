#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App;               // PersistentWalletProxy
using HiddenHarbours.Art;               // StationPieceDef / StationInteractable
using HiddenHarbours.Art.Editor;        // StationCatalog / StationReachAudit
using HiddenHarbours.Core;              // ITidalTerrain / FuelGrades
using HiddenHarbours.Economy;           // FuelPump / FuelStationDef
using HiddenHarbours.World;             // MainlandTidalTerrain / PropertyBoundary

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>NINE MILE CREEK BUYS FUEL — the two sites the owner asked for, 2026-08-20.</b>
    /// <i>"A couple fuel pumps at NMC wharf which charge a higher price, then there's a full gas station
    /// at the end of the wharf road intersection with Route 91."</i> Two places, one kit, two prices —
    /// and the whole bargain of the pair is the difference: the wharf saves you the walk, Route 91 saves
    /// you the money. Both <see cref="FuelStationDef"/> assets already say so in their own flavour text
    /// (#610); this is the ground they stand on.
    ///
    /// <para><b>Sites here, kit arithmetic in <see cref="StationForecourt"/></b> — the same division
    /// <see cref="NineMileCreekShops"/> and <see cref="ShopPlacement"/> draw, and for the same reason.
    /// </para>
    ///
    /// <para><b>⭐ THESE PUMPS FILL A BOAT, AND THAT BECAME TRUE SIX MINUTES BEFORE THIS FILE LANDED.</b>
    /// The economy lane was building the tank in a five-PR stack while this slice was written, so the
    /// paragraph that shipped here described a world that had already moved on. Corrected, with the
    /// numbers, because a stale "not wired" note is worse than none:</para>
    /// <list type="bullet">
    /// <item><description>#618 put the tank on the hull AS DATA; #619 gave <see cref="IFuelVessel"/> its
    /// <c>Draw</c> half; <b>#620 landed the runtime</b> — <c>BoatFuelTank</c>,
    /// <c>GameServices.ActiveBoatFuel</c>, and a <c>FuelPump.TargetVessel()</c> that falls back from your
    /// hands to the boat you are aboard. Its <c>Contexts</c> now carry <c>OnDeck</c>.</description></item>
    /// <item><description>So the wharf Def's own flavour — <i>"you lie alongside and fill without leaving
    /// the water"</i> — is now a promise the VERB keeps too, and the dock pedestal is doing the job it was
    /// sited for rather than waiting for one.</description></item>
    /// </list>
    /// <para><b>The siting carries it, measured.</b> The pump component stands <b>0.695 m</b> in from the
    /// apron's lip, so a body at a hull's INBOARD RAIL — hard against the wharf, which is where you stand
    /// to take a hose — is 0.69 m from it, well inside the pump's 2 m. On a dory even her centreline
    /// (1.59 m) reaches. ⚠️ From amidships on a lobster boat (3.19 m) you are out of range and must walk to
    /// the rail, which is the honest behaviour rather than a defect.</para>
    /// <para>⚠️ A CAN's level is still session-local (#623 persists the TANK, not the can), so the old
    /// leak closes for boats and not for cans.</para>
    ///
    /// <para><b>⚠️⚠️ ONE PUMP PER DISPENSER SIDE, AND THAT IS A FINDING RATHER THAN A CHOICE.</b> A
    /// multi-product machine's three nozzles share ONE standing spot — measured off the shipped Defs, the
    /// <c>sMpd</c>'s <c>a0/a1/a2</c> reach points are all <c>(1.21, −0.34)</c> and differ only in Z. Three
    /// <see cref="FuelPump"/>s there would be three candidates at one <c>WorldPosition</c>, and
    /// <c>InteractResolver</c>'s total order — priority, then distance, then LOWEST ID — would hand every
    /// press to the same one forever, so two of the three grades would be visible on the machine and
    /// unreachable by the player. So a side carries exactly one hose here, its grade named in the offer
    /// label before the press. Giving a pump a grade CHOICE is a change to the interact seam
    /// (<c>HiddenHarbours.Economy</c>), not to placement, and is left to that lane.</para>
    /// </summary>
    public static class NineMileCreekStation
    {
        public const string RootName = "CreekFuel";
        public const string WharfRootName = "WharfPumps";
        public const string ForecourtRootName = "Route91Station";

        const string Tag = "[NineMileCreekStation]";

        /// <summary>Stable ids of the two sites' Defs (#610), so nothing here spells one twice.</summary>
        public const string WharfStationId = "station.nmc_wharf_pumps";
        public const string RoadStationId = "station.route_91";

        /// <summary>Where #610 wrote them.</summary>
        public const string StationDefFolder = "Assets/_Project/Data/FuelStations";

        // =================================================================================================
        //  SITE A — THE WHARF PUMPS
        // =================================================================================================
        // ⭐ THE PLAN ALREADY SITED THESE, IN WORDS. NineMileCreekMainland's float note says the west wall
        // "is the service end (the fuel pump and the oil tank are both on it)" — written when the float was
        // authored and never built. This is that sentence taken literally, and it is also the only answer
        // the yard allows: the spit is FULL (the fish market misses its own site by 0.38 m), so a fuel
        // island ashore is not on the table here. The pumps go on the WHARF STRUCTURE, serving the water.
        //
        // The kit ships exactly the machine for it. `dispenser_sDock` is the DOCK PEDESTAL — one hose on a
        // hose mast with a 4.5 m reach, against 3.2 m for the kerb post and 4.0 for the twin slab. It is
        // the longest reach in the kit bar the high-flow truck pump, and it is the only piece whose art is
        // drawn for a wharf.

        /// <summary>The apron — the west wall, whose water side faces EAST. The one edge on this wharf a
        /// boat lies against that is not the fleet's own mooring face.</summary>
        public static Rect Apron => NineMileCreekWharf.ApronFootprint();

        /// <summary>The heading a thing on the apron looks along to face the water it serves. Read from
        /// the dressing rather than restated, so the pumps and the drawn quay face cannot disagree about
        /// which way the sea is.</summary>
        public static float ApronSeawardHeading => NineMileCreekDressing.ApronSeawardHeading;

        /// <summary>
        /// The cell a wharf pump is drawn at: the one that puts its NOZZLE over the water.
        ///
        /// <para>⚠️ Not <c>FacingForHeading(seaward)</c>, which would turn the piece's +Y that way and
        /// point the hose along the wall — a dispenser's business axis is +X and the difference is a
        /// quarter turn that draws perfectly. Derived from the pedestal's own published nozzle position,
        /// so a re-bake that moves the hose turns the pump with it. See
        /// <see cref="StationCatalog.FacingForLocalDirection"/>.</para>
        /// </summary>
        public static int WharfPumpFacing
        {
            get
            {
                Vector2 hose = Vector2.right;    // fallback: this kit's hose axis
                var def = StationCatalog.Find("dispenser", "sDock");
                if (def != null && def.Interactables != null && def.Interactables.Length > 0)
                {
                    Vector3 p = def.Interactables[0].Position;
                    if (new Vector2(p.x, p.y).sqrMagnitude > 1e-6f) hose = new Vector2(p.x, p.y);
                }
                return StationCatalog.FacingForLocalDirection(hose, ApronSeawardHeading);
            }
        }

        /// <summary>
        /// How much daylight a body keeps between itself and the quay wall (m).
        ///
        /// <para>⭐ This is the number that decides how far back the pedestals stand, and it is not
        /// decoration. The pedestal's standing spot is on its NOZZLE side — that is where the hose is —
        /// which on a pump facing the water is the seaward side, over a 4.6 m drop. #609 walled that edge
        /// (<see cref="NineMileCreekWalls"/>), so a pedestal set at the lip would publish a reach point
        /// inside its own quay wall and the whole-scene pass would have to march it back. Standing the
        /// machine back by the arithmetic below puts the body on the deck by construction.</para>
        /// </summary>
        public const float PumpBodyClearanceMetres = 0.30f;

        /// <summary>
        /// How far in from the apron's lip a pedestal stands — DERIVED, never typed: the piece's own
        /// published reach offset, plus a body, plus half the quay wall, plus
        /// <see cref="PumpBodyClearanceMetres"/>. Re-bake the pedestal with a different reach point and
        /// the pumps step back to suit it.
        /// </summary>
        public static float WharfPumpStandbackMetres
        {
            get
            {
                float reachOut = 1.06f;    // fallback: the shipped sDock's own nozzle-side reach offset
                var def = StationCatalog.Find("dispenser", "sDock");
                if (def != null && def.Interactables != null && def.Interactables.Length > 0 &&
                    def.Interactables[0].HasReachPoint)
                    reachOut = Mathf.Abs(def.Interactables[0].ReachPoint.x);

                return reachOut
                     + StationReachAudit.BodyRadiusMetres
                     + PropertyBoundary.DefaultThicknessMetres * 0.5f
                     + PumpBodyClearanceMetres;
            }
        }

        /// <summary>How far south of the brow the first pedestal stands (m) — far enough that a pump is
        /// not something you squeeze past walking down to the float, and the two offers cannot overlap
        /// (<see cref="FuelPump"/> reaches 2 m).</summary>
        public const float WharfPumpBrowClearMetres = 4f;

        /// <summary>Between the two pedestals (m). Twice the pump's own reach, so standing at one you are
        /// never offered the other.</summary>
        public const float WharfPumpSpacingMetres = 4f;

        /// <summary>
        /// The two pedestals, north first — on the apron's face, SOUTH of the brow and NORTH of the
        /// slipway, which is the only stretch of this wall with nothing already hanging on it.
        ///
        /// <para><b>⭐ And it is the stretch that keeps its water.</b> The basin bares at spring low
        /// (bed −1.6 m against a −2.2 m tide), so "alongside the wharf" is not everywhere a wet berth.
        /// The dredged channel's head is off this apron at (100, 70) with a 12 m half-width, and a hull
        /// lying 2.5 m off this face at either pedestal is inside it — which
        /// <c>NineMileCreekStationTests</c> measures rather than trusts.</para>
        /// </summary>
        public static IReadOnlyList<Vector2> WharfPumpPositions
        {
            get
            {
                float x = Apron.xMax - WharfPumpStandbackMetres;
                float north = NineMileCreekMainland.GangwayShoreEnd.y - WharfPumpBrowClearMetres;
                return new[]
                {
                    new Vector2(x, north),
                    new Vector2(x, north - WharfPumpSpacingMetres),
                };
            }
        }

        /// <summary>
        /// Which grade each pedestal sells, north first. Gas takes the pump nearer the brow because gas is
        /// what every outboard on this wharf burns; diesel is the bigger hulls' and there are fewer of
        /// them. ⚠️ Only these two are <c>Available</c> on the wharf Def — its mixed and oil rows are
        /// authored-but-off, deliberately, so that "anything past gas and diesel is a walk up the road"
        /// (its own flavour) is true in the data and not only in the prose.
        /// </summary>
        public static readonly string[] WharfPumpGrades = { FuelGrades.Gas, FuelGrades.Diesel };

        // =================================================================================================
        //  SITE B — THE ROUTE 91 FORECOURT
        // =================================================================================================

        /// <summary>The junction the owner named: Wharf Road's landward end, which is a vertex of the
        /// through-road as well. Both polylines carry the same point, so this reads it rather than
        /// restating it.</summary>
        public static Vector2 Route91Junction => NineMileCreekMainland.WharfRoad[0];

        /// <summary>
        /// ⭐ <b>THE FORECOURT'S ORIGIN — the island's ground centre, and THE ONE NUMBER THAT SITES THE
        /// STATION.</b> Everything else on this site is <c>StationIso.plan()</c>'s own offsets from it, so
        /// moving the station is editing this point and rebuilding. The <see cref="TruckParkPosDiscipline"/>
        /// remark below is the precedent.
        ///
        /// <para>The discipline is <c>NineMileCreekMainland.TruckParkPos</c>'s, taken literally: one
        /// authored point, everything else derived from it, and no second copy of the site anywhere.</para>
        ///
        /// <para><b>Why here — the south-west corner, 16.6 m from the junction.</b> Wharf Road DEAD-ENDS on
        /// Route 91 from the east, so this is a T and this corner is straight ahead as you drive up off the
        /// wharf. Chosen by the max-min-slack search <see cref="NineMileCreekShops"/> used for the
        /// restaurant, over all four corners and all four orientations, against the road corridors, the
        /// nine town lots at <c>TownLotRadius</c>, their yard polygons (#604) and the truck park, with the
        /// forecourt's own front edge held within 1.5 m of Route 91's corridor so the apron meets the
        /// carriageway rather than sitting in a field near it. Measured at this point:</para>
        /// <list type="bullet">
        /// <item><b>3.48 m</b> to the nearest thing — House South's dooryard — which is the binding
        /// clearance and the reason the site is not further south.</item>
        /// <item><b>3.51 m</b> to Wharf Road's own corridor.</item>
        /// <item><b>1.33 m</b> from the front edge to Route 91's corridor: the apron meets the road.</item>
        /// <item>Land at <c>LandElevation</c> against a spring high of <c>SpringHighWater</c> — dry at
        /// every tide, and asserted rather than assumed.</item>
        /// </list>
        /// <para>⚠️ The runner-up (2.5 m further west, road side turned about) scored 0.08 m better and was
        /// REJECTED on reading rather than on numbers: it puts the C-store between the pumps and the
        /// highway, which is a station with its back to the road. The corner across the junction to the
        /// north scored 2.35 m.</para>
        /// </summary>
        public static readonly Vector2 Route91ForecourtPos = new Vector2(-191f, 81.6f);

        /// <summary>Where the forecourt's road side looks: EAST, onto Route 91. The island therefore lies
        /// north–south, parallel to the highway, and a vehicle pulls in off it — which is the layout the
        /// rig's own frame is built for (<c>+y is the ROAD</c>).</summary>
        public const float Route91RoadHeadingDegrees = 90f;

        /// <summary>The grades the forecourt's four pump faces carry, in the order the pieces are laid
        /// out (slot 1 side a, slot 1 side b, slot 2 side a, slot 2 side b).
        ///
        /// <para>Three grades over four faces, so one repeats, and it is GAS — the grade every outboard on
        /// this coast burns. ⚠️ The site's Def sells five: <c>oil</c> and <c>stove_oil</c> are posted at
        /// Route 91 and have NO HOSE here, because the committed art is baked for three grades
        /// (<c>GasStationKit.BakedGrades</c>) and a station selling four is a different sheet rather than
        /// a tint of this one. That is the owner's open grade-count call, and it is reported on every
        /// build rather than hidden.</para></summary>
        public static readonly string[] Route91FaceGrades =
            { FuelGrades.Gas, FuelGrades.Diesel, FuelGrades.Mixed, FuelGrades.Gas };

        /// <summary>
        /// <b>THE FORECOURT, AS THE RIG LAID IT OUT.</b> Every offset below is
        /// <c>StationIso.plan()</c>'s own output for the cfg named here — not a transcription of its
        /// arithmetic and not a hand-composed arrangement.
        ///
        /// <para><b>The cfg, and the run.</b>
        /// <c>{ grades:['gas','diesel','mixed'], rows:[[{size:'sMpd'},{size:'sMpd'}]], canopy:'fascia',
        /// sign:'sPylon', store:'sStore', wear:'working', lit:false }</c> — one island of two, the
        /// multi-product machine (the only dispenser in the kit that draws three grades), a two-bay fascia
        /// canopy, a pylon sign and the C-store. Run 2026-08-20 in the standalone V8 harness on the
        /// committed <c>gasStationRig.js</c>, in the registered load order
        /// (<c>deckIsoSolid → fuel → gasStation → stationInterior</c>). <b>The harness was controlled
        /// first:</b> <c>StationIso.gameplayAll({grades:['gas','diesel','mixed']})</c> reproduced the
        /// committed sidecar BYTE-FOR-BYTE at 83,650 bytes, which is what proves the load order and the
        /// grade list before any of these numbers were read off it.</para>
        ///
        /// <para>⚠️ <b>The interior is NOT in <c>plan()</c>'s list and is added here.</b> The sales floor
        /// is a second rig painting into the storefront's own cell, so it shares the shell's position AND
        /// its pivot exactly — that identity is the seamless design (ADR 0036) and the reason each sheet
        /// can crop to its own ink and still register. It sorts one order under its shell through
        /// <c>_baseOrder</c>, which #613 already baked into its prefab.</para>
        /// </summary>
        public static StationForecourt.Layout Route91Layout()
        {
            var layout = new StationForecourt.Layout
            {
                Name = ForecourtRootName,
                Origin = Route91ForecourtPos,
                RoadHeadingDegrees = Route91RoadHeadingDegrees,
            };

            layout.Add("island_s2", new Vector2(0f, 0f),
                       "the island the whole layout is measured from — 7.75 m of kerb, two bolt-down " +
                       "slots at the kit's 3.30 m pitch, lying parallel to the highway");

            layout.Add("dispenser_sMpd", new Vector2(-1.65f, 0f),
                       "the north machine: multi-product, three hoses a side, the only dispenser in the " +
                       "kit whose art draws three grades");

            layout.Add("dispenser_sMpd", new Vector2(1.65f, 0f),
                       "the south machine, on the island's second slot");

            layout.Add("canopy_sB2", new Vector2(0f, 0f),
                       "two bays over the one island — 4.55 m of clearance, which is what a harbour " +
                       "builds and what keeps the near lane open to the camera");

            layout.Add("sign_sPylon", new Vector2(-6.275f, 5.5f),
                       "the price pylon at the road, on the junction corner where both roads read it");

            layout.Add("store_sStore", new Vector2(0f, -11.6f),
                       "the C-store across the back, its front to the pumps");

            layout.Add("interior_sStore", new Vector2(0f, -11.6f),
                       "…and its sales floor, in the SAME cell at the SAME pivot — the seamless interior " +
                       "(ADR 0036), which is where the six things you can buy inside live");

            layout.Add("fillport_sCluster", new Vector2(4.475f, -6.5f),
                       "the tanker fill caps, back on the building side away from the traffic path");

            layout.Add("fillport_sVent", new Vector2(6.775f, -7.3f),
                       "the vent risers beside them");

            return layout;
        }

        /// <summary>The forecourt's whole footprint in world metres — <c>plan()</c>'s own extent, turned
        /// and placed. What a clearance test measures and what the paving is cut from.</summary>
        public static Rect Route91Extent()
        {
            // plan().extent for this cfg, measured in the same harness run as the layout above.
            var lo = new Vector2(-7.875f, -16.9f);
            var hi = new Vector2(8.075f, 7.0f);
            return WorldRectOf(lo, hi);
        }

        /// <summary>
        /// The DRIVING half of the forecourt — from the storefront's front face out to the road edge.
        /// This is what gets paved: gravel under a building is pointless, and the pad's real job besides
        /// looking like a forecourt is that <c>NineMileCreekFields.IsGrassGround</c> steps off any pad, so
        /// the meadow stops at the concrete instead of growing between the pumps.
        /// </summary>
        public static Rect Route91ApronArea()
        {
            float storeFront = -11.6f + StoreHalfDepthMetres();
            return WorldRectOf(new Vector2(-7.875f, storeFront), new Vector2(8.075f, 7.0f));
        }

        /// <summary>Half the C-store's depth (m), from its own Def so a re-bake moves the paving with the
        /// building. Falls back to the shipped 8.82 m when the kit is not installed.</summary>
        public static float StoreHalfDepthMetres()
        {
            var def = StationCatalog.Find("store", "sStore");
            return (def != null && def.DepthMeters > 0f ? def.DepthMeters : 8.82f) * 0.5f;
        }

        /// <summary>A rectangle in the forecourt's own frame, turned and placed into the world.</summary>
        static Rect WorldRectOf(Vector2 lo, Vector2 hi)
        {
            int facing = StationCatalog.FacingForHeading(Route91RoadHeadingDegrees);
            var a = StationCatalog.LocalToWorld(lo, Route91ForecourtPos, facing);
            var b = StationCatalog.LocalToWorld(hi, Route91ForecourtPos, facing);
            var c = StationCatalog.LocalToWorld(new Vector2(lo.x, hi.y), Route91ForecourtPos, facing);
            var d = StationCatalog.LocalToWorld(new Vector2(hi.x, lo.y), Route91ForecourtPos, facing);

            float xMin = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float xMax = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float yMin = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float yMax = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        // =================================================================================================
        //  THE LOANER CANS — what the owner asked for: something to FILL
        // =================================================================================================
        // Owner, 2026-08-28: "the gas station needs an interior and you should have a few emtpy fuel
        // cansiters to test filling." The second half is this. Every mechanism already existed — the
        // pump fills any IFuelVessel and charges for it, the hands carry one, eighty-four container Defs
        // are baked — and none of it could be REACHED, because nowhere in the game was there a can. The
        // only way to hold one was a dev menu item.

        /// <summary>The cans' own root under the station, so they are one thing to find and to
        /// rebuild.</summary>
        public const string LoanerCansRootName = "LoanerCans";

        /// <summary>
        /// Where the row of cans stands, in the forecourt's own frame (metres, +y is the road).
        ///
        /// <para><b>Why here.</b> On the open apron between the C-store's front wall (its plan ends at
        /// local y − 7.5) and the pumps (y 0), over on the west side away from the tanker fill caps.
        /// Far enough from a hose that carrying one there is a real errand, and close enough that it
        /// reads as station property rather than litter in a field. ⚠️ It is inside
        /// <see cref="Route91ApronArea"/>, so the cans stand on the forecourt's own paving and not in
        /// the meadow that stops at it.</para>
        ///
        /// <para>Tunable by the owner: this point and the spacing below are the whole siting (rule 6).
        /// Every spot derived from them is PROVED before a can is placed — see
        /// <see cref="StationFuelCans.Standable"/> — so moving the row cannot silently strand one.</para>
        /// </summary>
        public static readonly Vector2 LoanerCanRowCentreLocal = new Vector2(-4f, -5f);

        /// <summary>Between two cans in the row (m). Wider than two cans' clearance, so each is its own
        /// interact candidate and the resolver never has to choose between two at one distance.</summary>
        public const float LoanerCanSpacingMetres = 0.6f;

        /// <summary>
        /// Which cans stand there, west to east — by Def ID, which is the append-only contract.
        ///
        /// <para>Two gas and one diesel, and the diesel is not decoration: the wharf sells diesel and
        /// Route 91 posts it, so a player needs a way to carry some. It also makes the honest refusal
        /// reachable — <c>FuelPump</c> claims the press for ANY fuel vessel precisely so that standing
        /// at a gas hose with a diesel can TELLS you, and until now nothing in the world could put a
        /// diesel can in your hands to try it.</para>
        ///
        /// <para>Two sizes because capacity is the thing that differs between them in play: 20 L and
        /// 10 L cost different amounts to fill from the same hose at the same price.</para>
        /// </summary>
        public static readonly string[] LoanerCanDefIds =
        {
            "fuelstore.gas_jerry_s20",
            "fuelstore.gas_jerry_s10",
            "fuelstore.diesel_jerry_s20",
        };

        /// <summary>The row, laid out from its centre — pure, so a test can check the geometry without
        /// a scene.</summary>
        public static IReadOnlyList<StationFuelCans.Spot> Route91CanSpots()
        {
            var spots = new List<StationFuelCans.Spot>();
            int n = LoanerCanDefIds.Length;
            float first = -(n - 1) * 0.5f * LoanerCanSpacingMetres;

            for (int i = 0; i < n; i++)
            {
                var at = new Vector2(LoanerCanRowCentreLocal.x + first + i * LoanerCanSpacingMetres,
                                     LoanerCanRowCentreLocal.y);
                spots.Add(new StationFuelCans.Spot(
                    LoanerCanDefIds[i], at,
                    "on the forecourt apron in front of the C-store, where a station keeps the cans it " +
                    "lends you"));
            }
            return spots;
        }

        // =================================================================================================
        //  THE REGION'S OWN OBSTRUCTIONS — what makes the reach pass a WHOLE-SCENE one
        // =================================================================================================

        /// <summary>
        /// Is a body standing here inside something the REGION put in the way? The predicate the reach
        /// pass takes, and the whole difference between the bake's per-piece audit and this one.
        ///
        /// <para>Three things bite at these two sites, and all three are measured rather than assumed:</para>
        /// <list type="number">
        /// <item><description><b>⭐⭐ GROUND THAT FLOODS — and this is the one that justifies the pass.</b>
        /// The bake's audit asks "is this point inside a solid" and never "is there anything to stand
        /// ON": at apron level it assumes a floor everywhere, which on a wharf is false four metres from
        /// the pump. A pedestal set at the lip publishes a standing spot 1.06 m out over the basin — not
        /// inside any blocker, and still nowhere a body can be. <b>Found by a positive control rather
        /// than by reading the code:</b> with only the wall test below, a pump standing in mid-air
        /// PASSED. Asked of the AUTHORED TERRAIN against spring high water, so it is the ground the
        /// scene really carries and not a rectangle traced round a deck.</description></item>
        /// <item><description>The quay wall #609 laid along the apron's edge — a standing spot INSIDE the
        /// wall rather than out beyond it. From <see cref="NineMileCreekWalls.Runs"/>, the runs the scene
        /// actually carries, derived from the terrain rather than traced along an edge.</description></item>
        /// <item><description>The road corridors: a station on a highway can publish a standing spot in
        /// the carriageway.</description></item>
        /// </list>
        ///
        /// <para>Ground level only: a sales floor is inside a building, and the region's outdoor
        /// obstructions do not reach it.</para>
        /// </summary>
        public static System.Func<Vector2, StationReachAudit.Level, bool> SceneObstructions(
            ITidalTerrain terrain)
        {
            List<Vector2[]> walls = terrain != null
                ? NineMileCreekWalls.Runs(terrain)
                : new List<Vector2[]>();

            float wallHalf = PropertyBoundary.DefaultThicknessMetres * 0.5f
                           + StationReachAudit.BodyRadiusMetres;

            Vector2[][] roads =
            {
                NineMileCreekMainland.WharfRoad,
                NineMileCreekMainland.ThroughRoad,
                NineMileCreekMainland.BarRoad,
            };

            return (p, level) =>
            {
                if (level != StationReachAudit.Level.Ground) return false;

                // 1. Is there dry ground here at all? Spring HIGH, not mean: a standing spot that is
                //    only there for half the day is not a standing spot, and this region's ruled
                //    character is that its harbours bare.
                if (terrain != null &&
                    terrain.ElevationAt(p) < NineMileCreekMainland.SpringHighWater) return true;

                foreach (Vector2[] run in walls)
                    if (run != null && run.Length >= 2 &&
                        MainlandTidalTerrain.DistanceToPolyline(run, p) < wallHalf) return true;

                foreach (Vector2[] road in roads)
                    if (NineMileCreekMainland.DistanceToRoute(road, p) <
                        NineMileCreekMainland.RoadHalfWidth) return true;

                return false;
            };
        }

        // =================================================================================================
        //  PLACEMENT
        // =================================================================================================

        /// <summary>
        /// Stand both sites up. Returns how many pieces were placed across the two.
        ///
        /// <para><b>Re-runnable</b> — the root is rebuilt from scratch, so a second builder pass leaves
        /// one of everything.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain)
        {
            if (!StationCatalog.IsInstalled)
            {
                Debug.LogWarning(
                    $"{Tag} the gas-station kit is not installed (no StationPieceDefs under " +
                    $"{StationCatalog.DefFolder}), so Nine Mile Creek sells no fuel. The wharf Def and " +
                    "the Route 91 Def both exist and are priced; only the art is missing.");
                return 0;
            }

            FuelStationDef wharfDef = LoadStation(WharfStationId);
            FuelStationDef roadDef = LoadStation(RoadStationId);
            GameObject wallet = WalletProvider();

            var root = new GameObject(RootName);
            var sceneBlocks = SceneObstructions(terrain);

            StationForecourt.Result wharf = PlaceWharfPumps(root.transform, wharfDef, wallet, sceneBlocks);
            StationForecourt.Result road = PlaceRoute91(root.transform, roadDef, wallet, sceneBlocks);

            // The cans go down against the forecourt AS LAID OUT, so "is this spot inside a canopy post"
            // is asked of the same pieces the reach pass audited rather than of a second description of
            // them.
            StationFuelCans.Result cans = StationFuelCans.Place(
                root.transform, LoanerCansRootName,
                Route91ForecourtPos, StationCatalog.FacingForHeading(Route91RoadHeadingDegrees),
                Route91CanSpots(), Route91Layout().AsPlaced(), sceneBlocks, Tag);

            ReportSite("the wharf pumps", wharf, wharfDef, WharfPumpGrades);
            ReportSite("the Route 91 station", road, roadDef, Route91FaceGrades);
            ReportTheGaps(roadDef);

            return wharf.Count + road.Count + cans.Count;
        }

        /// <summary>Site A: two dock pedestals on the apron, each one hose.</summary>
        static StationForecourt.Result PlaceWharfPumps(
            Transform parent, FuelStationDef station, GameObject wallet,
            System.Func<Vector2, StationReachAudit.Level, bool> sceneBlocks)
        {
            IReadOnlyList<Vector2> at = WharfPumpPositions;

            // Expressed as a one-piece-deep layout so the pair shares the pass and the reporting with the
            // forecourt. Origin is the northern pedestal; the second is 4 m down the wall from it.
            var layout = new StationForecourt.Layout
            {
                Name = WharfRootName,
                Origin = at[0],
                RoadHeadingDegrees = ApronSeawardHeading,
            };

            for (int i = 0; i < at.Count; i++)
            {
                // The offsets are along the WALL, stated in the layout's own frame: its +y is seaward
                // (east), so its +x runs south. ⚠ The layout's own cell and the PIECES' cell are
                // deliberately different here — the layout is turned so its offsets run down the wall,
                // and each pedestal is turned so its HOSE looks over the water, which is a quarter turn
                // off it. See StationCatalog.FacingForLocalDirection for why those are two questions.
                layout.Add("dispenser_sDock", new Vector2(i * WharfPumpSpacingMetres, 0f), WharfPumpFacing,
                           i == 0
                               ? "the gas hose, four metres south of the brow — clear of the way down to " +
                                 "the float, on the stretch of wall the channel keeps wet at spring low"
                               : "the diesel hose beside it, two pump-reaches away so one offer never " +
                                 "hides the other");
            }

            StationForecourt.Result result =
                StationForecourt.Place(layout, WharfRootName, Tag, parent, sceneBlocks);

            // The pump goes on the ANCHOR the whole-scene pass verified, not on the pedestal — the same
            // seam Route 91 uses, so a hose is hung one way in this file rather than two.
            var pedestal = StationCatalog.Find("dispenser", "sDock");
            for (int i = 0; i < result.Placed.Count && i < WharfPumpGrades.Length; i++)
            {
                StationInteractable nozzle = FirstNozzleOnSide(pedestal, "a");
                GameObject anchor = nozzle == null ? null : result.Fitting(i, nozzle.Id);
                if (anchor == null) continue;

                anchor.name = $"{StationForecourt.FittingPrefix}hose_{WharfPumpGrades[i]}";
                Hose(anchor, station, wallet, WharfPumpGrades[i],
                     $"fixture.nmc.wharf_pump_{WharfPumpGrades[i]}");
            }

            return result;
        }

        /// <summary>Site B: the forecourt, and one hose per dispenser side.</summary>
        static StationForecourt.Result PlaceRoute91(
            Transform parent, FuelStationDef station, GameObject wallet,
            System.Func<Vector2, StationReachAudit.Level, bool> sceneBlocks)
        {
            StationForecourt.Result result =
                StationForecourt.Place(Route91Layout(), ForecourtRootName, Tag, parent, sceneBlocks);

            var def = StationCatalog.Find("dispenser", "sMpd");
            int face = 0, machine = 0;

            for (int i = 0; i < result.Placed.Count && def != null; i++)
            {
                GameObject go = result.Placed[i];
                if (go == null || go.name != "dispenser_sMpd") continue;
                machine++;

                // One hose per SIDE — see the class remarks for why not one per nozzle. The pump goes on
                // the ANCHOR the whole-scene pass verified, not on the machine: the resolver measures to
                // the component, and a body cannot stand inside a cabinet.
                foreach (string side in new[] { "a", "b" })
                {
                    if (face >= Route91FaceGrades.Length) break;

                    StationInteractable nozzle = FirstNozzleOnSide(def, side);
                    GameObject anchor = nozzle == null ? null : result.Fitting(i, nozzle.Id);
                    if (anchor == null) continue;

                    string grade = Route91FaceGrades[face++];
                    anchor.name = $"{StationForecourt.FittingPrefix}hose_{side}_{grade}";
                    Hose(anchor, station, wallet, grade,
                         $"fixture.route_91.pump{machine}{side}_{grade}");
                }
            }

            // ⭐ AND THE C-STORE OPENS. Its sales floor has been placed, drawn and furnished since
            // #626 and has never once been reachable: the shell's `building` blocker fills the whole
            // plan solid, so the room stood behind a wall with no door in it. This cuts the doorway the
            // sidecar already publishes and lets BuildingInterior swap shell for room at the threshold
            // — the owner's 2026-08-11 ruling that no placed building is a facade.
            StationInteriorPlacement.OpenAll(result, Tag);

            return result;
        }

        /// <summary>The first nozzle on a dispenser side — they share a standing spot, so the first is as
        /// good as any and the choice is stated rather than incidental.</summary>
        static StationInteractable FirstNozzleOnSide(StationPieceDef def, string side)
        {
            if (def?.Interactables == null) return null;
            foreach (StationInteractable it in def.Interactables)
                if (it != null && it.Action == "fuel" && it.Id != null && it.Id.StartsWith("nozzle_" + side))
                    return it;
            return null;
        }

        /// <summary>Hang one hose on a GameObject: a <see cref="FuelPump"/> pointed at a site, a grade and
        /// a wallet.</summary>
        static void Hose(GameObject go, FuelStationDef station, GameObject wallet, string grade, string id)
        {
            if (go == null) return;

            var pump = go.AddComponent<FuelPump>();
            pump.Configure(station, grade, id);
            if (wallet != null) SetRef(pump, "_walletProvider", wallet);

            if (station == null)
                Debug.LogWarning(
                    $"{Tag} '{id}' has no FuelStationDef, so it posts no price and every press refuses. " +
                    $"Expected one under {StationDefFolder}.");
        }

        // =================================================================================================
        //  WIRING HELPERS
        // =================================================================================================

        /// <summary>The site Def with this id, or null.</summary>
        public static FuelStationDef LoadStation(string id)
        {
            if (!AssetDatabase.IsValidFolder(StationDefFolder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:FuelStationDef", new[] { StationDefFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FuelStationDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && def.Id == id) return def;
            }
            return null;
        }

        /// <summary>
        /// The GameObject a pump charges through. The region already stands a
        /// <see cref="PersistentWalletProxy"/> for the same reason every other till here uses one — the
        /// player's purse lives in the persistent core, which is a different scene, and Unity does not
        /// serialize a reference across scenes at all. Found rather than made, so there is one.
        /// </summary>
        static GameObject WalletProvider()
        {
            // ⚠️ FindAnyObjectByType, not FindFirstObjectByType — 6000.5 marks the latter obsolete
            // (it relied on instance-id ordering), and this repo compiles obsolete-as-ERROR in CI, so
            // the deprecated call is a green build locally and a red one there. There is exactly one
            // wallet proxy in the scene, so "any" and "first" are the same object anyway.
            var proxy = Object.FindAnyObjectByType<PersistentWalletProxy>(FindObjectsInactive.Include);
            if (proxy != null) return proxy.gameObject;

            Debug.LogWarning(
                $"{Tag} no PersistentWalletProxy in the scene, so the pumps have no purse to charge and " +
                "every press refuses rather than pouring for free. Build the region's wharf economy " +
                "first — it is what stands the proxy up.");
            return null;
        }

        static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // =================================================================================================
        //  REPORTING
        // =================================================================================================

        static void ReportSite(string what, StationForecourt.Result r, FuelStationDef def, string[] grades)
        {
            if (r == null || r.Count == 0)
            {
                Debug.LogWarning($"{Tag} nothing placed at {what}.");
                return;
            }

            string missing = r.Missing.Count == 0 ? "" : $" ⚠ missing: {string.Join(", ", r.Missing)}.";
            string price = def == null
                ? " ⚠ NO SITE DEF — no prices posted."
                : $" Posting {string.Join(" · ", System.Array.ConvertAll(grades, g => $"{g} {def.PricePerLitre(g):0.00}"))}.";

            Debug.Log(
                $"{Tag} {what}: {r.Count} piece(s), {grades.Length} hose(s).{price}{missing} " +
                $"WHOLE-SCENE reach pass — {StationReachAudit.Report(r.Reach)}. " +
                "⚠ That pass is the one the kit's README says is owed: the bake's own audit is scoped to " +
                "ONE piece, so a dispenser was never told the island is under it, nor the quay wall " +
                "beside it.");

            if (r.Unreachable > 0)
                Debug.LogError(
                    $"{Tag} {r.Unreachable} fitting(s) at {what} have NOWHERE TO STAND. That is a siting " +
                    "fault, not a note: move the piece rather than shrinking the body.");
        }

        /// <summary>The honest list of what these two sites do NOT do yet. Logged every build, because a
        /// gap nobody is told about reads as a bug when the owner walks it.</summary>
        static void ReportTheGaps(FuelStationDef road)
        {
            var unhosed = new List<string>();
            if (road != null)
                foreach (string g in FuelGrades.All)
                    if (road.Sells(g) && System.Array.IndexOf(Route91FaceGrades, g) < 0) unhosed.Add(g);

            Debug.Log(
                $"{Tag} ⚠ WHAT IS AND IS NOT WIRED. (1) BOAT TANKS: WIRED — #618/#619/#620 landed the " +
                "tank, the Draw seam and BoatFuelTank, so FuelPump.TargetVessel() falls back from your " +
                "hands to the boat you are aboard and these hoses fill her. The dock pedestal stands " +
                "0.695 m in from the apron's lip, so a body at a hull's inboard rail is 0.69 m from the " +
                "hose — inside the pump's 2 m. From amidships on a big hull you must walk to the rail. " +
                "(2) A CAN's level is still session-local while the money is SAVED, " +
                "so buying fuel into a can leaks until that state persists. (3) The six things for sale inside the " +
                "C-store are placed and measured but have NO VERB — the interact actions the sidecar " +
                "names (buy · drinks · coffee · food · cash · browse) do not exist, which #613 recorded " +
                "and the economy lane owns. (4) A multi-product machine's three nozzles share one " +
                "standing spot, so a side sells ONE grade" +
                (unhosed.Count == 0
                    ? "."
                    : $", and Route 91 posts {string.Join(" and ", unhosed)} with no hose to draw it — " +
                      "the committed art is baked for three grades and a fourth is a re-bake, which is " +
                      "the owner's open grade-count call."));
        }
    }
}
#endif
