#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class KeySceneImport
    {
        public const string EastEndLookPassId = "lookpass.stp_east_end";
        public const string EastEndFile = "scenes/lookpass.stp_east_end/scene.json";
        public const string LandingFile = "scenes/keyscene.stp_landing/scene.json";
        public const string EastEndSha256 = "ff311bcbcdad346c0b92795a5e28b38297404565015be2f1659259613738bf43";
        public const string EastEndLandingSha256 = "2d8bde76678511edfa188d3d5df58410dae14da69c862e1f7121a19c6778bb09";

        static bool IsEastEndCompanion(string sceneId, Json piece) => sceneId == "keyscene.stp_landing" &&
            piece.Get("take")?.Text == "with its building" && piece.Get("with")?.Text == "structure.stp_bait_store";

        /// <summary>CD's second issue: 17 east-end rows and the Landing's four. Both source pins are checked
        /// before ground is read. Ground remains authoritative; CD's z must agree within the ruled 4 cm.</summary>
        public static Result ImportEastEnd(byte[] bytes, byte[] landingBytes, Func<Vector2, float> ground,
            IReadOnlyDictionary<string, string> existing, string scriptGuid)
        {
            if (bytes == null || Sha256(bytes) != EastEndSha256 || landingBytes == null || Sha256(landingBytes) != EastEndLandingSha256)
                throw new Refusal("The east end needs both ruled second-issue files. STOP for the Art desk.");
            if (scriptGuid == null || !GuidRx.IsMatch(scriptGuid)) throw new Refusal("Invalid KeySceneDef script guid.");
            existing = WithoutNmc(existing);
            Json source = Json.Parse(Text(bytes, EastEndFile), EastEndFile);
            Json landing = Json.Parse(Text(landingBytes, LandingFile), LandingFile);
            if (Need(source, "id", EastEndFile).Text != EastEndLookPassId || Need(source, "schema", EastEndFile).Text != "hidden-harbours/look-pass@2")
                throw new Refusal("The east-end source is not look-pass@2.");
            var scene = new Scene
            {
                Id = "keyscene.stp_east_end", Name = "St Peters east end",
                Source = "HH-st-peters-one-scene-package-2026-10-07, " + EastEndFile + ", " + bytes.Length +
                    " bytes, sha256 " + EastEndSha256 + "; " + LandingFile + ", " + landingBytes.Length + " bytes, sha256 " + EastEndLandingSha256,
            };
            var result = new Result();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var pieces = Items(Need(source, "pieces", scene.Id), "east end pieces").ToList();
            var companions = Items(Need(landing, "pieces", "Landing"), "Landing pieces").Where(p => IsEastEndCompanion("keyscene.stp_landing", p)).ToList();
            if (pieces.Count != 17 || companions.Count != 4) throw new Refusal("The east end needs 17 rows and four bait-store companions.");
            pieces.AddRange(companions);
            foreach (Json p in pieces)
            {
                string take = Need(p, "take", scene.Id).Str("take");
                if (take != "owner" && take != "with its building") throw new Refusal("Unexpected east-end take: " + take);
                string id = Need(p, "id", scene.Id).Str("id");
                if (id.IndexOf('@') >= 0 || !ids.Add(id)) throw new Refusal("Invalid east-end id: " + id);
                var row = new Row
                {
                    Id = id, Kind = take == "owner" ? KeySceneDef.KindBuilding : KeySceneDef.KindSetPiece,
                    Kit = Need(p, "kit", id).Str("kit"), Piece = Need(p, "piece", id).Str("piece"),
                    Dir = Need(p, "dir", id).Int("dir"),
                };
                (row.X, row.Y) = Pair(Need(p, "at", id), id + " at");
                row.Z = ground(new Vector2(row.X, row.Y));
                if (float.IsNaN(row.Z) || float.IsInfinity(row.Z)) throw new Refusal("No ground at " + id);
                float cdZ = Need(p, "z", id).Float(id + " z");
                if (Math.Abs(row.Z - cdZ) > 0.04f) throw new Refusal(id + ": CD's z is more than 4 cm from committed ground.");
                row.Variants.Add(KeySceneDef.Today);
                Json options = p.Get("opts");
                if (options != null)
                    foreach (var option in options.Fields)
                        row.Options.Add(new KeyValuePair<string, string>(option.Key,
                            option.Value.Kind == Json.Of.String ? option.Value.Text : option.Value.Compact()));
                scene.Rows.Add(row);
                scene.Tally.Pieces++; scene.Tally.Today++; scene.Tally.Kits.Add(row.Kit);
                result.Report.Add($"{id}: CD {Num(cdZ)}; ground {Num(row.Z)}; take selects ownership; with routes companions; zPass2/seated stay in the package");
            }
            if (scene.Rows.Count != 21) throw new Refusal("The ruled east end must hold 21 rows.");
            var previous = ReadExisting(existing, scriptGuid);
            if (previous.TryGetValue(scene.Id, out Existing old))
            {
                var order = old.Rows.Select(r => r.Get("Id").Scalar).ToList();
                if (order.Count != ids.Count || order.Any(id => !ids.Contains(id)))
                    throw new Refusal("CD's east-end ids differ from the committed 21. STOP for the Art desk.");
                // Joining two files must not reorder the existing single asset.
                scene.Rows.Sort((a, b) => order.IndexOf(a.Id).CompareTo(order.IndexOf(b.Id)));
                result.Report.Add("east end: joined by id in the existing asset's order");
            }
            Write(scene, previous, existing, "region.st_peters", scriptGuid, result);
            return result;
        }

        /// <summary>One atomic, engine-free result for the nine scenes and the east end. Checks one home per
        /// piece across the result and any untouched St Peters assets before returning anything to write.</summary>
        public static Result ImportSecondIssue(string packageName, Func<string, byte[]> read,
            IReadOnlyDictionary<string, string> existing, string scriptGuid, byte[] idMap, byte[] hosts, Func<Vector2, float> ground)
        {
            var result = Import(packageName, read, existing, "region.st_peters", scriptGuid, idMap, hosts);
            var east = ImportEastEnd(read(EastEndFile), read(LandingFile), ground, existing, scriptGuid);
            foreach (var p in east.Assets) result.Assets.Add(p.Key, p.Value);
            foreach (var p in east.Metas) result.Metas.Add(p.Key, p.Value);
            foreach (var p in east.Tallies) result.Tallies.Add(p.Key, p.Value);
            result.Changed.UnionWith(east.Changed);
            result.Report.AddRange(east.Report);
            var all = new Dictionary<string, string>(StringComparer.Ordinal);
            if (existing != null) foreach (var p in WithoutNmc(existing)) all[p.Key] = p.Value;
            foreach (var p in result.Assets) all[p.Key] = p.Value;
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var asset in all)
                foreach (var row in Get(ReadAsset(asset.Value, asset.Key), "Pieces").Items)
                {
                    string id = row.Get("Id").Scalar;
                    if (owners.TryGetValue(id, out string owner)) throw new Refusal(id + " lives in both " + owner + " and " + asset.Key);
                    owners.Add(id, asset.Key);
                }
            result.Report.Add("one home per id: " + owners.Count + " pieces; second-issue assets: " + result.Assets.Count);
            return result;
        }

        /// <summary>The no-Unity entry point: read the committed ground asset and its referenced PNG, use the
        /// game's PaintedHeightField sampler, and return all ten assets. The caller chooses where to write.</summary>
        public static Result ReadSecondIssueFromFiles(string packageFolder, string assetFolder, string groundAsset,
            string scriptGuid, byte[] idMap, byte[] hosts)
        {
            string root = Path.GetFullPath(packageFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            byte[] Read(string relative)
            {
                string path = Path.GetFullPath(Path.Combine(root, relative));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Refusal("Package path escapes its root.");
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            var old = Directory.GetFiles(assetFolder, "KeyScene_stp_*.asset").ToDictionary(Path.GetFileName, File.ReadAllText);
            var ground = ReadGroundFromFiles(groundAsset);
            return ImportSecondIssue(PackageName(root), Read, old, scriptGuid, idMap, hosts, ground.ElevationAt);
        }

        public static PaintedHeightField ReadGroundFromFiles(string assetPath)
        {
            var fields = ReadAsset(File.ReadAllText(assetPath), assetPath);
            string texture = Get(fields, "_heightTexture").Scalar;
            string guid = Regex.Match(texture, @"guid: ([0-9a-f]{32})").Groups[1].Value;
            if (guid.Length == 0) throw new Refusal("Ground has no height texture guid.");
            string[] metas = Directory.GetFiles(Path.GetDirectoryName(assetPath), "*.png.meta")
                .Where(p => Regex.IsMatch(File.ReadAllText(p), @"(?m)^guid: " + guid + @"\r?$" )).ToArray();
            if (metas.Length != 1) throw new Refusal("Ground height texture must resolve once beside its asset.");
            var image = TerrainPlanPng.Read(File.ReadAllBytes(metas[0].Substring(0, metas[0].Length - 5)));
            if (image.BitDepth != 16 || image.Channels != 1) throw new Refusal("Ground must be an R16 PNG.");
            float Number(string name) => float.Parse(Get(fields, name).Scalar, CultureInfo.InvariantCulture);
            Vector2 Vector(string name)
            {
                Match m = Regex.Match(Get(fields, name).Scalar, @"^\{x: ([^,]+), y: ([^}]+)\}$");
                if (!m.Success) throw new Refusal("Invalid ground " + name);
                return new Vector2(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            }
            float min = Number("_minElevation"), max = Number("_maxElevation");
            var elevations = new float[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    elevations[y * image.Width + x] = PaintedHeightField.DecodeElevation(image.Samples[(image.Height - 1 - y) * image.Width + x] / 65535f, min, max);
            return new PaintedHeightField(elevations, image.Width, image.Height, Vector("_worldCenter"), Vector("_worldSize"));
        }
    }
}
#endif
