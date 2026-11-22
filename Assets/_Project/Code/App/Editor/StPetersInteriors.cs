#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's numbers
using HiddenHarbours.Art.Editor;          // InteriorCatalog, InteriorKit
using HiddenHarbours.World;               // BuildingInterior, InteriorFootprint, InteriorLevelLayout
// Aliased rather than a whole `using HiddenHarbours.Core;`: this file lives at the junction of three
// modules whose short type names already collide readily, and it wants exactly two Core types.
using PillowSide = HiddenHarbours.Core.PillowSide;
using BedPillow = HiddenHarbours.Core.BedPillow;

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
    /// <para><b>⚠️ The room's facing is MEASURED, not assumed.</b> Until the village return (drop 14)
    /// the room rig put its doorway on <c>−Y</c> and the exterior rigs their door on <c>+Y</c>, so the
    /// same cell index showed the two 180° apart and a room stood four facings round from its house. The
    /// returned room rig draws its doorway on <c>+Y</c> too, and the bake now measures 0. Whatever it
    /// measures, it writes into the contract, and <see cref="InteriorCatalog.InteriorFacingFor"/> is the
    /// only thing that applies it. Get this wrong and the player walks in the front door and appears at
    /// the BACK of the room — and it reads as an art bug, not a placement one.</para>
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
        // The names and the two numbers moved to BuildingInteriorStander, which is now the one recipe
        // for both families. Forwarded rather than deleted: a dozen readers across the village, the
        // shops and the proofs spell them `StPetersInteriors.…`, and a const forwarder costs nothing
        // at runtime while leaving one place where the value is actually decided.

        /// <inheritdoc cref="BuildingInteriorStander.RoomChildName"/>
        public const string RoomChildName = BuildingInteriorStander.RoomChildName;

        /// <inheritdoc cref="BuildingInteriorStander.PropsChildName"/>
        public const string PropsChildName = BuildingInteriorStander.PropsChildName;

        /// <inheritdoc cref="BuildingInteriorStander.WallsChildName"/>
        public const string WallsChildName = BuildingInteriorStander.WallsChildName;

        /// <inheritdoc cref="BuildingInteriorStander.UpperRoomChildName"/>
        public const string UpperRoomChildName = BuildingInteriorStander.UpperRoomChildName;

        /// <inheritdoc cref="BuildingInteriorStander.UpperPropsChildName"/>
        public const string UpperPropsChildName = BuildingInteriorStander.UpperPropsChildName;

        /// <summary>The child object name of the colliders that exist only upstairs — the partitions
        /// and the plug that closes the front doorway. The building's own walls are NOT in here.</summary>
        public const string UpperWallsChildName = BuildingInteriorStander.UpperWallsChildName;

        /// <summary>
        /// Wall thickness (m) the colliders are built with and the inside test uses.
        ///
        /// <para>Thicker than the rig's drawn 0.16 m wall on purpose: at the sprint speed
        /// (5.5 m/s) and a 0.02 s fixed step the player moves 0.11 m per step, so a 0.16 m wall is
        /// inside a factor of 1.5 of tunnelling and a fast diagonal into a corner is exactly the
        /// input that finds it. 0.3 m costs a hand's width of floor and cannot be walked through.</para>
        /// </summary>
        public const float WallThicknessMetres = BuildingInteriorStander.WallThicknessMetres;

        /// <summary>The gap left in the front wall (m) — wider than the drawn 1.05 m opening, because
        /// the player has width of their own and a threshold you have to line up on to the pixel is not
        /// cozy.</summary>
        public const float DoorwayWidthMetres = BuildingInteriorStander.DoorwayWidthMetres;

        // =====================================================================================
        //  THE FURNISHING (authored — the identity a hash could not have produced)
        // =====================================================================================

        /// <summary>
        /// One piece of furniture: which prop, where it stands in the ROOM's own frame (metres, origin
        /// the floor centre, <c>+y</c> toward the front door and <c>+x</c> across the room, which is to
        /// your LEFT as you come in at that door), and how many facings it is turned relative to the room.
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

            /// <summary>
            /// ⭐ <b>WHICH END OF THIS BED THE PILLOW IS ON</b>, in the ROOM's own model frame — the same
            /// frame as <see cref="RoomMetres"/>, so it is read straight off the room picture the owner is
            /// looking at: <c>PlusY</c> is the doorway end, <c>MinusY</c> the hearth end.
            /// <see cref="PillowSide.Undeclared"/> for everything that is not a bed.
            ///
            /// <para><b>Every bed must say, and content validation is what enforces it.</b> Not saying is
            /// how the sleeper came to lie the wrong way round: the pose fell back to one fixed heading,
            /// which is right for a bed in an unturned room and wrong the moment the builder faces the
            /// building any other way — and the result is a PICTURE, so nothing downstream can catch it.
            /// See <see cref="PillowSide"/> for why a side is stated rather than found in the sprite.</para>
            ///
            /// <para><b>⚠ It must AGREE with the way the bed is drawn.</b> The prop's own declaration
            /// (<see cref="InteriorKit.Build.Pillow"/>) turned by <see cref="FacingOffset"/> is where the
            /// pillow really is; this is where the author says it should be. <see cref="DrawnPillowSide"/>
            /// computes the first, the builder warns when the two differ, and a test refuses it — which is
            /// what catches the real authoring slip: turning a bed and forgetting its head end came with
            /// it.</para>
            /// </summary>
            public readonly PillowSide Pillow;

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why)
                : this(propKey, roomMetres, facingOffset, why, InteriorFixture.None)
            {
            }

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why,
                              InteriorFixture fixture)
                : this(propKey, roomMetres, facingOffset, why, fixture, PillowSide.Undeclared)
            {
            }

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why,
                              InteriorFixture fixture, PillowSide pillow)
            {
                PropKey = propKey; RoomMetres = roomMetres; FacingOffset = facingOffset; Why = why;
                Fixture = fixture; Pillow = pillow;
            }

            /// <summary>
            /// Where the pillow ACTUALLY is, in the room's frame, according to the art: the prop def's own
            /// declaration turned by this placement's <see cref="FacingOffset"/>.
            ///
            /// <para><see cref="PillowSide.Undeclared"/> when the prop is not a bed, when the kit has no
            /// such prop, and — deliberately — when the offset is ODD. An odd offset is a 45° step, which
            /// stands the bed on the diagonal of a rectangular room; there is no cardinal answer to round
            /// it to, and rounding one would be exactly the quiet guess this whole declaration exists to
            /// remove.</para>
            /// </summary>
            public PillowSide DrawnPillowSide
            {
                get
                {
                    InteriorKit.Build? prop = InteriorKit.FindProp(PropKey);
                    if (prop == null || !prop.Value.IsBed) return PillowSide.Undeclared;

                    int eighths = ((FacingOffset % InteriorKit.Facings) + InteriorKit.Facings)
                                  % InteriorKit.Facings;
                    if (eighths % 2 != 0) return PillowSide.Undeclared;

                    return BedPillow.Turned(prop.Value.Pillow, eighths / 2);
                }
            }

            /// <summary>Is this placement a bed at all? True when the KIT says the prop has a head end, or
            /// when the placement makes it something you can turn in at — either way it is a thing a
            /// person gets laid down on, and either way it owes a <see cref="Pillow"/>.</summary>
            public bool IsBed
            {
                get
                {
                    if (Fixture == InteriorFixture.PlayerBed || Fixture == InteriorFixture.OwnerBed)
                        return true;

                    InteriorKit.Build? prop = InteriorKit.FindProp(PropKey);
                    return prop != null && prop.Value.IsBed;
                }
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
        /// where <c>+y</c> runs toward the front door. <see cref="InteriorFootprint.Quad"/> does the rotation
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

            /// <summary>The storey above's own painted sheet (an asset path to a sprite), for a site whose
            /// upstairs is not a baked room. ⚠ An upper storey never draws the ground floor's sheet: that
            /// sheet paints the ground floor's stair, partitions and furniture, and drawn upstairs they read
            /// as the floor below showing through. A plan names either <see cref="RoomKey"/> or this.</summary>
            public readonly string UpperSheetPath;

            /// <summary>The storey BELOW's own furniture when this site's ground floor is not the shared
            /// build's (<see cref="FurnishingsFor"/>); null = the build's list. Ginny's cottage and the
            /// village's pilot cottage are the same build, and only hers has a bedroom upstairs, so only
            /// hers moves the bed off the ground floor.</summary>
            public readonly IReadOnlyList<Furnishing> GroundFurnishings;

            /// <summary>Walls and solid painted pieces that exist only on the storey BELOW (its room
            /// partitions, the painted settee, the stair run). Built under the ground furniture root, so
            /// they are switched off with it when you go up: the storey above is drawn higher on screen and
            /// overlaps the ground floor in world space, and a ground partition left standing would cut
            /// through the bedrooms.</summary>
            public readonly IReadOnlyList<WallRect> GroundWalls;

            public UpperLevelPlan(string roomKey,
                                  IReadOnlyList<Furnishing> groundAdditions,
                                  IReadOnlyList<Furnishing> furnishings,
                                  IReadOnlyList<WallRect> partitions,
                                  string upperSheetPath = null,
                                  IReadOnlyList<Furnishing> groundFurnishings = null,
                                  IReadOnlyList<WallRect> groundWalls = null)
            {
                RoomKey = roomKey ?? "";
                GroundAdditions = groundAdditions;
                Furnishings = furnishings;
                Partitions = partitions;
                UpperSheetPath = upperSheetPath ?? "";
                GroundFurnishings = groundFurnishings;
                GroundWalls = groundWalls ?? System.Array.Empty<WallRect>();
            }

            /// <summary>Whether the storey above has a picture of its own (a baked room or a painted
            /// sheet) rather than none.</summary>
            public bool HasOwnUpperPicture =>
                !string.IsNullOrEmpty(RoomKey) || !string.IsNullOrEmpty(UpperSheetPath);
        }

        /// <summary>The asset path of Ginny's painted upstairs sheet (same cell size and pivot as the
        /// sage room's ground sheet, so it lands on the same footprint one storey up).</summary>
        public const string GinnyUpperSheetPath =
            "Assets/_Project/Art/Sprites/Interiors/Painted/GinnyCottage_Upstairs.png";

        /// <summary>The ground furniture a site's building is stood with: the site's own list when its
        /// upper-level plan gives one, the build's otherwise. Additions (the stair foot) come on top.</summary>
        public static IReadOnlyList<Furnishing> GroundFurnishingsFor(string roomKey, string planKey)
        {
            if (HasUpperLevel(planKey))
            {
                UpperLevelPlan plan = UpperLevelFor(planKey);
                if (plan.GroundFurnishings != null) return plan.GroundFurnishings;
            }
            return FurnishingsFor(roomKey);
        }

        /// <summary>The plan key Ginny's cottage asks for. Named after the SITE, not the build, because
        /// the build (<c>sageCottage</c>) is shared with the village's pilot cottage and only one of the
        /// two has anyone living upstairs.</summary>
        public const string GinnyCottagePlanKey = "ginnyCottage";

        /// <summary>
        /// <b>Every upper-storey plan the island declares.</b> One today; the point is that it is a LIST
        /// rather than a constant nobody enumerates.
        ///
        /// <para>Furniture reaches the world down two separate paths — <see cref="FurnishingsFor"/> for a
        /// building's own room, and <see cref="UpperLevelFor"/> for the storey above a site that asked for
        /// one — and content validation has to walk BOTH or it silently checks half the beds. It used to
        /// walk only the first, which is exactly how the two beds that matter most (yours and your host's,
        /// upstairs at Ginny's) could have gone unvalidated. Adding a plan here is what puts its rooms
        /// under every test in the suite.</para>
        /// </summary>
        public static readonly string[] UpperLevelPlanKeys =
            { GinnyCottagePlanKey, "harbourGable", "creamSaltbox", "whiteGable" };

        /// <summary>
        /// <b>The storey above, per site.</b> Returns an empty plan for everything not listed, which is
        /// every building on the island but one.
        ///
        /// <para><b>The cottage's numbers, so the geometry below can be read.</b> <c>ginnyCottage</c> is
        /// 7.2 × 9.1 m, so the model frame runs x ±3.6 and y ±4.55, and the 0.3 m walls leave a floor
        /// of x ∈ [−3.3, 3.3], y ∈ [−4.25, 4.25]. <c>+y</c> runs toward the front door, which is the
        /// gap in that end wall, centred on x = 0. <c>+x</c> is north.</para>
        ///
        /// <para><b>The plan, in words (owner 2026-11-22: the beds on the south wall, the corridor on
        /// the north).</b> The stair climbs along the north wall from the front door to the back
        /// partition and comes up into a corridor along the north side. A north–south partition closes
        /// the corridor off from the two bedrooms, with a door into each; an east–west partition divides
        /// those two from one another. <b>Neither bedroom is a corridor to the other</b>: you reach both
        /// from the landing at the stair head, and Ginny does not walk through the room she is lending
        /// you. Both beds have their heads on the south wall.</para>
        ///
        /// <para><b>The stairs.</b> The foot's interact point is at (2.75, 3.70), on the clear landing
        /// between the foot of the flight and the front wall, the head's at (3.25, −0.20) where the flight
        /// arrives. Each half lands him at the other
        /// (<see cref="LinkStairLandings"/>); the interior then pushes him clear of anything solid.
        /// Ginny's ground floor has its own furniture (<see cref="UpperLevelPlan.GroundFurnishings"/>),
        /// so the village's pilot cottage keeps its own one-room layout.</para>
        ///
        /// <para><b>Its own picture upstairs.</b> The storey above draws a painted sheet of its own
        /// (<see cref="UpperLevelPlan.UpperSheetPath"/>): the bedrooms' partitions, the stairwell railing
        /// and a closed front wall. It used to draw the ground floor's sheet, which painted the ground
        /// floor's stair and partitions upstairs too, as if the floor below showed through.</para>
        /// </summary>
        public static UpperLevelPlan UpperLevelFor(string planKey) => planKey switch
        {
            "harbourGable" => EmptyUpperRoom("harbourGableUpper"),
            "creamSaltbox" => EmptyUpperRoom("creamSaltboxUpper"),
            "whiteGable" => EmptyUpperRoom("whiteGableUpper"),
            GinnyCottagePlanKey => new UpperLevelPlan(
                // No baked upstairs room: the storey above draws its own painted sheet (never the ground
                // floor's, which paints the stair, the partitions and the settee).
                roomKey: "",
                upperSheetPath: GinnyUpperSheetPath,

                // ---- the GROUND floor: three rooms, no bed (owner 2026-11-22) -----------------------
                // The entry/living room is the front (+y) half: the painted settee against the south wall by
                // the door, the table and its two chairs on the south side, and the stair run along the
                // north wall, its foot by the front door and its head at the back partition, so it takes
                // the strip of floor along the outside wall and leaves the room whole. A north-south
                // partition (y 0..0.3) closes off the back half; an east-west one splits that into the
                // kitchen (north) and the washroom (south). Each has its own doorway off the living room.
                groundFurnishings: new[]
                {
                    new Furnishing("table", new Vector2(-1.60f, 1.75f), 4,
                                   "the living room's table, on the south side clear of the doorway lane, " +
                                   "with a wide walk to the washroom door between it and the partition"),
                    new Furnishing("chair", new Vector2(-1.00f, 2.45f), 4,
                                   "pulled out on the door side of the table"),
                    new Furnishing("chair", new Vector2(-2.20f, 2.45f), 4,
                                   "its pair, clear of the settee"),
                },
                groundWalls: GinnyGroundWalls,

                // ---- what the GROUND floor gains: the foot of the stairs ---------------------------
                groundAdditions: new[]
                {
                    new Furnishing("seaChest", new Vector2(GinnyStairAtX, GinnyStairAtY), 4,
                                   "the stair's interact point, on the clear landing at the foot of the painted " +
                                   "flight along the north wall, by the front door. The sea chest prop is a STAND-IN the builder " +
                                   "places for it; the placed room hides its picture (the stair is painted " +
                                   "in the room sheet) and the run's own wall rect is the collider. Coming " +
                                   "down, he arrives here: his feet on the open floor south of the foot",
                                   InteriorFixture.StairUp),
                },

                // ---- the storey above ---------------------------------------------------------------
                furnishings: new[]
                {
                    new Furnishing("seaChest", new Vector2(GinnyStairHeadX, GinnyStairHeadY), 4,
                                   "the head of the same stairs, at the top of the flight where it comes up " +
                                   "into the corridor. Going up, he arrives here (the climb carries him " +
                                   "along the flight from the foot): his feet on the landing, clear of the " +
                                   "stairwell",
                                   InteriorFixture.StairDown),

                    new Furnishing("bed", new Vector2(-2.27f, 2.50f), 2,
                                   "THE PLAYER'S BED, in the front room, its head against the SOUTH wall " +
                                   "(owner 2026-11-22: the beds on the south wall, the corridor on the " +
                                   "north). Turned a quarter so the kit bed's head end is the south wall; " +
                                   "he gets in from either side",
                                   InteriorFixture.PlayerBed, PillowSide.MinusX),

                    new Furnishing("dresser", new Vector2(-0.75f, 3.98f), 4,
                                   "the wardrobe, STANDING IN as a dresser (the kit has no wardrobe " +
                                   "yet). Its back to the front wall, between the foot of his bed and the " +
                                   "washstand (whose site is (0.25, 3.90)). Well away from the stairwell, so its " +
                                   "top no longer draws over the stair rail as it did standing against the " +
                                   "corridor partition beside the well",
                                   InteriorFixture.Wardrobe),

                    new Furnishing("bed", new Vector2(-2.27f, -2.60f), 2,
                                   "GINNY'S BED, in the back room, laid the same way as the player's: head " +
                                   "against the south wall, so the two rooms read as one plan",
                                   InteriorFixture.OwnerBed, PillowSide.MinusX),
                },

                // ---- the walls that exist only up here -----------------------------------------------
                // The building's OWN four walls are NOT listed here: BuildUpperWalls stands a lifted copy
                // of them for this storey, and the ground floor's walls go off up here. Only the
                // partitions are authored. Each rect deliberately runs INTO the outer wall band it meets (y ±4.55, x ±3.6)
                // so there is no hairline gap at the join — the quads are separate colliders, so an
                // overlap is safe, whereas a shared edge is what leaks a player through a corner.
                partitions: new[]
                {
                    new WallRect(1.70f, 2.00f, 0.30f, 4.55f,
                                 "the corridor partition, front run — the player's room's wall along the " +
                                 "stairwell, from the front wall to his door"),
                    new WallRect(1.70f, 2.00f, -1.10f, -0.70f,
                                 "the corridor partition between the two bedroom doors, which the " +
                                 "partition between the bedrooms tees into"),
                    new WallRect(1.70f, 2.00f, -4.55f, -2.10f,
                                 "the corridor partition, back run — from Ginny's door to the back wall"),
                    new WallRect(-3.60f, 1.70f, -1.00f, -0.70f,
                                 "the partition BETWEEN the two bedrooms, from the corridor to the south " +
                                 "wall. Solid: each room is entered from the corridor, so neither is a " +
                                 "way through to the other"),
                    new WallRect(GinnyStairX0, GinnyStairX1, GinnyStairY0, GinnyStairY1,
                                 "the stairwell: the hole the flight comes up through, along the north " +
                                 "wall. Its open end is the corridor, where the flight arrives"),
                }),

            _ => default,
        };

        // Ginny's stair run, in the plan frame: along the north (+x) wall, its foot toward the front door (+y)
        // and its head at the back partition, where it comes up into the corridor. The run stops short of the
        // front wall so the foot has a LANDING of clear floor in front of it, y GinnyStairY1..the wall's inside
        // face (owner 2026-11-22: "theres no floor space in front of the stairs"): 1.10 m deep, more than his
        // body is wide, and as wide as the flight. Eleven treads climb the storey, a steep cottage stair. The
        // foot's interact point stands on that landing; the head's at the top of the flight. Each is where he
        // arrives when he takes the stairs from the other (InteriorStair's landing).
        public const float GinnyStairX0 = 2.25f, GinnyStairX1 = 3.30f, GinnyStairY0 = 0.30f, GinnyStairY1 = 3.15f;
        public const float GinnyStairAtX = 2.75f, GinnyStairAtY = 3.70f;
        public const float GinnyStairHeadX = 3.25f, GinnyStairHeadY = -0.20f;

        // Ginny's corridor upstairs and its two bedroom doorways (plan frame).
        public const float GinnyCorridorX0 = 2.00f;
        public const float GinnyPlayerDoorY0 = -0.70f, GinnyPlayerDoorY1 = 0.30f;
        public const float GinnyOwnerDoorY0 = -2.10f, GinnyOwnerDoorY1 = -1.10f;

        /// <summary>Ginny's ground-floor walls (plan frame; each rect runs into the wall band it meets).</summary>
        static WallRect[] GinnyGroundWalls => new[]
        {
            new WallRect(1.25f, 3.60f, 0.00f, 0.30f,
                         "the living room's back partition, north run: from the north wall to the kitchen door " +
                         "(x -0.1..1.25, wider than he is)"),
            new WallRect(-0.95f, -0.10f, 0.00f, 0.30f,
                         "the living room's back partition between the kitchen and washroom doors"),
            new WallRect(-3.60f, -2.45f, 0.00f, 0.30f,
                         "the living room's back partition, south stub: south wall to the washroom door " +
                         "(x -2.45..-0.95, wider than he is)"),
            new WallRect(-0.60f, -0.30f, -4.55f, 0.00f,
                         "the partition between the kitchen (north) and the washroom (south)"),
            new WallRect(GinnyStairX0, GinnyStairX1, GinnyStairY0, GinnyStairY1,
                         "the stair run along the north wall (painted in the room sheet)"),
            new WallRect(-3.60f, -2.45f, 3.05f, 4.55f,
                         "the painted settee against the south wall, by the front door"),
        };

        static UpperLevelPlan EmptyUpperRoom(string key) => new UpperLevelPlan(key,
            System.Array.Empty<Furnishing>(), System.Array.Empty<Furnishing>(),
            System.Array.Empty<WallRect>());

        public static string VillageUpperPlan(string shellKey) => shellKey switch
        {
            "harbourGable" => shellKey,
            "creamSaltbox" => shellKey,
            "whiteGable" => shellKey,
            _ => null,
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
        /// the doorway is at <c>(0, +Ln/2)</c>, on the same gable as the house's own door, and the hearth
        /// is at the far end.
        /// <b>Each room has its own extent</b> — from the school's 6.36 × 7.63 m to the farmhouse's
        /// 7.68 × 9.94 m — so every list below states the one it was placed against, and
        /// <c>StPetersInteriorsTests</c> checks each against its own <c>size</c> rather than against a
        /// single hard-coded pair (which is what it used to do, and it silently stopped checking the
        /// moment a second room was furnished).</para>
        ///
        /// <para><b>⚠ Turned half a turn with the village return</b> (drop 14, #898). The room rig before
        /// it drew the doorway at <c>(0, −Ln/2)</c>; every coordinate here and in
        /// <see cref="UpperLevelFor"/> was negated with the new rig, each piece turned four facings more
        /// and each bed's pillow end flipped, so a room stood from these tables lays out exactly as the
        /// placed rooms do.</para>
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
                new Furnishing("bed", new Vector2(2.35f, -1.50f), 4,
                               "along the left wall in the back half — the far corner from the door, " +
                               "which is where a bed goes in a one-room cottage. Head to +y, the kit " +
                               "bed's own head end at this offset",
                               InteriorFixture.None, PillowSide.PlusY),
                new Furnishing("seaChest", new Vector2(2.30f, 0.40f), 4,
                               "at the foot of the bed; the shortest prop in the set, so it is the " +
                               "sorting edge case as well as the fisherman's detail"),
                new Furnishing("table", new Vector2(-0.90f, 0.40f), 4,
                               "middle of the room, off centre so the doorway lane stays clear — " +
                               "THE prop the owner is asked to walk behind and then in front of"),
                new Furnishing("chair", new Vector2(-0.30f, 1.15f), 4,
                               "pulled out on the door side of the table"),
                new Furnishing("chair", new Vector2(-1.50f, 1.15f), 4,
                               "the second chair — a one-room cottage that seats two reads as lived in"),
                new Furnishing("dresser", new Vector2(-2.75f, -0.50f), 6,
                               "against the right wall, turned a quarter so its back is to the wall " +
                               "— proves a prop can sit flush without fighting the wall collider"),
            },

            // ---- the school: desks facing the stove end -------------------------------------------
            // 6.36 × 7.63 m, so x runs ±3.18 and y ±3.815 (usable ±2.88 / ±3.515 inside the walls).
            // The kit has no desk, so a table IS the desk — two of them in a row facing the teacher's,
            // which is the shape a one-room school reads as even with a house's furniture.
            "school" => new[]
            {
                new Furnishing("table", new Vector2(0f, -0.55f), 4,
                               "the pupils' bench-desk, square in the middle of the room"),
                new Furnishing("chair", new Vector2(0.60f, 0.15f), 4,
                               "two seats at it, on the door side so the class faces the front"),
                new Furnishing("chair", new Vector2(-0.60f, 0.15f), 4,
                               "the second seat"),
                new Furnishing("table", new Vector2(0f, -2.10f), 4,
                               "the teacher's desk at the stove end, facing back down the room"),
                new Furnishing("chair", new Vector2(0f, -2.85f), 4,
                               "her chair behind it, between the desk and the hearth"),
                new Furnishing("dresser", new Vector2(-2.55f, -1.30f), 6,
                               "the book press against the right wall, turned a quarter"),
                new Furnishing("seaChest", new Vector2(2.35f, -1.30f), 4,
                               "the wood box by the stove — a schoolroom that heats itself"),
            },

            // ---- the red saltbox: the plainest keeping room ---------------------------------------
            // 6.96 × 8.68 m, so x runs ±3.48 and y ±4.34 (usable ±3.18 / ±4.04).
            "redSaltbox" => new[]
            {
                new Furnishing("bed", new Vector2(2.35f, -2.30f), 4,
                               "back-left corner, the furthest point from the door. Head to +y, the " +
                               "kit bed's own head end at this offset",
                               InteriorFixture.None, PillowSide.PlusY),
                new Furnishing("seaChest", new Vector2(2.35f, -0.75f), 4,
                               "at the foot of the bed"),
                new Furnishing("table", new Vector2(-1.20f, -0.40f), 4,
                               "off centre to the right, so the doorway lane stays clear"),
                new Furnishing("chair", new Vector2(-0.70f, 0.35f), 4,
                               "pulled out on the door side"),
                new Furnishing("chair", new Vector2(-1.75f, 0.35f), 4,
                               "the second chair"),
                new Furnishing("dresser", new Vector2(-2.85f, -2.20f), 6,
                               "against the right wall in the back half, turned a quarter"),
            },

            // ---- the white farmhouse: the hall ----------------------------------------------------
            // 7.68 × 9.94 m, so x runs ±3.84 and y ±4.97 (usable ±3.54 / ±4.67) — the biggest floor on
            // the island, and the only one that seats four. No bed: this is the downstairs hall of a
            // house with a family in it, and they sleep upstairs.
            "whiteFarmhouse" => new[]
            {
                new Furnishing("table", new Vector2(-0.60f, -0.70f), 4,
                               "the family table, off centre so the doorway lane stays clear"),
                new Furnishing("chair", new Vector2(0.20f, 0.10f), 4,
                               "near side, left"),
                new Furnishing("chair", new Vector2(-1.40f, 0.10f), 4,
                               "near side, right"),
                new Furnishing("chair", new Vector2(0.20f, -1.50f), 4,
                               "far side, left — the seat you have to walk round the table to reach"),
                new Furnishing("chair", new Vector2(-1.40f, -1.50f), 4,
                               "far side, right"),
                new Furnishing("dresser", new Vector2(-3.10f, -1.60f), 6,
                               "the dresser against the right wall, turned a quarter"),
                new Furnishing("seaChest", new Vector2(2.90f, -2.60f), 4,
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
        ///
        /// <para><b>The recipe itself is <see cref="BuildingInteriorStander"/> now</b>, and the shops
        /// call the same one. What stayed here is the village's CONTENT — <see cref="FurnishingsFor"/>
        /// and the upper-storey plans — which the stander calls back into. The <c>buildingKey</c> is
        /// declared to be a VILLAGE building by this overload; <c>generalStore</c> is a key in both kits
        /// and the two carry different rooms, so the family is never inferred.</para>
        /// </summary>
        public static bool Stand(GameObject buildingGo, SpriteRenderer shell, string buildingKey,
                                 int exteriorFacing, Transform occupant, string upperPlanKey = null) =>
            BuildingInteriorStander.Stand(buildingGo, buildingKey, InteriorFamily.VillageBuilding,
                                          exteriorFacing, occupant, upperPlanKey, shell,
                                          "[StPetersInteriors]").Enterable;

        /// <summary>Stand the furniture. Each prop is its own sprite with its own Y-sort and its own
        /// footprint collider — the two things that make walking round a table work.</summary>
        public static int Furnish(Transform propsRoot, IReadOnlyList<Furnishing> furnishings, string roomKey,
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
                collider.SetPath(0, BuildingInteriorStander.ToLocal(
                    footprint.PropQuad(f.RoomMetres, prop.Entry.propFootprintWidth,
                                       prop.Entry.propFootprintDepth, f.FacingOffset),
                    world));

                Fit(go, f, interior, fixtureIdPrefix,
                    PillowReachMetresFor(f.PropKey, prop.Entry.propFootprintDepth));
                WarnIfThePillowFightsTheArt(roomKey, f);

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
        /// as one by <see cref="BuildingInterior"/>. It shares the footprint's SHAPE, its facing and the
        /// house's walls with the storey below — which is what "true to the footprint" means when the
        /// footprint has two floors in it — so nothing here re-derives any of those.</para>
        ///
        /// <para><b>It does not share the storey below's HEIGHT.</b> Everything up here is placed from
        /// the same rectangle lifted by the rig's declared storey (see <c>upperY</c> below): the sheet,
        /// the furniture, the partitions and the doorway plug all move together, because a bedroom whose
        /// floor is upstairs and whose walls are downstairs is worse than either. That lift is the whole
        /// of the 2026-08-23 amendment to ADR 0036 — before it, the two storeys were drawn at the same
        /// transform and the upper one landed pixel-for-pixel over the lower.</para>
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
        public static int StandUpperLevel(GameObject buildingGo, BuildingInterior interior,
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
            if (!string.IsNullOrEmpty(plan.UpperSheetPath))
            {
                Sprite painted = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(plan.UpperSheetPath);
                if (painted != null) upperSprite = painted;
                else
                    Debug.LogError(
                        $"[StPetersInteriors] upper-level plan '{planKey}' names the sheet " +
                        $"'{plan.UpperSheetPath}', which does not load as a sprite.");
            }
            else if (!plan.HasOwnUpperPicture)
                Debug.LogError(
                    $"[StPetersInteriors] upper-level plan '{planKey}' names no picture of its own; the " +
                    "storey above would draw the ground floor's sheet, stair and partitions included.");
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

            // --- ⭐ HOW HIGH THE STOREY IS. Read from the bake's contract — the rig DECLARES a
            //     floor-to-floor rise (the returned interiorIsoRig's dims() storeys, differenced; the rig
            //     before it, anchors().storeyZ) and the bake writes it as storeyHeightMetres — then
            //     projected once at the shared camera. Never typed here: the
            //     precedent is the facing offset three lines up the file, which is measured at bake time
            //     for exactly the same reason. A contract with no declared height reports 0, and
            //     ConfigureUpperLevel says so out loud rather than quietly drawing one storey on another.
            //     Read off the storey BELOW, not the upper room: "how far above the ground floor the next
            //     floor sits" is a fact about the storey you are standing in — its ceiling plus its
            //     joists. An `upstairs` bake, when the art lane makes one, declares the rise to ITS
            //     ceiling, which is a different storey and would land the bedroom in the roof.
            float storeyHeight = room.Entry != null ? room.Entry.storeyHeightMetres : 0f;
            float upperY = InteriorLevelLayout.UpperLevelY(storeyHeight);

            // The storey's own footprint: the same rectangle, one storey up. EVERYTHING placed on the
            // upper level is measured from this rather than from the ground floor's, so a prop, a
            // partition and the occupant standing between them cannot end up on different floors.
            var upperFootprint = new InteriorFootprint(
                (Vector2)buildingGo.transform.position + new Vector2(0f, upperY),
                room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                interiorFacing, room.Entry.facings, SpriteLightMath.GroundDepthScale,
                footprint.DoorSign, footprint.DoorAcrossMetres);

            var upperRoomGo = new GameObject(UpperRoomChildName);
            upperRoomGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperRoomGo.transform.localPosition = Vector3.zero;   // ConfigureUpperLevel lifts it
            SpriteRenderer upperRenderer =
                InteriorCatalog.ConfigureRoom(upperRoomGo, upperRoom, upperSprite);

            // The upper sheet JOINS THE Y-SORT BAND (ADR 0032), where the ground floor's stays under it
            // at a fixed order. The ground room is buried inside the footprint and has nothing drawn over
            // it; the upper room is lifted up-screen over ground that has grass, trees and a dooryard on
            // it, every one of them a four-digit Y-sorted order. Ranked by its FAR edge so everything
            // standing on that floor still outranks it — see InteriorLevelLayout.UpperRoomSortingOrder.
            upperRenderer.sortingOrder = InteriorLevelLayout.UpperRoomSortingOrder(upperFootprint);

            // --- the upper furniture.
            var upperPropsGo = new GameObject(UpperPropsChildName);
            upperPropsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperPropsGo.transform.localPosition = Vector3.zero;
            int placed = Furnish(upperPropsGo.transform, plan.Furnishings, room.Entry.key, upperFootprint,
                                 interiorFacing, room.Entry.facings, interior, idPrefix);

            // --- the colliders that exist only up here.
            var upperWallsGo = new GameObject(UpperWallsChildName);
            upperWallsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            upperWallsGo.transform.localPosition = Vector3.zero;
            BuildUpperWalls(upperWallsGo.transform, upperFootprint, plan);

            // --- the foot of the stairs, added to the storey BELOW. The GROUND footprint, because that
            //     is the floor it stands on — and the same model coordinate as the head of them, which is
            //     what makes the climb a straight rise and the way back a press where you land.
            placed += Furnish(groundProps, plan.GroundAdditions, room.Entry.key, footprint,
                              interiorFacing, room.Entry.facings, interior, idPrefix);

            // --- the ground floor's own walls, under its furniture root so they go off with it.
            if (plan.GroundWalls.Count > 0)
            {
                var groundWallsGo = new GameObject(GroundWallsChildName);
                groundWallsGo.transform.SetParent(groundProps, worldPositionStays: false);
                groundWallsGo.transform.localPosition = Vector3.zero;
                BuildRects(groundWallsGo.transform, footprint, plan.GroundWalls);
            }

            // --- a painted upstairs paints the stair on both sheets: the stand-in prop keeps its
            //     interact and loses its picture, and its footprint too (the run's own wall rect is the
            //     solid part; the head stands in the corridor, which its box would block).
            if (!string.IsNullOrEmpty(plan.UpperSheetPath))
                foreach (Transform root in new[] { groundProps, upperPropsGo.transform })
                    foreach (InteriorStair stair in root.GetComponentsInChildren<InteriorStair>(true))
                    {
                        if (stair.TryGetComponent(out SpriteRenderer sr)) sr.enabled = false;
                        if (stair.TryGetComponent(out PolygonCollider2D box)) box.enabled = false;
                    }

            // --- each half lands him at the other: up at the head, down at the foot. A plan whose two
            //     halves share one point is unchanged (the landing is straight above or below the press).
            LinkStairLandings(groundProps, upperPropsGo.transform);

            // ⚠ AFTER both furniture roots exist and BEFORE anything else looks at the interior:
            //   ConfigureUpperLevel applies the swap immediately, which is what leaves the upstairs
            //   switched OFF in the editor instead of drawn straight over the ground floor.
            interior.ConfigureUpperLevel(upperRenderer, upperPropsGo.transform, upperWallsGo.transform,
                                         upperY, groundWalls: buildingGo.transform.Find(WallsChildName));

            Debug.Log(
                $"[StPetersInteriors] upper level '{planKey}': {placed} piece(s) of furniture, " +
                $"{plan.Partitions.Count} partition(s) plus the doorway plug, drawing " +
                $"{(string.IsNullOrEmpty(plan.RoomKey) ? "the storey below's room sheet (greybox)" : "'" + plan.RoomKey + "'")}" +
                $", standing {storeyHeight:0.##} m up (the rig's declared storey) = {upperY:0.##} world " +
                $"units up the screen, sheet at sorting order {upperRenderer.sortingOrder}.");
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
        /// <summary>The name of the ground floor's own walls, under its furniture root.</summary>
        public const string GroundWallsChildName = "WallsGround";

        /// <summary>Point the foot of the stairs (ground) at the head (upstairs) and back, so a climb ends
        /// where the other half stands. Pairs the first up-half with the first down-half.</summary>
        public static void LinkStairLandings(Transform groundProps, Transform upperProps)
        {
            InteriorStair up = null, down = null;
            foreach (InteriorStair s in groundProps.GetComponentsInChildren<InteriorStair>(true))
                if (s.FromLevel == 0 && s.ToLevel == 1) { up = s; break; }
            foreach (InteriorStair s in upperProps.GetComponentsInChildren<InteriorStair>(true))
                if (s.FromLevel == 1 && s.ToLevel == 0) { down = s; break; }
            if (up == null || down == null) return;
            up.SetLanding(down.transform);
            down.SetLanding(up.transform);
        }

        /// <summary>One <see cref="PolygonCollider2D"/> per rect on <paramref name="root"/>, whose position is
        /// the building's (it hangs at local zero under the building or under a furniture root at local
        /// zero).</summary>
        static void BuildRects(Transform root, in InteriorFootprint footprint, IReadOnlyList<WallRect> rects)
        {
            Vector2 origin = root.position;
            foreach (WallRect r in rects)
            {
                var collider = root.gameObject.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;
                collider.SetPath(0, BuildingInteriorStander.ToLocal(footprint.Quad(r.X0, r.X1, r.Y0, r.Y1), origin));
            }
        }

        static void BuildUpperWalls(Transform root, in InteriorFootprint footprint, in UpperLevelPlan plan)
        {
            Vector2 origin = root.parent != null ? (Vector2)root.parent.position : Vector2.zero;

            BuildRects(root, footprint, plan.Partitions);

            // The storey's OWN outer walls, lifted with it. The ground floor's walls are drawn at the
            // ground's height, so its north wall crosses this floor UpperLevelY up the screen; they are
            // handed to the interior as ground walls and go off up here (ConfigureUpperLevel).
            foreach (Vector2[] quad in footprint.WallQuads(WallThicknessMetres, DoorwayWidthMetres))
            {
                var wall = root.gameObject.AddComponent<PolygonCollider2D>();
                wall.pathCount = 1;
                wall.SetPath(0, BuildingInteriorStander.ToLocal(quad, origin));
            }

            // The doorway, closed. Derived from the same arithmetic that cut the gap, so the two can
            // never disagree about where the door is.
            var plug = root.gameObject.AddComponent<PolygonCollider2D>();
            plug.pathCount = 1;
            plug.SetPath(0, BuildingInteriorStander.ToLocal(
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
        static void Fit(GameObject go, in Furnishing f, BuildingInterior interior, string idPrefix,
                        float pillowReachMetres)
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
                    // The interior comes along for TWO facts now. Which STOREY the bed is on, so a rest
                    // upstairs is recorded as one (ADR 0037) — the player's bed is upstairs, and an anchor
                    // that did not say so would wake them in the room below it. And the room's own FACING,
                    // which is the frame the pillow side is declared in: without it a head-to-pillow
                    // heading is a direction with no north.
                    go.AddComponent<InteriorBed>().Configure(
                        idPrefix + ".bed_player", isPlayerBed: true, ownerName: "",
                        placeName: PlayerBedPlaceName, reachMeters: BedReachMetres, interior: interior,
                        pillowSide: f.Pillow, pillowReachMetres: pillowReachMetres);
                    break;

                case InteriorFixture.OwnerBed:
                    go.AddComponent<InteriorBed>().Configure(
                        idPrefix + ".bed_owner", isPlayerBed: false, ownerName: BedOwnerName,
                        placeName: "", reachMeters: BedReachMetres, interior: interior,
                        pillowSide: f.Pillow, pillowReachMetres: pillowReachMetres);
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

        // =====================================================================================
        //  THE PILLOW — one definition, read by the builder and by content validation alike
        // =====================================================================================

        /// <summary>
        /// How far a bed's pillow centre is from the middle of the bed, in ground metres — the reach a
        /// sleeping head travels from the mattress's own point. 0 for anything that is not a bed, and for
        /// a prop the kit does not carry.
        ///
        /// <para><b>Derived from two facts neither of which lives here</b> (rule 6): the BAKE's own
        /// <c>propFootprintDepth</c> and the KIT's declared <c>PillowInsetMetres</c>, which is the rig's
        /// number. So a re-bake at a different bed size moves the pillow with the bed, and there is no
        /// tuned constant in this file pointing at where a pillow used to be.</para>
        ///
        /// <para>Public because the builder and the content test must agree about it to the last
        /// millimetre — a test that recomputed this its own way would be testing its own arithmetic.</para>
        /// </summary>
        public static float PillowReachMetresFor(string propKey, float propDepthMetres)
        {
            InteriorKit.Build? prop = InteriorKit.FindProp(propKey);
            if (prop == null || !prop.Value.IsBed) return 0f;

            return BedPillow.PillowReachMetres(propDepthMetres, prop.Value.PillowInsetMetres);
        }

        /// <summary>
        /// <b>CONTENT VALIDATION for a bed's pillow</b> — the one predicate that decides whether a
        /// furnishing row is fit to ship. True (with prose in <paramref name="fault"/>) when the row is a
        /// bed and its pillow is either missing or fighting the art; false for everything else, which is
        /// every prop that is not a bed and every bed that was declared properly.
        ///
        /// <para><b>Two faults, both silent without this.</b> A bed with NO declaration falls back to a
        /// single fixed heading, which is right for a bed in an unturned room and wrong at the other seven
        /// facings — the defect this whole seam exists to end. A bed whose declaration disagrees with the
        /// facing offset it is drawn at gets laid along the declaration with the headboard at the other
        /// end. Neither throws, neither logs, and both draw perfectly; the only way either is caught is by
        /// something asking this question.</para>
        ///
        /// <para><b>Pure, public and message-carrying</b> so the builder's console warning and the content
        /// test are the SAME check rather than two that can drift — and so a failing test says which bed
        /// and why, rather than which line number.</para>
        /// </summary>
        public static bool TryFindPillowFault(in Furnishing f, out string fault)
        {
            fault = null;
            if (!f.IsBed) return false;

            if (f.Pillow == PillowSide.Undeclared)
            {
                fault = $"the bed at {f.RoomMetres} declares no pillow side, so the sleeping pose falls " +
                        "back to one fixed heading and the fisher lies the wrong way round at every " +
                        "facing but one. Give the furnishing row a PillowSide.";
                return true;
            }

            PillowSide drawn = f.DrawnPillowSide;

            // An ODD facing offset stands the bed on the diagonal, where DrawnPillowSide has no cardinal
            // answer to offer — so there is nothing to compare against and nothing to refuse. That is a
            // separate complaint, made below, rather than a pillow that disagrees with itself.
            if (drawn == PillowSide.Undeclared)
            {
                int eighths = ((f.FacingOffset % InteriorKit.Facings) + InteriorKit.Facings)
                              % InteriorKit.Facings;
                if (eighths % 2 == 0) return false;      // the kit simply has no pillow for this prop

                fault = $"the bed at {f.RoomMetres} is turned {f.FacingOffset} facings, which is a 45deg " +
                        "step: its head end is on a diagonal, and no cardinal PillowSide is the truth " +
                        "about it. Turn a bed by an EVEN number of facings.";
                return true;
            }

            if (drawn == f.Pillow) return false;

            fault = $"the bed at {f.RoomMetres} declares its pillow on {f.Pillow}, but at facing offset " +
                    $"{f.FacingOffset} the kit draws its head end on {drawn}. The sleeper is laid along " +
                    "the declaration and the headboard ends up at the other end. Turn the bed, or fix " +
                    "the declaration — they are the same bed.";
            return true;
        }

        /// <summary>
        /// Say the fault above out loud at build time, naming the room.
        ///
        /// <para><b>Why a warning here as well as a test.</b> The test is the gate, but the owner meets
        /// this at the moment he clicks Build, looking at the room it is about; a line in the console
        /// naming the room and both sides is the difference between "fix the table" and "why is she asleep
        /// upside down". Nothing is corrected automatically: silently swinging a bed round to match a
        /// declaration would move a collider the room was laid out against.</para>
        /// </summary>
        static void WarnIfThePillowFightsTheArt(string roomKey, in Furnishing f)
        {
            if (TryFindPillowFault(f, out string fault))
                Debug.LogWarning($"[StPetersInteriors] '{roomKey}': {fault}");
        }

        /// <summary>Whose bed the one you may NOT sleep in is. Authored here rather than in the
        /// component so the refusal names the host, and so the day a second lodging exists it is a plan
        /// field rather than a second string in a component.</summary>
        public const string BedOwnerName = "Ginny";

        /// <summary>Where the player is turning in, for the notice.</summary>
        public const string PlayerBedPlaceName = "your bed at Ginny's";
    }
}
#endif
