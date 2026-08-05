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

            public Furnishing(string propKey, Vector2 roomMetres, int facingOffset, string why)
            {
                PropKey = propKey; RoomMetres = roomMetres; FacingOffset = facingOffset; Why = why;
            }
        }

        /// <summary>
        /// How the pilot room is furnished. A handful, honestly placed — enough to prove the prop
        /// pipeline end to end (bake → slice → place → collide → Y-sort past), not a decorating pass.
        ///
        /// <para>Coordinates are in the ROOM's frame, which is what the owner sees in the room art: the
        /// doorway is at <c>(0, −Ln/2)</c> at the bottom of the picture and the hearth at the top. The
        /// sage cottage is 6.6 × 8.05 m, so <c>x</c> runs ±3.3 and <c>y</c> ±4.03.</para>
        ///
        /// <para>The doorway lane — <c>x</c> within ±0.7 of centre, near the front wall — is left clear
        /// on purpose: a prop parked in the threshold is a prop you cannot get past, and its collider
        /// would close the one gap in the house.</para>
        /// </summary>
        public static IReadOnlyList<Furnishing> FurnishingsFor(string roomKey) =>
            roomKey == "sageCottage"
                ? new[]
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
                }
                : System.Array.Empty<Furnishing>();

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
                                 int exteriorFacing, Transform occupant)
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

            // --- the footprint everything else is measured from.
            var footprint = new InteriorFootprint(
                buildingGo.transform.position,
                room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                interiorFacing, room.Entry.facings, SpriteLightMath.GroundDepthScale);

            // --- the walls. Always on, from both sides: the cutaway that drops the two camera-facing
            //     walls is a courtesy to the camera, not a hole in the house.
            BuildWalls(buildingGo.transform, footprint);

            // --- the furniture.
            var propsGo = new GameObject(PropsChildName);
            propsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
            propsGo.transform.localPosition = Vector3.zero;
            int furnished = Furnish(propsGo.transform, room.Entry.key, footprint, interiorFacing,
                                    room.Entry.facings);

            // --- the behaviour.
            var interior = buildingGo.AddComponent<BuildingInterior>();
            interior.Configure(shell, roomRenderer, propsGo.transform,
                               room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                               interiorFacing, room.Entry.facings,
                               SpriteLightMath.GroundDepthScale,
                               WallThicknessMetres, DoorwayWidthMetres);
            interior.SetOccupant(occupant);

            Debug.Log(
                $"[StPetersInteriors] '{buildingKey}' is enterable: room d{interiorFacing} under shell " +
                $"d{exteriorFacing} (the contract's MEASURED offset), " +
                $"{room.Entry.footprintWidthMetres:0.#}×{room.Entry.footprintLengthMetres:0.#} m of " +
                $"floor, {furnished} piece(s) of furniture, threshold at " +
                $"({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#}).");
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
                    child.name == WallsChildName)
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
        static int Furnish(Transform propsRoot, string roomKey, in InteriorFootprint footprint,
                           int interiorFacing, int facings)
        {
            int placed = 0;
            foreach (Furnishing f in FurnishingsFor(roomKey))
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

                placed++;
            }

            return placed;
        }

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
