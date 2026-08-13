#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash
using HiddenHarbours.Art.Editor;          // ShopCatalog, ShopKit
using HiddenHarbours.World;               // BuildingInterior, InteriorFootprint

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>HOW A BAKED SHOP IS STOOD UP</b> — the part of opening a shop that is about the KIT rather than
    /// about a region: the floor plan under the shell, the doorway cut where the door is drawn, and the
    /// walls that make the room a room.
    ///
    /// <para><b>Why this is shared and the SITES are not.</b> <see cref="StPetersShops"/> and
    /// <see cref="NineMileCreekShops"/> disagree about everything a place disagrees about — which trades,
    /// which ground, what each door should look at, what counts as flooded. They agree completely about
    /// what a shop IS, and that agreement is arithmetic: a MEASURED facing offset, a MEASURED door
    /// side, a MEASURED off-centre door. Two copies of that is two chances to carry the house family's
    /// offset of 4 into a kit whose answer is 0 — and a shop whose doorway is against its back wall draws
    /// perfectly from outside.</para>
    ///
    /// <para>Region-agnostic on purpose: nothing here knows a tide, a village green or a wharf. The one
    /// thing a caller passes that is not from the contract is the log prefix, so a build report still says
    /// which region asked.</para>
    /// </summary>
    public static class ShopPlacement
    {
        /// <summary>The child object name of a shop's floor plan under its shell.</summary>
        public const string InteriorChildName = "Interior";

        /// <summary>The child object name of the wall colliders.</summary>
        public const string WallsChildName = "Walls";

        /// <summary>Wall thickness (m) the colliders are built with and the inside test uses. Same value
        /// and same reasoning as <see cref="StPetersInteriors.WallThicknessMetres"/>: thicker than the
        /// rig's drawn wall so a sprinting diagonal into a corner cannot tunnel it.</summary>
        public const float WallThicknessMetres = StPetersInteriors.WallThicknessMetres;

        /// <summary>The gap left in the front wall (m) — wider than the drawn opening, because the player
        /// has width of their own. ⚠️ It is cut at the DRAWN DOOR, not at the middle of the wall.</summary>
        public const float DoorwayWidthMetres = StPetersInteriors.DoorwayWidthMetres;

        /// <summary>
        /// Stand the floor plan under a placed shell and open the door. Returns true if a room went up.
        ///
        /// <para><b>⚠️ THE ROOM IS SHOWN AT THE SAME FACING AS THE SHELL, and that is measured.</b>
        /// <see cref="ShopCatalog.InteriorFacingFor"/> reads the contract's <c>shellFacingOffset</c>,
        /// which this kit's bake measures as <b>0</b> because both its rigs put the street door on the
        /// <c>+Y</c> gable. The HOUSE family's answer is <b>4</b> — <c>interiorIsoRig</c>'s door is on
        /// <c>−Y</c> — and carrying that across would put every shop's doorway against its back wall.</para>
        ///
        /// <para><b>⚠️ And the doorway is cut where the door is DRAWN.</b> The post office's street door
        /// sits 1.68 m left of its wall's centre and the restaurant's 2.52 m, measured from the bake's own
        /// anchors. A gap cut at the wall centre would block the door the player can see and open a hole
        /// in the wall beside it — and both would draw perfectly.</para>
        /// </summary>
        public static bool StandInterior(GameObject shopGo, SpriteRenderer shellRenderer, string key,
                                         int exteriorFacing, Transform occupant, string logPrefix)
        {
            if (shopGo == null) return false;

            ClearExisting(shopGo.transform);

            var level = ShopCatalog.FindLevel(key, ShopKit.GroundLevel);
            if (!level.IsValid) return false;

            int interiorFacing = ShopCatalog.InteriorFacingFor(exteriorFacing);
            Sprite roomSprite = ShopCatalog.LoadFacing(level, interiorFacing);
            if (roomSprite == null)
            {
                Debug.LogWarning(
                    $"{logPrefix} '{key}' has a baked ground plan but no facing-{interiorFacing} " +
                    $"sprite ({level.SheetPath}). Leaving the shop solid rather than standing a blank " +
                    "room inside it.");
                return false;
            }

            // --- the plan: same position as the shell, because both pivot on the ground centre.
            var roomGo = new GameObject(InteriorChildName);
            roomGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
            roomGo.transform.localPosition = Vector3.zero;
            SpriteRenderer roomRenderer = ShopCatalog.Configure(
                roomGo, level, roomSprite, ShopCatalog.RoomSortingOrder, ySort: false);

            // --- where the door is, in the plan's own model frame. MEASURED, per trade.
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
            return true;
        }

        /// <summary>Destroy anything a previous run left on this shop, so re-running the builder leaves
        /// one room and one set of walls rather than two of each.</summary>
        static void ClearExisting(Transform shopRoot)
        {
            var existing = shopRoot.GetComponent<BuildingInterior>();
            if (existing != null) Object.DestroyImmediate(existing);

            for (int i = shopRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = shopRoot.GetChild(i);
                if (child.name == InteriorChildName || child.name == WallsChildName)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>The wall colliders: five quads on one child object. One
        /// <see cref="PolygonCollider2D"/> per wall rather than five paths on one — several paths on one
        /// collider turn their overlaps into HOLES, and a hole at a corner is a player who occasionally
        /// slips through it.</summary>
        static void BuildWalls(Transform shopRoot, in InteriorFootprint footprint)
        {
            var wallsGo = new GameObject(WallsChildName);
            wallsGo.transform.SetParent(shopRoot, worldPositionStays: false);
            wallsGo.transform.localPosition = Vector3.zero;

            Vector2 origin = shopRoot.position;
            foreach (Vector2[] quad in footprint.WallQuads(WallThicknessMetres, DoorwayWidthMetres))
            {
                var collider = wallsGo.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;

                var local = new Vector2[quad.Length];
                for (int i = 0; i < quad.Length; i++) local[i] = quad[i] - origin;
                collider.SetPath(0, local);
            }
        }
    }
}
#endif
