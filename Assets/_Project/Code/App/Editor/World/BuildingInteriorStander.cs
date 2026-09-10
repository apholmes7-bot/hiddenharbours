#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's numbers
using HiddenHarbours.Art.Editor;          // InteriorCatalog, InteriorKit, ShopCatalog, ShopKit
using HiddenHarbours.World;               // BuildingInterior, InteriorFootprint
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Which kit registered the room that goes inside a building.</summary>
    ///
    /// <remarks>
    /// ⚠️ <b>This cannot be inferred from the building key.</b> <c>generalStore</c> is a key in BOTH
    /// <see cref="VillageBuildingKit"/>'s M1 set and <see cref="ShopKit"/>'s shop set, and the two carry
    /// different rooms with different measured facing offsets. A key-only lookup would quietly put the
    /// village shell's room inside the shop, or the shop's trading floor inside the house — and both
    /// would draw. Every caller therefore says which family it is placing;
    /// <see cref="BuildingInteriorStander.TryResolveFamily"/> exists for the cases that genuinely have
    /// only a key, and it REFUSES on the ambiguity rather than picking one.
    /// </remarks>
    public enum InteriorFamily
    {
        /// <summary>A village building — <see cref="InteriorCatalog"/>'s rooms, furnished, optionally with
        /// a storey above.</summary>
        VillageBuilding,

        /// <summary>A shop — <see cref="ShopCatalog"/>'s ground plan, unfurnished (the fixtures are their
        /// own family) and single-storey.</summary>
        Shop,
    }

    /// <summary>What one call to <see cref="BuildingInteriorStander.Stand"/> actually built.</summary>
    public readonly struct StandResult
    {
        /// <summary>True if a room went up and the door opens.</summary>
        public readonly bool Enterable;

        /// <summary>The family the caller asked for.</summary>
        public readonly InteriorFamily Family;

        /// <summary>The building key that was asked for.</summary>
        public readonly string Key;

        /// <summary>The key of the room that actually went up — null when nothing did.</summary>
        public readonly string RoomKey;

        /// <summary>The facing the ROOM is drawn at: the exterior facing plus the contract's MEASURED
        /// offset, which is 4 for the house family and 0 for shops.</summary>
        public readonly int InteriorFacing;

        /// <summary>The floor, in metres, as the contract measured it.</summary>
        public readonly float WidthMetres, LengthMetres;

        /// <summary>Pieces of furniture placed, both storeys. Always 0 for a shop.</summary>
        public readonly int FurnitureCount;

        /// <summary>True if a storey went up above the ground floor.</summary>
        public readonly bool HasUpperLevel;

        /// <summary>Why nothing went up, when <see cref="Enterable"/> is false. Never null in that case —
        /// "this building has no interior" is a fact a world creator has to be able to read.</summary>
        public readonly string Reason;

        StandResult(bool enterable, InteriorFamily family, string key, string roomKey, int interiorFacing,
                    float widthMetres, float lengthMetres, int furnitureCount, bool hasUpperLevel,
                    string reason)
        {
            Enterable = enterable;
            Family = family;
            Key = key;
            RoomKey = roomKey;
            InteriorFacing = interiorFacing;
            WidthMetres = widthMetres;
            LengthMetres = lengthMetres;
            FurnitureCount = furnitureCount;
            HasUpperLevel = hasUpperLevel;
            Reason = reason;
        }

        /// <summary>Nothing went up, and here is why.</summary>
        public static StandResult No(InteriorFamily family, string key, string reason) =>
            new StandResult(false, family, key, null, 0, 0f, 0f, 0, false, reason);

        /// <summary>A room went up.</summary>
        public static StandResult Yes(InteriorFamily family, string key, string roomKey, int interiorFacing,
                                      float widthMetres, float lengthMetres, int furnitureCount,
                                      bool hasUpperLevel) =>
            new StandResult(true, family, key, roomKey, interiorFacing, widthMetres, lengthMetres,
                            furnitureCount, hasUpperLevel, null);
    }

    /// <summary>
    /// <b>THE ONE RECIPE FOR STANDING AN INTERIOR</b> — the shared editor service that puts a baked room
    /// under a placed building, cuts the doorway, walls it in, furnishes it if it is a home, and hangs the
    /// storey above it if the site asked for one.
    ///
    /// <para>It exists because there were two copies of it: <c>StPetersInteriors.Stand</c> for village
    /// buildings and <c>ShopPlacement.StandInterior</c> for shops, differing only in which catalog they
    /// asked and whether they furnished. Both are now this, called with a different
    /// <see cref="InteriorFamily"/>; the builders keep their own site tables and call in. The furnishing
    /// tables and the upper-storey plans stay with the village content in
    /// <see cref="StPetersInteriors"/> — this owns the recipe, not the furniture.</para>
    ///
    /// <para><b>Re-runnable.</b> Anything a previous run left on the building is destroyed first, so a
    /// second call leaves one room and one set of furniture rather than two of each. That is what will
    /// let a world creator drop a building, turn it, and get the inside re-hung rather than doubled.</para>
    ///
    /// <para><b>⚠️ The room is NOT necessarily shown at the building's facing.</b> The exterior house rigs
    /// put their door on the <c>+Y</c> gable and the room rig puts its doorway on <c>−Y</c>, so the same
    /// cell index shows the two 180° apart. The bake MEASURES the offset that lines their door anchors up
    /// at all eight facings and writes it into the contract, and <c>InteriorFacingFor</c> is the only
    /// thing that applies it. The shop kit's measured answer is <b>0</b> and the house family's is
    /// <b>4</b> — carrying either across would put the doorway against the back wall, and it would read
    /// as an art bug rather than a placement one.</para>
    ///
    /// <para><b>⚠️ The world XY plane is the SQUASHED ground plane.</b> One metre of northward ground
    /// travel draws <c>sin 40° ≈ 0.643</c> world units up the screen
    /// (<see cref="SpriteLightMath.GroundDepthScale"/>). Every collider here therefore comes out of
    /// <see cref="InteriorFootprint"/>, which does that squash — and which is why the walls are
    /// <see cref="PolygonCollider2D"/> quads and not rotated boxes: a rotated footprint is a
    /// parallelogram, and a box collider cannot express a shear.</para>
    /// </summary>
    public static class BuildingInteriorStander
    {
        // =====================================================================================
        //  THE NAMES AND THE NUMBERS — one of each, for both families
        // =====================================================================================

        /// <summary>The child object name of a room's sprite under its building.</summary>
        public const string RoomChildName = "Interior";

        /// <summary>The child object name of the furniture root.</summary>
        public const string PropsChildName = "Furniture";

        /// <summary>The child object name of the wall colliders.</summary>
        public const string WallsChildName = "Walls";

        /// <summary>The child object name of the storey above's room sprite.</summary>
        public const string UpperRoomChildName = "InteriorUpper";

        /// <summary>The child object name of the storey above's furniture root.</summary>
        public const string UpperPropsChildName = "FurnitureUpper";

        /// <summary>The child object name of the storey above's wall colliders.</summary>
        public const string UpperWallsChildName = "WallsUpper";

        /// <summary>Wall thickness (m) the colliders are built with and the inside test uses. Thicker than
        /// the rig's drawn wall so a sprinting diagonal into a corner cannot tunnel it.</summary>
        public const float WallThicknessMetres = 0.3f;

        /// <summary>The gap left in the front wall (m) — wider than the drawn opening, because the player
        /// has width of their own. ⚠️ It is cut at the DRAWN DOOR, not at the middle of the wall.</summary>
        public const float DoorwayWidthMetres = 1.4f;

        /// <summary>What the console line is tagged with when a caller does not name itself.</summary>
        public const string DefaultLogPrefix = "[BuildingInteriorStander]";

        // =====================================================================================
        //  THE RECIPE
        // =====================================================================================

        /// <summary>
        /// Stand the interior for one placed building, if a room has been baked for it.
        ///
        /// <para>Returns a <see cref="StandResult"/> that is false — quietly, and having built nothing —
        /// when this building has no baked room. That is the normal case for most of the village, and a
        /// partial art state should still place what it can: "declared in the contract" and "has pixels
        /// on disk" are different questions.</para>
        /// </summary>
        /// <param name="buildingGo">The placed shell. Its transform position is the ground centre.</param>
        /// <param name="key">The building key, as its kit registered it.</param>
        /// <param name="family">Which kit's room to look for. ⚠️ Not inferable from the key — see
        /// <see cref="InteriorFamily"/>.</param>
        /// <param name="exteriorFacing">The facing the SHELL is drawn at. The room's own facing is derived
        /// from it by the contract's measured offset.</param>
        /// <param name="occupant">Who is inside, for the runtime re-bind. Null is fine.</param>
        /// <param name="upperPlanKey">The SITE's upper-storey plan, if it has one. Keyed by site and not
        /// by build, because two sites can share one build and only one of them has the lodger.</param>
        /// <param name="shell">The shell's renderer. Defaults to the one on <paramref name="buildingGo"/>,
        /// which is where every placement pass puts it.</param>
        /// <param name="logPrefix">What the console line is tagged with.</param>
        public static StandResult Stand(GameObject buildingGo, string key, InteriorFamily family,
                                        int exteriorFacing, Transform occupant = null,
                                        string upperPlanKey = null, SpriteRenderer shell = null,
                                        string logPrefix = null)
        {
            if (buildingGo == null)
                return StandResult.No(family, key, "there is no building object to stand it under");

            if (string.IsNullOrEmpty(logPrefix)) logPrefix = DefaultLogPrefix;
            if (shell == null) shell = buildingGo.GetComponent<SpriteRenderer>();

            // Cleared BEFORE the room is looked up, and with the full name set for both families, so a
            // re-run whose art has since gone away leaves ONE solid shell rather than a stale room under
            // a building that no longer claims one. A shop has no Furniture/Upper children, so those four
            // names are simply a no-op for it.
            ClearExisting(buildingGo.transform);

            return family == InteriorFamily.Shop
                ? StandShop(buildingGo, shell, key, exteriorFacing, occupant, logPrefix)
                : StandVillageBuilding(buildingGo, shell, key, exteriorFacing, occupant, upperPlanKey,
                                       logPrefix);
        }

        // -------------------------------------------------------------------------------------
        //  the village building: a furnished room, and sometimes a storey above it
        // -------------------------------------------------------------------------------------

        static StandResult StandVillageBuilding(GameObject buildingGo, SpriteRenderer shell, string key,
                                                int exteriorFacing, Transform occupant,
                                                string upperPlanKey, string logPrefix)
        {
            InteriorCatalog.Placement room = InteriorCatalog.FindRoom(key);
            if (!room.IsValid)
                return StandResult.No(InteriorFamily.VillageBuilding, key,
                                      "the interior kit has not baked a room under this key");

            int interiorFacing = InteriorCatalog.InteriorFacingFor(exteriorFacing);
            Sprite roomSprite = InteriorCatalog.LoadFacing(room, interiorFacing);
            if (roomSprite == null)
            {
                Debug.LogWarning(
                    $"{logPrefix} '{key}' has a baked room but no facing-{interiorFacing} " +
                    $"sprite — its sheet is missing or unsliced ({room.SheetPath}). Leaving the building " +
                    "solid and un-enterable rather than standing a blank room inside it.");
                return StandResult.No(InteriorFamily.VillageBuilding, key,
                                      $"no facing-{interiorFacing} sprite in {room.SheetPath}");
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

            // --- the furniture. The tables live with the village content, not with the recipe.
            var propsGo = new GameObject(PropsChildName);
            propsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            propsGo.transform.localPosition = Vector3.zero;
            int furnished = StPetersInteriors.Furnish(
                propsGo.transform, StPetersInteriors.FurnishingsFor(room.Entry.key), room.Entry.key,
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
            int upstairs = StPetersInteriors.StandUpperLevel(
                buildingGo, interior, room, roomSprite, footprint, interiorFacing, propsGo.transform,
                upperPlanKey);
            if (upstairs > 0) furnished += upstairs;

            Debug.Log(
                $"{logPrefix} '{key}' is enterable: room d{interiorFacing} under shell " +
                $"d{exteriorFacing} (the contract's MEASURED offset), " +
                $"{room.Entry.footprintWidthMetres:0.#}×{room.Entry.footprintLengthMetres:0.#} m of " +
                $"floor, {furnished} piece(s) of furniture, doorway on the " +
                $"{(doorSign > 0f ? "+Y" : "−Y")} wall {door.x:+0.00;-0.00} m off its centre, threshold " +
                $"at ({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#})" +
                (interior.HasUpperLevel ? ", AND a storey above it." : "."));

            return StandResult.Yes(InteriorFamily.VillageBuilding, key, room.Entry.key, interiorFacing,
                                   room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                                   furnished, interior.HasUpperLevel);
        }

        // -------------------------------------------------------------------------------------
        //  the shop: a trading floor, no furniture (the fixtures are their own family)
        // -------------------------------------------------------------------------------------

        static StandResult StandShop(GameObject shopGo, SpriteRenderer shellRenderer, string key,
                                     int exteriorFacing, Transform occupant, string logPrefix)
        {
            ShopCatalog.Placement level = ShopCatalog.FindLevel(key, ShopKit.GroundLevel);
            if (!level.IsValid)
                return StandResult.No(InteriorFamily.Shop, key,
                                      "the shop kit has not baked a ground plan under this key");

            int interiorFacing = ShopCatalog.InteriorFacingFor(exteriorFacing);
            Sprite roomSprite = ShopCatalog.LoadFacing(level, interiorFacing);
            if (roomSprite == null)
            {
                Debug.LogWarning(
                    $"{logPrefix} '{key}' has a baked ground plan but no facing-{interiorFacing} " +
                    $"sprite ({level.SheetPath}). Leaving the shop solid rather than standing a blank " +
                    "room inside it.");
                return StandResult.No(InteriorFamily.Shop, key,
                                      $"no facing-{interiorFacing} sprite in {level.SheetPath}");
            }

            // --- the plan: same position as the shell, because both pivot on the ground centre.
            var roomGo = new GameObject(RoomChildName);
            roomGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
            roomGo.transform.localPosition = Vector3.zero;
            SpriteRenderer roomRenderer = ShopCatalog.Configure(
                roomGo, level, roomSprite, ShopCatalog.RoomSortingOrder, ySort: false);

            // --- where the door is, in the plan's own model frame. MEASURED, per trade: the post
            //     office's street door sits 1.68 m left of its wall's centre and the restaurant's
            //     2.52 m. A gap cut at the wall centre would block the door the player can see and open
            //     a hole in the wall beside it — and both would draw perfectly.
            Vector2 door = ShopCatalog.DoorModelMetres(level);
            float doorSign = door.y >= 0f ? 1f : -1f;

            var footprint = new InteriorFootprint(
                shopGo.transform.position,
                level.FootprintMetres.x, level.FootprintMetres.y,
                interiorFacing, level.Entry.facings, SpriteLightMath.GroundDepthScale,
                doorSign, door.x);

            BuildWalls(shopGo.transform, footprint);

            var interior = shopGo.AddComponent<BuildingInterior>();
            interior.Configure(shellRenderer, roomRenderer, props: null,
                               level.FootprintMetres.x, level.FootprintMetres.y,
                               interiorFacing, level.Entry.facings,
                               SpriteLightMath.GroundDepthScale,
                               WallThicknessMetres, DoorwayWidthMetres,
                               doorOnPlusY: doorSign > 0f, doorAcrossMetres: door.x);
            interior.SetOccupant(occupant);

            Debug.Log(
                $"{logPrefix} '{key}' is enterable: plan d{interiorFacing} under shell " +
                $"d{exteriorFacing} (the contract's MEASURED offset of " +
                $"{ShopKit.Load()?.shellFacingOffset ?? 0}), " +
                $"{level.FootprintMetres.x:0.#}×{level.FootprintMetres.y:0.#} m of floor, street door on " +
                $"the {(doorSign > 0f ? "+Y" : "−Y")} wall {door.x:+0.00;-0.00} m off its centre, " +
                $"threshold at ({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#}).");

            return StandResult.Yes(InteriorFamily.Shop, key, level.Key, interiorFacing,
                                   level.FootprintMetres.x, level.FootprintMetres.y,
                                   furnitureCount: 0, hasUpperLevel: false);
        }

        // =====================================================================================
        //  THE STEPS BOTH FAMILIES SHARE
        // =====================================================================================

        /// <summary>
        /// Destroy anything a previous run left on this building, so re-running leaves one room and one
        /// set of furniture rather than two of each. Public because re-standing a scene full of placed
        /// pieces has to be able to take a building back to a solid shell first.
        /// </summary>
        public static void ClearExisting(Transform buildingRoot)
        {
            if (buildingRoot == null) return;

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
        /// mean a future wall that does overlap cannot silently open a gap at a corner — and a hole at a
        /// corner is a player who occasionally slips through it.</para>
        /// </summary>
        public static void BuildWalls(Transform buildingRoot, in InteriorFootprint footprint)
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

        /// <summary>World-space quad → collider-local points. <see cref="PolygonCollider2D.SetPath"/>
        /// takes LOCAL coordinates, and handing it world ones puts every wall an entire village away
        /// from the house — silently, because nothing about a collider is drawn.</summary>
        public static Vector2[] ToLocal(Vector2[] worldPoints, Vector2 origin)
        {
            var local = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++) local[i] = worldPoints[i] - origin;
            return local;
        }

        // =====================================================================================
        //  FOR THE CASES THAT HAVE ONLY A KEY
        // =====================================================================================

        /// <summary>
        /// Work out which kit a key belongs to, for a caller that genuinely has nothing else — a prefab
        /// dragged into a scene, say.
        ///
        /// <para><b>⚠️ It REFUSES on an ambiguity rather than guessing.</b> <c>generalStore</c> is a key
        /// in both kits: the village M1 set draws a shop-fronted house with a furnished room behind it,
        /// and the shop kit draws a trading floor whose measured facing offset is 0 against the house
        /// family's 4. Picking either would stand a room the world creator did not ask for, at a facing
        /// that puts the doorway against the back wall — and it would draw perfectly. A caller that hits
        /// this has to say which it meant.</para>
        /// </summary>
        /// <returns>False when no kit claims the key, or when both do.</returns>
        public static bool TryResolveFamily(string key, out InteriorFamily family, out string why)
        {
            family = InteriorFamily.VillageBuilding;

            bool village = InteriorCatalog.FindRoom(key).IsValid;
            bool shop = ShopCatalog.FindLevel(key, ShopKit.GroundLevel).IsValid;

            if (village && shop)
            {
                why = $"'{key}' is registered by BOTH the interior kit and the shop kit, and their rooms " +
                      "and measured facing offsets differ. Say which family you mean; this will not guess.";
                return false;
            }

            if (!village && !shop)
            {
                why = $"no kit has baked a room under '{key}'.";
                return false;
            }

            family = shop ? InteriorFamily.Shop : InteriorFamily.VillageBuilding;
            why = null;
            return true;
        }
    }
}
#endif
