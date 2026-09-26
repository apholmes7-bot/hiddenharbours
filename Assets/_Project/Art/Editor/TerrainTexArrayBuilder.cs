using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Packs the terrain material kit (docs/art/rigs/terrain — ADR 0028 PR 2) into the ONE
    /// Texture2DArray the splat shader samples: a 256-class array (8 m tiles), three slices per
    /// material (_Lo / base / _Hi — the kit's intensity ladder, README §2).
    ///
    /// <para>One array since the px flip (2026-09-17, owner ruling A2). The greenery px kit ships
    /// every plan material at 256 px / 8 m (one pixel per texel), so the seven that used to fill a
    /// 512-class array were APPENDED to <see cref="Order256"/> after Lawn and the 512 array was
    /// retired. See <see cref="Array512Path"/> for the one name that outlives it.</para>
    ///
    /// <para>The array is a DERIVED asset (never hand-edited): re-run this after the kit's PNGs
    /// change. The asset is created once and thereafter written IN PLACE (CopySerialized) so its
    /// GUID — which scenes reference — survives every rebuild (the ShoreIso2 lesson, inverted: it IS
    /// scene-referenced, so the GUID must be stable).</para>
    ///
    /// <para>⚠ <see cref="Order256"/> and the slice layout are MIRRORED by the static tables in
    /// <c>HiddenHarboursTerrainSplat.shader</c> (MAT_SLICE/MAT_METRES/MAT_OFFSET). A pin test holds
    /// the two in sync — change one, change both.</para>
    ///
    /// <para>Only the kit's PLAN-projection materials are packed here. Sandstone and Bank are cliff
    /// FACES (their UVs run along and down a wall, not over the ground) and the five edge strips are
    /// non-square decals laid along a spline — both are imported but belong to arrays this builder
    /// does not own.</para>
    ///
    /// <para>Terrain pass 9 adds TerrainLight6's inputs, built by their own entry point
    /// (<see cref="BuildRelight"/>) from the kit's relight bake: three more arrays of baked maps in the
    /// albedo array's slice order, and a palette ramp. They are derived and GUID-stable the same way.
    /// <see cref="Build"/> is unchanged by them.</para>
    /// </summary>
    public static class TerrainTexArrayBuilder
    {
        public const string TexDir = "Assets/_Project/Art/Terrain";
        public const string DerivedDir = TexDir + "/Derived";
        public const string Array256Path = DerivedDir + "/TerrainDetail256.asset";
        /// <summary>⚠ OUTLIVES ITS ASSET. The 512-class array was retired by the px flip (2026-09-17,
        /// owner ruling A2): nothing builds it and the asset is deleted. The path stays only because
        /// the two region builders (StPetersBuilder, NineMileCreekBuilder — exporter-tracked, left
        /// untouched on purpose) still load it for <c>TerrainSplatSurface.ConfigureDetail</c>'s second
        /// argument, which now loads null and is ignored. Drop the path and that argument at the
        /// builders' next legitimate edit.</summary>
        public const string Array512Path = DerivedDir + "/TerrainDetail512.asset";

        /// <summary>The plan materials (256 px, 8 m tiles) in canonical slice order — slice base =
        /// index × 3. APPEND ONLY: the shader's MAT_SLICE table reads these positions by number.
        /// Eelgrass and Irishmoss arrived with kit v3 (materials.json sizes them 256).</summary>
        public static readonly string[] Order256 =
        {
            "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed",
            "Eelgrass", "Irishmoss",
            // The mown dooryard lawn (2026-08-26). APPENDED, because the shader's MAT_SLICE table
            // holds each material's base slice as a literal — inserting anywhere but the end would
            // silently repaint every material after it.
            "Lawn",
            // The px flip (2026-09-17, owner ruling A2): the kit ships these seven at 256 px / 8 m, so
            // they left the retired 512 array and were APPENDED here, after Lawn (slices 36..54). Their
            // splat indices (3, 4, 6, 10, 11, 14, 15) do not move; only their MAT_SLICE and MAT_METRES
            // rows in the shader do.
            "Shingle", "Ripple", "Silt", "Foreshore", "Talus", "Musselbed", "Oysterreef",
            // Mud, new with the px kit (2026-09-17, owner ruling M1): splat index 19, _SplatE.a. Slice 57.
            "Mud",
            // Path, new with terrain pass 9 (2026-09-25): splat index 20, _SplatF.r. Slice 60.
            "Path",
        };

        /// <summary>The ladder suffixes, in slice order (README §2: _Lo = 0, base = 1, _Hi = 2).</summary>
        public static readonly string[] LadderSteps = { "_Lo", "", "_Hi" };

        /// <summary>Terrain pass 9: the kit's relight bake manifest (bakePass9.js, docs/art/rigs/terrain/pass9).
        /// Per kit tile: its palettes, height range and pond depth. The tile's three maps lie beside its
        /// albedo as <c>&lt;Name&gt;&lt;Step&gt;_normal.png</c>, <c>_light.png</c> and <c>_detail.png</c>.</summary>
        public const string RelightJsonPath = TexDir + "/TerrainRelight.json";

        /// <summary>The maps' suffixes, in the order of <see cref="RelightArrayPaths"/>.</summary>
        public static readonly string[] RelightMapSuffixes = { "_normal", "_light", "_detail" };

        /// <summary>The relight maps, packed as the albedo is: one slice per <see cref="Order256"/> material
        /// and ladder step, at the albedo's slice numbers. Linear and without mips, because the shader
        /// LOADs them texel by texel as bytes. A material with no baked tiles (the lawn) has zero slices.</summary>
        public static readonly string[] RelightArrayPaths =
        {
            DerivedDir + "/TerrainRelightNormal.asset",
            DerivedDir + "/TerrainRelightLight.asset",
            DerivedDir + "/TerrainRelightDetail.asset",
        };

        /// <summary>The palette ramp: a row per slice, <see cref="TerrainSplatSurface.RelightRampWidth"/>
        /// wide (see <see cref="PackRampRow"/>). Float, because a slice's parameters are metres.</summary>
        public const string RelightRampPath = DerivedDir + "/TerrainRelightRamp.asset";

        /// <summary>The ramp's palettes per slice and bands per palette. The bake refuses a tile of more
        /// than sixteen palettes (bakePass9.js), and the ramp holds sixteen.</summary>
        public const int RelightPalettes = 16, RelightBands = 5;

        [MenuItem("Hidden Harbours/Art/Build Terrain Texture Arrays", priority = 24)]
        public static void BuildMenu() => Build();

        /// <summary>Build the array. Returns the slices written, 0 if the kit is absent
        /// (warn-and-skip, the shore painter's convention — never half a kit).</summary>
        public static int Build()
        {
            var a256 = BuildArray(Order256, 256);
            if (a256 == null) return 0;

            if (!AssetDatabase.IsValidFolder(DerivedDir))
                AssetDatabase.CreateFolder(TexDir, "Derived");
            SaveInPlace(a256, Array256Path);
            AssetDatabase.SaveAssets();
            // ⚠ Read the PERSISTED assets, never the temps: on the REBUILD path SaveInPlace has just
            // CopySerialized'd the temp into the existing asset and DESTROYED the temp — so `a256.depth`
            // here was a MissingReferenceException that killed every St Peters build on any machine that
            // had built the arrays before (first hit: the owner's, 2026-08-02; every agent machine and CI
            // builds into a fresh worktree, takes the CreateAsset path, and never sees it). Reading back
            // from the path also proves the save actually landed.
            var p256 = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Array256Path);
            int slices = p256 != null ? p256.depth : 0;
            Debug.Log($"[TerrainTexArrayBuilder] Packed {slices} slices " +
                      $"({Order256.Length} materials x {LadderSteps.Length} steps).");
            return slices;
        }

        [MenuItem("Hidden Harbours/Art/Build Terrain Relight Arrays", priority = 25)]
        public static void BuildRelightMenu() => BuildRelight();

        /// <summary>Terrain pass 9: pack the kit's relight maps and palette ramp for TerrainLight6, from the
        /// bake at <see cref="RelightJsonPath"/>. Returns the slices written: 0 when there is no bake (said in
        /// the log) or when it is refused (warned). It never builds half a kit: a material has all its
        /// ladder steps baked or none, and every baked tile's maps must be present and whole.</summary>
        public static int BuildRelight()
        {
            if (!File.Exists(RelightJsonPath))
            {
                Debug.Log($"[TerrainTexArrayBuilder] No relight bake at '{RelightJsonPath}': relight arrays " +
                          "not built (the ground keeps the albedo).");
                return 0;
            }

            RelightManifest manifest;
            try { manifest = JsonUtility.FromJson<RelightManifest>(File.ReadAllText(RelightJsonPath)); }
            catch (System.ArgumentException e) { return RefuseRelight($"'{RelightJsonPath}' does not parse ({e.Message})."); }
            int size = TerrainSplatSurface.RelightTileTexels;
            if (manifest?.tiles == null || manifest.size != size)
                return RefuseRelight($"'{RelightJsonPath}' has no tiles, or its tiles are not {size} px.");

            var byName = new Dictionary<string, RelightTile>();
            foreach (var t in manifest.tiles)
            {
                if (t?.name == null || byName.ContainsKey(t.name))
                    return RefuseRelight($"a tile is unnamed or named twice ('{t?.name}').");
                byName.Add(t.name, t);
            }

            // Each slice's tile, or null for a slice without maps.
            int depth = Order256.Length * LadderSteps.Length;
            var tiles = new RelightTile[depth];
            for (int m = 0; m < Order256.Length; m++)
            {
                int baked = 0;
                for (int s = 0; s < LadderSteps.Length; s++)
                {
                    if (!byName.TryGetValue(Order256[m] + LadderSteps[s], out var t)) continue;
                    if (t.step != s) return RefuseRelight($"'{t.name}' says step {t.step}; its name says {s}.");
                    tiles[m * LadderSteps.Length + s] = t;
                    baked++;
                }
                if (baked != 0 && baked != LadderSteps.Length)
                    return RefuseRelight($"{Order256[m]} has {baked} of its {LadderSteps.Length} steps baked.");
            }

            var mapPaths = new List<string>();
            foreach (var t in tiles)
                if (t != null)
                    foreach (string suffix in RelightMapSuffixes)
                        mapPaths.Add($"{TexDir}/{t.name}{suffix}.png");
            foreach (string p in mapPaths)
                if (!(AssetImporter.GetAtPath(p) is TextureImporter))
                    return RefuseRelight($"'{p}' is missing.");
            int importsSet = SetRelightImports(mapPaths);

            var arrays = new Texture2DArray[RelightMapSuffixes.Length];
            for (int a = 0; a < arrays.Length; a++)
            {
                // linear:true, no mips: the maps are numbers, LOADed at mip 0 (Include/TerrainLight6.hlsl).
                arrays[a] = new Texture2DArray(size, size, depth, TextureFormat.RGBA32, mipChain: false, linear: true)
                {
                    name = Path.GetFileNameWithoutExtension(RelightArrayPaths[a]),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                    anisoLevel = 0,
                };
            }
            var ramp = new Texture2D(TerrainSplatSurface.RelightRampWidth, depth, TextureFormat.RGBAFloat,
                                     mipChain: false, linear: true)
            {
                name = Path.GetFileNameWithoutExtension(RelightRampPath),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
            var rampPx = new Color[TerrainSplatSurface.RelightRampWidth * depth];
            var blank = new Color32[size * size];

            string fault = null;
            for (int slice = 0; slice < depth && fault == null; slice++)
            {
                var t = tiles[slice];
                if (t == null)
                {
                    // No maps: zero texels, and a zero ramp row, whose parameters' w = 0 tells the shader
                    // to keep this material's albedo.
                    foreach (var arr in arrays) arr.SetPixels32(blank, slice);
                    continue;
                }
                fault = PackRampRow(t.palettes, t.heightMin, t.heightRange, t.pondMax, rampPx,
                                    slice * TerrainSplatSurface.RelightRampWidth);
                if (fault != null) fault = $"'{t.name}': {fault}";
                for (int a = 0; a < arrays.Length && fault == null; a++)
                {
                    string path = $"{TexDir}/{t.name}{RelightMapSuffixes[a]}.png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex == null || tex.width != size || tex.height != size || !tex.isReadable)
                        fault = $"'{path}' did not load as a readable {size} px texture.";
                    else
                        arrays[a].SetPixels32(tex.GetPixels32(), slice);
                }
            }
            if (fault != null)
            {
                foreach (var arr in arrays) Object.DestroyImmediate(arr);
                Object.DestroyImmediate(ramp);
                return RefuseRelight(fault);
            }

            foreach (var arr in arrays) arr.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            ramp.SetPixels(rampPx);
            ramp.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            if (!AssetDatabase.IsValidFolder(DerivedDir))
                AssetDatabase.CreateFolder(TexDir, "Derived");
            for (int a = 0; a < arrays.Length; a++) SaveInPlace(arrays[a], RelightArrayPaths[a]);
            SaveInPlace(ramp, RelightRampPath);
            AssetDatabase.SaveAssets();

            // Read the persisted assets back, never the temps (Build()'s lesson: a rebuild destroys them).
            int slices = depth;
            foreach (string p in RelightArrayPaths)
            {
                var persisted = AssetDatabase.LoadAssetAtPath<Texture2DArray>(p);
                slices = Mathf.Min(slices, persisted != null ? persisted.depth : 0);
            }
            var persistedRamp = AssetDatabase.LoadAssetAtPath<Texture2D>(RelightRampPath);
            if (persistedRamp == null || persistedRamp.height != slices) slices = 0;

            int relit = 0;
            foreach (var t in tiles) if (t != null) relit++;
            var unslotted = new List<string>();
            foreach (var t in manifest.tiles) if (System.Array.IndexOf(tiles, t) < 0) unslotted.Add(t.name);
            Debug.Log($"[TerrainTexArrayBuilder] Packed the relight: {slices} slices, {relit} of them with maps" +
                      (importsSet > 0 ? $"; {importsSet} map imports set to linear data" : "") +
                      (unslotted.Count > 0 ? $"; baked but not in {nameof(Order256)}: {string.Join(", ", unslotted)}" : "") +
                      ".");
            return slices;
        }

        /// <summary>One slice's ramp row, at <paramref name="rowStart"/> in <paramref name="ramp"/>: palette
        /// p's band b at x = p * <see cref="RelightBands"/> + b as sRGB bytes 0..255 (alpha 255), then the
        /// slice's parameters (heightMin, heightRange, pondMax, 1) at x = <see cref="RelightPalettes"/> *
        /// <see cref="RelightBands"/>. That is the layout Include/TerrainLight6.hlsl's TL6Pal and TL6Params
        /// read. <paramref name="palettes"/> is the bake's flat list of "#rrggbb", five to a palette.
        /// Returns null, or why the row was refused.</summary>
        public static string PackRampRow(string[] palettes, float heightMin, float heightRange, float pondMax,
                                         Color[] ramp, int rowStart)
        {
            if (palettes == null || palettes.Length == 0 || palettes.Length % RelightBands != 0)
                return $"{palettes?.Length ?? 0} palette colours, not whole palettes of {RelightBands}.";
            if (palettes.Length > RelightPalettes * RelightBands)
                return $"{palettes.Length / RelightBands} palettes; the ramp holds {RelightPalettes}.";
            for (int x = 0; x < TerrainSplatSurface.RelightRampWidth; x++) ramp[rowStart + x] = default;
            for (int i = 0; i < palettes.Length; i++)
            {
                string h = palettes[i];
                if (h == null || h.Length != 7 || h[0] != '#'
                    || !int.TryParse(h.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                                     System.Globalization.CultureInfo.InvariantCulture, out int rgb))
                    return $"palette colour '{h}' is not #rrggbb.";
                ramp[rowStart + i] = new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255, 255);
            }
            ramp[rowStart + RelightPalettes * RelightBands] = new Color(heightMin, heightRange, pondMax, 1f);
            return null;
        }

        private static int RefuseRelight(string why)
        {
            Debug.LogWarning($"[TerrainTexArrayBuilder] Relight arrays not built: {why} The ground keeps the albedo.");
            return 0;
        }

        /// <summary>A relight map is numbers, not colour: linear, its alpha kept as data (alphaIsTransparency
        /// would bleed RGB into every texel whose alpha is 0, which is most of them), uncompressed and
        /// readable. ArtImportPipeline stamps a first import as an sRGB sprite, and its data-channel suffixes
        /// cannot claim "_light" (foliage's *_light.png are colour), so the builder sets its own maps.
        /// Returns how many it had to set.</summary>
        private static int SetRelightImports(List<string> paths)
        {
            int set = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string p in paths)
                {
                    var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                    if (imp.textureType == TextureImporterType.Default && !imp.sRGBTexture && !imp.alphaIsTransparency
                        && imp.isReadable && !imp.mipmapEnabled && imp.filterMode == FilterMode.Point
                        && imp.textureCompression == TextureImporterCompression.Uncompressed
                        && imp.npotScale == TextureImporterNPOTScale.None)
                        continue;
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = false;
                    imp.alphaIsTransparency = false;
                    imp.isReadable = true;
                    imp.mipmapEnabled = false;
                    imp.filterMode = FilterMode.Point;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.SaveAndReimport();
                    set++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return set;
        }

        [System.Serializable]
        private class RelightManifest
        {
            public int size;
            public RelightTile[] tiles;
        }

        [System.Serializable]
        private class RelightTile
        {
            public string name;
            public int step;
            public string[] palettes;
            public float heightMin, heightRange, pondMax;
        }

        private static Texture2DArray BuildArray(string[] order, int size)
        {
            int depth = order.Length * LadderSteps.Length;
            // linear:false — the kit is sRGB albedo (materials.json), sampled as colour.
            var arr = new Texture2DArray(size, size, depth, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = $"TerrainDetail{size}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,   // the kit contract: Repeat + Point (README §6)
                anisoLevel = 0,
            };

            for (int m = 0; m < order.Length; m++)
            for (int s = 0; s < LadderSteps.Length; s++)
            {
                string path = $"{TexDir}/{order[m]}{LadderSteps[s]}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null || tex.width != size || tex.height != size)
                {
                    Debug.LogWarning($"[TerrainTexArrayBuilder] '{path}' missing or not {size}px — " +
                                     "terrain detail arrays not built (the shader falls back to flat colours).");
                    return null;
                }
                if (!tex.isReadable)
                {
                    Debug.LogWarning($"[TerrainTexArrayBuilder] '{path}' is not readable — check the import " +
                                     "settings (isReadable must be on for the pack).");
                    return null;
                }
                arr.SetPixels32(tex.GetPixels32(), m * LadderSteps.Length + s);
            }

            arr.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return arr;
        }

        private static void SaveInPlace<T>(T built, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return;
            }
            EditorUtility.CopySerialized(built, existing);   // keep the GUID scenes reference
            Object.DestroyImmediate(built);
        }
    }
}
