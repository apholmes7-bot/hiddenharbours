using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE PLAN'S MAPS, WRITTEN</b> (terrain PR 5, Phase B). A derivation's result to the committed files through the
    /// project's own writers: the height map at sixteen bits over the plan's range (<see cref="PaintedHeightPng"/>, the
    /// paint tool's path, over the same file so the seabed asset's reference holds), the still-water map beside it, the
    /// seabed asset's range and still-map binding, the six splat maps (<see cref="TerrainSplatAssets"/>, the brush's
    /// commit), and the manifest.
    ///
    /// <para>Then every file is read back and its values compared with the derivation's (<see cref="Differences"/>):
    /// a write that does not read back as derived throws, and the manifest is not written. The determinism test runs the
    /// same comparison against a fresh derivation.</para>
    ///
    /// <para>What it does not touch: the scene's copies of the range (the splat's and the sea's <c>_HeightMin</c> and
    /// <c>_HeightMax</c>) and its map bindings, which PR 5 edits by hand in the scene's YAML, gated by named deletions.</para>
    /// </summary>
    public static class TerrainPlanMapWriter
    {
        public const string HeightRole = "height";
        public const string StillRole = "still";

        public static string SplatRole(int map) => "splat" + "ABCDEF"[map];

        /// <summary>Every map's values as its file holds them (Unity pixel order, row 0 south), and the range.</summary>
        public sealed class Expected
        {
            public int W, H;
            public float Min, Max;
            public ushort[] Height, Still;
            public byte[][] Splat;
        }

        /// <summary>
        /// What the files must hold for this derivation at the plan's range. With a ground file the height is its import's
        /// codes as they are (PR 5 B), and the ground the paint and the water were laid on must encode to exactly them.
        /// </summary>
        public static Expected ExpectedOf(RegionTerrainPlanDef plan, TerrainPlanResult r)
        {
            float min = plan.HeightRange.x, max = plan.HeightRange.y;
            var height = TerrainPlanMaps.Codes(TerrainPlanMaps.HeightR01(r.Grid, r.E, min, max));
            if (r.GroundCodes != null)
            {
                if (r.GroundCodes.Length != height.Length) throw new InvalidDataException("[TerrainPlan] the import's codes do not fill the grid.");
                int off = 0, first = -1;
                for (int i = 0; i < height.Length; i++)
                    if (height[i] != r.GroundCodes[i]) { off++; if (first < 0) first = i; }
                if (off > 0)
                    throw new InvalidDataException("[TerrainPlan] the plan's ground does not encode to its import's codes at " + off +
                                                   " pixels, the first " + first + " (" + height[first] + " for " + r.GroundCodes[first] + ").");
                height = (ushort[])r.GroundCodes.Clone();
            }
            return new Expected
            {
                W = r.Grid.W, H = r.Grid.H, Min = min, Max = max,
                Height = height,
                Still = TerrainPlanMaps.StillCodes(r.Grid, r.Still, min, max),
                Splat = TerrainPlanMaps.Splat(r.Grid, r.Zone, r.PathWeight),
            };
        }

        /// <summary>Write the maps, the seabed asset's range and bindings, and the manifest; returns the manifest.</summary>
        public static TerrainPlanManifest Write(RegionTerrainPlanDef plan, TerrainPlanSources src, TerrainPlanResult r)
        {
            var x = ExpectedOf(plan, r);
            var map = StPetersTerrainPlan.LoadSeabed();
            string heightPath = AssetDatabase.GetAssetPath(map.HeightTexture);
            if (map.HeightTexture.width != x.W || map.HeightTexture.height != x.H)
                throw new InvalidDataException("[TerrainPlan] " + heightPath + " is " + map.HeightTexture.width + " × " + map.HeightTexture.height + ", not the grid's " + x.W + " × " + x.H + ".");

            // the height and the still water, at sixteen bits: each code as normalized R, which EncodeCodes rounds back to it
            var heightTex = PaintedHeightPng.WriteAndImport(heightPath, Grey(x.Height), x.W, x.H);
            var stillTex = PaintedHeightPng.WriteAndImport(StPetersTerrainPlan.StillPath, Grey(x.Still), x.W, x.H);
            using (var so = new SerializedObject(map))
            {
                Prop(so, "_heightTexture").objectReferenceValue = heightTex;
                Prop(so, "_stillLevelTexture").objectReferenceValue = stillTex;
                Prop(so, "_minElevation").floatValue = x.Min;
                Prop(so, "_maxElevation").floatValue = x.Max;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(map);

            // the splat, through the brush's own commit
            var textures = new Texture2D[TerrainSplatBrush.TextureCount];
            var pixels = new Color[TerrainSplatBrush.TextureCount][];
            if (!TerrainSplatAssets.LoadOrCreate(new Vector2Int(x.W, x.H), textures, pixels))
                throw new InvalidOperationException("[TerrainPlan] the splat maps would not load.");
            for (int m = 0; m < TerrainPlanMaps.SplatMaps; m++)
            {
                if (textures[m].width != x.W || textures[m].height != x.H)
                    throw new InvalidDataException("[TerrainPlan] " + TerrainSplatAssets.PathOf(m) + " is not the grid's size.");
                pixels[m] = Rgba(x.Splat[m]);
            }
            TerrainSplatAssets.Commit(textures, pixels);
            AssetDatabase.SaveAssets();

            var problems = Differences(x);
            if (problems.Count > 0)
                throw new InvalidDataException("[TerrainPlan] the maps did not read back as derived (" + problems.Count + "): " + string.Join("; ", problems.Take(8)));

            var man = Manifest(plan, src, r, x);
            File.WriteAllText(StPetersTerrainPlan.ManifestPath, man.Write(r), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(StPetersTerrainPlan.ManifestPath, ImportAssetOptions.ForceSynchronousImport);
            return man;
        }

        /// <summary>
        /// How the committed files differ from <paramref name="x"/>: the seabed asset's range and still binding, and each
        /// map's values as its file holds them. Empty when they all agree.
        /// </summary>
        public static List<string> Differences(Expected x)
        {
            var o = new List<string>();
            var map = StPetersTerrainPlan.LoadSeabed();
            if (map.MinElevation != x.Min || map.MaxElevation != x.Max)
                o.Add("the seabed asset's range is " + map.MinElevation + " to " + map.MaxElevation + ", not " + x.Min + " to " + x.Max);
            string still = map.StillLevelTexture == null ? "(none)" : AssetDatabase.GetAssetPath(map.StillLevelTexture);
            if (still != StPetersTerrainPlan.StillPath) o.Add("the seabed asset's still map is " + still + ", not " + StPetersTerrainPlan.StillPath);
            Codes16(o, HeightRole, AssetDatabase.GetAssetPath(map.HeightTexture), x.Height, x.W, x.H);
            Codes16(o, StillRole, StPetersTerrainPlan.StillPath, x.Still, x.W, x.H);
            for (int m = 0; m < TerrainPlanMaps.SplatMaps; m++) Rgba8(o, SplatRole(m), TerrainSplatAssets.PathOf(m), x.Splat[m], x.W, x.H);
            return o;
        }

        static void Codes16(List<string> o, string role, string path, ushort[] want, int w, int h)
        {
            if (!File.Exists(path)) { o.Add(role + ": no file at " + path); return; }
            var img = TerrainPlanPng.ReadFile(path);
            if (img.Width != w || img.Height != h || img.BitDepth != 16 || img.Channels != 1)
            {
                o.Add(role + ": " + path + " is " + img.Width + " × " + img.Height + " " + img.BitDepth + "-bit with " + img.Channels + " channels, not 16-bit grey");
                return;
            }
            var got = TerrainPlanPng.FlipRows(img.Channel(0), w, h);
            int diff = 0, first = -1;
            for (int i = 0; i < got.Length; i++) if (got[i] != want[i]) { diff++; if (first < 0) first = i; }
            if (diff > 0) o.Add(role + ": " + diff + " codes differ, the first at pixel " + first + ": " + got[first] + " vs " + want[first]);
        }

        static void Rgba8(List<string> o, string role, string path, byte[] want, int w, int h)
        {
            if (!File.Exists(path)) { o.Add(role + ": no file at " + path); return; }
            var img = TerrainPlanPng.ReadFile(path);
            if (img.Width != w || img.Height != h || img.BitDepth != 8 || img.Channels != 4)
            {
                o.Add(role + ": " + path + " is " + img.Width + " × " + img.Height + " " + img.BitDepth + "-bit with " + img.Channels + " channels, not 8-bit RGBA");
                return;
            }
            int diff = 0, first = -1;
            for (int ch = 0; ch < 4; ch++)
            {
                var got = TerrainPlanPng.FlipRows(img.Channel8(ch), w, h);
                for (int i = 0; i < got.Length; i++)
                    if (got[i] != want[i * 4 + ch]) { diff++; if (first < 0 || i * 4 + ch < first) first = i * 4 + ch; }
            }
            if (diff > 0) o.Add(role + ": " + diff + " channel values differ, the first at pixel " + first / 4 + " channel " + first % 4);
        }

        /// <summary>The manifest of this write: the plan, the sources, every Def by file, and every map.</summary>
        public static TerrainPlanManifest Manifest(RegionTerrainPlanDef plan, TerrainPlanSources src, TerrainPlanResult r, Expected x)
        {
            var man = new TerrainPlanManifest
            {
                PlanId = plan.Id, Seed = plan.Seed, Part2Seed = plan.Part2Seed, HeightMin = x.Min, HeightMax = x.Max,
                SourcesPath = StPetersTerrainPlan.SourcesPath, SourcesSha256 = StPetersTerrainPlan.FileSha256(StPetersTerrainPlan.SourcesPath),
                BaseFrom = StPetersTerrainPlan.BaseFrom, BaseSha256 = TerrainPlanMaps.Sha256(src.Base),
            };
            var defs = StPetersTerrainPlan.LoadDefs(out var files);
            for (int k = 0; k < defs.Count; k++)
            {
                if (defs[k] == null) continue;
                string path = StPetersTerrainPlan.DefsFolder + "/" + files[k];
                var field = defs[k].GetType().GetField("SourceId");
                man.Defs.Add((TerrainPlanValidation.IdOf(defs[k]), path, StPetersTerrainPlan.FileSha256(path), field?.GetValue(defs[k]) as string ?? ""));
            }
            // the Defs the plan reads from other folders: the village's routes (PR 5 B), and the ground file with its asks
            void Other(string id, ScriptableObject def)
            {
                string path = AssetDatabase.GetAssetPath(def);
                man.Defs.Add((id, path, StPetersTerrainPlan.FileSha256(path), ""));
            }
            foreach (var route in plan.Routes ?? new RouteDef[0])
                if (route != null) Other(route.Id, route);
            if (plan.Ground != null)
            {
                Other(plan.Ground.Id, plan.Ground);
                foreach (var ask in plan.Ground.Asks ?? new GroundAskDef[0])
                    if (ask != null) Other(ask.Id, ask);
            }
            RecordGround(man, plan.Ground, src.Import);
            string heightPath = AssetDatabase.GetAssetPath(StPetersTerrainPlan.LoadSeabed().HeightTexture);
            man.Maps.Add((HeightRole, heightPath, TerrainPlanMaps.Sha256(x.Height), StPetersTerrainPlan.FileSha256(heightPath)));
            for (int m = 0; m < TerrainPlanMaps.SplatMaps; m++)
            {
                string path = TerrainSplatAssets.PathOf(m);
                man.Maps.Add((SplatRole(m), path, TerrainPlanMaps.Sha256(x.Splat[m]), StPetersTerrainPlan.FileSha256(path)));
            }
            man.Maps.Add((StillRole, StPetersTerrainPlan.StillPath, TerrainPlanMaps.Sha256(x.Still), StPetersTerrainPlan.FileSha256(StPetersTerrainPlan.StillPath)));
            return man;
        }

        /// <summary>
        /// The manifest's record of the ground file an import came from (amendment 1 §4.1 item 8): the file's sha256, its
        /// base's pixel hash and each live patch's, as the import checked them, and the import's codes. Nothing without one.
        /// </summary>
        public static void RecordGround(TerrainPlanManifest man, GroundFileDef f, GroundFileImport.Result imp)
        {
            if (f == null || imp == null) return;
            man.GroundId = f.Id;
            man.GroundFileSha256 = f.FileSha256 ?? "";
            man.GroundBasePixelsSha256 = imp.BasePixelsSha256;
            man.GroundCodesSha256 = imp.CodesSha256;
            foreach (var d in f.Asks ?? new GroundAskDef[0])
                if (d != null && !d.Retired && d.Kind == GroundAskKind.Patch) man.GroundPatches.Add((d.Id, d.PatchSha256 ?? ""));
        }

        static Color[] Grey(ushort[] codes)
        {
            var px = new Color[codes.Length];
            for (int i = 0; i < codes.Length; i++)
            {
                float v = codes[i] / (float)TerrainPlanMaps.CodeCount;
                px[i] = new Color(v, v, v, 1f);
            }
            return px;
        }

        static Color[] Rgba(byte[] rgba)
        {
            var px = new Color[rgba.Length / 4];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(rgba[4 * i] / 255f, rgba[4 * i + 1] / 255f, rgba[4 * i + 2] / 255f, rgba[4 * i + 3] / 255f);
            return px;
        }

        static SerializedProperty Prop(SerializedObject so, string name) =>
            so.FindProperty(name) ?? throw new MissingFieldException("[TerrainPlan] " + so.targetObject.GetType().Name + " has no serialized field " + name + ".");
    }
}
