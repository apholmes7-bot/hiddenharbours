#if UNITY_EDITOR
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE LAYDOWN — one of each of the road fleet, stood off the park spur.</b> The owner's ask,
    /// in his words: <i>"one of each parked at NMC — the truck park cannot hold them — a laydown
    /// proposal off the spur, walk-gated; all trailers ready to tow; a hitch (couple) affordance when a
    /// semi is placed right."</i>
    ///
    /// <para><b>ONE constant, everything derived</b> — the discipline
    /// <see cref="NineMileCreekMainland.TruckParkPos"/> established and the Otter's landing repeated.
    /// <see cref="NineMileCreekMainland.LaydownPos"/> sites the yard; the apron, every bay, every
    /// heading, the spur and all nine placements fall out of it. The owner's walk verdict moves that
    /// one Vector3 and the whole yard follows, which is what makes the site a PROPOSAL rather than a
    /// decision taken on his behalf.</para>
    ///
    /// <para><b>Places, does NOT draw</b> — the moorage law. The mesh path is runtime-owned, so every
    /// machine here skins herself at play (memory <c>mesh-hulls-must-skin-at-runtime</c>).</para>
    ///
    /// <para><b>Geometry is solved without touching the scene.</b> <see cref="Solve"/> returns where
    /// every machine stands and which way she points, reading the MEASURED envelopes off the baked
    /// defs — never a transcribed LOA. <see cref="Place"/> then does nothing but build GameObjects
    /// from that answer, so the whole yard is testable in EditMode without instantiating anything.</para>
    ///
    /// <para>⚠️⚠️ <b>THE YARD FACES SOUTH, AND THAT IS NOT A TASTE DECISION.</b>
    /// <see cref="VehicleCouplingMath.BodyOriginFromKingpin"/> and <c>TowedBody.KingpinWorld</c> rotate
    /// a local offset COUNTER-clockwise by the heading, while the transform frame every other reader
    /// uses — <c>VehicleHitch.HeadingDegrees</c>, <c>VehicleMeshDriver.CurrentDirUnits</c> and
    /// <c>BoatKinematics.BearingDegrees(transform.up)</c> — is CLOCKWISE-positive compass. The two
    /// agree only where <c>sin(heading) = 0</c>, i.e. due north and due south. Every coupling fixture
    /// that ships builds its tractor heading north, so nothing has ever measured the difference. On
    /// <see cref="YardHeadingDegrees"/> = 180° the pair's pin lands within a micrometre of the plate;
    /// turn this yard 30° and the couple would silently stop being offered. <b>Do not rotate the yard
    /// off the north–south axis until that mirror is reconciled</b> — <c>NineMileCreekLaydownTests</c>
    /// pins the couple offer at the heading actually placed, so a rotation reddens rather than
    /// disappears.</para>
    /// </summary>
    public static class NineMileCreekLaydown
    {
        // -------------------------------------------------------------------------------------------
        //  THE ENVELOPES — declared here, bound to the shipped defs by a test
        // -------------------------------------------------------------------------------------------
        // Stated as envelopes rather than read off the defs for the reason NineMileCreekRoads states
        // its own: the apron is region geometry and must be answerable without loading vehicle content
        // (NineMileCreekRoads.Pads() paves it). The binding in the other direction is a test's job —
        // NineMileCreekLaydownTests re-measures every one of these off the baked assets and fails if a
        // machine outgrows her ground, so a future longer trailer hangs a red rather than a tail.

        /// <summary>The widest machine in the pack, metres — 2.54 m over the semis' mirrors, rounded
        /// up. The trailers are 2.44 and the vans narrower still.</summary>
        public const float WidestUnitMetres = 2.6f;

        /// <summary>The longest single body, metres — the 53-ft reefer at 16.43 m, rounded up. Not what
        /// sizes a bay: the coupled pair is longer, and every bay is cut to the deepest occupant so the
        /// apron is one rectangle rather than a comb.</summary>
        public const float LongestUnitMetres = 16.5f;

        /// <summary>
        /// The longest thing that stands in a single bay, metres: the coupled pair, nose of tractor to
        /// tail of trailer, at 22.08 m — rounded up.
        ///
        /// <para>Derived, not guessed: the tractor's nose is 4.12 m ahead of her origin, her plate
        /// 2.4 m behind it, the 53-ft trailer's pin 7.175 m ahead of HER origin and her tail 8.075 m
        /// behind it — 21.77 m nose to tail with the pin ON THE SEAT.</para>
        ///
        /// <para>⚠️ <b>But she is not parked on the seat, and that 0.31 m is the whole reason this
        /// number is not 21.8.</b> <see cref="CoupleReadyPinWorld"/> stands her pin at the MIDDLE of the
        /// capture window rather than on its fore boundary, which puts her 0.31 m further aft and the
        /// pair at 22.08 m. Sizing the bay off the seat arithmetic left the yard correct only because
        /// the bay happened to carry 1.2 m of slack — a declared envelope that disagrees with the
        /// placement by less than its own slack is a envelope that is not being checked. The test now
        /// measures the SOLVED extent instead, so the two cannot drift again.</para>
        /// </summary>
        public const float LongestPairMetres = 22.2f;

        /// <summary>
        /// ⭐ The access lane's width, metres — <b>one full-lock turn radius of the worst machine in the
        /// pack</b>, which is the classic semi at 10.626 m (wheelbase 5.65, Ackermann pair 30°/26.23°).
        ///
        /// <para>That is a geometric statement, not a margin picked to feel safe: a 90° turn at full
        /// lock displaces the rear axle exactly one radius sideways, so a lane one radius wide is a
        /// lane the worst machine can swing square into her bay from in ONE movement. Narrower and she
        /// shunts — a yard you fight, and the village would have bulldozed the extra metre.</para>
        ///
        /// <para>Derive, never transcribe — the test computes every road machine's
        /// <c>FullLockTurnRadiusMeters</c> off her own baked def and fails if any exceeds this.</para>
        /// </summary>
        public const float LaneWidthMetres = 10.7f;

        /// <summary>Clearance each side of a machine, metres: a person walks between two parked units
        /// and opens a door. The truck park measured a front door's swept arc at 2.07 m from a 1.36 m
        /// half-width centreline — 0.71 m of swing beyond the body — so 1.5 m leaves a fully-open door
        /// and most of a metre to get past it.</summary>
        public const float SlotSideClearanceMetres = 1.5f;

        /// <summary>How far a machine's nose sits back from the lane edge, metres, and the same
        /// clearance again behind her tail — so nothing overhangs the lane and nothing touches the back
        /// fence.</summary>
        public const float NoseSetbackMetres = 1.5f;

        /// <summary>How many bays. Eight, for nine machines: the semi and her trailer share one, which
        /// is the point of the pair.</summary>
        public const int BayCount = 8;

        /// <summary>One bay's width, metres — the widest machine plus a clearance each side.</summary>
        public static float BayWidthMetres => WidestUnitMetres + 2f * SlotSideClearanceMetres;

        /// <summary>One bay's depth, metres — the deepest occupant with a setback at each end.</summary>
        public static float BayDepthMetres => LongestPairMetres + 2f * NoseSetbackMetres;

        /// <summary>Which way every machine in the yard points: due south, onto the lane.
        /// ⚠️ See the class note — this being 0 or 180 is load-bearing, not cosmetic.</summary>
        public const float YardHeadingDegrees = 180f;

        // -------------------------------------------------------------------------------------------
        //  THE GROUND
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// The apron: <b>eight bays across, and one lane plus one bay deep</b>, centred on
        /// <see cref="NineMileCreekMainland.LaydownPos"/>. 44.8 × 35.5 m.
        ///
        /// <para>Every number is the fleet's, not the region's — bay width is the widest machine plus
        /// her door swing, bay depth is the coupled pair, and the lane is the worst turning circle. A
        /// yard sized any other way would be a number somebody chose.</para>
        ///
        /// <para>⚠ Unclipped by any fill, like the truck park and unlike the buyers' gravel: this is
        /// the mainland plateau at <see cref="NineMileCreekMainland.LandElevation"/>, where there is
        /// nothing to spill off. If the owner walks the yard onto softer ground the dry-ground rule in
        /// <c>NineMileCreekRoads.Pave</c> trims it and the test fails on the first trimmed cell.</para>
        /// </summary>
        public static Rect ApronArea()
        {
            var centre = new Vector2(NineMileCreekMainland.LaydownPos.x,
                                     NineMileCreekMainland.LaydownPos.y);
            float halfWidth = BayCount * BayWidthMetres * 0.5f;
            float halfDepth = (LaneWidthMetres + BayDepthMetres) * 0.5f;

            return Rect.MinMaxRect(centre.x - halfWidth, centre.y - halfDepth,
                                   centre.x + halfWidth, centre.y + halfDepth);
        }

        /// <summary>
        /// The spur: from the truck park's centre to the laydown's. <b>Off the spur, which is what was
        /// asked</b> — the park spur already reaches the park from Wharf Road, so this continues the
        /// same gravel one ground further rather than cutting a second approach off the highway.
        ///
        /// <para>Runs centre to centre for the reason <c>NineMileCreekRoads.ParkSpurRoute</c> does:
        /// gravel under gravel is not wrong, both pads outrank the spur, and a route to the centre
        /// meets each pad whichever way the owner's walk moves either one.</para>
        /// </summary>
        public static Vector2[] LaydownSpurRoute()
        {
            var park = new Vector2(NineMileCreekMainland.TruckParkPos.x,
                                   NineMileCreekMainland.TruckParkPos.y);
            var yard = new Vector2(NineMileCreekMainland.LaydownPos.x,
                                   NineMileCreekMainland.LaydownPos.y);
            return new[] { park, yard };
        }

        /// <summary>The world Y every machine's nose stands on — the lane's north edge, set back.</summary>
        public static float NoseLineY() => ApronArea().yMin + LaneWidthMetres + NoseSetbackMetres;

        /// <summary>The world X down the middle of bay <paramref name="index"/>, counted west to
        /// east.</summary>
        public static float BayCentreX(int index) =>
            ApronArea().xMin + (index + 0.5f) * BayWidthMetres;

        /// <summary>Bay <paramref name="index"/>'s own ground — the rectangle nothing else may
        /// claim.</summary>
        public static Rect BayArea(int index)
        {
            Rect apron = ApronArea();
            float x = apron.xMin + index * BayWidthMetres;
            float y = apron.yMin + LaneWidthMetres;
            return Rect.MinMaxRect(x, y, x + BayWidthMetres, y + BayDepthMetres);
        }

        /// <summary>The lane — the apron's south strip, which no machine may stand on.</summary>
        public static Rect LaneArea()
        {
            Rect apron = ApronArea();
            return Rect.MinMaxRect(apron.xMin, apron.yMin, apron.xMax, apron.yMin + LaneWidthMetres);
        }

        // -------------------------------------------------------------------------------------------
        //  THE NINE
        // -------------------------------------------------------------------------------------------

        public const string DefDir = "Assets/_Project/Data/Vehicles/";
        public const string MeshDir = "Assets/_Project/Data/Vehicles/Meshes/";

        /// <summary>What stands in a bay: a driven machine carries a <c>VehicleDef</c>, a towed body
        /// carries only her mesh — PR 2's deliberate omission, standing.</summary>
        public readonly struct Unit
        {
            /// <summary>The placed object's name, so a rebuild finds and replaces it.</summary>
            public readonly string Name;

            /// <summary>Repo path of her <c>VehicleDef</c>, or null when she is a towed body.</summary>
            public readonly string DefPath;

            /// <summary>Repo path of her <c>VehicleMeshDef</c> — every machine has one.</summary>
            public readonly string MeshPath;

            /// <summary>Which bay she stands in, counted west to east.</summary>
            public readonly int Bay;

            /// <summary>True when she is the trailer of the coupling pair, seated on the plate of the
            /// tractor ahead of her rather than standing at the bay's own nose line.</summary>
            public readonly bool SeatsOnTheTractorAhead;

            public Unit(string name, string defPath, string meshPath, int bay,
                        bool seatsOnTheTractorAhead = false)
            {
                Name = name;
                DefPath = defPath;
                MeshPath = meshPath;
                Bay = bay;
                SeatsOnTheTractorAhead = seatsOnTheTractorAhead;
            }

            /// <summary>She has no <c>VehicleDef</c> and never will — every field on one is a driven
            /// machine's.</summary>
            public bool IsTowed => DefPath == null;
        }

        /// <summary>
        /// ⭐ <b>The nine, west to east.</b> Five driven machines and four towed bodies — one of each
        /// the drop shipped, which is the whole ask.
        ///
        /// <para><b>The pair is bay 0</b>, nearest the spur: it is the yard's centrepiece and the first
        /// thing the owner meets walking up from the park, which is where the judgment he is being
        /// asked for wants to happen.</para>
        /// </summary>
        public static readonly Unit[] Units =
        {
            // Bay 0 — THE PAIR: the aero semi backed under a 53-ft flatbed, pin in the slot.
            new Unit("AeroSemiAtTheLaydown", DefDir + "AeroSemi.asset",
                     MeshDir + "AeroSemiVehicleMesh.asset", 0),
            new Unit("Flatbed53AtTheLaydown", null,
                     MeshDir + "TrailerFlatbed53VehicleMesh.asset", 0, seatsOnTheTractorAhead: true),

            // Bays 1–4 — the rest of the driven fleet.
            new Unit("ClassicSemiAtTheLaydown", DefDir + "ClassicSemi.asset",
                     MeshDir + "ClassicSemiVehicleMesh.asset", 1),
            new Unit("ConvBoxAtTheLaydown", DefDir + "ConvBox.asset",
                     MeshDir + "ConvBoxVehicleMesh.asset", 2),
            new Unit("CaboverBoxAtTheLaydown", DefDir + "CaboverBox.asset",
                     MeshDir + "CaboverBoxVehicleMesh.asset", 3),
            new Unit("HightopVanAtTheLaydown", DefDir + "HightopVan.asset",
                     MeshDir + "HightopVanVehicleMesh.asset", 4),

            // Bays 5–7 — the towed bodies, on their own legs, ready to tow.
            new Unit("Reefer53AtTheLaydown", null,
                     MeshDir + "TrailerReefer53VehicleMesh.asset", 5),
            new Unit("Flatbed28AtTheLaydown", null,
                     MeshDir + "TrailerFlatbed28VehicleMesh.asset", 6),
            new Unit("Reefer28AtTheLaydown", null,
                     MeshDir + "TrailerReefer28VehicleMesh.asset", 7),
        };

        /// <summary>Where one machine stands and which way she points — the whole answer, with no
        /// GameObject in it.</summary>
        public readonly struct Placement
        {
            public readonly Unit Unit;
            public readonly Vector2 Position;
            public readonly float HeadingDegrees;
            public readonly VehicleMeshDef Mesh;
            public readonly VehicleDef Def;

            public Placement(Unit unit, Vector2 position, float headingDegrees,
                             VehicleMeshDef mesh, VehicleDef def)
            {
                Unit = unit;
                Position = position;
                HeadingDegrees = headingDegrees;
                Mesh = mesh;
                Def = def;
            }

            /// <summary>Her drawn rotation. z = −bearing, the exact inverse of
            /// <c>BoatKinematics.BearingDegrees(transform.up)</c> — which is what the mesh driver and
            /// the hitch both read her heading back out of.</summary>
            public Quaternion Rotation => Quaternion.Euler(0f, 0f, -HeadingDegrees);
        }

        /// <summary>
        /// ⭐ <b>Solve the yard</b> — where all nine stand, from the one constant and their own measured
        /// envelopes. No scene and no GameObjects, so a test can move
        /// <see cref="NineMileCreekMainland.LaydownPos"/> and watch the whole yard follow.
        ///
        /// <para>Every machine is placed by her NOSE on the bay's nose line and her body derived back
        /// from it off her published collider box — so a longer trailer grows northward into her own
        /// bay rather than out into the lane, and nothing here transcribes a length.</para>
        ///
        /// <para>Returns what it could solve, warning about what it could not: a region built before
        /// the vehicle bake gets an empty yard, stated rather than silent.</para>
        /// </summary>
        public static List<Placement> Solve()
        {
            var placements = new List<Placement>(Units.Length);
            float noseLine = NoseLineY();

            // The tractors first: a trailer that seats on one needs that plate already placed.
            var tractorByBay = new Dictionary<int, Placement>();

            for (int i = 0; i < Units.Length; i++)
            {
                Unit unit = Units[i];
                if (unit.SeatsOnTheTractorAhead) continue;

                var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(unit.MeshPath);
                if (mesh == null)
                {
                    Debug.LogWarning($"[NineMileCreekLaydown] No mesh at {unit.MeshPath} — {unit.Name} " +
                                     "is left out of the yard. Bake the road fleet before the region.");
                    continue;
                }

                VehicleDef def = null;
                if (!unit.IsTowed)
                {
                    def = AssetDatabase.LoadAssetAtPath<VehicleDef>(unit.DefPath);
                    if (def == null)
                    {
                        Debug.LogWarning($"[NineMileCreekLaydown] No def at {unit.DefPath} — " +
                                         $"{unit.Name} is left out of the yard.");
                        continue;
                    }
                }

                // Her nose on the bay's nose line, her origin derived back from it. Facing south, a
                // local +Y offset lands SOUTH of the origin, so the origin sits her nose-depth north.
                var position = new Vector2(BayCentreX(unit.Bay),
                                           noseLine + mesh.ColliderMaxMeters.y);

                var placement = new Placement(unit, position, YardHeadingDegrees, mesh, def);
                placements.Add(placement);
                if (mesh.CanTow) tractorByBay[unit.Bay] = placement;
            }

            // Then the trailer of the pair, seated on the plate of the tractor in her bay.
            for (int i = 0; i < Units.Length; i++)
            {
                Unit unit = Units[i];
                if (!unit.SeatsOnTheTractorAhead) continue;

                var mesh = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(unit.MeshPath);
                if (mesh == null)
                {
                    Debug.LogWarning($"[NineMileCreekLaydown] No mesh at {unit.MeshPath} — the pair " +
                                     "stands without her trailer.");
                    continue;
                }

                if (!tractorByBay.TryGetValue(unit.Bay, out Placement tractor))
                {
                    Debug.LogWarning($"[NineMileCreekLaydown] {unit.Name} seats on a tractor in bay " +
                                     $"{unit.Bay} and none was placed there — she is left out.");
                    continue;
                }

                Vector2 pinWorld = CoupleReadyPinWorld(tractor);
                Vector2 origin = VehicleCouplingMath.BodyOriginFromKingpin(
                    pinWorld, YardHeadingDegrees, mesh.Kingpin);

                placements.Add(new Placement(unit, origin, YardHeadingDegrees, mesh, null));
            }

            return placements;
        }

        /// <summary>
        /// ⭐ <b>Where a trailer's pin must be for the couple to be OFFERED</b> — the middle of this
        /// tractor's own capture window, in the world.
        ///
        /// <para><b>Not the seat.</b> <see cref="VehicleCouplingMath.IsCaptured"/> tests a window
        /// running from the ramp mouth to the slot seat, widened each way by the pin's own 45 mm
        /// radius — and the SEAT is that window's fore boundary. A trailer parked exactly on it is a
        /// trailer whose couple offer turns on the last bit of a float, which is the failure the
        /// coupling code documents having already had once. The midpoint stands 0.355 m clear of both
        /// ends instead.</para>
        ///
        /// <para>The pin's radius is left out of the midpoint deliberately: <c>IsCaptured</c> widens
        /// the window by the same amount at BOTH ends, so widening moves the two boundaries apart
        /// without moving their middle. Reading it here would need the trailer's def to answer a
        /// question about the tractor's plate.</para>
        /// </summary>
        public static Vector2 CoupleReadyPinWorld(in Placement tractor)
        {
            VehicleFifthWheel wheel = tractor.Mesh.FifthWheel;

            float aft = Mathf.Min(wheel.RampMouthY, wheel.SlotSeatY);
            float fore = Mathf.Max(wheel.RampMouthY, wheel.SlotSeatY);
            var local = new Vector2(wheel.CouplingPointLocal.x, (aft + fore) * 0.5f);

            return tractor.Position + Rotate(local, tractor.HeadingDegrees);
        }

        /// <summary>A local offset in the WORLD, on a compass heading — the transform's own convention
        /// (z = −bearing), which is the frame the hitch and the mesh driver read.</summary>
        static Vector2 Rotate(Vector2 local, float headingDegrees)
        {
            float rad = -headingDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
            return new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
        }

        /// <summary>
        /// Stand the nine in the yard. Returns what was placed — empty when the bake is not on disk.
        ///
        /// <para>Places and does NOT draw: a driven machine gets a <see cref="ParkedVehicle"/> carrying
        /// her def, a towed body a <see cref="ParkedTrailer"/> carrying her mesh, and each skins herself
        /// on enable at play.</para>
        /// </summary>
        public static List<GameObject> Place()
        {
            var made = new List<GameObject>();

            foreach (Placement p in Solve())
            {
                // A towed body carries no Rigidbody2D: she is placed and pulled, never simulated on her
                // own — the shape the coupling fixtures build her in.
                GameObject go = p.Unit.IsTowed
                    ? new GameObject(p.Unit.Name)
                    : new GameObject(p.Unit.Name, typeof(Rigidbody2D));

                go.transform.position = new Vector3(p.Position.x, p.Position.y, 0f);
                go.transform.rotation = p.Rotation;

                if (p.Unit.IsTowed)
                {
                    go.AddComponent<ParkedTrailer>().Configure(p.Mesh);
                }
                else
                {
                    // Serialized state carries gravityScale 0 too, though VehicleController.Awake
                    // re-zeroes it at play — a truck must not fall south through a top-down world.
                    go.GetComponent<Rigidbody2D>().gravityScale = 0f;
                    go.AddComponent<ParkedVehicle>().Configure(p.Def, drivable: true);
                }

                made.Add(go);
            }

            return made;
        }
    }
}
#endif
