using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class GroundFileIntake
    {
        static Vector4 Box(double[] b)
        {
            if (b.Length != 4 || b.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || b[0] > b[2] || b[1] > b[3]) throw Bad("invalid issue box");
            return new Vector4((float)b[0], (float)b[1], (float)b[2], (float)b[3]);
        }

        static IEnumerable<Dictionary<string, object>> Rows(Dictionary<string, object> o, string key) =>
            Arr(o, key).Select(v => v as Dictionary<string, object> ?? throw Bad(key + " contains a non-object"));

        static void ValidateStill(FileRead f)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in Rows(f.StillWater, "ponds").Concat(Rows(f.StillWater, "pools")))
            {
                string id = Str(p, "id");
                if (!TerrainPlanValidation.IsId(id) || !ids.Add(id)) throw Bad("invalid or repeated still-water id " + id);
                Nums(p, "centre", 2); Nums(p, p.ContainsKey("radiiM") ? "radiiM" : "radii", 2);
                if (!(Num(p, "surface") > Bed(p))) throw Bad(id + " has no depth");
            }
            foreach (var s in Rows(f.StillWater, "brooks"))
            {
                string id = Str(s, "id");
                if (!TerrainPlanValidation.IsId(id) || !ids.Add(id)) throw Bad("invalid or repeated brook id " + id);
                int n = Points(Get(s, "line"), id).Length;
                Nums(s, "widthsM", n); Nums(s, "bed", n); Nums(s, "level", n);
                if (s.TryGetValue("fallStations", out var stations))
                    foreach (var v in (List<object>)stations)
                        if (!(v is double j) || j != Math.Floor(j) || j < 0 || j >= n) throw Bad(id + " has an invalid fall station");
            }
            if (f.Sill == null || f.HeathStations == null) throw Bad("the issue has no sill or brook stations");
            var fen = Rows(f.StillWater, "ponds").Single(p => Str(p, "id") == "pond.stp_fen_pool");
            if (Num(f.Sill, "crest") != Num(fen, "surface")) throw Bad("the fen's sill and surface disagree");
            string outlet = Str(Obj(fen, "outlet"), "brook");
            var run = Rows(f.StillWater, "brooks").Single(s => Str(s, "id") == outlet);
            if (Nums(run, "level", Arr(run, "line").Count)[0] != Num(fen, "surface")) throw Bad("the fen and outlet disagree");
            var heath = Rows(f.StillWater, "brooks").Single(s => s.ContainsKey("mouth"));
            var line = Points(Get(heath, "line"), Str(heath, "id")); var beds = Nums(heath, "bed", line.Length);
            if (f.HeathStations.Count != line.Length) throw Bad("the beach and brook station counts differ");
            for (int i = 0; i < line.Length; i++)
            {
                var row = (List<object>)f.HeathStations[i];
                if (row.Count != 3 || (float)(double)row[0] != line[i].x || (float)(double)row[1] != line[i].y || (double)row[2] != beds[i])
                    throw Bad("the beach and brook disagree at station " + i);
            }
        }

        /// <summary>The copies are evidence, never replacement rules. Called before any asset mutation.</summary>
        static void Preflight(FileRead f, string folder, ushort[] baseCodes)
        {
            var plan = StPetersTerrainPlan.LoadPlan();
            var existing = AssetDatabase.FindAssets("t:GroundAskDef", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GroundAskDef>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var byId = existing.ToDictionary(d => d.Id, StringComparer.Ordinal);
            var asks = new List<GroundFileImport.Ask>();
            foreach (var k in f.Asks)
            {
                if (k.IsGameCopy)
                {
                    if (!byId.TryGetValue(k.Id, out var game) || game.Source != GroundAskSource.Game || game.Retired) throw Bad(k.Id + " is not a live game ask");
                    continue;
                }
                if (byId.TryGetValue(k.Id, out var old) && (old.Source != GroundAskSource.Package || old.Retired)) throw Bad("the package clashes with a reserved id " + k.Id);
                asks.Add(ImportAsk(k));
            }
            foreach (var id in f.Dropped.Keys)
                if (!byId.TryGetValue(id, out var d) || d.Source != GroundAskSource.Package) throw Bad("dropped id is not the package's " + id);
            var oldFile = AssetDatabase.LoadAssetAtPath<GroundFileDef>(FilePathOf(folder, f.Id));
            if (oldFile == null || oldFile.Id != f.Id) throw Bad("issue 3 needs the existing ground file");
            var order = (oldFile.Asks ?? new GroundAskDef[0]).Where(d => d != null && d.Source == GroundAskSource.Game).ToArray();
            if (!new HashSet<string>(order.Where(d => !d.Retired).Select(d => d.Id)).SetEquals(f.GameAsks)) throw Bad("the game's ask set changed");
            foreach (var d in order.Where(d => !d.Retired))
            {
                var line = string.IsNullOrEmpty(d.LineOf) ? d.Line : f.Asks.Single(a => a.Id == d.LineOf).Shore;
                var keep = string.IsNullOrEmpty(d.KeepOutOf) ? null : byId[d.KeepOutOf];
                asks.Add(GroundFileImport.AskOf(d, null, line, keep));
            }
            var b = new GroundFileImport.BaseMap { Width = f.BaseWidth, Height = f.BaseHeight, Codes = baseCodes, PixelsSha256 = f.BasePixelsSha256,
                Lo = TerrainPlanMath.Num(f.BaseRange.x), Hi = TerrainPlanMath.Num(f.BaseRange.y), X0 = f.RectMin.x, Y0 = f.RectMin.y, X1 = f.RectMax.x, Y1 = f.RectMax.y };
            var laid = GroundFileImport.Run(b, asks, -TerrainPlanMath.Num(plan.SpringM), plan.HeightRange.x, plan.HeightRange.y);
            CheckGameCopies(f, b, laid, plan.HeightRange.x, plan.HeightRange.y);
        }

        public static GroundFileImport.Ask ImportAsk(FileAsk k) => new GroundFileImport.Ask {
            Id = k.Id, Source = GroundAskSource.Package, Kind = k.HasPatch ? GroundAskKind.Patch : GroundAskKind.Rule,
            BoxX0 = k.X0, BoxY0 = k.Y0, BoxX1 = k.X1, BoxY1 = k.Y1, Step = k.Step, Lo = k.Lo, Hi = k.Hi,
            Width = k.Width, Height = k.Height, Codes = k.Codes, PatchSha256 = k.PatchSha256,
            CentreX = k.Centre.x, CentreY = k.Centre.y, Reach = k.Reach };

        // AssetDatabase is confined to this adapter. Re-runs find the same identities and never append duplicates.
        static void ApplyIssue3(FileRead f, RegionTerrainPlanDef plan, List<string> log)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(plan)).Replace('\\', '/');
            var ponds = plan.Ponds.ToList(); var pools = plan.StillPools.ToList();
            foreach (var a in Rows(f.StillWater, "ponds").Concat(Rows(f.StillWater, "pools")))
            {
                string id = Str(a, "id"); bool fen = id == "pond.stp_fen_pool";
                if (!fen && Str(a, "kind") != "rock pool") continue;
                var list = fen ? ponds : pools;
                var p = list.SingleOrDefault(d => d.Id == id);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<PondDef>(); p.Id = id;
                    string tail = id.Substring(id.IndexOf('.') + 1);
                    string name = (fen ? "Pond_" : "Pool_") + NameOf("ground." + tail);
                    AssetDatabase.CreateAsset(p, folder + "/" + name + ".asset"); list.Add(p);
                }
                p.Kind = fen ? "fen_pool" : "rock_pool";
                p.Centre = Pair(Nums(a, "centre", 2));
                p.Radii = Pair(Nums(a, a.ContainsKey("radiiM") ? "radiiM" : "radii", 2));
                p.RotationDeg = (float)(a.ContainsKey("rotDeg") ? Num(a, "rotDeg") : Num(a, "rotRad") * 180 / Math.PI);
                p.Surface = (float)Num(a, "surface"); p.Bed = (float)Bed(a);
                p.Basin = Vector2.zero; p.AdjustToSpill = true; p.FloorZone = fen ? "" : Str(a, "floorZone");
                p.Outlet = fen ? plan.Streams.Single(s => s.Id == Str(Obj(a, "outlet"), "brook")) : null;
                if (p.Outlet != null) { p.Outlet.Source = p; EditorUtility.SetDirty(p.Outlet); }
                p.Why = "2026-10-07 island-ground@3 " + f.Sha256 + ": flood on imported ground; no second basin.";
                EditorUtility.SetDirty(p); log.Add("  still water " + id);
            }
            plan.Ponds = ponds.ToArray(); plan.StillPools = pools.ToArray();
            foreach (var a in Rows(f.StillWater, "brooks"))
            {
                var s = plan.Streams.Single(d => d.Id == Str(a, "id"));
                if (a.ContainsKey("mouth"))
                {
                    s.Points = Points(Get(a, "line"), s.Id);
                    s.BedZ = Nums(a, "bed", s.Points.Length).Select(v => (float)v).ToArray();
                    s.Widths = Nums(a, "widthsM", s.Points.Length).Select(v => (float)v).ToArray();
                    EditorUtility.SetDirty(s);
                }
                if (a.TryGetValue("fallStations", out var stations))
                {
                    var fall = plan.Falls.Single(d => d.Stream == s);
                    fall.StreamStations = ((List<object>)stations).Select(v => (int)(double)v).ToArray();
                    EditorUtility.SetDirty(fall);
                }
            }
            EditorUtility.SetDirty(plan);
        }

        static double Bed(Dictionary<string, object> a) => a.ContainsKey("bed") ? Num(a, "bed") : Num(Obj(a, "spill"), "bed");

        static Vector2 Pair(double[] a) => new Vector2((float)a[0], (float)a[1]);

        /// <summary>Pure preflight: every covered copy cell against the game-rule result, at the published 0.122 mm limit.</summary>
        public static void CheckGameCopies(FileRead f, GroundFileImport.BaseMap b, GroundFileImport.Result laid, float lo, float hi)
        {
            foreach (var k in f.Asks.Where(a => a.IsGameCopy))
            {
                var copy = GroundFileImport.Run(b, new[] { ImportAsk(k) }, 0, lo, hi);
                for (int i = 0; i < copy.E.Length; i++)
                    if (copy.Owner[i] >= 0 && Math.Abs(copy.E[i] - TerrainPlanMaps.Decode(laid.Codes[TerrainPlanMaps.Pixel(laid.Grid, i)], lo, hi)) > 0.000122)
                        throw Bad(k.Id + " copy differs from the game's laid cell " + i + " by more than 0.122 mm");
            }
        }
    }
}
