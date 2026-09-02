#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;               // StationPieceDef / StationBlocker / StationSurface
using HiddenHarbours.Art.Editor;        // StationCatalog
using HiddenHarbours.World;             // BuildingInterior / InteriorFootprint

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE WAY INTO A STATION STOREFRONT</b> — the half of a C-store that was never built. The room
    /// itself has shipped since #612/#613 and has stood at Route 91 since #626: a 11.2 × 7.8 m sales
    /// floor, seven fittings with colliders, six things to buy, an entry and a service door, drawn one
    /// sorting order under its own shell. What it has never had is a DOOR YOU CAN WALK THROUGH.
    ///
    /// <para><b>What was actually wrong, measured.</b> <c>StationPieceDefBuilder</c> turns every
    /// blocker whose treatment is <c>wall</c> into one collider over its own footprint, and a
    /// storefront's <c>building</c> blocker IS its whole plan — 11.6 × 8.2 m of it. So the prefab ships
    /// a single <see cref="PolygonCollider2D"/> filling the building solid, with a furnished room drawn
    /// inside it that nothing can reach. That is not a bug in the bake: a blocker footprint is the
    /// ground a piece COVERS, and the building does cover it. It is only wrong once something is meant
    /// to be in there. This class is the "once".</para>
    ///
    /// <para><b>⭐ It is the owner's standing ruling, not a nicety.</b> 2026-08-11: every placed
    /// building gets a seamless, walkable, true-to-footprint interior — <i>no façade-only buildings,
    /// ever</i> (ADR 0036, Ruling 1b). A C-store with a baked sales floor behind a solid wall is a
    /// façade with the interior already paid for.</para>
    ///
    /// <para><b>The shape of the fix is <see cref="ShopPlacement"/>'s, deliberately.</b> Cut the solid
    /// footprint down to a RING of walls with a gap where the door is drawn, then let
    /// <see cref="BuildingInterior"/> swap shell for room at the threshold. Every piece of that already
    /// exists and is tested; what this adds is the arithmetic that reads a station shell's geometry
    /// instead of a shop's.</para>
    ///
    /// <para><b>⭐ AND THE SQUASH IS THE SAME ONE THE SHOP PASSES.</b> <see cref="ShopPlacement"/> hands
    /// <see cref="InteriorFootprint"/> <c>SpriteLightMath.GroundDepthScale</c> (0.643) because the house
    /// family's rooms are stated in ground metres and drawn into a squashed picture — and so are this
    /// kit's. The squash is baked into every cell's pixels, not applied at render time, so a wall ring
    /// that is to stand on the drawn walls carries it too (ADR 0042). Every other station collider is
    /// placed by <see cref="StationCatalog.LocalToWorld"/>, which is the same rotate-then-squash, and
    /// <c>GasStationCansAndInteriorTests</c> pins the two frames equal at all eight facings, so the
    /// doorway can never be cut in a different frame from the building it belongs to.</para>
    ///
    /// <para>⚠️ Until ADR 0042 this file passed a depth scale of 1 to match a station kit that placed in
    /// unsquashed metres — the C-store's wall ring then stood 2.07 m outside each drawn side wall, and
    /// drew perfectly. The ADR measured it and settled the frame; the interim and its "the two kits
    /// disagree" remark are gone with it.</para>
    /// </summary>
    public static class StationInteriorPlacement
    {
        /// <summary>The child the wall ring is built on, so a re-run can find and replace it.</summary>
        public const string WallsChildName = "InteriorWalls";

        /// <summary>The sidecar's own id for a storefront's floor. Named once.</summary>
        public const string SalesFloorId = "sales_floor";

        /// <summary>How far the two derivations of the wall thickness (across and deep) may differ
        /// before the plan refuses (m). The shipped pieces agree exactly; this is slack for a re-bake
        /// that rounds, not permission for a shell whose walls are not walls.</summary>
        public const float WallThicknessAgreementMetres = 0.02f;

        // =====================================================================================
        //  THE PLAN — pure arithmetic over two Defs, no scene
        // =====================================================================================

        /// <summary>
        /// Everything needed to cut a doorway in one storefront, derived from the shell and its room.
        /// Pure numbers so a test can check the geometry of every pair in the kit without a scene.
        /// </summary>
        public readonly struct Plan
        {
            /// <summary>The building's own plan, across (local +x) and deep (local +y), in metres —
            /// the <c>building</c> blocker's footprint, NOT <see cref="StationPieceDef.WidthMeters"/>.
            /// ⚠️ The Def's width and depth include what is BOLTED ON: the C-store's declared 12.46 m
            /// reaches out to the ice box and the propane cage, which are not walls and must not be
            /// walled.</summary>
            public readonly float WidthMetres, LengthMetres;

            /// <summary>The wall, in metres — <b>derived, never typed</b>: exactly the ground the shell
            /// covers that the sales floor does not. Measured on the shipped kit it is 0.20 m for the
            /// C-store and 0.16 m for the kiosk, and both agree on BOTH axes, which is what makes it a
            /// wall thickness rather than a coincidence.</summary>
            public readonly float WallThicknessMetres;

            /// <summary>The clear opening the entry publishes (m).</summary>
            public readonly float DoorwayWidthMetres;

            /// <summary>Which wall the doorway is in. Both shipped storefronts put it on +Y, and both
            /// are MEASURED rather than assumed — the house family's door is on −Y, and carrying a
            /// remembered answer across kits is exactly the trap that opened three St Peters houses
            /// onto a blank gable (#509).</summary>
            public readonly bool DoorOnPlusY;

            /// <summary>How far along that wall, from its centre (m). The C-store's is 0.30 m off
            /// centre — small, and still enough to miss a gap cut at the middle by a third of its own
            /// width.</summary>
            public readonly float DoorAcrossMetres;

            public Plan(float widthMetres, float lengthMetres, float wallThicknessMetres,
                        float doorwayWidthMetres, bool doorOnPlusY, float doorAcrossMetres)
            {
                WidthMetres = widthMetres;
                LengthMetres = lengthMetres;
                WallThicknessMetres = wallThicknessMetres;
                DoorwayWidthMetres = doorwayWidthMetres;
                DoorOnPlusY = doorOnPlusY;
                DoorAcrossMetres = doorAcrossMetres;
            }
        }

        /// <summary>
        /// Work out how to open <paramref name="shell"/> onto <paramref name="room"/>, or say why not.
        ///
        /// <para><b>Every refusal is loud and named.</b> A storefront that cannot be opened must not be
        /// opened HALFWAY — a wall ring cut from a bad measurement is a building the player walks
        /// through, and it draws exactly like one that works. So this returns false with a sentence and
        /// the caller leaves the solid blocker alone, which is the safe state.</para>
        /// </summary>
        public static bool TryPlan(StationPieceDef shell, StationPieceDef room, out Plan plan, out string why)
        {
            plan = default;
            why = null;

            if (shell == null || room == null) { why = "one of the pair is missing"; return false; }

            StationBlocker building = BuildingBlockerOf(shell, room);
            if (building == null)
            {
                why = $"'{shell.name}' has no wall-treatment blocker big enough to hold " +
                      $"'{room.name}'s floor, so there is nothing to cut a door in";
                return false;
            }

            if (!Bounds(building.Footprint, out Vector2 bMin, out Vector2 bMax))
            { why = $"'{shell.name}'s building blocker has no footprint"; return false; }

            StationSurface floor = FloorOf(room);
            if (floor == null || !Bounds(floor.Outline, out Vector2 fMin, out Vector2 fMax))
            { why = $"'{room.name}' publishes no '{SalesFloorId}' outline to size the wall against"; return false; }

            // ⚠️ The shell's blocker must be CENTRED on the piece's own origin, because that origin is
            // where the prefab stands and BuildingInterior measures its footprint from transform
            // position. An off-centre building would put the walls right and the inside test half a
            // building away — and both would draw perfectly.
            Vector2 centre = (bMin + bMax) * 0.5f;
            if (centre.sqrMagnitude > 1e-4f)
            {
                why = $"'{shell.name}'s building blocker is centred at ({centre.x:0.###},{centre.y:0.###}) " +
                      "rather than on the piece origin, which is the point the room is placed at";
                return false;
            }

            float halfW = (bMax.x - bMin.x) * 0.5f;
            float halfL = (bMax.y - bMin.y) * 0.5f;

            // The wall IS the difference between the two footprints, read on each axis independently so
            // the two readings can be compared. That comparison is the whole check: a shell and a room
            // that were never meant for each other disagree here, and nothing else would notice.
            float tAcross = halfW - (fMax.x - fMin.x) * 0.5f;
            float tDeep = halfL - (fMax.y - fMin.y) * 0.5f;

            if (tAcross <= 0f || tDeep <= 0f)
            {
                why = $"'{room.name}'s floor is not inside '{shell.name}'s walls " +
                      $"(across {tAcross:0.###} m, deep {tDeep:0.###} m) — one of them is the wrong size";
                return false;
            }

            if (Mathf.Abs(tAcross - tDeep) > WallThicknessAgreementMetres)
            {
                why = $"'{shell.name}' walls {tAcross:0.###} m across and {tDeep:0.###} m deep — the two " +
                      "disagree by more than a rounding, so this is not one wall thickness and the ring " +
                      "would be cut to the wrong one";
                return false;
            }

            StationDoorway entry = shell.Entry;
            if (entry == null || !entry.Exists || entry.ClearWidthMeters <= 0f)
            { why = $"'{shell.name}' publishes no entry, so there is no doorway to cut"; return false; }

            // ⚠️ MEASURE WHICH WALL, never remember it. The threshold has to lie ON one of the two
            // ±y walls for a gap to be cuttable there at all; a door on an x wall (or floating in the
            // middle of the plan) is a shell this geometry cannot describe, and saying so is far better
            // than cutting the gap somewhere plausible. Same failure this repo has already paid for
            // once, in houses whose anchor and drawn door disagreed (#509).
            float t = (tAcross + tDeep) * 0.5f;
            float onWall = Mathf.Abs(Mathf.Abs(entry.Threshold.y) - halfL);
            if (onWall > t)
            {
                why = $"'{shell.name}'s entry threshold is {onWall:0.###} m off either ±y wall — this kit " +
                      "cuts a doorway in a wall, and that door is not in one";
                return false;
            }

            plan = new Plan(halfW * 2f, halfL * 2f, t, entry.ClearWidthMeters,
                            doorOnPlusY: entry.Threshold.y >= 0f,
                            doorAcrossMetres: entry.Threshold.x);
            return true;
        }

        /// <summary>The room piece that belongs to this shell: same <see cref="StationPieceDef.SizeKey"/>,
        /// on the interior side of the kit. Paired by SIZE rather than by rewriting the key's prefix,
        /// because the size is the thing the two rigs actually share — it is what makes the room fit the
        /// shell's cell and pivot at all.</summary>
        public static StationPieceDef RoomFor(StationPieceDef shell)
        {
            if (shell == null || shell.IsInterior || string.IsNullOrEmpty(shell.SizeKey)) return null;
            foreach (var kv in StationCatalog.Defs())
            {
                StationPieceDef d = kv.Value;
                if (d != null && d.IsInterior &&
                    string.Equals(d.SizeKey, shell.SizeKey, System.StringComparison.Ordinal))
                    return d;
            }
            return null;
        }

        /// <summary>The blocker that IS the building — the one solid whose footprint holds the whole
        /// sales floor. Found by measurement rather than by the name "building", so a re-bake that
        /// renames it does not silently leave the storefront sealed.</summary>
        public static StationBlocker BuildingBlockerOf(StationPieceDef shell, StationPieceDef room)
        {
            StationSurface floor = FloorOf(room);
            if (shell?.Blockers == null || floor == null || !Bounds(floor.Outline, out Vector2 fMin, out Vector2 fMax))
                return null;

            foreach (StationBlocker b in shell.Blockers)
            {
                if (b == null || !b.Blocks || b.IsCircle) continue;
                if (!Bounds(b.Footprint, out Vector2 min, out Vector2 max)) continue;
                if (min.x <= fMin.x && min.y <= fMin.y && max.x >= fMax.x && max.y >= fMax.y) return b;
            }
            return null;
        }

        /// <summary>A room's floor surface, by the sidecar's own id.</summary>
        public static StationSurface FloorOf(StationPieceDef room)
        {
            if (room?.Walkables == null) return null;
            foreach (StationSurface s in room.Walkables)
                if (s != null && string.Equals(s.Id, SalesFloorId, System.StringComparison.Ordinal)) return s;
            return null;
        }

        static bool Bounds(Vector2[] polygon, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            if (polygon == null || polygon.Length < 3) return false;
            foreach (Vector2 v in polygon) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            return true;
        }

        // =====================================================================================
        //  THE PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Open a placed storefront onto its placed room. Returns true if the way in was cut.
        ///
        /// <para><b>Re-runnable</b> — a previous run's walls are cleared first, so a second builder pass
        /// leaves one ring rather than two.</para>
        ///
        /// <para><paramref name="occupant"/> may be null: <see cref="BuildingInterior"/> falls back to
        /// Core's player transform at runtime, which is the only thing that works in a region scene
        /// saved long before the persistent player exists.</para>
        /// </summary>
        public static bool Open(GameObject shellGo, GameObject roomGo,
                                StationPieceDef shellDef, StationPieceDef roomDef,
                                int facing, string tag, Transform occupant = null)
        {
            if (shellGo == null || roomGo == null) return false;

            if (!TryPlan(shellDef, roomDef, out Plan plan, out string why))
            {
                // ⚠️ Loud, and the storefront stays SOLID. A half-cut building is worse than a shut one:
                // the shut one is honest about not being finished.
                Debug.LogWarning(
                    $"{tag} '{shellDef?.name}' stays sealed — {why}. Its sales floor is placed and drawn " +
                    "but cannot be reached, which is a façade the owner's 2026-08-11 ruling does not " +
                    "allow. Fix the pair rather than hand-cutting a hole.", shellGo);
                return false;
            }

            ClearExisting(shellGo.transform);

            var footprint = new InteriorFootprint(
                shellGo.transform.position,
                plan.WidthMetres, plan.LengthMetres,
                facing, StationCatalog.Facings, SpriteLightMath.GroundDepthScale,
                plan.DoorOnPlusY ? 1f : -1f, plan.DoorAcrossMetres);

            BuildWalls(shellGo.transform, footprint, plan);

            // The shut leaves go — on BOTH pieces, because the bake gives the room a copy of the same
            // doorway. A C-store's entry is an automatic bipart slider (the sidecar says so: two 1.2 m
            // leaves, 1.26 m of travel), and nothing in the game drives one yet. Until something does,
            // the honest reading of a shop you can walk into is the one every St Peters shop already
            // uses — the doorway is an opening. ⚠️ The SERVICE door is deliberately left shut: it is
            // the staff door, it owns a real keep-clear, and it is not a way in for the player.
            int leaves = OpenEntry(shellGo.transform) + OpenEntry(roomGo.transform);

            // ⚠️ And the solid goes LAST, after the ring that replaces it is standing. Disabled rather
            // than destroyed: the prefab connection stays intact and a builder re-run reverts cleanly.
            bool solid = SealOff(shellGo.transform, shellDef, roomDef, facing);

            var interior = shellGo.AddComponent<BuildingInterior>();
            interior.Configure(shellGo.GetComponent<SpriteRenderer>(),
                               roomGo.GetComponent<SpriteRenderer>(),
                               props: null,
                               plan.WidthMetres, plan.LengthMetres,
                               facing, StationCatalog.Facings,
                               SpriteLightMath.GroundDepthScale,
                               plan.WallThicknessMetres, plan.DoorwayWidthMetres,
                               doorOnPlusY: plan.DoorOnPlusY,
                               doorAcrossMetres: plan.DoorAcrossMetres);
            if (occupant != null) interior.SetOccupant(occupant);

            Debug.Log(
                $"{tag} '{shellDef.name}' is enterable: {plan.WidthMetres:0.##}×{plan.LengthMetres:0.##} m " +
                $"of plan, walls {plan.WallThicknessMetres:0.###} m DERIVED from the floor inside them, " +
                $"a {plan.DoorwayWidthMetres:0.##} m doorway on the " +
                $"{(plan.DoorOnPlusY ? "+Y" : "−Y")} wall {plan.DoorAcrossMetres:+0.00;-0.00} m off its " +
                $"centre, threshold at ({footprint.DoorWorld.x:0.#},{footprint.DoorWorld.y:0.#}). " +
                $"{leaves} shut leaf/leaves opened; the solid building blocker is " +
                $"{(solid ? "off" : "⚠ STILL ON — the walls are doubled")}.", shellGo);

            return true;
        }

        /// <summary>
        /// Open every storefront in a placed forecourt that had its room placed with it. Returns how
        /// many went from sealed to enterable.
        ///
        /// <para><b>A shell is opened only when its ROOM is standing in the same spot.</b> That is the
        /// rule, and it is the honest one in both directions: a storefront placed on its own is a
        /// building with nothing inside, and cutting a doorway into it would let the player walk into an
        /// empty rectangle under a roof. Absence is data — no room, no door.</para>
        ///
        /// <para>⚠️ The two pieces must also be turned the SAME way. They share one cell and one
        /// pivot by design (ADR 0036), so a room at a different facing is a room whose fittings stand
        /// across its own walls — and it draws perfectly. Refused rather than opened.</para>
        /// </summary>
        public static int OpenAll(StationForecourt.Result result, string tag, Transform occupant = null)
        {
            if (result == null) return 0;

            int opened = 0;
            for (int i = 0; i < result.Placed.Count; i++)
            {
                GameObject shellGo = result.Placed[i];
                if (shellGo == null) continue;

                StationPieceDef shellDef = StationCatalog.Find(shellGo.name);
                if (shellDef == null || shellDef.IsInterior) continue;

                StationPieceDef roomDef = RoomFor(shellDef);
                if (roomDef == null) continue;      // a shell with no room in the kit at all

                int facing = i < result.Facings.Count ? result.Facings[i] : 0;
                GameObject roomGo = FindRoom(result, roomDef.name, shellGo.transform.position, facing,
                                             out string mismatch);
                if (roomGo == null)
                {
                    Debug.LogWarning(
                        $"{tag} '{shellDef.name}' is standing without '{roomDef.name}'" +
                        (mismatch == null ? "" : " (" + mismatch + ")") +
                        ", so it stays sealed. A storefront the player can enter needs its sales floor " +
                        "placed at the same spot and the same cell.", shellGo);
                    continue;
                }

                if (Open(shellGo, roomGo, shellDef, roomDef, facing, tag, occupant)) opened++;
            }
            return opened;
        }

        /// <summary>The placed room that belongs to a shell standing here: right piece, same position,
        /// same cell. Position compared with a millimetre of slack — the two are placed from the same
        /// layout offset, so they either coincide or they are a different pair.</summary>
        static GameObject FindRoom(StationForecourt.Result result, string roomName, Vector3 at,
                                   int facing, out string mismatch)
        {
            mismatch = null;
            for (int i = 0; i < result.Placed.Count; i++)
            {
                GameObject go = result.Placed[i];
                if (go == null || go.name != roomName) continue;
                if (((Vector2)(go.transform.position - at)).sqrMagnitude > 1e-6f)
                {
                    mismatch = "its room is placed somewhere else";
                    continue;
                }
                int roomFacing = i < result.Facings.Count ? result.Facings[i] : facing;
                if (roomFacing != facing)
                {
                    mismatch = $"its room is drawn at cell {roomFacing} and the shell at cell {facing}";
                    continue;
                }
                return go;
            }
            return null;
        }

        /// <summary>Undo a previous run, so re-building leaves one of everything.</summary>
        static void ClearExisting(Transform shellRoot)
        {
            var existing = shellRoot.GetComponent<BuildingInterior>();
            if (existing != null) Object.DestroyImmediate(existing);

            for (int i = shellRoot.childCount - 1; i >= 0; i--)
                if (shellRoot.GetChild(i).name == WallsChildName)
                    Object.DestroyImmediate(shellRoot.GetChild(i).gameObject);
        }

        /// <summary>
        /// The wall ring, as colliders.
        ///
        /// <para>⚠️ One <see cref="PolygonCollider2D"/> PER WALL rather than five paths on one — several
        /// paths on a single collider turn their overlaps into HOLES, and a hole at a corner is a player
        /// who occasionally slips through it. <see cref="ShopPlacement"/> pays for the same lesson.</para>
        ///
        /// <para>⚠️ And the quads go on a child at LOCAL ZERO with no rotation of its own, holding
        /// world-derived paths — exactly the way the kit's own blockers are placed now
        /// (<c>StationForecourt.ProjectColliders</c>). <see cref="InteriorFootprint.WallQuads"/> has
        /// already applied the facing AND the squash; a rotation on the child would turn the ring twice,
        /// and could not have squashed it anyway.</para>
        /// </summary>
        static void BuildWalls(Transform shellRoot, in InteriorFootprint footprint, in Plan plan)
        {
            var wallsGo = new GameObject(WallsChildName);
            wallsGo.transform.SetParent(shellRoot, worldPositionStays: false);
            wallsGo.transform.localPosition = Vector3.zero;
            wallsGo.transform.localRotation = Quaternion.identity;

            Vector2 origin = shellRoot.position;
            foreach (Vector2[] quad in footprint.WallQuads(plan.WallThicknessMetres, plan.DoorwayWidthMetres))
            {
                var collider = wallsGo.AddComponent<PolygonCollider2D>();
                collider.pathCount = 1;

                var local = new Vector2[quad.Length];
                for (int i = 0; i < quad.Length; i++) local[i] = quad[i] - origin;
                collider.SetPath(0, local);
            }
        }

        /// <summary>Switch off the shut ENTRY leaf wherever the bake put one. Returns how many it
        /// found — 0 is worth reporting, because it means the doorway was never plugged and something
        /// about the prefab has changed under this code.</summary>
        static int OpenEntry(Transform root)
        {
            int opened = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name != "door_Entry_shut") continue;
                if (child.gameObject.activeSelf) { child.gameObject.SetActive(false); opened++; }
            }
            return opened;
        }

        /// <summary>
        /// Switch off the one solid the wall ring replaces.
        ///
        /// <para>⚠️ Matched by NAME <b>and</b> by its collider's own bounds, because a station shell can
        /// carry two children of the same name (the C-store has two <c>blocker_plant</c>s) and a name
        /// alone is not an identity here. The bounds come from the Def's footprint projected the way the
        /// placement projected the child's path — rotate, then squash, at <paramref name="facing"/>
        /// (<see cref="StationCatalog.FootprintPath"/>) — so this switches off exactly the blocker
        /// <see cref="BuildingBlockerOf"/> measured and never a neighbour.</para>
        /// </summary>
        static bool SealOff(Transform shellRoot, StationPieceDef shellDef, StationPieceDef roomDef, int facing)
        {
            StationBlocker building = BuildingBlockerOf(shellDef, roomDef);
            if (building == null) return false;
            if (!Bounds(StationCatalog.FootprintPath(building.Footprint, facing), out Vector2 min, out Vector2 max))
                return false;

            string wanted = "blocker_" + building.Kind;
            for (int i = 0; i < shellRoot.childCount; i++)
            {
                Transform child = shellRoot.GetChild(i);
                if (child.name != wanted) continue;

                var poly = child.GetComponent<PolygonCollider2D>();
                if (poly == null || poly.pathCount < 1) continue;

                Vector2[] path = poly.GetPath(0);
                if (!Bounds(path, out Vector2 pMin, out Vector2 pMax)) continue;
                if ((pMin - min).sqrMagnitude > 1e-4f || (pMax - max).sqrMagnitude > 1e-4f) continue;

                child.gameObject.SetActive(false);
                return true;
            }
            return false;
        }
    }
}
#endif
