using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Turns the baked gas-station contract and the rig's gameplay sidecar into content: one
    /// <see cref="StationPieceDef"/> per piece and one placeable prefab each.
    ///
    /// <para>Everything is DERIVED from the contract, the sliced sheets and the sidecar, so a re-bake
    /// regenerates rather than leaving stale assets behind. Ids are stable and append-only, which is
    /// what lets this be re-run safely: an existing Def is UPDATED in place (same GUID, so every
    /// scene reference survives) rather than replaced.</para>
    ///
    /// <para><b>Why this lives in <c>Tools.RigBaking</c> and not beside the other Def builders in
    /// <c>Art.Editor</c>:</b> it needs <see cref="DeckSidecarJson"/> to read nested arrays of arrays
    /// (a footprint is <c>[[x,y], …]</c>, which <c>JsonUtility</c> cannot express), and
    /// <c>Art.Editor → Tools</c> is the forbidden direction. This assembly may reach Art.Editor;
    /// Art.Editor may not reach back.</para>
    /// </summary>
    public static class StationPieceDefBuilder
    {
        public const string DefFolder = "Assets/_Project/Data/StationPieces";
        public const string PrefabFolder = "Assets/_Project/Prefabs/GasStation";

        /// <summary>Id prefix. Append-only and never renamed — <c>station.dispenser_stwin</c>.</summary>
        public const string IdPrefix = "station";

        static string SidecarPath =>
            Path.Combine(RigCatalog.RepoRoot,
                         "docs/art/rigs/gas-station-rig/gameplay/gasStationRig.gameplay.json");

        /// <summary>
        /// The piece's stable id. Append-only and never renamed.
        ///
        /// <para>⚠️ The size is LOWERCASED. The rig spells its size keys in camelCase (<c>sTwin</c>,
        /// <c>sB4</c>, <c>sPylon</c>) and CLAUDE.md requires Def ids to be <c>type.snake_case</c>, so
        /// <c>sTwin</c> becomes <c>stwin</c> here. The rig's own spelling survives verbatim on
        /// <see cref="StationPieceDef.SizeKey"/>, which is what a re-bake matches against; the id is
        /// an identifier, not a lookup key.</para>
        /// </summary>
        public static string IdFor(string type, string size) =>
            $"{IdPrefix}.{type}_{size}".ToLowerInvariant();

        [MenuItem("Hidden Harbours/Art/Build Gas Station Defs + Prefabs", priority = 80)]
        public static void BuildAll()
        {
            var contract = GasStationSheetSlicer.LoadContract();
            if (contract == null) return;

            if (!File.Exists(SidecarPath))
            {
                Debug.LogError($"[StationPieceDefBuilder] No sidecar at {SidecarPath}. The Defs carry " +
                               "blockers, reach points and doorways from it; building without it " +
                               "would ship pieces that stop nobody and offer nothing.");
                return;
            }

            var sidecar = DeckSidecarJson.Parse(File.ReadAllText(SidecarPath));
            var blocks = IndexPieces(sidecar);

            Directory.CreateDirectory(DefFolder);
            Directory.CreateDirectory(PrefabFolder);

            int defs = 0, prefabs = 0;
            var problems = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var sheet in contract.sheets)
                {
                    var def = BuildDef(sheet, contract, blocks, problems);
                    if (def == null) continue;
                    defs++;
                    if (BuildPrefab(def)) prefabs++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string msg = $"[StationPieceDefBuilder] {defs} Defs, {prefabs} prefabs.";
            if (problems.Count > 0)
                Debug.LogError($"{msg} {problems.Count} problems:\n  " + string.Join("\n  ", problems));
            else
                Debug.Log(msg);
        }

        /// <summary>Headless entry point for CI / <c>-executeMethod</c>. Needs <c>-quit</c>.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StationPieceDefBuilder] headless build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the sidecar ---------------------------------------------------------------------------

        /// <summary>
        /// The sidecar's 21 piece blocks, by (type, size).
        ///
        /// <para>There are 21 blocks for 23 sheets, and that is not a shortfall: the two sales floors
        /// have no block of their own because the interior rig writes its <c>SOLE</c> and its
        /// <c>INTERACT</c> INTO the storefront's block. One building, one description.</para>
        /// </summary>
        static Dictionary<string, object> IndexPieces(object sidecar)
        {
            var byKey = new Dictionary<string, object>(StringComparer.Ordinal);
            var pieces = DeckSidecarJson.AsArray(DeckSidecarJson.Member(sidecar, "pieces"));
            if (pieces == null) return byKey;

            foreach (object p in pieces)
            {
                string type = Str(DeckSidecarJson.Member(p, "type"));
                string size = Str(DeckSidecarJson.Member(p, "size"));
                if (type.Length > 0 && size.Length > 0) byKey[$"{type}_{size}"] = p;
            }

            return byKey;
        }

        static StationPieceDef BuildDef(GasStationKit.SheetEntry sheet,
                                        GasStationKit.Contract contract,
                                        IReadOnlyDictionary<string, object> blocks,
                                        List<string> problems)
        {
            string sheetPath = $"{GasStationKit.OutputFolder}/{sheet.key}.png";

            // ⚠️ spriteMode Multiple is NOT the same as sliced. A fresh import is Multiple with ZERO
            // rects, and LoadAllAssetsAtPath then returns nothing while everything still looks
            // imported. A Def built from that would carry an all-null frame table and draw nothing.
            var byName = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                                      .OfType<Sprite>()
                                      .ToDictionary(s => s.name, StringComparer.Ordinal);

            if (byName.Count != sheet.facings)
            {
                problems.Add($"{sheet.key}: {byName.Count} sprites on the sheet, expected " +
                             $"{sheet.facings}. Slice the kit before building Defs.");
                return null;
            }

            // The sales floor borrows its host storefront's block — see IndexPieces.
            string blockKey = sheet.interior ? $"store_{sheet.size}" : sheet.key;
            if (!blocks.TryGetValue(blockKey, out object block))
            {
                problems.Add($"{sheet.key}: the sidecar has no block '{blockKey}'. The kit and the " +
                             "sidecar have come apart; re-generate the sidecar rather than shipping " +
                             "a piece that stops nobody.");
                return null;
            }

            string path = $"{DefFolder}/{sheet.key}.asset";
            var def = AssetDatabase.LoadAssetAtPath<StationPieceDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<StationPieceDef>();

            def.Id = IdFor(sheet.type, sheet.size);
            def.DisplayName = DisplayNameFor(sheet);
            def.PieceType = sheet.type;
            def.SizeKey = sheet.size;
            def.Label = sheet.label ?? "";
            def.IsInterior = sheet.interior;
            def.Facings = sheet.facings;
            def.Fold180 = sheet.fold180;
            def.BakedGrades = (sheet.grades ?? Array.Empty<string>()).ToArray();
            def.SlotPitchMeters = contract.slotPitch;

            var frames = new Sprite[sheet.facings];
            for (int cell = 0; cell < sheet.facings; cell++)
            {
                if (!byName.TryGetValue(sheet.SpriteName(cell), out var sprite))
                    problems.Add($"{sheet.key}: no sprite named {sheet.SpriteName(cell)}.");
                frames[cell] = sprite;
            }

            def.Frames = frames;

            ReadDimensions(def, block);

            if (sheet.interior) ReadSalesFloor(def, block);
            else ReadExterior(def, block);

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);

            return def;
        }

        // ---- the piece's own numbers -------------------------------------------------------------

        static void ReadDimensions(StationPieceDef def, object block)
        {
            // Dispenser: one hose per grade position, and the reach the nozzle actually has.
            var hoses = DeckSidecarJson.AsArray(DeckSidecarJson.Member(block, "HOSES"));
            if (hoses != null && hoses.Count > 0)
            {
                def.MaxHoses = hoses.Count;
                def.MaxSides = hoses.Select(h => Str(DeckSidecarJson.Member(h, "side")))
                                    .Distinct(StringComparer.Ordinal).Count();
                def.HoseReachMeters = Num(DeckSidecarJson.Member(hoses[0], "reach_m"));
                def.LitresPerMinute = Num(DeckSidecarJson.Member(hoses[0], "lpm"));
            }

            // Island: bolt-down plates on a fixed pitch.
            var slots = DeckSidecarJson.AsArray(DeckSidecarJson.Member(block, "SLOTS"));
            if (slots != null && slots.Count > 0)
            {
                def.Slots = slots.Count;
                float pitch = Num(DeckSidecarJson.Member(slots[0], "pitch_m"));
                if (pitch > 0f) def.SlotPitchMeters = pitch;
            }

            // Canopy: the clearance a vehicle has to fit under, and the rectangle it covers.
            object clear = DeckSidecarJson.Member(block, "CLEAR");
            if (clear != null)
            {
                def.ClearHeightMeters = Num(DeckSidecarJson.Member(clear, "height_m"));
                def.Bays = (int)Num(DeckSidecarJson.Member(clear, "bays"));
                var covers = Polygon(DeckSidecarJson.Member(clear, "covers"));
                if (covers.Length >= 3)
                {
                    def.WidthMeters = covers.Max(v => v.x) - covers.Min(v => v.x);
                    def.DepthMeters = covers.Max(v => v.y) - covers.Min(v => v.y);
                }

                def.HeightMeters = Num(DeckSidecarJson.Member(clear, "deck_z"));
            }

            // Storefront: the shell's own box.
            object shell = DeckSidecarJson.Member(block, "SHELL");
            if (shell != null)
            {
                def.WidthMeters = Num(DeckSidecarJson.Member(shell, "w"));
                def.DepthMeters = Num(DeckSidecarJson.Member(shell, "d"));
                def.HeightMeters = Num(DeckSidecarJson.Member(shell, "h"));
            }

            // Anything still unsized takes the bounding box of its own blockers, which is the only
            // footprint those pieces publish.
            if (def.WidthMeters <= 0f || def.DepthMeters <= 0f)
            {
                var all = Blockers(block, "BLOCKERS").SelectMany(FootprintOf).ToArray();
                if (all.Length > 0)
                {
                    def.WidthMeters = all.Max(v => v.x) - all.Min(v => v.x);
                    def.DepthMeters = all.Max(v => v.y) - all.Min(v => v.y);
                }
            }

            if (def.HeightMeters <= 0f)
            {
                var tall = Blockers(block, "BLOCKERS")
                           .Select(b => b.HeightAboveGradeMeters)
                           .DefaultIfEmpty(0f).Max();
                def.HeightMeters = tall;
            }

            if (def.LengthMeters <= 0f) def.LengthMeters = def.WidthMeters;
        }

        // ---- the two layers -----------------------------------------------------------------------

        /// <summary>
        /// The exterior layer: what stands on the apron, what a body can walk on outside, and the two
        /// doors. Everything at <c>sales_floor</c> level belongs to the room and is skipped here.
        /// </summary>
        static void ReadExterior(StationPieceDef def, object block)
        {
            def.Blockers = Blockers(block, "BLOCKERS").ToArray();
            def.Walkables = Surfaces(DeckSidecarJson.Member(block, "WALK")).ToArray();
            def.Interactables = Interactables(block, inside: false).ToArray();
            def.Entry = Doorway(DeckSidecarJson.Member(block, "THRESHOLD"));
            def.ServiceDoor = Doorway(DeckSidecarJson.Member(block, "SERVICE_DOOR"));
        }

        /// <summary>
        /// The room layer: the sales floor, its fit-out, and the six things on it.
        ///
        /// <para>The fit-out is not in <c>BLOCKERS</c> — it lives in the sole's own <c>_notes</c>,
        /// with heights measured above the SOLE rather than above grade, because that is where the
        /// interior rig publishes it. Not duplicated onto the storefront Def: one building, two
        /// sprite layers, and each Def owns its own layer's gameplay.</para>
        /// </summary>
        static void ReadSalesFloor(StationPieceDef def, object block)
        {
            var soles = DeckSidecarJson.AsArray(DeckSidecarJson.Member(block, "SOLE"));
            var surfaces = new List<StationSurface>();
            var fitOut = new List<StationBlocker>();

            foreach (object sole in soles ?? new List<object>())
            {
                surfaces.Add(new StationSurface
                {
                    Id = Str(DeckSidecarJson.Member(sole, "id")),
                    HeightMeters = Num(DeckSidecarJson.Member(sole, "z")),
                    Outline = Polygon(DeckSidecarJson.Member(sole, "polygon")),
                    Treatment = "flat",
                });

                var notes = DeckSidecarJson.AsArray(DeckSidecarJson.Member(sole, "_notes"));
                foreach (object n in notes ?? new List<object>())
                    fitOut.Add(new StationBlocker
                    {
                        Kind = Str(DeckSidecarJson.Member(n, "id")),
                        Level = "sales_floor",
                        Footprint = Polygon(DeckSidecarJson.Member(n, "footprint")),
                        HeightAboveGradeMeters = Num(DeckSidecarJson.Member(n, "height_above_sole_m")),
                        Treatment = Str(DeckSidecarJson.Member(n, "treatment")),
                    });
            }

            def.Walkables = surfaces.ToArray();
            def.Blockers = fitOut.ToArray();
            def.Interactables = Interactables(block, inside: true).ToArray();

            // The way in belongs to the shell, but the room draws its own side of the same opening —
            // same numbers, other side. Carried so a caller inside the room knows where the door is
            // without loading the storefront Def.
            def.Entry = Doorway(DeckSidecarJson.Member(block, "THRESHOLD"));
            def.ServiceDoor = Doorway(DeckSidecarJson.Member(block, "SERVICE_DOOR"));
        }

        // ---- section readers ------------------------------------------------------------------------

        static IEnumerable<StationBlocker> Blockers(object block, string section)
        {
            var list = DeckSidecarJson.AsArray(DeckSidecarJson.Member(block, section));
            foreach (object b in list ?? new List<object>())
            {
                var pos = Vec3(DeckSidecarJson.Member(b, "pos"));
                yield return new StationBlocker
                {
                    Kind = Str(DeckSidecarJson.Member(b, "kind")),
                    Level = Str(DeckSidecarJson.Member(b, "level")),
                    Footprint = Polygon(DeckSidecarJson.Member(b, "footprint")),
                    Center = new Vector2(pos.x, pos.y),
                    Radius = Num(DeckSidecarJson.Member(b, "r")),
                    HeightAboveGradeMeters = Num(DeckSidecarJson.Member(b, "height_above_grade_m")),
                    Treatment = Str(DeckSidecarJson.Member(b, "treatment")),
                };
            }
        }

        static IEnumerable<StationSurface> Surfaces(object walk)
        {
            var list = DeckSidecarJson.AsArray(walk);
            foreach (object w in list ?? new List<object>())
                yield return new StationSurface
                {
                    Id = Str(DeckSidecarJson.Member(w, "id")),
                    HeightMeters = Num(DeckSidecarJson.Member(w, "z")),
                    Outline = Polygon(DeckSidecarJson.Member(w, "polygon")),
                    Treatment = Str(DeckSidecarJson.Member(w, "treatment")),
                };
        }

        /// <summary>
        /// The piece's interactables, split by whether they stand on the sales floor.
        ///
        /// <para>⚠️ A <c>reach_point</c> of <b>null</b> is a REFUSAL, not missing data: the rig's
        /// audit could find nowhere inside arm's reach that clears every blocker, and publishing null
        /// with a reason is deliberately preferred to a plausible lie. So the flag is carried rather
        /// than the absence being silently zeroed to the origin.</para>
        /// </summary>
        static IEnumerable<StationInteractable> Interactables(object block, bool inside)
        {
            var list = DeckSidecarJson.AsArray(DeckSidecarJson.Member(block, "INTERACT"));
            foreach (object i in list ?? new List<object>())
            {
                string level = Str(DeckSidecarJson.Member(i, "level"));
                if (inside != (level == "sales_floor")) continue;

                object rp = DeckSidecarJson.Member(i, "reach_point");
                object reach = DeckSidecarJson.Member(i, "reach");

                yield return new StationInteractable
                {
                    Id = Str(DeckSidecarJson.Member(i, "id")),
                    Label = Str(DeckSidecarJson.Member(i, "label")),
                    Action = Str(DeckSidecarJson.Member(i, "action")),
                    Position = Vec3(DeckSidecarJson.Member(i, "pos")),
                    ReachPoint = Vec3(rp),
                    HasReachPoint = rp != null,
                    ReachVerdict = Str(DeckSidecarJson.Member(reach, "verdict")),
                    Level = level,
                    VisibleFacings = Strings(DeckSidecarJson.Member(i, "visible_facings")),
                };
            }
        }

        static StationDoorway Doorway(object d)
        {
            if (d == null) return new StationDoorway();

            object keep = DeckSidecarJson.Member(d, "keep_clear");
            return new StationDoorway
            {
                Kind = Str(DeckSidecarJson.Member(d, "kind")),
                ClearWidthMeters = Num(DeckSidecarJson.Member(d, "clear_w_m")),
                ClearHeightMeters = Num(DeckSidecarJson.Member(d, "clear_h_m")),
                Threshold = Vec3(DeckSidecarJson.Member(d, "threshold")),
                LeafWidthMeters = Num(DeckSidecarJson.Member(d, "leaf_w_m")),
                TravelMeters = Num(DeckSidecarJson.Member(d, "travel_m")),
                ClearAtOpenFraction = Num(DeckSidecarJson.Member(d, "clear_at_open_fraction")),
                SillStepMeters = Num(DeckSidecarJson.Member(d, "sill_step_m")),
                Treatment = Str(DeckSidecarJson.Member(d, "treatment")),

                // ⚠️ NULL BY DESIGN on the bipart slider — it sweeps nothing, so there is no volume
                // to keep clear and the collider is the leaf. Never read this false as missing data.
                HasKeepClear = keep != null,
                KeepClear = Polygon(DeckSidecarJson.Member(keep, "footprint")),
            };
        }

        // ---- prefabs ---------------------------------------------------------------------------------

        /// <summary>
        /// One placeable prefab per piece: the sprite, and the colliders its blockers earn.
        ///
        /// <para><b>Treatment decides the collider, and only two of the four earn one.</b> <c>wall</c>
        /// and <c>waist_block</c> stop a body; <c>step_over</c> and <c>flat</c> do not — a 0.165 m
        /// island kerb is crossed rather than climbed, and giving it a collider would fence the pumps
        /// off from the player who came to use them.</para>
        ///
        /// <para><b>Doors default SHUT.</b> The storefront entry is a bipart slider, so its collider
        /// is the LEAF rather than a swept arc: two leaves meeting at the threshold midpoint, which
        /// is what a shut door is. The service door is a single leaf swinging out and owns a
        /// keep-clear, but shut it is still just its own leaf.</para>
        /// </summary>
        static bool BuildPrefab(StationPieceDef def)
        {
            string path = $"{PrefabFolder}/{def.name}.prefab";
            var go = new GameObject(def.name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.Frame(0);
                sr.spriteSortPoint = SpriteSortPoint.Pivot;

                AddYSort(go, def);

                foreach (var b in def.Blockers)
                {
                    if (!b.Blocks) continue;
                    AddCollider(go, b);
                }

                AddDoorLeaf(go, def.Entry, "Entry");
                AddDoorLeaf(go, def.ServiceDoor, "ServiceDoor");

                PrefabUtility.SaveAsPrefabAsset(go, path);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// The ADR 0032 depth band, on every piece — a forecourt stands among boats, buildings and
        /// people and has to interleave with all of them.
        ///
        /// <para><b>⚠️ The room's one-step offset lives in <c>_baseOrder</c>, NOT in
        /// <c>SpriteRenderer.sortingOrder</c>.</b> A sales floor and its shell occupy the SAME cell at
        /// the SAME world position — that identity is the whole seamless design — so nothing about
        /// their positions can separate them, and a hand-set <c>sortingOrder</c> is simply overwritten
        /// the moment <c>YSortSprite.OnEnable</c> runs. Lowering the room's base by one order is a
        /// CONSTANT offset at every Y, so the room draws behind its own walls wherever the building is
        /// placed. At the very ends of the band both clamp and TIE rather than invert, which is the
        /// harmless direction.</para>
        ///
        /// <para>The <c>sortingOrder</c> that lands in the saved prefab is a CACHED band output, not a
        /// parked constant: <c>YSortSprite</c> is <c>[ExecuteAlways]</c>, so adding it sorts
        /// immediately and <c>OnEnable</c> recomputes it on every load. Assert on <c>_baseOrder</c>,
        /// never on that cache.</para>
        /// </summary>
        static void AddYSort(GameObject go, StationPieceDef def)
        {
            var sort = go.AddComponent<YSortSprite>();
            if (!def.IsInterior) return;

            var so = new SerializedObject(sort);
            so.FindProperty("_baseOrder").floatValue = SortingBands.DecorBase - 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool AddCollider(GameObject parent, StationBlocker b)
        {
            var child = new GameObject($"blocker_{b.Kind}");
            child.transform.SetParent(parent.transform, worldPositionStays: false);

            if (b.IsCircle)
            {
                var c = child.AddComponent<CircleCollider2D>();
                c.offset = b.Center;
                c.radius = b.Radius;
                return true;
            }

            if (b.Footprint == null || b.Footprint.Length < 3)
            {
                UnityEngine.Object.DestroyImmediate(child);
                return false;
            }

            var poly = child.AddComponent<PolygonCollider2D>();
            poly.pathCount = 1;
            poly.SetPath(0, b.Footprint);
            return true;
        }

        /// <summary>
        /// The shut door, as a collider.
        ///
        /// <para>For the bipart slider that is the two leaves meeting at the threshold — the collider
        /// IS the leaf, which is why the sidecar publishes <c>keep_clear: null</c> for it and why
        /// reading that null as "no data" would leave the shop's front wall with a hole in it.</para>
        /// </summary>
        static void AddDoorLeaf(GameObject parent, StationDoorway d, string name)
        {
            if (!d.Exists || d.ClearWidthMeters <= 0f) return;

            var child = new GameObject($"door_{name}_shut");
            child.transform.SetParent(parent.transform, worldPositionStays: false);

            var box = child.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(d.Threshold.x, d.Threshold.y);
            box.size = new Vector2(d.ClearWidthMeters, Mathf.Max(0.08f, d.SillStepMeters));
        }

        // ---- naming ------------------------------------------------------------------------------------

        static string DisplayNameFor(GasStationKit.SheetEntry s)
        {
            string label = string.IsNullOrEmpty(s.label) ? s.size : s.label;
            string pretty = Title(label);
            return s.interior ? $"{pretty} Sales Floor" : pretty;
        }

        static string Title(string upper)
        {
            var words = upper.Split(new[] { ' ', '-', '—' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Select(w =>
                w.Length <= 1 ? w.ToUpperInvariant()
                              : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
        }

        // ---- json helpers ---------------------------------------------------------------------------------

        static string Str(object v) => v as string ?? "";

        static float Num(object v) => v is double d ? (float)d : 0f;

        static string[] Strings(object v)
        {
            var list = DeckSidecarJson.AsArray(v);
            return list == null ? Array.Empty<string>() : list.Select(Str).ToArray();
        }

        static Vector3 Vec3(object v)
        {
            var list = DeckSidecarJson.AsArray(v);
            if (list == null || list.Count < 2) return Vector3.zero;
            return new Vector3(Num(list[0]), Num(list[1]), list.Count > 2 ? Num(list[2]) : 0f);
        }

        static Vector2[] Polygon(object v)
        {
            var list = DeckSidecarJson.AsArray(v);
            if (list == null) return Array.Empty<Vector2>();

            var pts = new List<Vector2>(list.Count);
            foreach (object p in list)
            {
                var xy = DeckSidecarJson.AsArray(p);
                if (xy == null || xy.Count < 2) continue;
                pts.Add(new Vector2(Num(xy[0]), Num(xy[1])));
            }

            return pts.ToArray();
        }

        static IEnumerable<Vector2> FootprintOf(StationBlocker b)
        {
            if (b.IsCircle)
            {
                yield return b.Center + new Vector2(-b.Radius, -b.Radius);
                yield return b.Center + new Vector2(b.Radius, b.Radius);
                yield break;
            }

            foreach (var p in b.Footprint ?? Array.Empty<Vector2>()) yield return p;
        }
    }
}
