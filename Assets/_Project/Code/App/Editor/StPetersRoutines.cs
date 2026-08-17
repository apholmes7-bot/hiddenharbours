#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.World;               // RoutineDef, RoutineStations, RoutineLanes, VillagerRoutine

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE VILLAGE COMES ALIVE</b> — the island's named places, the lanes between them, and the wiring
    /// that puts every placed islander on their own day. <see cref="StPetersInhabitants"/> decided WHO
    /// stands where; this decides where they GO, and it is the placement pass for
    /// <c>design/npcs-and-routines.md</c> §2's routine system (P3, A Living Working Coast).
    ///
    /// <para><b>⭐ EVERY COORDINATE HERE IS DERIVED.</b> A dooryard comes out of its building's own site
    /// through <see cref="StPetersInhabitants.Dooryard"/>; the counter's customer side comes out of
    /// <see cref="StPetersBuilder.GeneralStoreCounterPos"/>; a room point comes out of the baked
    /// <c>InteriorFootprint</c>; and two of the three lanes are literally the dirt paths the terrain
    /// painter already paints (<see cref="StPetersStarterSplat.VillageToSlipPath"/> /
    /// <see cref="StPetersStarterSplat.VillageToBarHeadPath"/>) — so a villager walking east is walking on
    /// ground the world already draws as walked. Move a building and its keeper's door, dooryard, lane node
    /// and route all move with it. #345 is the standing lesson: a copied coordinate is a coordinate that
    /// will stop agreeing.</para>
    ///
    /// <para><b>⚠️ NOTHING HERE IS AUTHORED IN THE SCENE.</b> The station table and the lane network are
    /// rebuilt from scratch every run, the same rule the freezer, the wet bucket, the five islanders and
    /// the store counter already live under — a station dragged in by hand would be silently undone by the
    /// next build, and a villager sent to it would go quietly still.</para>
    ///
    /// <para><b>What it VALIDATES, loudly, against the authored world rather than against these
    /// constants.</b> A person standing in the sea and a lane running through the surf are both authoring
    /// bugs whose only symptom is a villager wading, so every station and every metre of every lane is
    /// checked against the terrain (<see cref="StPetersBuilder.TideMean"/> +
    /// <see cref="StPetersBuilder.TideAmplitude"/>, spring high water) or against the wharf deck's own
    /// footprint — the <see cref="StPetersInhabitants.Footing"/> distinction, lifted from a point to a
    /// path. Every leg's walking time is checked against the block it has to fit inside, because a
    /// villager who is still walking when her next block starts never arrives anywhere.</para>
    /// </summary>
    public static class StPetersRoutines
    {
        /// <summary>The root the station table and the lane network hang under.</summary>
        public const string RootName = "IslandRoutines";

        const string DataRoutines = "Assets/_Project/Data/Routines";

        // =====================================================================================
        //  TUNABLES (rule 6 — every number named, with the reason it is that number)
        // =====================================================================================

        /// <summary>
        /// How far past the doorway wall a villager stands when they are "inside", in metres.
        ///
        /// <para>Past the 0.3 m wall by a comfortable margin, and inside the doorway LANE the furnishing
        /// pass deliberately keeps clear (<c>StPetersInteriors.FurnishingsFor</c>: "the doorway lane —
        /// <c>x</c> within ±0.7 of centre, near the front wall — is left clear on purpose"). So a villager
        /// standing here is standing on floor, not in the bed.</para>
        /// </summary>
        public const float InsideStandInsetMetres = 1.2f;

        /// <summary>How far apart two housemates stand inside one room, in metres — half either side of the
        /// doorway lane's centre, so both are inside the ±0.7 m clear lane and neither is inside the
        /// other.</summary>
        public const float HousemateStrideMetres = 0.6f;

        /// <summary>
        /// How far out from the store's COUNTER a customer stands, in metres — the keeper is one stride
        /// behind it, the customer one stride in front, so it is
        /// <see cref="StPetersBuilder.StoreCounterStrideMetres"/> again and the two end up 3 m apart:
        /// comfortably inside the 4 m <c>StallGate.DefaultRange</c> every stall interaction uses, even with
        /// a slot's sideways offset on top. A customer further out would be a person queueing in the lane
        /// rather than standing at the counter.
        ///
        /// <para>⚠️ It was <c>× 2</c> for one build, which measured from the DOORYARD rather than from the
        /// counter and put the far slot <b>4.59 m</b> from the keeper — outside the reach, so the shop and
        /// the shopkeeper came apart into two things to walk up to.
        /// <c>TheThreeCustomerSlotsAreDistinct_AndAllWithinStallReachOfTheKeeper</c> caught it, and holds
        /// the bound rather than the number.</para>
        /// </summary>
        public const float CustomerStrideMetres = StPetersBuilder.StoreCounterStrideMetres;

        /// <summary>How far apart the counter's customer slots stand, ALONG the counter, in metres. Wide
        /// enough that three people at once read as three people and not as one flickering sprite.</summary>
        public const float CustomerSlotStrideMetres = 0.9f;

        /// <summary>
        /// How far apart the green's standing slots are, in metres.
        ///
        /// <para><b>⚠️ THE GREEN NEEDS SLOTS FOR THE SAME REASON THE COUNTER DOES, and the first build
        /// proved it.</b> Four of the six villagers end their day on the green; sent to one point they
        /// stood inside each other — the builder's own muster read "Marguerite LeBlanc at (3,7) · Rose
        /// MacIsaac at (3,7)". Wider than the counter's stride because these people are stood about
        /// talking rather than queuing at a plank: about the distance two neighbours leave between them.</para>
        /// </summary>
        public const float GreenSlotStrideMetres = 1.4f;

        /// <summary>How far off a station another villager's variant of it sits, in metres — Eileen's
        /// sunset spot beside Junior's working one. Toward the village, so the offset can only ever move a
        /// station onto HIGHER ground than the one it was derived from.</summary>
        public const float BesideStationMetres = 3f;

        /// <summary>Sample step (m) along a lane when checking it against the terrain. Finer than the
        /// narrowest thing a lane could stray into — the wharf deck is 6 m wide — so a two-metre puddle
        /// cannot hide between samples.</summary>
        public const float LaneSampleStepMetres = 1.5f;

        // ⭐ CottageGardenOffset MOVED to StPetersGinnyPlot.GardenOffset on 2026-08-16, along with the
        // cottage it was an offset FROM. It lived here while the garden was a village fixture; now that
        // Ginny has her own plot, every number about that plot belongs to the plot and this file derives
        // its stations from them. The value changed too — west was her quiet flank in the village and is
        // the FRONT of the house out in the woods, so the garden went round to the south side.

        // =====================================================================================
        //  GEOMETRY (pure — no assets, no scene, so a test can assert the whole layout without either)
        // =====================================================================================

        /// <summary>
        /// The compass heading (degrees, 0 = North, CW) from one world point toward another.
        ///
        /// <para><b>A GROUND bearing</b> (<see cref="IsoGround.BearingDegrees(Vector2, Vector2)"/>) — the
        /// same un-squash <c>BuildingFacing</c> applies, and for the same reason: world XY is the SQUASHED
        /// ground plane.</para>
        ///
        /// <para><b>⚠️ This used to be a plain world-XY <c>atan2</c>, on a premise that has since been
        /// measured false.</b> The argument was that a character is different from a building because
        /// <see cref="IsoCharacterSprite"/> reads its heading off world-XY motion, so a stand heading taken
        /// in the ground plane would disagree with the walk heading and the villager would turn on arrival.
        /// The goal was right and is unchanged — one plane, one answer — but the plane was the wrong one:
        /// ADR 0034 measured the baked character rows and they are evenly-spaced GROUND bearings, so the
        /// presenter now un-squashes its own step too. Both sides moved together; a stated stance and a
        /// walked approach still agree, and both are now agreeing with the artwork rather than with each
        /// other alone.</para>
        /// </summary>
        public static float HeadingTo(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            // Facing the camera is the friendly default for a villager with nowhere to look; IsoGround
            // answers 0 (North) for a degenerate delta, so the fallback stays explicit here.
            return d.sqrMagnitude < 1e-8f ? 180f : IsoGround.BearingDegrees(d);
        }

        /// <summary>A point <paramref name="metres"/> from <paramref name="from"/> toward
        /// <paramref name="toward"/>. The one primitive every derived station here is built out of.</summary>
        public static Vector2 Toward(Vector2 from, Vector2 toward, float metres)
        {
            Vector2 d = toward - from;
            return d.sqrMagnitude < 1e-8f ? from : from + d.normalized * metres;
        }

        /// <summary>
        /// Where the general store's counter is served FROM — the customer's side, one stride past the
        /// counter, offset along the counter by <paramref name="slot"/> so several customers at once are
        /// several people.
        ///
        /// <para>Derived from <see cref="StPetersBuilder.GeneralStoreCounterPos"/> and the outward direction
        /// the counter itself is turned by, so the slots swing with the store rather than being three more
        /// coordinates to keep in step.</para>
        /// </summary>
        public static Vector2 CustomerSlot(int slot)
        {
            Vector2 counter = StPetersBuilder.GeneralStoreCounterPos;
            Vector2 outward = StPetersBuilder.VillageGreen - counter;
            if (outward.sqrMagnitude < 1e-8f) return counter;
            outward.Normalize();
            Vector2 along = new Vector2(-outward.y, outward.x);   // along the counter face
            return counter + outward * CustomerStrideMetres + along * (slot * CustomerSlotStrideMetres);
        }

        /// <summary>
        /// Where somebody stands ON the green — offset <paramref name="slot"/> places ACROSS it, so the four
        /// villagers whose evening ends here read as a group of neighbours rather than one flickering
        /// sprite. Slot 0 is the green itself.
        ///
        /// <para>The row runs perpendicular to the hearth→green line, so it is a row facing the way the
        /// village faces and it swings with the village rather than being four more coordinates.</para>
        /// </summary>
        public static Vector2 GreenSlot(int slot)
        {
            Vector2 green = StPetersBuilder.VillageGreen;
            Vector2 fromHearth = green - new Vector2(StPetersBuilder.VillageHearthPos.x,
                                                     StPetersBuilder.VillageHearthPos.y);
            if (fromHearth.sqrMagnitude < 1e-8f) return green;
            fromHearth.Normalize();
            Vector2 across = new Vector2(-fromHearth.y, fromHearth.x);
            return green + across * (slot * GreenSlotStrideMetres);
        }

        /// <summary>
        /// A spot inside a baked room: <see cref="InsideStandInsetMetres"/> past the doorway wall, offset
        /// <paramref name="lateralMetres"/> along that wall from the DRAWN door.
        ///
        /// <para>Measured off the room's own <c>InteriorFootprint</c> — the door's wall
        /// (<c>DoorSign</c>) and its off-centre position (<c>DoorAcrossMetres</c>) — never off the house
        /// family's defaults. The shop kit's room is its shopfront seen from inside, so its street door is
        /// on the OPPOSITE wall and, on the post office, 1.68 m off its centre; a point derived from the
        /// wrong wall would put the postmistress outside her own back wall, and it would look like nothing
        /// at all.</para>
        ///
        /// <para>Clamped inside the walls, so a mis-measured anchor narrows the room rather than putting
        /// somebody in the clapboard.</para>
        /// </summary>
        public static Vector2 InsideStandPoint(in InteriorFootprint room, float lateralMetres)
        {
            float halfLength = room.LengthMetres * 0.5f;
            float halfWidth = room.WidthMetres * 0.5f;
            float inset = Mathf.Min(InsideStandInsetMetres, Mathf.Max(0f, halfLength - 0.1f));

            float across = Mathf.Clamp(room.DoorAcrossMetres + lateralMetres,
                                       -halfWidth + inset, halfWidth - inset);
            float depth = room.DoorSign * (halfLength - inset);
            return room.ModelToWorld(new Vector2(across, depth));
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Stand the routine layer up: the station table, the lane network, and one
        /// <see cref="VillagerRoutine"/> per islander who has a routine authored. Returns how many
        /// villagers were set living.
        ///
        /// <para>Null-tolerant throughout, the same way every layer on this island is: a routine asset that
        /// has not imported, an interior that has not been baked, an islander who was skipped — each is
        /// REPORTED and skipped, and the islander keeps the anchored spot they have had since #354. An
        /// island whose routine content is half-authored looks exactly like the island did before routines
        /// existed, which is the only acceptable failure mode for a layer that moves people around.</para>
        /// </summary>
        public static int Place(ITidalTerrain terrain)
        {
            var root = new GameObject(RootName);

            // ---- 1. THE INTERIORS, by the key of the building they are inside. Both placement passes
            //         have already run by the time this does, so the rooms that exist are standing.
            // FindObjectsInactive.Include, and NOT the FindObjectsSortMode overload — that one is obsolete
            // in 6000.5 (two older files in the repo still use it and warn), and a sort order is meaningless
            // here anyway: this indexes by name.
            var interiors = new Dictionary<string, BuildingInterior>(System.StringComparer.Ordinal);
            foreach (BuildingInterior bi in
                     Object.FindObjectsByType<BuildingInterior>(FindObjectsInactive.Include))
                interiors[bi.gameObject.name] = bi;

            // ---- 2. THE LANES.
            var lanes = BuildLanes(root.transform, out Dictionary<string, int> laneNodes);

            // ---- 3. THE STATIONS.
            var stations = BuildStations(root.transform, interiors, laneNodes);

            // ---- 4. VALIDATE against the authored world (never against the constants above).
            ValidateStations(stations, terrain);
            ValidateLanes(lanes, terrain);

            // ---- 5. THE PEOPLE.
            int living = WireVillagers(stations, lanes);

            Debug.Log(
                $"[StPetersRoutines] {stations.Count} station(s), {lanes.NodeCount} lane junction(s), " +
                $"{living} villager(s) living a full day. Where anyone IS at a given hour is a pure " +
                "function of (worldSeed, gameTime) — recomputed, never saved (rule 5).");

            return living;
        }

        // ---- the lane network ----------------------------------------------------------------------

        /// <summary>
        /// The island's lanes, as a tree rooted at the green — see <see cref="RoutineLaneTree"/> for why a
        /// tree. Three branches leave the green: the east–west LANE the buildings stand on, the painted
        /// dirt path EAST to the slip, and the painted dirt path WEST to the flats. Every dooryard hangs
        /// off one of them.
        ///
        /// <para><b>The two painted paths are not re-derived, they are READ.</b> The polylines come
        /// straight out of <see cref="StPetersStarterSplat"/>, which is what the terrain painter dabs the
        /// dirt along, so the lane a villager walks and the dirt the ground draws are the same points and
        /// cannot come apart. Their interior bend points become the edge's via list, REVERSED into the
        /// child→parent order <see cref="RoutineLaneTree"/> stores them in.</para>
        /// </summary>
        /// <summary>
        /// The lane table as plain arrays — no scene, no GameObject, so a test can assert the island's whole
        /// lane network (is it a tree? do the stations hang off nodes that exist? does the painted dirt come
        /// through in the right order?) without building a region.
        /// </summary>
        public readonly struct LaneTable
        {
            public readonly string[] Names;
            public readonly Vector2[] Positions;
            public readonly int[] Parents;
            public readonly int[] ViaStart;
            public readonly int[] ViaCount;
            public readonly Vector2[] Via;

            public LaneTable(string[] names, Vector2[] positions, int[] parents,
                             int[] viaStart, int[] viaCount, Vector2[] via)
            {
                Names = names; Positions = positions; Parents = parents;
                ViaStart = viaStart; ViaCount = viaCount; Via = via;
            }

            public int Count => Names.Length;

            public int IndexOf(string name) => System.Array.IndexOf(Names, name);
        }

        static RoutineLanes BuildLanes(Transform parent, out Dictionary<string, int> nodeIndex)
        {
            LaneTable table = BuildLaneTable();
            nodeIndex = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < table.Count; i++) nodeIndex[table.Names[i]] = i;

            var go = new GameObject(RoutineLanes.ObjectName);
            go.transform.SetParent(parent, worldPositionStays: false);
            var lanes = go.AddComponent<RoutineLanes>();
            lanes.Configure(table.Positions, table.Parents, table.Names,
                            table.ViaStart, table.ViaCount, table.Via);

            string fault = lanes.Validate();
            if (!string.IsNullOrEmpty(fault))
                Debug.LogError(
                    $"[StPetersRoutines] the lane network is unwalkable — {fault}. Every villager will " +
                    "stand still. This is a bug in this file, not in the content.");

            return lanes;
        }

        /// <inheritdoc cref="LaneTable"/>
        public static LaneTable BuildLaneTable()
        {
            var names = new List<string>();
            var positions = new List<Vector2>();
            var parents = new List<int>();
            var viaStart = new List<int>();
            var viaCount = new List<int>();
            var via = new List<Vector2>();

            int Add(string name, Vector2 pos, int parentIndex, Vector2[] bendsChildToParent = null)
            {
                int i = names.Count;
                names.Add(name);
                positions.Add(pos);
                parents.Add(parentIndex);
                viaStart.Add(via.Count);
                int count = bendsChildToParent != null ? bendsChildToParent.Length : 0;
                viaCount.Add(count);
                for (int k = 0; k < count; k++) via.Add(bendsChildToParent[k]);
                return i;
            }

            Vector2 green = StPetersBuilder.VillageGreen;

            // The root: where the life is (the midpoint of the hearth and the start spawn), and the point
            // every door on this island is already turned toward.
            int nGreen = Add(LaneGreen, green, RoutineLaneTree.NoParent);

            // --- the lane, north of the cottage: the store at its centre where the path from the spawn
            //     meets it (#353's own words), the school a little apart to the west, the farmhouse
            //     closing the east end, and the post office set back up the slope opposite the store.
            int nStore = Add(LaneStore, StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos), nGreen);
            Add(LaneSchool, StPetersInhabitants.Dooryard(StPetersBuilder.SchoolPos), nStore);
            Add(LaneFarmhouse, StPetersInhabitants.Dooryard(StPetersBuilder.WhiteFarmhousePos), nStore);
            Add(LanePostOffice, StPetersInhabitants.Dooryard(StPetersShops.PostOfficePos), nStore);

            // --- the two houses that come round the green rather than standing on the lane, and Ginny's
            //     hearth in the middle of it all.
            Add(LaneSaltbox, StPetersInhabitants.Dooryard(StPetersBuilder.RedSaltboxPos), nGreen);
            Add(LaneSageCottage, StPetersInhabitants.Dooryard(StPetersBuilder.SageCottagePos), nGreen);
            Add(LaneCottage, StPetersBuilder.GinnyPos, nGreen);

            // --- EAST, up off the road: Ginny's plot in the woods. The one node that is a walk rather
            //     than a step — 85 m from the green, which at her 1 m/s and a 1800 s day is 1.13 GAME
            //     HOURS each way. That number is why her routine has three blocks and not four, and why
            //     she leaves for the village at 4.5 to be standing on her mark before the game starts at
            //     six. No via-bends: there is no painted path out there yet, and cutting one is the
            //     forest lane's work (its path corridors), not this file's.
            Add(LaneGinnyPlot, StPetersGinnyPlot.Dooryard, nGreen);

            // --- WEST, on the painted dirt: out to the head of the flats where the clam digging is. The
            //     node is the digger's own validated spot rather than the bar head itself, because the bar
            //     FLOODS at every tide (crest 0.88 m against a 2.2 m spring high) and a lane must not end
            //     in the sea.
            Add(LaneFlatsHead, JuniorSpot, nGreen,
                InteriorBends(StPetersStarterSplat.VillageToBarHeadPath()));

            // --- EAST, on the painted dirt: down the island to the slip, then out along the planks. The
            //     slip end lands ON the wharf deck (BerthTo is inside StPetersWharf.DeckFootprint), which
            //     is why the deck is a legal footing for a lane and not just for a person.
            int nSlip = Add(LaneSlipHead,
                            new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y), nGreen,
                            InteriorBends(StPetersStarterSplat.VillageToSlipPath()));
            Add(LaneWharfHead, BasilSpot, nSlip);

            return new LaneTable(names.ToArray(), positions.ToArray(), parents.ToArray(),
                                 viaStart.ToArray(), viaCount.ToArray(), via.ToArray());
        }

        /// <summary>The bend points of a painted path — everything but its two ends — REVERSED, because
        /// <see cref="RoutineLaneTree"/> stores an edge's via list in child→parent order and the painter
        /// draws these from the green (the parent) outward.</summary>
        static Vector2[] InteriorBends(Vector2[] paintedPath)
        {
            if (paintedPath == null || paintedPath.Length <= 2) return System.Array.Empty<Vector2>();
            int count = paintedPath.Length - 2;
            var bends = new Vector2[count];
            for (int i = 0; i < count; i++) bends[i] = paintedPath[paintedPath.Length - 2 - i];
            return bends;
        }

        // ---- the station table ---------------------------------------------------------------------

        /// <summary>Every named place the island's routines can send anyone.</summary>
        static RoutineStations BuildStations(Transform parent,
                                            Dictionary<string, BuildingInterior> interiors,
                                            Dictionary<string, int> laneNodes)
        {
            var list = new List<RoutineStationEntry>();
            Vector2 green = StPetersBuilder.VillageGreen;

            void Outdoor(string id, Vector2 pos, float heading, string laneNode, string why) =>
                list.Add(new RoutineStationEntry
                {
                    Id = id, Position = pos, StandHeadingDegrees = heading,
                    Interior = null, LaneNodeName = laneNode, Why = why,
                });

            void Indoor(string id, string buildingKey, float lateral, string laneNode, string why)
            {
                if (!interiors.TryGetValue(buildingKey, out BuildingInterior interior) || interior == null)
                {
                    Debug.LogWarning(
                        $"[StPetersRoutines] '{id}' wanted the inside of '{buildingKey}', which has no " +
                        "baked room standing — so nobody can go in there and whoever was sent will stand " +
                        "still. Bake the room (Hidden Harbours ▸ Art ▸ Bake Interiors / Bake Shops) rather " +
                        "than re-routing the villager.");
                    return;
                }

                InteriorFootprint room = interior.Footprint;
                Vector2 inside = InsideStandPoint(room, lateral);
                list.Add(new RoutineStationEntry
                {
                    Id = id, Position = inside,
                    // Facing INTO the room, away from the door they came through — which is what somebody
                    // who has just come home is doing, and it keeps their back to the cutaway wall.
                    StandHeadingDegrees = HeadingTo(interior.DoorWorld, inside),
                    Interior = interior, LaneNodeName = laneNode, Why = why,
                });
            }

            // --- the green: the one place everybody's evening ends up, so it has SLOTS. Four of six
            //     villagers finish their day here; on one point they stood inside each other.
            for (int slot = 0; slot < GreenSlotIds.Length; slot++)
            {
                Vector2 at = GreenSlot(slot - 1);   // −1, 0, +1, +2 across the green
                Outdoor(GreenSlotIds[slot], at, HeadingTo(at, green), LaneGreen,
                        $"standing on the green, place {slot + 1} of {GreenSlotIds.Length} — the ground " +
                        "between the hearth and the spawn, where the life is");
            }

            // --- Ginny's VILLAGE MARK. Where she has stood since the opening shipped, and it does not
            //     move: a new game starts at hour 6 (GameClock._startHour) with the player waking beside
            //     her, so this is load-bearing for the onboarding whatever happens to her house. Her home
            //     went east into the woods on 2026-08-16; her working day did not follow it, because
            //     that would be rewriting the opening and the opening is the owner's to rewrite.
            Outdoor(StationGinnyVillageMark, StPetersBuilder.GinnyPos, 180f, LaneCottage,
                    "Ginny's mark in the village — the spot the opening's first conversation happens on, " +
                    "and where she is through the working day now that she lives out on her plot");

            // --- Ginny's, on her own land. Both of these DERIVE from StPetersGinnyPlot, so the day the
            //     owner walks the site and moves it, the stations follow the cottage without an edit here.
            Vector2 ginnyStep = StPetersGinnyPlot.Dooryard;
            Outdoor(StationCottageStep, ginnyStep,
                    HeadingTo(ginnyStep, StPetersGinnyPlot.Approach), LaneGinnyPlot,
                    "Ginny's own step, out on her plot in the eastern woods — turned to look back down " +
                    "the way you walk up from the village");

            Vector2 gardenSpot = StPetersGinnyPlot.GardenPos;
            Outdoor(StationCottageGarden, gardenSpot,
                    HeadingTo(gardenSpot, StPetersGinnyPlot.CottagePos), LaneGinnyPlot,
                    "the garden on the cottage's south side — the sunny flank, and the one clear of both " +
                    "the door and the sheds now that she gardens her own ground");

            // --- the dooryards: on your own step with the tea, which is where a neighbour is at seven.
            Vector2 saltbox = StPetersInhabitants.Dooryard(StPetersBuilder.RedSaltboxPos);
            Outdoor(StationSaltboxDooryard, saltbox, HeadingTo(saltbox, green), LaneSaltbox,
                    "the red saltbox's own dooryard, the green's east side");

            Vector2 farmhouse = StPetersInhabitants.Dooryard(StPetersBuilder.WhiteFarmhousePos);
            Outdoor(StationFarmhouseDooryard, farmhouse, HeadingTo(farmhouse, green), LaneFarmhouse,
                    "the white farmhouse's dooryard, closing the lane's east end");

            Vector2 sage = StPetersInhabitants.Dooryard(StPetersBuilder.SageCottagePos);
            Outdoor(StationSageDooryard, sage, HeadingTo(sage, green), LaneSageCottage,
                    "the sage cottage's dooryard — the last house before the shore");

            // --- the store. The KEEPER's spot is the storekeeper's own dooryard (where #354 put her,
            //     behind the counter #506 baked); the three CUSTOMER slots are the far side of it.
            Vector2 keeper = StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos);
            Outdoor(StationStoreCounter, keeper, HeadingTo(keeper, green), LaneStore,
                    "behind the store counter — the storekeeper's own doorstep, unmoved");
            for (int slot = 0; slot < CustomerSlotIds.Length; slot++)
            {
                Vector2 at = CustomerSlot(slot - 1);   // −1, 0, +1 along the counter
                Outdoor(CustomerSlotIds[slot], at, HeadingTo(at, keeper), LaneStore,
                        $"customer slot {slot + 1} at the store counter, a stride the near side of it");
            }

            // --- the post office: a door to post at from outside, and the counter inside it.
            Vector2 postDoor = StPetersInhabitants.Dooryard(StPetersShops.PostOfficePos);
            Outdoor(StationPostOfficeDoor, postDoor, HeadingTo(postDoor, StPetersShops.PostOfficePos),
                    LanePostOffice, "the post office's own doorstep — where you hand in the day's mail");

            // --- the working spots, west and east.
            Outdoor(StationFlatsHead, JuniorSpot, 270f, LaneFlatsHead,
                    "the head of the flats, a few strides off the wet bucket — where a man who digs for a " +
                    "living stands, looking west out over the bar (#354's own spot, unmoved)");
            Vector2 lookout = Toward(JuniorSpot, green, BesideStationMetres);
            Outdoor(StationFlatsLookout, lookout, 270f, LaneFlatsHead,
                    "a few strides inland of the digging, looking west — the sun goes down over the bar, " +
                    "and this is the only place on the island you can watch it do that");
            Outdoor(StationSlipHead, new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y),
                    HeadingTo(new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y), BasilSpot),
                    LaneSlipHead,
                    "the shoreward end of the slip, on the planks — where a boat's line gets taken");
            Outdoor(StationWharfHead, BasilSpot, 90f, LaneWharfHead,
                    "up the wharf from the slip head, watching the water (#354's own spot, unmoved) — " +
                    "the dock is the east end, so this is who is there when you come home under power");

            // --- the insides. Homes first, then the two workplaces that are indoors.
            Indoor(StationHomeSaltbox, "redSaltbox", 0f, LaneSaltbox,
                   "the saltbox's keeping room — Rose's");
            Indoor(StationHomeFarmhouse, "whiteFarmhouse", 0f, LaneFarmhouse,
                   "the farmhouse hall — the biggest floor on the island, and the only one that seats four");
            Indoor(StationHomeSageA, "sageCottage", -HousemateStrideMetres, LaneSageCottage,
                   "the fisherman's cottage, one side of the doorway lane — Junior's");
            Indoor(StationHomeSageB, "sageCottage", HousemateStrideMetres, LaneSageCottage,
                   "the same cottage, the other side — Basil's. Two men of the water under one roof, and " +
                   "two people leaving one door at dawn is a thing you can watch happen");
            Indoor(StationHomeStore, "generalStore", 0f, LaneStore,
                   "the store's own floor — the storekeeper lives over her shop, which is why the shop is " +
                   "dark at night by the same data that draws her asleep");
            Indoor(StationSchoolDesk, "school", 0f, LaneSchool,
                   "the schoolroom — the teacher's desk at the stove end, facing back down the room");
            Indoor(StationPostOfficeCounter, "postOffice", 0f, LanePostOffice,
                   "behind the post office counter. ⚠️ Its street door is on the OPPOSITE wall to a " +
                   "house's and 1.68 m off its centre, both MEASURED — see InsideStandPoint");

            // Ids are the resolver's only handle, so a duplicate is a silent mis-route.
            var duplicates = list.GroupBy(s => s.Id, System.StringComparer.Ordinal)
                                 .Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            if (duplicates.Length > 0)
                Debug.LogError(
                    $"[StPetersRoutines] duplicate station id(s): {string.Join(", ", duplicates)}. A " +
                    "routine naming one would resolve to whichever happened to be added first. This is a " +
                    "bug in this file.");

            foreach (RoutineStationEntry s in list)
                if (!laneNodes.ContainsKey(s.LaneNodeName))
                    Debug.LogError(
                        $"[StPetersRoutines] station '{s.Id}' hangs off lane node '{s.LaneNodeName}', " +
                        "which this file does not build. Whoever is sent there will stand still.");

            // Which of the DECLARED stations actually stood. A missing one is always an unbaked interior —
            // reported above, one line per room — and this is the summary the owner reads to know whether
            // "the postmistress is standing still" is a content gap or an art gap.
            var stood = new HashSet<string>(list.Select(s => s.Id), System.StringComparer.Ordinal);
            var absent = AllStationIds.Where(id => !stood.Contains(id)).ToArray();
            var undeclared = stood.Where(id => !AllStationIds.Contains(id)).ToArray();
            if (undeclared.Length > 0)
                Debug.LogError(
                    $"[StPetersRoutines] built station(s) not in AllStationIds: " +
                    $"{string.Join(", ", undeclared)}. The declared list is what the content test checks " +
                    "routines against, so an undeclared station is one a typo could never be caught by.");
            if (absent.Length > 0)
                Debug.LogWarning(
                    $"[StPetersRoutines] {absent.Length} declared station(s) did NOT stand: " +
                    $"{string.Join(", ", absent)} — every one of these is an interior whose room has not " +
                    "been baked. Whoever a routine sends there stays anchored.");

            var go = new GameObject(RoutineStations.ObjectName);
            go.transform.SetParent(parent, worldPositionStays: false);
            var stations = go.AddComponent<RoutineStations>();
            stations.Configure(list.ToArray());
            return stations;
        }

        // ---- validation against the authored world -------------------------------------------------

        /// <summary>
        /// Every station on legal footing: dry at spring high water, on the wharf deck, or inside a room.
        /// Checked against the TERRAIN rather than against the constants above — the #345 lesson, and the
        /// same reason <see cref="StPetersInhabitants"/> carries a <see cref="StPetersInhabitants.Footing"/>
        /// per person. Loud, because a villager standing in the sea is an authoring bug to fix and not a
        /// station to quietly drop.
        /// </summary>
        static void ValidateStations(RoutineStations stations, ITidalTerrain terrain)
        {
            if (stations == null) return;
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            Rect deck = StPetersWharf.DeckFootprint();
            int wet = 0;

            foreach (RoutineStationEntry s in stations.Stations)
            {
                if (s.Indoors) continue;                       // a floor is a floor
                if (deck.Contains(s.Position)) continue;       // planks over water are still planks
                if (terrain == null) continue;

                float ground = terrain.ElevationAt(s.Position);
                if (ground > springHigh) continue;

                wet++;
                Debug.LogError(
                    $"[StPetersRoutines] station '{s.Id}' is at {s.Position} where the ground is " +
                    $"{ground:0.00} m — at or below spring high water ({springHigh:0.00} m), and it is not " +
                    $"on the wharf deck ({deck}). Villagers do not stand in the sea. It stands {s.Why}. " +
                    "Move the derivation; do not lower the tide.");
            }

            if (wet == 0)
                Debug.Log($"[StPetersRoutines] all {stations.Count} station(s) on legal footing — dry " +
                          $"above {springHigh:0.00} m, on the wharf deck, or on a baked floor.");
        }

        /// <summary>
        /// Every metre of every lane on legal footing, by the same rule. A lane is sampled rather than
        /// spot-checked at its ends: the whole point of a lane is the ground BETWEEN two places, and the
        /// one that matters most here — the painted dirt east to the slip — crosses the beach toe on its
        /// way onto the planks.
        /// </summary>
        static void ValidateLanes(RoutineLanes lanes, ITidalTerrain terrain)
        {
            if (lanes == null || terrain == null) return;
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            Rect deck = StPetersWharf.DeckFootprint();

            int nodeCount = lanes.NodeCount;
            var worst = new Dictionary<string, (Vector2 at, float ground)>(System.StringComparer.Ordinal);

            for (int i = 0; i < nodeCount; i++)
            {
                int p = lanes.NodeParents[i];
                if (p < 0 || p >= nodeCount) continue;

                // Walk the edge as the villager would: child, its bends, then the parent.
                var chain = new List<Vector2> { lanes.NodePositions[i] };
                for (int k = 0; k < lanes.ViaCount[i]; k++) chain.Add(lanes.Via[lanes.ViaStart[i] + k]);
                chain.Add(lanes.NodePositions[p]);

                for (int seg = 0; seg + 1 < chain.Count; seg++)
                {
                    Vector2 a = chain[seg], b = chain[seg + 1];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / LaneSampleStepMetres));
                    for (int t = 0; t <= steps; t++)
                    {
                        Vector2 at = Vector2.Lerp(a, b, t / (float)steps);
                        if (deck.Contains(at)) continue;
                        float ground = terrain.ElevationAt(at);
                        if (ground > springHigh) continue;

                        string key = lanes.NodeNames[i];
                        if (!worst.TryGetValue(key, out var held) || ground < held.ground)
                            worst[key] = (at, ground);
                    }
                }
            }

            if (worst.Count == 0)
            {
                Debug.Log($"[StPetersRoutines] every lane is dry above spring high water " +
                          $"({springHigh:0.00} m) or on the wharf deck, sampled every " +
                          $"{LaneSampleStepMetres:0.#} m.");
                return;
            }

            foreach (var kv in worst)
                Debug.LogError(
                    $"[StPetersRoutines] the lane from '{kv.Key}' to its junction crosses ground at " +
                    $"{kv.Value.at} that is only {kv.Value.ground:0.00} m — at or below spring high water " +
                    $"({springHigh:0.00} m) and off the wharf deck. Villagers walking it would wade twice " +
                    "a day. Re-route the lane (or move the node inland); do not lower the tide.");
        }

        // ---- the people ----------------------------------------------------------------------------

        /// <summary>
        /// Put every islander who has a routine authored onto their day.
        ///
        /// <para>Matched by ASSET NAME: <see cref="StPetersInhabitants"/> names an islander's GameObject
        /// after their <c>NpcDef</c> asset, and Ginny's is named after hers too, so
        /// <c>routine.Npc.name</c> IS the object to find. Matching on the def rather than on a hard-coded
        /// list is what makes adding the next person's day an asset and not a code change (rule 2).</para>
        ///
        /// <para><b>Their Y-sort goes DYNAMIC, and they tread the grass.</b> An anchored islander sorted
        /// once on enable and stood its dispatch down, which was right while nobody walked; a villager who
        /// crosses the green has to re-sort as they go or they would draw in front of the house they just
        /// walked behind. And a walker who crosses the green should part it — see
        /// <see cref="WireLivingGrass"/>.</para>
        /// </summary>
        static int WireVillagers(RoutineStations stations, RoutineLanes lanes)
        {
            RoutineDef[] routines = LoadRoutines();
            if (routines.Length == 0)
            {
                Debug.LogWarning(
                    $"[StPetersRoutines] no routine assets under {DataRoutines} — every islander stays " +
                    "anchored, exactly as they were before this pass existed. Run Hidden Harbours ▸ World " +
                    "▸ Build St Peters Routines to author them.");
                return 0;
            }

            // Everybody placed who can be talked to — the five islanders and the opening cast — indexed by
            // the name their NpcDef gave them.
            var placed = new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
            foreach (Interactable it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include))
                placed[it.gameObject.name] = it.gameObject;

            int living = 0;
            var report = new List<string>();
            var missing = new List<string>();
            var muster = new List<(string who, RoutinePlan plan)>();
            float secondsPerGameHourPreview = GameConfig.DefaultSecondsPerDay / RoutineSchedule.HoursPerDay;

            foreach (RoutineDef routine in routines)
            {
                if (routine == null) continue;
                if (routine.Npc == null)
                {
                    missing.Add($"{routine.Id} (no NPC)");
                    continue;
                }
                if (!placed.TryGetValue(routine.Npc.name, out GameObject go) || go == null)
                {
                    missing.Add($"{routine.Id} → '{routine.Npc.name}' is not placed in this region");
                    continue;
                }

                var villager = go.GetComponent<VillagerRoutine>();
                if (villager == null) villager = go.AddComponent<VillagerRoutine>();
                villager.Configure(routine, stations, lanes, go.GetComponent<SpriteRenderer>());

                WireLivingGrass(go);

                // Report the day the way the owner will read it, and check that every walk fits inside the
                // block it has to happen in — the one authoring error the runtime cannot recover from
                // gracefully, because a villager still walking at the next departure never arrives.
                RoutinePlan plan = RoutinePlanner.Build(routine, stations, lanes, PreviewSeed,
                                                        out _, out string problem);
                if (plan == null)
                {
                    Debug.LogWarning(
                        $"[StPetersRoutines] {routine.Npc.DisplayName}'s day does not resolve: {problem}. " +
                        "She keeps her anchored spot.", go);
                    missing.Add($"{routine.Id} ({problem})");
                    continue;
                }

                float secondsPerGameHour = secondsPerGameHourPreview;
                var overruns = new List<string>();
                float longest = 0f;
                for (int i = 0; i < plan.BlockCount; i++)
                {
                    float travel = plan.TravelHours(i, secondsPerGameHour);
                    float block = RoutineSchedule.GapHours(plan.DepartureHours, i);
                    if (travel > longest) longest = travel;
                    if (travel > block)
                        overruns.Add($"block {i} ({routine.Entries[i].StationId}) needs {travel:0.00} h of " +
                                     $"walking in a {block:0.00} h block");
                }
                if (overruns.Count > 0)
                    Debug.LogError(
                        $"[StPetersRoutines] {routine.Npc.DisplayName}'s day cannot be walked: " +
                        $"{string.Join("; ", overruns)}. She would still be on the lane when her next " +
                        "block began. Give the block more hours, shorten the walk, or walk her faster — " +
                        "all three are one-line edits on her routine asset.", routine);

                living++;
                report.Add($"{routine.Npc.DisplayName}: {plan.BlockCount} block(s), longest walk " +
                           $"{longest * 60f:0} game min");
                muster.Add((routine.Npc.DisplayName, plan));
            }

            Debug.Log($"[StPetersRoutines] {living} of {routines.Length} routine(s) wired: " +
                      $"{string.Join(" · ", report)}.");

            LogTheDay(muster, secondsPerGameHourPreview);

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[StPetersRoutines] not wired: {string.Join(" · ", missing)}. Those people keep the " +
                    "anchored spot they had before — a visible absence rather than an invisible one.");

            return living;
        }

        /// <summary>
        /// The LIVING-GRASS hooks a walking villager needs, in one idempotent pass (the owner rebuilds the
        /// region repeatedly): the Y-sort goes DYNAMIC (see <see cref="WireVillagers"/>), and a
        /// <see cref="GrassFootstep"/> makes the meadow answer them — the trail is a POOL of
        /// <see cref="GrassFootstep.MaxWalkers"/> slots now (#517), so a villager's trail no longer erases
        /// the player's. Left at the default <see cref="GrassFootstep.Priority"/> 0: the player is built at
        /// <see cref="GrassFootstep.PlayerPriority"/> and eviction is strictly-below, so a full pool drops a
        /// villager's trail (cosmetic), never the player's — and equals never churn each other.
        /// </summary>
        public static void WireLivingGrass(GameObject go)
        {
            var ysort = go.GetComponent<YSortSprite>();
            if (ysort != null) ysort.Dynamic = true;

            if (go.GetComponent<GrassFootstep>() == null)
                go.AddComponent<GrassFootstep>();
        }

        /// <summary>
        /// The hours the muster below is taken at — dawn, mid-morning, the noon queue, the evening on the
        /// green, and after everybody has gone in.
        /// </summary>
        public static readonly float[] MusterHours = { 6f, 9f, 13f, 19f, 23f };

        /// <summary>
        /// <b>WHO IS WHERE, at five hours of the day</b> — the builder's own read-out of what it just wired,
        /// so the owner can see the village happen without playing a thirty-minute day to find out.
        ///
        /// <para>Every figure is <see cref="RoutinePlan.SampleAt"/>, i.e. the same function the runtime
        /// reads, so this is a REPORT and not a second model of the day. It is the closest thing to a proof
        /// sheet that costs nothing: a rendered one would need a camera and a graphics device, and would say
        /// less than the numbers.</para>
        /// </summary>
        static void LogTheDay(List<(string who, RoutinePlan plan)> muster, float secondsPerGameHour)
        {
            if (muster.Count == 0) return;

            var lines = new List<string>();
            foreach (float hour in MusterHours)
            {
                var at = new List<string>();
                foreach ((string who, RoutinePlan plan) in muster)
                {
                    RoutinePose pose = plan.SampleAt(hour, secondsPerGameHour);
                    string what = pose.Shelter == RoutineShelter.Outdoors
                        ? (pose.Walking ? "walking" : "at")
                        : (pose.Walking ? "walking (indoors)" : "INSIDE");
                    at.Add($"{who} {what} ({pose.Position.x:0},{pose.Position.y:0}) [{pose.Activity}]");
                }
                lines.Add($"  {hour:00}:00 — {string.Join(" · ", at)}");
            }

            Debug.Log("[StPetersRoutines] THE VILLAGE'S DAY, as this build wired it:\n" +
                      string.Join("\n", lines) +
                      "\n  (sampled through RoutinePlan.SampleAt at the preview seed " + PreviewSeed +
                      " — the same function the runtime reads, so this is a report and not a second model.)");
        }

        /// <summary>
        /// The seed the BUILDER previews a day with. It is not the runtime's — the runtime takes the live
        /// <c>IEnvironmentService.WorldSeed</c> — and it does not need to be: the jitter it drives is
        /// clamped to well under half of the smallest block, so a walk that fits at this seed fits at every
        /// seed. Fixed rather than random so two builder runs report the same numbers.
        /// </summary>
        public const int PreviewSeed = 0;

        /// <summary>Every routine asset under <c>Data/Routines</c>, in a stable order (by id) so two
        /// builder runs wire them identically and the log reads the same.</summary>
        public static RoutineDef[] LoadRoutines() =>
            AssetDatabase.FindAssets("t:RoutineDef", new[] { DataRoutines })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<RoutineDef>)
                         .Where(r => r != null)
                         .OrderBy(r => r.Id, System.StringComparer.Ordinal)
                         .ToArray();

        // =====================================================================================
        //  THE NAMES (ids are content — one place, append-only, snake_case)
        // =====================================================================================

        // Lane junctions.
        public const string LaneGreen = "green";
        public const string LaneStore = "lane_store";
        public const string LaneSchool = "lane_school";
        public const string LaneFarmhouse = "lane_farmhouse";
        public const string LanePostOffice = "lane_post_office";
        public const string LaneSaltbox = "yard_saltbox";
        public const string LaneSageCottage = "yard_sage_cottage";
        public const string LaneCottage = "yard_cottage";

        /// <summary>
        /// Ginny's plot, out in the eastern woods — the one lane node that is not in or beside the
        /// village. It hangs off the green like every other yard, which makes her walk in a straight run
        /// down the island; there is no painted path out there to give it bends yet, and inventing one
        /// would be the forest lane's road, not this file's.
        /// </summary>
        public const string LaneGinnyPlot = "yard_ginny_plot";
        public const string LaneFlatsHead = "flats_head";
        public const string LaneSlipHead = "slip_head";
        public const string LaneWharfHead = "wharf_head";

        // Stations.
        const string P = "station.st_peters.";
        public const string StationGreenA = P + "green_a";
        public const string StationGreenB = P + "green_b";
        public const string StationGreenC = P + "green_c";
        public const string StationGreenD = P + "green_d";
        /// <summary>Her own step. ⚠ It FOLLOWS THE COTTAGE — since 2026-08-16 that is out on her plot in
        /// the woods, not in the village. The id is unchanged because ids are append-only and stable
        /// (CLAUDE.md §5) and because it still means exactly what it says: the step of Ginny's
        /// cottage.</summary>
        public const string StationCottageStep = P + "cottage_step";

        /// <summary>Her garden. Follows the cottage too — see <see cref="StationCottageStep"/>.</summary>
        public const string StationCottageGarden = P + "cottage_garden";

        /// <summary>
        /// <b>The mark the opening's first conversation happens on</b>, in the village, four metres off
        /// the green. NEW on 2026-08-16 and the reason the cottage could move at all: a new game starts
        /// at hour 6 with the player waking beside Ginny, so when her HOME went east this had to stay
        /// behind as its own declared spot rather than travel with the step it used to be part of.
        /// </summary>
        public const string StationGinnyVillageMark = P + "ginny_village_mark";
        public const string StationSaltboxDooryard = P + "saltbox_dooryard";
        public const string StationFarmhouseDooryard = P + "farmhouse_dooryard";
        public const string StationSageDooryard = P + "sage_dooryard";
        public const string StationStoreCounter = P + "store_counter";
        public const string StationStoreCustomerA = P + "store_customer_a";
        public const string StationStoreCustomerB = P + "store_customer_b";
        public const string StationStoreCustomerC = P + "store_customer_c";
        public const string StationPostOfficeDoor = P + "post_office_door";
        public const string StationPostOfficeCounter = P + "post_office_counter";
        public const string StationSchoolDesk = P + "school_desk";
        public const string StationFlatsHead = P + "flats_head";
        public const string StationFlatsLookout = P + "flats_lookout";
        public const string StationSlipHead = P + "slip_head";
        public const string StationWharfHead = P + "wharf_head";
        public const string StationHomeSaltbox = P + "home_saltbox";
        public const string StationHomeFarmhouse = P + "home_farmhouse";
        public const string StationHomeSageA = P + "home_sage_a";
        public const string StationHomeSageB = P + "home_sage_b";
        public const string StationHomeStore = P + "home_store";

        /// <summary>The counter's three customer slots, in the order they are laid along it.</summary>
        public static readonly string[] CustomerSlotIds =
            { StationStoreCustomerA, StationStoreCustomerB, StationStoreCustomerC };

        /// <summary>The green's four standing slots, in the order they are laid across it. Index 1 is the
        /// green itself (offset 0) — Ginny's, because it is the one nearest her own step.</summary>
        public static readonly string[] GreenSlotIds =
            { StationGreenA, StationGreenB, StationGreenC, StationGreenD };

        /// <summary>
        /// Every station this region declares. <see cref="BuildStations"/> checks that it produced exactly
        /// this set (less any interior that has not been baked), and <c>StPetersRoutineContentTests</c>
        /// checks that every routine asset names one of them — so a typo in a routine is a red test rather
        /// than a villager who quietly stands still for the rest of the milestone.
        /// </summary>
        public static readonly string[] AllStationIds =
        {
            StationGreenA, StationGreenB, StationGreenC, StationGreenD,
            StationCottageStep, StationCottageGarden, StationGinnyVillageMark,
            StationSaltboxDooryard, StationFarmhouseDooryard, StationSageDooryard,
            StationStoreCounter, StationStoreCustomerA, StationStoreCustomerB, StationStoreCustomerC,
            StationPostOfficeDoor, StationPostOfficeCounter, StationSchoolDesk,
            StationFlatsHead, StationFlatsLookout, StationSlipHead, StationWharfHead,
            StationHomeSaltbox, StationHomeFarmhouse, StationHomeSageA, StationHomeSageB,
            StationHomeStore,
        };

        /// <summary>Every lane junction this region declares — the names a station may hang off.</summary>
        public static readonly string[] AllLaneNodeNames =
        {
            LaneGreen, LaneStore, LaneSchool, LaneFarmhouse, LanePostOffice,
            LaneSaltbox, LaneSageCottage, LaneCottage, LaneGinnyPlot,
            LaneFlatsHead, LaneSlipHead, LaneWharfHead,
        };

        // =====================================================================================
        //  THE TWO SPOTS THIS FILE SHARES WITH THE ANCHORED PLACEMENT
        // =====================================================================================

        /// <summary>Junior's working spot — the head of the flats. Taken from
        /// <see cref="StPetersInhabitants"/>' own derivation rather than retyped, so the man and his
        /// station cannot end up in two places.</summary>
        public static Vector2 JuniorSpot => PersonSpot("JuniorPoirier");

        /// <summary>Basil's spot up the wharf, by the same rule.</summary>
        public static Vector2 BasilSpot => PersonSpot("BasilSamson");

        static Vector2 PersonSpot(string assetName)
        {
            foreach (StPetersInhabitants.Person p in StPetersInhabitants.People)
                if (string.Equals(p.AssetName, assetName, System.StringComparison.Ordinal))
                    return p.Position;

            Debug.LogError(
                $"[StPetersRoutines] '{assetName}' is not in StPetersInhabitants.People any more, so the " +
                "lane that ends at their working spot has nowhere to end. Falling back to the green, " +
                "which is wrong but visible.");
            return StPetersBuilder.VillageGreen;
        }
    }
}
#endif
