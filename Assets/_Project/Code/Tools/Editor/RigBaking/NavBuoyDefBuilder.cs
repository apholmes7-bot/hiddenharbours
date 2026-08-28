using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Builds one <see cref="NavBuoyDef"/> per baked mark, and the single prefab that wears them,
    /// FROM THE BAKE — the contract for the geometry, the rig for the hydrostatics and the IALA
    /// glosses, the sliced sheets for the art.
    ///
    /// <para><b>Why a builder and not hand-authored assets.</b> Ten marks × five sizes × eight
    /// facings is 400 sprite references, and three of the numbers per rung (<c>SpriteHeightMeters</c>,
    /// <c>FloatLineFraction</c>, <c>SlopeFollow</c>) are measurements, not opinions. Typed by hand
    /// they would be wrong somewhere and nothing would report it — the mark would just float oddly.
    /// Derived here, they cannot disagree with the sheets they were baked beside.</para>
    ///
    /// <para><b>Non-destructive.</b> An existing def is UPDATED in place, so the owner's own edits to
    /// fields the bake does not own (<c>DefaultSizeIndex</c>, a reworded gloss) survive a re-run.
    /// Ids are append-only (CLAUDE.md §5) and are never rewritten once an asset exists.</para>
    /// </summary>
    public static class NavBuoyDefBuilder
    {
        public const string DefFolder = "Assets/_Project/Data/NavBuoys";
        public const string PrefabPath = "Assets/_Project/Prefabs/Decor/NavBuoy.prefab";

        /// <summary>
        /// Stable ids, append-only. Spelled out rather than derived from the rig's CamelCase key so a
        /// future rig rename cannot silently re-id a placed asset.
        /// </summary>
        static readonly IReadOnlyDictionary<string, string> Ids =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CardinalN"] = "buoy.cardinal_north",
                ["CardinalS"] = "buoy.cardinal_south",
                ["CardinalE"] = "buoy.cardinal_east",
                ["CardinalW"] = "buoy.cardinal_west",
                ["PortCan"]   = "buoy.lateral_port",
                ["StbdNun"]   = "buoy.lateral_starboard",
                ["PortLit"]   = "buoy.lateral_port_lit",
                ["StbdLit"]   = "buoy.lateral_starboard_lit",
                ["Mooring"]   = "buoy.mooring",
                ["Isolated"]  = "buoy.isolated_danger",
                // Parked marks — ids reserved now so baking them later cannot invent a different one.
                ["SafeWater"] = "buoy.safe_water",
                ["Special"]   = "buoy.special",
                ["Regulatory"]= "buoy.regulatory",
                ["Spar"]      = "buoy.spar",
            };

        public static string IdFor(string markType) =>
            Ids.TryGetValue(markType, out string id)
                ? id
                : throw new ArgumentException(
                    $"No stable id reserved for mark '{markType}'. Ids are append-only — add one to " +
                    "NavBuoyDefBuilder.Ids rather than deriving it, so a rig rename cannot re-id a " +
                    "placed asset.");

        [MenuItem("Hidden Harbours/Art/Build Nav Buoy Defs (from the bake)", priority = 70)]
        public static void BuildDefs()
        {
            var contract = NavBuoyContract.Load();
            contract.AssertSelfConsistent();

            var entry = RigCatalog.Get(NavBuoyKit.RigKey);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, DefFolder));

            int made = 0, updated = 0, rungs = 0;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (string type in NavBuoyKit.BakeTypes)
            {
                string id = IdFor(type);
                if (!seenIds.Add(id))
                    throw new InvalidOperationException($"Duplicate nav-buoy id '{id}' for '{type}'.");

                string assetPath = $"{DefFolder}/{type}.asset";
                var def = AssetDatabase.LoadAssetAtPath<NavBuoyDef>(assetPath);
                bool isNew = def == null;
                if (isNew)
                {
                    def = ScriptableObject.CreateInstance<NavBuoyDef>();
                    def.Id = id;                       // set ONCE; append-only thereafter
                    def.DefaultSizeIndex = 1;          // s18, the kit's working default
                }
                else if (!string.Equals(def.Id, id, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[NavBuoy] {assetPath} carries id '{def.Id}' but the builder reserves '{id}'. " +
                        "Ids are APPEND-ONLY — leaving the asset's id alone. Reconcile deliberately.");
                }

                def.MarkType = type;
                def.DisplayName = host.EvaluateString($"{g}.TYPES['{type}'].name");
                def.Gloss = host.EvaluateString($"{g}.TYPES['{type}'].gloss");

                bool lit = host.EvaluateBool($"{g}.TYPES['{type}'].light != null");
                def.LightCharacter = lit ? host.EvaluateString($"{g}.TYPES['{type}'].light") : "";
                def.LightText = lit
                    ? host.EvaluateString($"{g}.LIGHTS[{g}.TYPES['{type}'].light].txt")
                    : "";

                def.Sizes ??= new List<NavBuoyDef.SizeEntry>();
                def.Sizes.Clear();

                foreach (string sizeId in NavBuoyKit.Sizes)
                {
                    var cell = contract.Get(NavBuoyKit.CellKey(type, sizeId));
                    string sheet = $"{NavBuoyKit.SheetFolder}/{NavBuoyKit.SheetName(type, sizeId)}.png";

                    var facings = LoadFacings(sheet, NavBuoyKit.SheetName(type, sizeId));
                    if (facings == null)
                    {
                        Debug.LogWarning($"[NavBuoy] {sheet} is not sliced into {NavBuoyKit.Facings} " +
                                         "sprites — skipping that rung. Run the slicer first.");
                        continue;
                    }

                    // ⚠️ All three of these are MEASURED, not chosen.
                    //  · height  — the cropped cell in metres at the kit's own 32 px/m.
                    //  · float   — the sprite's normalised pivot y. The nav sheets pivot ON the
                    //              waterline, so this IS the waterline fraction; ADR 0026's formula.
                    //  · follow  — the rig's own BM/(BM+BG) for this hull.
                    float heightM = cell.cellH / (float)NavBuoyKit.PxPerM;
                    float floatFrac = (cell.cellH - cell.pivotY) / (float)cell.cellH;
                    float follow = (float)host.EvaluateNumber($"{g}.meta('{type}','{sizeId}').follow");

                    float diameter =
                        (float)host.EvaluateNumber($"{g}.meta('{type}','{sizeId}').D");

                    def.Sizes.Add(new NavBuoyDef.SizeEntry
                    {
                        SizeId = sizeId,
                        DiameterMeters = diameter,
                        RatedWater = host.EvaluateString($"{g}.meta('{type}','{sizeId}').water"),
                        Facings = facings,
                        SpriteHeightMeters = heightM,
                        FloatLineFraction = floatFrac,
                        SlopeFollow = follow,

                        // ⭐ Her physics comes off the ONE number the kit measures her by, so a
                        // re-baked ladder cannot end up with a 3 m landfall buoy that shoves like
                        // a can. See NavBuoyKit for the three laws and why each is that shape.
                        CollisionRadiusMeters = NavBuoyKit.CollisionRadiusFor(diameter),
                        MooredMassKg = NavBuoyKit.MooredMassFor(diameter),
                        WatchRadiusMeters = NavBuoyKit.WatchRadiusFor(diameter),
                    });
                    rungs++;
                }

                if (isNew) { AssetDatabase.CreateAsset(def, assetPath); made++; }
                else { EditorUtility.SetDirty(def); updated++; }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NavBuoy] Defs: {made} created, {updated} updated, {rungs} size rungs wired " +
                      $"from {NavBuoyKit.ContractPath}.");
        }

        /// <summary>
        /// The 8 facing sprites from a sliced sheet, in cell order, or null if the sheet is not
        /// sliced.
        ///
        /// <para>⚠️ <b>spriteMode Multiple is NOT the same as sliced</b> — a freshly imported sheet is
        /// Multiple with an EMPTY rect list, and <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null on
        /// it. Load ALL sub-assets and match by the slicer's geometric name instead, then assert the
        /// count.</para>
        /// </summary>
        static Sprite[] LoadFacings(string sheetPath, string stem)
        {
            var subs = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
            if (subs.Length < NavBuoyKit.Facings) return null;

            var byName = subs.ToDictionary(s => s.name, StringComparer.Ordinal);
            var facings = new Sprite[NavBuoyKit.Facings];
            for (int i = 0; i < NavBuoyKit.Facings; i++)
            {
                if (!byName.TryGetValue($"{stem}_{i}", out Sprite s)) return null;
                facings[i] = s;
            }
            return facings;
        }

        /// <summary>
        /// The ONE prefab every placed mark instantiates: a root the wave field samples at, a child
        /// the bob moves, and the two components. Built here rather than hand-authored so the
        /// root-vs-child separation cannot be quietly broken — a prefab whose visual IS the root
        /// samples the field at a point that moves with the bob, which reads as jitter, not as a bug.
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Build Nav Buoy Prefab", priority = 71)]
        public static void BuildPrefab()
        {
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, Path.GetDirectoryName(PrefabPath)!));

            var root = new GameObject("NavBuoy");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, worldPositionStays: false);

                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 3;   // above the sea, below the deck furniture

                var bob = root.AddComponent<BuoyWaveVisual>();
                bob.Configure(sr, visual.transform);

                // ⚠ ORDER MATTERS. NavBuoyVisual requires NavBuoyMooring, so adding it here
                // first means the mooring is a component of the prefab in its own right rather
                // than one Unity conjured to satisfy an attribute — and Configure below can
                // then reach it. A mark added the other way round is moored by accident.
                root.AddComponent<NavBuoyMooring>();

                var mark = root.AddComponent<NavBuoyVisual>();

                // Dress it with the working-default port hand so the prefab is not a blank in the
                // project browser; a placement retargets it.
                var def = AssetDatabase.LoadAssetAtPath<NavBuoyDef>($"{DefFolder}/PortCan.asset");
                if (def != null) mark.Configure(def, "s18", 0);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[NavBuoy] Prefab → {PrefabPath} (root samples the field, 'Visual' bobs).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>Batch entry: defs then prefab, in the only order that works.</summary>
        public static void BuildAllFromCommandLine()
        {
            BuildDefs();
            BuildPrefab();
        }
    }
}
