#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's numbers
using HiddenHarbours.Art.Editor;          // InteriorCatalog, InteriorKit
using HiddenHarbours.World;               // BuildingInterior, InteriorFootprint

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE INSIDES</b> — stands a baked room under a placed village building and furnishes it, so the
    /// door on the front of the cottage opens onto a room you can walk around in. The owner's
    /// 2026-07-30 ruling made interiors SEAMLESS (no scene load, no separate screen) and TRUE TO THE
    /// FOOTPRINT (a cottage interior is cottage-sized, because felt size IS the progression fantasy);
    /// this is the placement pass that makes the first one real.
    ///
    /// <para><b>Data-driven, so the next building is a table row.</b> Nothing here names a building:
    /// <see cref="Stand"/> asks <see cref="InteriorCatalog"/> whether a room has been baked under the
    /// building's own key and does nothing at all if not. Baking a second room and giving it the key of
    /// the building it belongs inside is the whole of "make the school enterable".</para>
    ///
    /// <para><b>⚠️ The room is NOT shown at the building's facing.</b> The exterior rigs put their door
    /// on the <c>+Y</c> gable and the room rig puts its doorway on <c>−Y</c> (the wall the cutaway
    /// drops), so the same cell index shows the two 180° apart. The bake MEASURES the offset that lines
    /// their door anchors up at all eight facings and writes it into the contract;
    /// <see cref="InteriorCatalog.InteriorFacingFor"/> is the only thing that applies it. Get this wrong
    /// and the player walks in the front door and appears at the BACK of the room — and it reads as an
    /// art bug, not a placement one.</para>
    ///
    /// <para><b>⚠️ The world XY plane is the SQUASHED ground plane.</b> One metre of northward ground
    /// travel draws <c>sin 40° ≈ 0.643</c> world units up the screen
    /// (<see cref="SpriteLightMath.GroundDepthScale"/>). Every collider here therefore comes out of
    /// <see cref="InteriorFootprint"/>, which does that squash — and which is why the walls are
    /// <see cref="PolygonCollider2D"/> quads and not rotated boxes: a rotated footprint is a
    /// parallelogram, and a box collider cannot express a shear.</para>
    /// </summary>
    public static class StPetersInteriors
    {
        /// <summary>The child object name of a room's sprite under its building.</summary>
        public const string RoomChildName = "Interior";

        /// <summary>The child object name of the furniture root.</summary>
        public const string PropsChildName = "Furniture";

        /// <summary>The child object name of the wall colliders.</summary>
        public const string WallsChildName = "Walls";

        /// <summary>The child object name of the UPPER storey's room sprite.</summary>
        public const string UpperRoomChildName = "InteriorUpper";

        /// <summary>The child object name of the upper storey's furniture root.</summary>
        public const string UpperPropsChildName = "FurnitureUpper";

        /// <summary>The child object name of the colliders that exist only upstairs — the partitions
        /// and the plug that closes the front doorway. The building's own walls are NOT in here.</summary>
        public const string UpperWallsChildName = "WallsUpper";

        /// <summary>
        /// Wall thickness (m) the colliders are built with and the inside test uses.
        ///
        /// <para>Thicker than the rig's drawn 0.16 m wall on purpose: at the sprint speed
        /// (5.5 m/s) and a 0.02 s fixed step the player moves 0.11 m per step, so a 0.16 m wall is
        /// inside a factor of 1.5 of tunnelling and a fast diagonal into a corner is exactly the
        /// input that finds it. 0.3 m costs a hand's width of floor and cannot be walked through.</para>
        /// </summary>
        public const float WallThicknessMetres = 0.3f;

        /// <summary>The gap left in the front wall (m) — wider than the drawn 1.05 m opening, because
        /// the player has width of their own and a threshold you have to line up on to the pixel is not
        /// cozy.</summary>
        public const float DoorwayWidthMetres = 1.4f;

        // =====================================================================================
        //  THE FURNISHING (authored — the identity a hash could not have produced)
        // =====================================================================================

        /// <summary>
        /// One piece of furniture: which prop, where it stands in the ROOM's own frame (metres,
        /// <c>+x</c> right across the room, <c>+y</c> toward the back/hearth wall, origin the floor
        /// centre), and how many facings it is turned relative to the room.
        /// </summary>
        public readonly struct Furnishing
        {
            public readonly string PropKey;
            public readonly Vector2 RoomMetres;
            public readonly int FacingOffset;
            public readonly string Why;

            /// <summary>What this piece of furniture also IS, if anything — a bed you can turn in at,
            /// the stairwell, the wardrobe. <see cref="InteriorFixture.None"/> for the great majority,
            /// which are scenery with a collider and nothing more.</summary>
            public readonly InteriorFixture Fixture;

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why)
                : this(propKey, roomMetres, facingOffset, why, InteriorFixture.None)
            {
            }

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why,
                              InteriorFixture fixture)
            {
                PropKey = propKey; RoomMetres = roomMetres; FacingOffset = facingOffset; Why = why;
                Fixture = fixture;
            }
        }

        /// <summary>
        /// What a placed prop can additionally BE. The prop is still an ordinary sprite with an ordinary
        /// collider; this only decides which behaviour rides along on it.
        ///
        /// <para>One enum rather than five parallel placement tables, because the alternative was five
        /// lists that all had to agree about the room's coordinate frame — and the first one to disagree
        /// would put a bed you can sleep in half a metre from the bed you can see.</para>
        /// </summary>
        public enum InteriorFixture
        {
            /// <summary>Scenery. The overwhelming default.</summary>
            None = 0,
            /// <summary>The foot of the stairs, on the lower storey: press to go up.</summary>
            StairUp,
            /// <summary>The head of the stairs, on the upper storey: press to come down.</summary>
            StairDown,
            /// <summary>The player's own bed — the one that offers to keep the day.</summary>
            PlayerBed,
            /// <summary>Somebody else's bed. Interacting is a polite refusal, never a save.</summary>
            OwnerBed,
            /// <summary>The wardrobe: raises the customization signal and says there is nothing to
            /// change into yet.</summary>
            Wardrobe,
        }

        /// <summary>
        /// A rectangle of extra WALL, in the room's own model frame (metres), that exists only on an
        /// upper storey — the partition between two bedrooms.
        ///
        /// <para>Model-frame numbers rather than world ones for the same reason every furnishing is: the
        /// room is drawn at a facing the builder chooses and the owner reads these against the picture,
        /// where <c>y</c> runs toward the hearth. <see cref="InteriorFootprint.Quad"/> does the rotation
        /// and the squash.</para>
        /// </summary>
        public readonly struct WallRect
        {
            public readonly float X0, X1, Y0, Y1;
            public readonly string Why;

            public WallRect(float x0, float x1, float y0, float y1, string why)
            {
                X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; Why = why;
            }
        }

        /// <summary>
        /// <b>A building's storey above</b>, as data: what furnishes it, what extra walls divide it, and
        /// what the lower storey gains so you can get up there.
        ///
        /// <para><b>⚠ Why this is keyed separately from the room key, and not off it.</b> Ginny's
        /// cottage and the village's pilot cottage are the SAME build — both are
        /// <c>sageCottage</c>, deliberately (see <c>StPetersGinnyPlot.CottageKey</c>). Hanging the
        /// upstairs off the room key would therefore have given the village cottage an upstairs too:
        /// two beds, a wardrobe and a stairwell inside a house nobody lodges in, with the player able to
        /// save in a stranger's bedroom. The plan is asked for BY NAME by the site that wants one, and
        /// every other building on the island is untouched by construction.</para>
        /// </summary>
        public readonly struct UpperLevelPlan
        {
            /// <summary>Which baked room the upper storey draws. Empty = the same room sheet as the
            /// storey below, which is what the greybox uses: the footprint is identical, so the floor
            /// and far walls are already the right size and shape.</summary>
            public readonly string RoomKey;

            /// <summary>Furniture added to the storey BELOW — in practice just the foot of the stairs,
            /// which has to exist downstairs or there is no way up.</summary>
            public readonly IReadOnlyList<Furnishing> GroundAdditions;

            /// <summary>What furnishes the storey above.</summary>
            public readonly IReadOnlyList<Furnishing> Furnishings;

            /// <summary>Walls that exist only up here — the partition. The building's OWN walls are not
            /// listed and are not rebuilt: they are the same walls on both storeys.</summary>
            public readonly IReadOnlyList<WallRect> Partitions;

            public UpperLevelPlan(string roomKey,
                                  IReadOnlyList<Furnishing> groundAdditions,
                                  IReadOnlyList<Furnishing> furnishings,
                                  IReadOnlyList<WallRect> partitions)
            {
                RoomKey = roomKey ?? "";
                GroundAdditions = groundAdditions;
                Furnishings = furnishings;
                Partitions = partitions;
            }
        }

        /// <summary>The plan key Ginny's cottage asks for. Named after the SITE, not the build, because
        /// the build (<c>sageCottage</c>) is shared with the village's pilot cottage and only one of the
        /// two has anyone living upstairs.</summary>
        public const string GinnyCottagePlanKey = "ginnyCottage";

        /// <summary>
        /// <b>The storey above, per site.</b> Returns an empty plan for everything not listed, which is
        /// every building on the island but one.
        ///
        /// <para><b>The cottage's numbers, so the geometry below can be read.</b> <c>sageCottage</c> is
        /// 6.6 × 8.05 m, so the model frame runs x ±3.3 and y ±4.025, and the 0.3 m walls leave a floor
        /// of x ∈ [−3.0, 3.0], y ∈ [−3.725, 3.725]. <c>y</c> runs toward the hearth (the top of the
        /// room picture); the front door is the gap at the bottom, centred on x = 0.</para>
        ///
        /// <para><b>The plan, in words.</b> A landing runs up the right-hand side, wide enough to walk
        /// two abreast past the stair head. A north–south partition closes it off from the two bedrooms,
        /// with a door into each; an east–west partition divides those two from one another. The result
        /// is that <b>neither bedroom is a corridor to the other</b> — you reach both from the landing,
        /// and Ginny does not walk through the room she is lending you. That cost 1.8 m of floor and is
        /// the single thing about this layout worth defending.</para>
        ///
        /// <para><b>Where the stairwell had to go, and why it is not prettier.</b> It stands at
        /// (2.50, −0.90) on BOTH storeys — that is what makes the level swap need no teleport — so it
        /// has to be clear of the ground floor's shipped furniture as well as of the landing. The
        /// ground floor is already furnished (the dresser at (2.75, 0.50), the table and its two chairs
        /// across the middle) and that furniture is the VILLAGE cottage's too, so it cannot be moved to
        /// suit this one. (2.50, −0.90) is the gap that leaves: hard against the right wall, a clear
        /// 0.68 m south of the dresser, and well outside the doorway lane.</para>
        ///
        /// <para><b>⚠ Greybox, and it looks like one.</b> The upper storey draws the SAME baked room
        /// sheet as the one below, because the footprint is identical and the floor and far walls are
        /// therefore already the right size — but that sheet has a front door drawn in it, and upstairs
        /// that door goes nowhere. It is plugged solid (see <see cref="InteriorFootprint.DoorwayPlugQuad"/>)
        /// so you cannot walk out of it, but it is still DRAWN. That is the one honest ugliness here and
        /// it is the art lane's to fix: an <c>upstairs</c> room bake, named in <see cref="UpperLevelPlan.RoomKey"/>,
        /// replaces it without touching a line of this file.</para>
        /// </summary>
        public static UpperLevelPlan UpperLevelFor(string planKey) => planKey switch
        {
            GinnyCottagePlanKey => new UpperLevelPlan(
                // Empty = draw the storey below's own room sheet. See the greybox note above.
                roomKey: "",

                // ---- what the GROUND floor gains: the foot of the stairs ---------------------------
                groundAdditions: new[]
                {
                    new Furnishing("seaChest", new Vector2(2.50f, -0.90f), 0,
                                   "the foot of the stairs, against the right wall in the gap between " +
                                   "the dresser and the chairs — a sea chest STANDING IN for the stair " +
                                   "art, because it is the only prop in the kit you would plausibly " +
                                   "step up onto, and a bare interact point would be a way upstairs " +
                                   "with nothing to see",
                                   InteriorFixture.StairUp),
                },

                // ---- the storey above ---------------------------------------------------------------
                furnishings: new[]
                {
                    new Furnishing("seaChest", new Vector2(2.50f, -0.90f), 0,
                                   "the head of the same stairs — the SAME model coordinate as the foot, " +
                                   "which is precisely why coming up needs no teleport",
                                   InteriorFixture.StairDown),

                    new Furnishing("bed", new Vector2(-2.10f, -2.30f), 0,
                                   "THE PLAYER'S BED, in the front room: along the left wall, the far " +
                                   "corner from the landing door, which is where a bed goes in a room " +
                                   "this size — and is the arrangement the ground floor already uses",
                                   InteriorFixture.PlayerBed),

                    new Furnishing("dresser", new Vector2(0.35f, -0.60f), 2,
                                   "the wardrobe, STANDING IN as a dresser (the kit has no wardrobe " +
                                   "yet). Turned a quarter so its back is to the landing partition — " +
                                   "against the one wall in the player's room that is not the outside " +
                                   "of the house, and across the room from the bed so both are " +
                                   "reachable without threading between them",
                                   InteriorFixture.Wardrobe),

                    new Furnishing("bed", new Vector2(-2.10f, 2.30f), 0,
                                   "GINNY'S BED, in the back room over the hearth — the warm end of the " +
                                   "house, which is the host's, and the same left-wall line as the " +
                                   "player's so the two rooms read as one plan",
                                   InteriorFixture.OwnerBed),
                },

                // ---- the walls that exist only up here -----------------------------------------------
                // The building's OWN four walls are NOT here: they are the same walls on both storeys
                // and BuildWalls already stood them. Only the partitions and the doorway plug ride the
                // level. Each rect deliberately runs INTO the outer wall band it meets (±4.025, ∓3.3)
                // so there is no hairline gap at the join — the quads are separate colliders, so an
                // overlap is safe, whereas a shared edge is what leaks a player through a corner.
                partitions: new[]
                {
                    new WallRect(0.90f, 1.20f, -4.025f, -3.10f,
                                 "the landing partition, front run — from the front wall to the " +
                                 "player's door"),
                    new WallRect(0.90f, 1.20f, -1.90f, 1.40f,
                                 "the landing partition, middle run — between the two bedroom doors, " +
                                 "and what the east-west partition tees into"),
                    new WallRect(0.90f, 1.20f, 2.60f, 4.025f,
                                 "the landing partition, back run — from Ginny's door to the back wall"),
                    new WallRect(-3.30f, 1.20f, 0.40f, 0.70f,
                                 "the partition BETWEEN the two bedrooms, running west from the landing " +
                                 "to the left wall. Solid: each room is entered from the landing, so " +
                                 "neither is a way through to the other"),
                }),

            _ => default,
        };

        /// <summary>Whether a plan key names a storey above. <c>default(UpperLevelPlan)</c> — everything
        /// not listed — has null lists and is the "no upstairs" answer.</summary>
        public static bool HasUpperLevel(string planKey) =>
            !string.IsNullOrEmpty(planKey) && UpperLevelFor(planKey).Furnishings != null;

        /// <summary>
        /// How each room is furnished. A handful per room, honestly placed — enough that a house reads
        /// as lived in and that the prop pipeline is exercised end to end (bake → slice → place →
        /// collide → Y-sort past), not a decorating pass.
        ///
        /// <para>Coordinates are in the ROOM's own frame, which is what the owner sees in the room art:
        /// the doorway is at <c>(0, −Ln/2)</c> at the bottom of the picture and the hearth at the top.
        /// <b>Each room has its own extent</b> — from the school's 6.36 × 7.63 m to the farmhouse's
        /// 7.68 × 9.94 m — so every list below states the one it was placed against, and
        /// <c>StPetersInteriorsTests</c> checks each against its own <c>size</c> rather than against a
        /// single hard-coded pair (which is what it used to do, and it silently stopped checking the
        /// moment a second room was furnished).</para>
        ///
        /// <para>The doorway lane — <c>x</c> within ±0.7 of centre, near the front wall — is left clear
        /// on purpose: a prop parked in the threshold is a prop you cannot get past, and its collider
        /// would close the one gap in the house.</para>
        ///
        /// <para>Furniture MAY sit within the wall's thickness — that is what "against the wall" looks
        /// like, and the shipped cottage's bed is 100 mm into it. What it may never do is stick out
        /// through the wall into open air, which is the bound the tests hold.</para>
        /// </summary>
        public static IReadOnlyList<Furnishing> FurnishingsFor(string roomKey) => roomKey switch
        {
            // ---- the sage cottage: one room, a fisherman's ----------------------------------------
            // 6.6 × 8.05 m, so x runs ±3.3 and y ±4.03.
            "sageCottage" => new[]
            {
                new Furnishing("bed", new Vector2(-2.35f, 1.50f), 0,
                               "along the left wall in the back half — the far corner from the door, " +
                               "which is where a bed goes in a one-room cottage"),
                new Furnishing("seaChest", new Vector2(-2.30f, -0.40f), 0,
                               "at the foot of the bed; the shortest prop in the set, so it is the " +
                               "sorting edge case as well as the fisherman's detail"),
                new Furnishing("table", new Vector2(0.90f, -0.40f), 0,
                               "middle of the room, off centre so the doorway lane stays clear — " +
                               "THE prop the owner is asked to walk behind and then in front of"),
                new Furnishing("chair", new Vector2(0.30f, -1.15f), 0,
                               "pulled out on the door side of the table"),
                new Furnishing("chair", new Vector2(1.50f, -1.15f), 0,
                               "the second chair — a one-room cottage that seats two reads as lived in"),
                new Furnishing("dresser", new Vector2(2.75f, 0.50f), 2,
                               "against the right wall, turned a quarter so its back is to the wall " +
                               "— proves a prop can sit flush without fighting the wall collider"),
            },

            // ---- the school: desks facing the stove end -------------------------------------------
            // 6.36 × 7.63 m, so x runs ±3.18 and y ±3.815 (usable ±2.88 / ±3.515 inside the walls).
            // The kit has no desk, so a table IS the desk — two of them in a row facing the teacher's,
            // which is the shape a one-room school reads as even with a house's furniture.
            "school" => new[]
            {
                new Furnishing("table", new Vector2(0f, 0.55f), 0,
                               "the pupils' bench-desk, square in the middle of the room"),
                new Furnishing("chair", new Vector2(-0.60f, -0.15f), 0,
                               "two seats at it, on the door side so the class faces the front"),
                new Furnishing("chair", new Vector2(0.60f, -0.15f), 0,
                               "the second seat"),
                new Furnishing("table", new Vector2(0f, 2.10f), 0,
                               "the teacher's desk at the stove end, facing back down the room"),
                new Furnishing("chair", new Vector2(0f, 2.85f), 0,
                               "her chair behind it, between the desk and the hearth"),
                new Furnishing("dresser", new Vector2(2.55f, 1.30f), 2,
                               "the book press against the right wall, turned a quarter"),
                new Furnishing("seaChest", new Vector2(-2.35f, 1.30f), 0,
                               "the wood box by the stove — a schoolroom that heats itself"),
            },

            // ---- the red saltbox: the plainest keeping room ---------------------------------------
            // 6.96 × 8.68 m, so x runs ±3.48 and y ±4.34 (usable ±3.18 / ±4.04).
            "redSaltbox" => new[]
            {
                new Furnishing("bed", new Vector2(-2.35f, 2.30f), 0,
                               "back-left corner, the furthest point from the door"),
                new Furnishing("seaChest", new Vector2(-2.35f, 0.75f), 0,
                               "at the foot of the bed"),
                new Furnishing("table", new Vector2(1.20f, 0.40f), 0,
                               "off centre to the right, so the doorway lane stays clear"),
                new Furnishing("chair", new Vector2(0.70f, -0.35f), 0,
                               "pulled out on the door side"),
                new Furnishing("chair", new Vector2(1.75f, -0.35f), 0,
                               "the second chair"),
                new Furnishing("dresser", new Vector2(2.85f, 2.20f), 2,
                               "against the right wall in the back half, turned a quarter"),
            },

            // ---- the white farmhouse: the hall ----------------------------------------------------
            // 7.68 × 9.94 m, so x runs ±3.84 and y ±4.97 (usable ±3.54 / ±4.67) — the biggest floor on
            // the island, and the only one that seats four. No bed: this is the downstairs hall of a
            // house with a family in it, and they sleep upstairs.
            "whiteFarmhouse" => new[]
            {
                new Furnishing("table", new Vector2(0.60f, 0.70f), 0,
                               "the family table, off centre so the doorway lane stays clear"),
                new Furnishing("chair", new Vector2(-0.20f, -0.10f), 0,
                               "near side, left"),
                new Furnishing("chair", new Vector2(1.40f, -0.10f), 0,
                               "near side, right"),
                new Furnishing("chair", new Vector2(-0.20f, 1.50f), 0,
                               "far side, left — the seat you have to walk round the table to reach"),
                new Furnishing("chair", new Vector2(1.40f, 1.50f), 0,
                               "far side, right"),
                new Furnishing("dresser", new Vector2(3.10f, 1.60f), 2,
                               "the dresser against the right wall, turned a quarter"),
                new Furnishing("seaChest", new Vector2(-2.90f, 2.60f), 0,
                               "back-left corner, out of the way of the table"),
            },

            _ => System.Array.Empty<Furnishing>(),
        };

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the interior for one placed village building, if one has been baked for it.
        ///
        /// <para>Returns true if a room went up. Returns false — quietly, and having changed
        /// nothing — when this building has no baked room, which is the normal case for four of the
        /// five. Null-tolerant throughout for the same reason the village placement is: "declared in the
        /// contract" and "has pixels on disk" are different questions, and a partial art state should
        /// still place what it can.</para>
        ///
        /// <para><b>Re-runnable.</b> Any interior already under this building is destroyed first, so a
        /// second builder run leaves one room and one set of furniture rather than two of each. That is
        /// the whole of "non-destructive" here: the builder owns these objects and rebuilds them.</para>
        /// </summary>
        public static bool Stand(GameObject buildingGo, SpriteRenderer shell, string buildingKey,
                                 int exteriorFacing, Transform occupant, string upperPlanKey = null)
        {
            if (buildingGo == null) return false;

            ClearExisting(buildingGo.transform);

            InteriorCatalog.Placement room = InteriorCatalog.FindRoom(buildingKey);
            if (!room.IsValid) return false;

            int interiorFacing = InteriorCatalog.InteriorFacingFor(exteriorFacing);
            Sprite roomSprite = InteriorCatalog.LoadFacing(room, interiorFacing);
            if (roomSprite == null)
            {
                Debug.LogWarning(
                    $"[StPetersInteriors] '{buildingKey}' has a baked room but no facing-{interiorFacing} " +
                    $"sprite — its sheet is missing or unsliced ({room.SheetPath}). Leaving the building " +
                    "solid and un-enterable rather than standing a blank room inside it.");
                return false;
            }

            // --- the room sprite: same position as the shell, because both pivot on the ground centre.
            var roomGo = new GameObject(RoomChildName);
            roomGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            roomGo.transform.localPosition = Vector3.zero;
            SpriteRenderer roomRenderer = InteriorCatalog.ConfigureRoom(roomGo, room, roomSprite);

            // --- where the doorway is, in the room's own model frame. MEASURED, per room, from the
            //     bake's own anchors — not taken from InteriorFootprint's house-family defaults. The
            //     two agree today (this rig's door anchor is pj(0,−Ln/2,fZ), so it cannot be anything
            //     but centred on −y); measuring is what keeps that true after the next rig drop.
            Vector2 door = InteriorCatalog.DoorModelMetres(room);
            float doorSign = door.y >= 0f ? 1f : -1f;

            // --- the footprint everything else is measured from.
            var footprint = new InteriorFootprint(
                buildingGo.transform.position,
                room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                interiorFacing, room.Entry.facings, SpriteLightMath.GroundDepthScale,
                doorSign, door.x);

            // --- the walls. Always on, from both sides: the cutaway that drops the two camera-facing
            //     walls is a courtesy to the camera, not a hole in the house.
            BuildWalls(buildingGo.transform, footprint);

            // --- the furniture.
            var propsGo = new GameObject(PropsChildName);
            propsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            propsGo.transform.localPosition = Vector3.zero;
            int furnished = Furnish(propsGo.transform, FurnishingsFor(room.Entry.key), room.Entry.key,
                                    footprint, interiorFacing, room.Entry.facings);

            // --- the behaviour.
            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(shell, roomRenderer, propsGo.transform,
                               room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                               interiorFacing, room.Entry.facings,
                               SpriteLightMath.GroundDepthScale,
                               WallThicknessMetres, DoorwayWidthMetres,
                               doorOnPlusY: doorSign > 0f, doorAcrossMetres: door.x);
            interior.SetOccupant(occupant);

            // --- THE STOREY ABOVE, if this SITE asked for one. Keyed by plan and not by room, because
            //     Ginny's cottage and the village's pilot cottage are the same build — see UpperLevelFor.
            int upstairs = StandUpperLevel(buildingGo, interior, room, roomSprite, footprint,
                                           interiorFacing, propsGo.transform, upperPlanKey);
            if (upstairs > 0) furnished += upstairs;

            Debug.Log(
                $"[StPetersInteriors] '{buildingKey}' is enterable: room d{interiorFacing} under shell " +
                $"d{exteriorFacing} (the contract's MEASURED offset), " +
                $"{room.Entry.footprintWidthMetres:0.#}×{room.Entry.footprintLengthMetres:0.#} m of " +
                $"floor, {furnished} piece(s) of furniture, doorway on the " +
                $"{(doorSign > 0f ? "+Y" : "−Y")} wall {door.x:+0.00;-0.00} m off its centre, threshold " +
                $"at ({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#})" +
                (interior.HasUpperLevel ? ", AND a storey above it." : "."));
            return true;
        }

        /// <summary>Destroy anything a previous run of this pass left on a building, so re-running the
        /// builder cannot double the furniture or leave an orphaned room behind a new one.</summary>
        static void ClearExisting(Transform buildingRoot)
        {
            var existing = buildingRoot.GetComponent<BuildingInterior>();
            if (existing != null) Object.DestroyImmediate(existing);

            for (int i = buildingRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = buildingRoot.GetChild(i);
                if (child.name == RoomChildName || child.name == PropsChildName ||
                    child.name == WallsChildName || child.name == UpperRoomChildName ||
                    child.name == UpperPropsChildName || child.name == UpperWallsChildName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// The wall colliders: five quads on one child object — back, left, right, and the front wall
        /// split around the doorway.
        ///
        /// <para>One <see cref="PolygonCollider2D"/> per wall rather than five paths on one collider.
        /// Several paths on one collider turn their overlaps into HOLES, and while
        /// <see cref="InteriorFootprint.WallQuads"/> already returns disjoint quads, separate colliders
        /// mean a future wall that does overlap cannot silently open a gap at a corner.</para>
        /// </summary>
        static void BuildWalls(Transform buildingRoot, in InteriorFootprint footprint)
        {
            var wallsGo = new GameObject(WallsChildName);
            wallsGo.transform.SetParent(buildingRoot, worldPositionStays: false);
            wallsGo.transform.localPosition = Vector3.zero;

            Vector2 origin = buildingRoot.position;
            foreach (Vector2[] quad in footprint.WallQuads(WallThicknessMetres, DoorwayWidthMetres))
            {
                var collider = wallsGo.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;
                collider.SetPath(0, ToLocal(quad, origin));
            }
        }

        /// <summary>Stand the furniture. Each prop is its own sprite with its own Y-sort and its own
        /// footprint collider — the two things that make walking round a table work.</summary>
        static int Furnish(Transform propsRoot, IReadOnlyList<Furnishing> furnishings, string roomKey,
                           in InteriorFootprint footprint, int interiorFacing, int facings,
                           BuildingInterior interior = null, string fixtureIdPrefix = null)
        {
            if (furnishings == null) return 0;

            int placed = 0;
            foreach (Furnishing f in furnishings)
            {
                InteriorCatalog.Placement prop = InteriorCatalog.FindProp(f.PropKey);
                if (!prop.IsValid)
                {
                    Debug.LogWarning(
                        $"[StPetersInteriors] '{roomKey}' asks for a '{f.PropKey}' but the interior " +
                        "contract has no such prop. Skipping it rather than leaving a gap in the " +
                        "collision where a solid object should be — re-bake the kit.");
                    continue;
                }

                int propFacing = ((interiorFacing + f.FacingOffset) % facings + facings) % facings;
                Sprite sprite = InteriorCatalog.LoadFacing(prop, propFacing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[StPetersInteriors] '{f.PropKey}' facing {propFacing} has no sprite — its " +
                        $"sheet is missing or unsliced ({prop.SheetPath}). Skipping.");
                    continue;
                }

                var go = new GameObject($"{prop.Entry.label} ({f.RoomMetres.x:0.#},{f.RoomMetres.y:0.#})");
                go.transform.SetParent(propsRoot, worldPositionStays: false);

                // The prop's origin is the floor CENTRE of its own footprint — the same convention as
                // the room's pivot — so the room's floor point IS the prop's position. No offset maths,
                // which is the property the two rigs were drawn to share.
                Vector2 world = footprint.ModelToWorld(f.RoomMetres);
                go.transform.position = new Vector3(world.x, world.y, 0f);

                InteriorCatalog.ConfigureProp(go, prop, sprite);

                var collider = go.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;
                collider.SetPath(0, ToLocal(
                    footprint.PropQuad(f.RoomMetres, prop.Entry.propFootprintWidth,
                                       prop.Entry.propFootprintDepth, f.FacingOffset),
                    world));

                Fit(go, f, interior, fixtureIdPrefix);

                placed++;
            }

            return placed;
        }

        // =====================================================================================
        //  THE STOREY ABOVE
        // =====================================================================================

        /// <summary>
        /// Stand the upper storey, if this site's plan declares one. Returns how many pieces of
        /// furniture went up there (0 for the great majority of buildings, which have no plan).
        ///
        /// <para><b>What an upper storey actually IS here.</b> A second room sprite, a second furniture
        /// root and a small set of extra colliders, all parented to the same building and all switched
        /// as one by <see cref="BuildingInterior"/>. It shares the footprint, the facing and the walls
        /// with the storey below — which is what "true to the footprint" means when the footprint has
        /// two floors in it — so nothing here re-derives any of those.</para>
        ///
        /// <para><b>The three colliders that differ, and why only three.</b> The house's four walls are
        /// the same walls upstairs and are already standing, so the upper level adds only what is
        /// genuinely different about it: the partitions from the plan, and a PLUG over the front
        /// doorway, because there is nothing outside a first-floor door but air. Building a whole second
        /// wall set for the sake of one missing gap would have been two places for the door's position
        /// to live.</para>
        ///
        /// <para><b>Why the stair is placed twice.</b> Once in each storey's furniture root, at the same
        /// model coordinate. The two are separate objects carrying opposite halves of the stairwell
        /// (<see cref="InteriorFixture.StairUp"/> / <see cref="InteriorFixture.StairDown"/>), and
        /// because the inactive storey's root is switched OFF, the half you are not standing on
        /// un-registers itself from <see cref="Interactables"/> entirely. The press can therefore never
        /// resolve to the wrong direction — not because the resolver was careful, but because only one
        /// of them is in the room.</para>
        /// </summary>
        static int StandUpperLevel(GameObject buildingGo, BuildingInterior interior,
                                   InteriorCatalog.Placement room, Sprite groundRoomSprite,
                                   in InteriorFootprint footprint, int interiorFacing,
                                   Transform groundProps, string planKey)
        {
            if (!HasUpperLevel(planKey)) return 0;

            UpperLevelPlan plan = UpperLevelFor(planKey);
            string idPrefix = "fixture." + planKey;

            // --- the upper room sprite. An empty RoomKey means "the same sheet as downstairs", which is
            //     the greybox: identical footprint, so the floor and far walls are already correct.
            Sprite upperSprite = groundRoomSprite;
            InteriorCatalog.Placement upperRoom = room;
            if (!string.IsNullOrEmpty(plan.RoomKey))
            {
                InteriorCatalog.Placement named = InteriorCatalog.FindRoom(plan.RoomKey);
                if (named.IsValid)
                {
                    Sprite s = InteriorCatalog.LoadFacing(named, interiorFacing);
                    if (s != null) { upperRoom = named; upperSprite = s; }
                    else
                        Debug.LogWarning(
                            $"[StPetersInteriors] upper-level plan '{planKey}' names room " +
                            $"'{plan.RoomKey}', but it has no facing-{interiorFacing} sprite. Falling " +
                            "back to the storey below's sheet.");
                }
                else
                    Debug.LogWarning(
                        $"[StPetersInteriors] upper-level plan '{planKey}' names room '{plan.RoomKey}', " +
                        "which the interior contract does not have. Falling back to the storey below's " +
                        "sheet — re-bake the kit.");
            }

            var upperRoomGo = new GameObject(UpperRoomChildName);
            upperRoomGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperRoomGo.transform.localPosition = Vector3.zero;
            SpriteRenderer upperRenderer =
                InteriorCatalog.ConfigureRoom(upperRoomGo, upperRoom, upperSprite);

            // --- the upper furniture.
            var upperPropsGo = new GameObject(UpperPropsChildName);
            upperPropsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperPropsGo.transform.localPosition = Vector3.zero;
            int placed = Furnish(upperPropsGo.transform, plan.Furnishings, room.Entry.key, footprint,
                                 interiorFacing, room.Entry.facings, interior, idPrefix);

            // --- the colliders that exist only up here.
            var upperWallsGo = new GameObject(UpperWallsChildName);
            upperWallsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperWallsGo.transform.localPosition = Vector3.zero;
            BuildUpperWalls(upperWallsGo.transform, footprint, plan);

            // --- the foot of the stairs, added to the storey BELOW.
            placed += Furnish(groundProps, plan.GroundAdditions, room.Entry.key, footprint,
                              interiorFacing, room.Entry.facings, interior, idPrefix);

            // ⚠ AFTER both furniture roots exist and BEFORE anything else looks at the interior:
            //   ConfigureUpperLevel applies the swap immediately, which is what leaves the upstairs
            //   switched OFF in the editor instead of drawn straight over the ground floor.
            interior.ConfigureUpperLevel(upperRenderer, upperPropsGo.transform, upperWallsGo.transform);

            Debug.Log(
                $"[StPetersInteriors] upper level '{planKey}': {placed} piece(s) of furniture, " +
                $"{plan.Partitions.Count} partition(s) plus the doorway plug, drawing " +
                $"{(string.IsNullOrEmpty(plan.RoomKey) ? "the storey below's room sheet (greybox)" : "'" + plan.RoomKey + "'")}.");
            return placed;
        }

        /// <summary>
        /// The colliders that exist only on the upper storey: the plan's partitions, and the plug that
        /// closes the front doorway.
        ///
        /// <para>One <see cref="PolygonCollider2D"/> per rect, exactly as <see cref="BuildWalls"/> does
        /// and for the same reason — several paths on one collider turn their overlaps into holes, and
        /// these rects DO overlap the house's walls on purpose (each partition runs into the wall it
        /// meets, so the join cannot leak).</para>
        /// </summary>
        static void BuildUpperWalls(Transform root, in InteriorFootprint footprint, in UpperLevelPlan plan)
        {
            Vector2 origin = root.parent != null ? (Vector2)root.parent.position : Vector2.zero;

            foreach (WallRect r in plan.Partitions)
            {
                var collider = root.gameObject.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;
                collider.SetPath(0, ToLocal(footprint.Quad(r.X0, r.X1, r.Y0, r.Y1), origin));
            }

            // The doorway, closed. Derived from the same arithmetic that cut the gap, so the two can
            // never disagree about where the door is.
            var plug = root.gameObject.AddComponent<PolygonCollider2D>();
            plug.pathCount = 1;
            plug.SetPath(0, ToLocal(
                footprint.DoorwayPlugQuad(WallThicknessMetres, DoorwayWidthMetres), origin));
        }

        /// <summary>
        /// Give a placed prop the behaviour its <see cref="Furnishing.Fixture"/> asks for — a stair you
        /// can climb, a bed you can turn in at, the wardrobe. Does nothing at all for
        /// <see cref="InteriorFixture.None"/>, which is every prop in the village.
        ///
        /// <para><b>Ids are derived, not authored.</b> Each is the site's plan key plus the fixture's
        /// role, so they are unique among live registrants by construction — which is the one property
        /// <see cref="IInteractable.Id"/> genuinely requires, and the one an author retyping strings
        /// into a table would eventually break.</para>
        /// </summary>
        static void Fit(GameObject go, in Furnishing f, BuildingInterior interior, string idPrefix)
        {
            if (f.Fixture == InteriorFixture.None) return;

            if (string.IsNullOrEmpty(idPrefix)) idPrefix = "fixture.interior";

            switch (f.Fixture)
            {
                case InteriorFixture.StairUp:
                    go.AddComponent<InteriorStair>().Configure(
                        interior, idPrefix + ".stair_up", fromLevel: 0, toLevel: 1,
                        reachMeters: StairReachMetres, arrivalText: "You go up.");
                    break;

                case InteriorFixture.StairDown:
                    go.AddComponent<InteriorStair>().Configure(
                        interior, idPrefix + ".stair_down", fromLevel: 1, toLevel: 0,
                        reachMeters: StairReachMetres, arrivalText: "You come back down.");
                    break;

                case InteriorFixture.PlayerBed:
                    // The interior comes along so the bed can report which STOREY it is on when the
                    // player turns in (ADR 0037) — the player's bed is upstairs, and an anchor that did
                    // not say so would wake them in the room below it.
                    go.AddComponent<InteriorBed>().Configure(
                        idPrefix + ".bed_player", isPlayerBed: true, ownerName: "",
                        placeName: PlayerBedPlaceName, reachMeters: BedReachMetres, interior: interior);
                    break;

                case InteriorFixture.OwnerBed:
                    go.AddComponent<InteriorBed>().Configure(
                        idPrefix + ".bed_owner", isPlayerBed: false, ownerName: BedOwnerName,
                        placeName: "", reachMeters: BedReachMetres, interior: interior);
                    break;

                case InteriorFixture.Wardrobe:
                    go.AddComponent<InteriorWardrobe>().Configure(
                        idPrefix + ".wardrobe", reachMeters: WardrobeReachMetres);
                    break;
            }
        }

        /// <summary>How close (m) you must stand to take the stairs. Tighter than a shore fixture's
        /// reach: a stairwell is a specific place in a small room, and a generous radius would have it
        /// outranking the wardrobe a stride away.</summary>
        public const float StairReachMetres = 1.2f;

        /// <summary>How close (m) you must stand to turn in. A bed is big and you sit down on it, so
        /// this is a step-up distance.</summary>
        public const float BedReachMetres = 1.4f;

        /// <summary>How close (m) you must stand to open the wardrobe.</summary>
        public const float WardrobeReachMetres = 1.2f;

        /// <summary>Whose bed the one you may NOT sleep in is. Authored here rather than in the
        /// component so the refusal names the host, and so the day a second lodging exists it is a plan
        /// field rather than a second string in a component.</summary>
        public const string BedOwnerName = "Ginny";

        /// <summary>Where the player is turning in, for the notice.</summary>
        public const string PlayerBedPlaceName = "your bed at Ginny's";

        /// <summary>World-space quad → collider-local points. <see cref="PolygonCollider2D.SetPath"/>
        /// takes LOCAL coordinates, and handing it world ones puts every wall an entire village away
        /// from the house — silently, because nothing about a collider is drawn.</summary>
        static Vector2[] ToLocal(Vector2[] worldPoints, Vector2 origin)
        {
            var local = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++) local[i] = worldPoints[i] - origin;
            return local;
        }
    }
}
#endif
