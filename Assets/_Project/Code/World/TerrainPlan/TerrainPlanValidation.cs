using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PLAN'S DEF RULES,</b> engine-free so they run headless and in the content validation alike: every Def
    /// has an id of the form <c>type.snake_case</c> whose type matches its class, the ids are unique, each file holds
    /// one Def, every reference resolves to a Def the plan carries, and each path's label maps to the kit's slot as
    /// the table below says (handoff §4.3, step 2).
    /// </summary>
    public static class TerrainPlanValidation
    {
        /// <summary>A key-scene label and the kit's slot it paints: its material, its ruts, its weight range.</summary>
        public struct LabelSlot
        {
            public string Label, Material;
            public bool Ruts;
            public float MinWeight, MaxWeight;

            public LabelSlot(string label, string material, bool ruts, float minWeight, float maxWeight)
            {
                Label = label; Material = material; Ruts = ruts; MinWeight = minWeight; MaxWeight = maxWeight;
            }
        }

        /// <summary>
        /// CD's five labels, and the lane's one for the bar walk (M5). Road and cart track are the path slot with ruts
        /// (part 1 §5); worn grass is the path slot at a light weight, which the plate judges; dirt is the dirt slot.
        /// </summary>
        public static readonly LabelSlot[] Labels =
        {
            new LabelSlot("road", "path", true, 1f, 1f),
            new LabelSlot("cart track", "path", true, 1f, 1f),
            new LabelSlot("path", "path", false, 1f, 1f),
            new LabelSlot("worn grass", "path", false, 0.05f, 0.95f),
            new LabelSlot("dirt", "dirt", false, 1f, 1f),
            new LabelSlot("crest-top walk (not painted)", "path", false, 0f, 0f),
        };

        /// <summary>
        /// The village plan's route classes and the label each paints as (amendment 2 §4.6): a footpath is a path, a street
        /// the road's slot. A class the table lacks is a problem.
        /// </summary>
        public static readonly Dictionary<string, string> RouteClasses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "footpath", "path" }, { "street", "road" },
        };

        /// <summary>The id's type for each Def class.</summary>
        public static readonly Dictionary<Type, string> IdTypes = new Dictionary<Type, string>
        {
            { typeof(RegionTerrainPlanDef), "terrain_plan" }, { typeof(CoastSectionDef), "coast" },
            { typeof(CoastRecipeDef), "coast_recipe" }, { typeof(PondDef), "pond" }, { typeof(StreamDef), "stream" },
            { typeof(TidalCreekDef), "creek" }, { typeof(SaltPanDef), "pan" }, { typeof(PathDef), "path" },
            { typeof(BiomeDef), "biome" }, { typeof(TidalPoolDef), "pool" }, { typeof(FormDef), "form" },
            { typeof(GroundRampDef), "ground" }, { typeof(GroundPlatformDef), "ground" }, { typeof(TidalBarDef), "bar" },
            { typeof(WaterfallDef), "fall" }, { typeof(GroundPatchDef), "patch" }, { typeof(BayDef), "bay" },
        };

        /// <summary>The second id type a Def class may carry: a key scene's still pool is a PondDef with a pool id (PR 5 B).</summary>
        public static readonly Dictionary<Type, string> IdTypesAlso = new Dictionary<Type, string> { { typeof(PondDef), "pool" } };

        static readonly Regex IdForm = new Regex(@"^[a-z][a-z0-9]*(_[a-z0-9]+)*\.[a-z0-9]+(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        /// <summary>A Def's id (every plan Def carries a public string Id), or null for another object.</summary>
        public static string IdOf(ScriptableObject def)
        {
            if (ReferenceEquals(def, null)) return null;
            var f = def.GetType().GetField("Id");
            return f != null && f.FieldType == typeof(string) ? (string)f.GetValue(def) : null;
        }

        public static bool IsId(string id) => id != null && IdForm.IsMatch(id);

        /// <summary>The label's row, or false where the table has none.</summary>
        public static bool TryLabel(string label, out LabelSlot slot)
        {
            foreach (var l in Labels)
                if (string.Equals(l.Label, label, StringComparison.Ordinal)) { slot = l; return true; }
            slot = default;
            return false;
        }

        /// <summary>
        /// Every problem with the plan and its Defs, in a stable order; empty when all hold. <paramref name="defs"/> are the
        /// Defs of the plan's folder, one per file (<paramref name="files"/>, the same length: each file's name).
        /// </summary>
        public static List<string> Validate(RegionTerrainPlanDef plan, IList<ScriptableObject> defs, IList<string> files)
        {
            var o = new List<string>();
            if (ReferenceEquals(plan, null)) { o.Add("no plan"); return o; }
            if (files == null || files.Count != defs.Count) { o.Add("each Def needs its file"); return o; }

            // ids: the form, the type, unique; one Def per file
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            var fileSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool planInFolder = false;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                string where = files[i];
                if (!fileSeen.Add(files[i])) o.Add(where + ": a second Def in one file");
                if (ReferenceEquals(d, null)) { o.Add(where + ": no Def"); continue; }
                if (ReferenceEquals(d, plan)) planInFolder = true;
                if (!IdTypes.TryGetValue(d.GetType(), out var type)) { o.Add(where + ": " + d.GetType().Name + " is not a plan Def"); continue; }
                string id = IdOf(d);
                if (!IsId(id)) { o.Add(where + ": id '" + id + "' is not type.snake_case"); continue; }
                if (!id.StartsWith(type + ".", StringComparison.Ordinal) &&
                    !(IdTypesAlso.TryGetValue(d.GetType(), out var also) && id.StartsWith(also + ".", StringComparison.Ordinal)))
                    o.Add(where + ": id '" + id + "' is not a " + type + " id");
                if (byId.TryGetValue(id, out int j)) o.Add(where + ": id '" + id + "' is also " + files[j]);
                else byId[id] = i;
            }
            if (!planInFolder) o.Add("the plan is not in its own folder");
            if (!(plan.HeightRange.x < plan.HeightRange.y)) o.Add("the plan's HeightRange does not rise (" + plan.HeightRange.x + " to " + plan.HeightRange.y + ")");

            // what the plan carries, and each reference resolving to one of them
            var carried = new HashSet<ScriptableObject>(new RefEq());
            void Carry<T>(string what, T[] arr) where T : ScriptableObject
            {
                if (arr == null) { o.Add("the plan's " + what + " is missing"); return; }
                for (int k = 0; k < arr.Length; k++)
                {
                    if (ReferenceEquals(arr[k], null) || !arr[k]) o.Add("the plan's " + what + "[" + k + "] does not resolve");
                    else carried.Add(arr[k]);
                }
            }
            Carry("Sections", plan.Sections); Carry("PaintOnly", plan.PaintOnly); Carry("Recipes", plan.Recipes);
            Carry("Ponds", plan.Ponds); Carry("Streams", plan.Streams); Carry("Creeks", plan.Creeks); Carry("Pans", plan.Pans);
            Carry("Paths", plan.Paths); Carry("Biomes", plan.Biomes); Carry("Pools", plan.Pools); Carry("Forms", plan.Forms);
            Carry("Ramps", plan.Ramps); Carry("Platforms", plan.Platforms); Carry("Bars", plan.Bars); Carry("Falls", plan.Falls);
            Carry("Patches", plan.Patches); Carry("StillPools", plan.StillPools); Carry("Bays", plan.Bays);
            Carry("Routes", plan.Routes);
            // the village's routes are the village plan's Defs, in its own folder: each needs a class the table paints, a width and a line
            if (plan.Routes != null)
                foreach (var r in plan.Routes)
                {
                    if (ReferenceEquals(r, null) || !r) continue;
                    if (r.Class == null || !RouteClasses.ContainsKey(r.Class)) o.Add(r.Id + ": class '" + r.Class + "' is not in the route table");
                    if (!(r.WidthMetres > 0)) o.Add(r.Id + ": its width is " + r.WidthMetres);
                    if (r.Points == null || r.Points.Length < 2) o.Add(r.Id + ": its line needs two points");
                }
            if (ReferenceEquals(plan.Crossing, null) || !plan.Crossing) o.Add("the plan's Crossing does not resolve");
            else carried.Add(plan.Crossing);
            // a fall carries its own plunge pool: the fall shapes it, so the plan's Ponds leave it out (it would be cut twice)
            if (plan.Falls != null)
                foreach (var f in plan.Falls)
                    if (!ReferenceEquals(f, null) && f && !ReferenceEquals(f.Pool, null) && f.Pool) carried.Add(f.Pool);

            void Need(string who, string field, ScriptableObject r, bool optional = false)
            {
                if (ReferenceEquals(r, null) || !r) { if (!optional) o.Add(who + "'s " + field + " does not resolve"); return; }
                if (!carried.Contains(r)) o.Add(who + "'s " + field + " (" + IdOf(r) + ") is not in the plan");
            }
            foreach (var d in defs)
            {
                if (ReferenceEquals(d, null)) continue;
                string who = IdOf(d);
                switch (d)
                {
                    case CoastSectionDef s: Need(who, "Recipe", s.Recipe); break;
                    case PondDef p: Need(who, "Outlet", p.Outlet, optional: true); break;
                    case StreamDef s: Need(who, "Source", s.Source, optional: true); break;
                    case TidalCreekDef c: Need(who, "Joins", c.Joins); break;
                    case FormDef f: Need(who, "Recipe", f.Recipe); break;
                    case GroundRampDef r: Need(who, "Form", r.Form); Need(who, "Recipe", r.Recipe); break;
                    case GroundPlatformDef p: Need(who, "Form", p.Form); Need(who, "Recipe", p.Recipe); break;
                    case TidalBarDef b: Need(who, "Recipe", b.Recipe); break;
                    case WaterfallDef w: Need(who, "Stream", w.Stream); Need(who, "Pool", w.Pool); break;
                    case BayDef b:
                        Need(who, "Recipe", b.Recipe);
                        if (TerrainPlanZones.IndexOf(b.BackZone) < 0) o.Add(who + ": its back zone '" + b.BackZone + "' is not a ground zone");
                        if (b.Shore == null || b.Shore.Length < 2 || b.Rim == null || b.Rim.Length < 2) o.Add(who + ": its shore and rim need two points each");
                        break;
                    case PathDef p:
                        if (!TryLabel(p.Label, out var slot)) { o.Add(who + ": label '" + p.Label + "' is not in the table"); break; }
                        if (!string.Equals(p.Material, slot.Material, StringComparison.Ordinal)) o.Add(who + ": a " + p.Label + " paints " + slot.Material + ", not " + p.Material);
                        if (p.Ruts != slot.Ruts) o.Add(who + ": a " + p.Label + (slot.Ruts ? " has ruts" : " has no ruts"));
                        if (p.Weight < slot.MinWeight || p.Weight > slot.MaxWeight) o.Add(who + ": a " + p.Label + "'s weight is " + slot.MinWeight + "–" + slot.MaxWeight + ", not " + p.Weight);
                        if (p.JoinPoints != null && p.JoinPoints.Length > 0 && string.IsNullOrEmpty(p.LineSource)) o.Add(who + ": join points run on from a builder's line, and it has none");
                        break;
                }
                if (!ReferenceEquals(d, plan) && !carried.Contains(d)) o.Add(who + " is in the folder but not in the plan");
            }
            return o;
        }

        sealed class RefEq : IEqualityComparer<ScriptableObject>
        {
            public bool Equals(ScriptableObject a, ScriptableObject b) => ReferenceEquals(a, b);
            public int GetHashCode(ScriptableObject o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
    }
}
