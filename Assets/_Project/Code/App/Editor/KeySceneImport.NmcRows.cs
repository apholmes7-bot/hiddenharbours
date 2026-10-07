#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static partial class KeySceneImport
    {
        static Light NmcLight(string id, Json light)
        {
            if (light.Kind != Json.Of.Object || light.Fields.Any(f => !LightKeys.Contains(f.Key) && f.Key != "dot" && f.Key != "emitters"))
                throw new Refusal(id + ": unknown NMC light field");
            string normalized = "{" + string.Join(",", light.Fields.Where(f => LightKeys.Contains(f.Key))
                .Select(f => SystemQuote(f.Key) + ":" + f.Value.Compact())) + "}";
            return LightOf(id, Json.Parse(normalized, id + " light"), id);
        }
        /// <summary>H1 names these additional data kinds explicitly; none enables a runtime system.</summary>
        public static string NmcPlanKindOf(string type)
        {
            switch (type)
            {
                case "RoadPlan": return "road";
                case "LotPlan": return "lot";
                case "BridgePlan": return "bridge";
                case "HouseholdPlan": return "household";
                case "PathDef": return nameof(PathDef);
                case "StreamDef": return nameof(StreamDef);
                case "PondDef": return nameof(PondDef);
                case "CoastSectionDef": return nameof(CoastSectionDef);
                default: throw new Refusal("unnamed NMC plan kind: " + type);
            }
        }

        /// <summary>The NMC type/kit contract, distinct from St Peters' kit naming.</summary>
        public static string NmcKindOf(string kit, string type, string id)
        {
            string family = kit.Split(' ', '.', '(')[0];
            bool Is(params string[] names) => names.Contains(family, StringComparer.Ordinal);
            switch (type)
            {
                case "the kit’s own (HarbourDef)":
                    if (Is("wharfRig2", "shipyard2")) return "harbour";
                    break;
                case "the kit’s own":
                    if (Is("seagull")) return KeySceneDef.KindGull;
                    if (Is("dory")) return KeySceneDef.KindBoat;
                    if (Is("wharfDecor")) return KeySceneDef.KindSetPiece;
                    break;
                case "SetPieceDef":
                    if (Is("nmcSetPieces", "NmcSetPieces", "fuelStopRig", "NmcCampKit", "NmcCreekHouses", "NmcShoreLane", "NmcStores"))
                        return KeySceneDef.KindSetPiece;
                    break;
                case "BuildingDef":
                    if (Is("wharfBuilding2", "ManorIso", "NmcCampKit", "NmcCreekHouses", "StackFlatsIso", "HouseIso", "NmcStores", "NmcShoreLane"))
                        return KeySceneDef.KindBuilding;
                    break;
                case "BoatDef (the register’s)": case "AmbientBoatDef":
                    if (Is("DoryIso", "ConsoleIso", "PuntIso")) return KeySceneDef.KindBoat;
                    break;
                case "VehicleDef": case "AmbientVehicleDef":
                    if (Is("VehicleIso", "VanIso", "TrailerIso", "AeroSemiIso", "NmcCampKit", "CamperIso")) return "vehicle";
                    break;
                case "RockDef":
                    if (Is("PxRockIso2")) return KeySceneDef.KindRock;
                    break;
            }
            throw new Refusal($"{id}: incompatible or unnamed NMC kind/kit: {type} / {kit}.");
        }

        static Row NmcRow(Scene scene, string id, Json p, Dictionary<string, string> held, Beside beside)
        {
            Json def = p.Get("def");
            string kit = (p.Get("kit") ?? def?.Get("kit") ?? throw new Refusal(id + ": no kit")).Str(id + " kit");
            string type = (p.Get("defType") ?? def?.Get("defType") ?? throw new Refusal(id + ": no defType")).Str(id + " defType");
            var row = new Row { Id = id, Kit = kit, Kind = NmcKindOf(kit, type, id), Owner = NmcPlacementOwner(scene.Id) };
            row.Piece = p.Get("piece")?.Str(id + " piece") ?? p.Get("rig")?.Str(id + " rig") ?? p.Get("form")?.Str(id + " form") ??
                throw new Refusal(id + ": no piece, rig or rock form");
            (row.X, row.Y) = Pair(Need(p, "at", id), id + " at");
            row.Z = p.Get("z")?.Float(id + " z") ?? 0;
            row.Dir = p.Get("dir")?.Int(id + " dir") ?? 0;
            if (row.Dir < 0 || row.Dir > 7) throw new Refusal(id + ": dir outside 0..7");
            row.HasSortY = p.Has("sortY"); row.SortY = p.Get("sortY")?.Float(id + " sortY") ?? 0;
            row.Layer = p.Get("layer")?.Str(id + " layer") ?? "";
            row.Walk = p.Get("walk")?.Str(id + " walk") ?? "";
            if (p.Has("float")) row.StandsOn = KeySceneDef.StandsOnWater;
            if (!p.Has("z") && !p.Has("float")) throw new Refusal(id + ": no height or float contract");
            if (p.Get("variants") is Json variants)
                foreach (Json v in Items(variants, "variants"))
                {
                    string name = v.Str("variant");
                    if (name.Length == 0 || row.Variants.Contains(name)) throw new Refusal(id + ": empty or duplicate variant");
                    row.Variants.Add(name);
                }
            else row.Variants.Add(KeySceneDef.Today);
            if (row.Variants.Count == 0) throw new Refusal(id + ": no variant");
            if (p.Get("mount") is Json mount)
            {
                if (!beside.Hosts.TryGetValue(id, out HostAnswer answer) || answer.Mount != mount.Str("mount"))
                    throw new Refusal(id + ": unknown mount words/host");
                beside.Answered.Add(id);
                row.StandsOn = answer.Rule.StandsOn; row.MountHost = answer.Rule.Host; row.MountAnchor = answer.Rule.Anchor;
                if (row.MountHost.Length > 0 && !held.ContainsKey(row.MountHost)) throw new Refusal(id + ": missing host " + row.MountHost);
            }
            if (p.Get("opts") is Json opts)
            {
                foreach (var o in opts.Fields)
                {
                    if (o.Key.StartsWith("nmc.", StringComparison.Ordinal)) throw new Refusal(id + ": reserved option prefix");
                    string mapped = NmcMapped(o.Value, beside);
                    row.Options.Add(new KeyValuePair<string, string>(o.Key, o.Value.Kind == Json.Of.String
                        ? Json.Parse(mapped, id + " option").Text : mapped));
                }
                row.State = opts.Get("state")?.Str("state") ?? "";
            }
            // These are data contracts, not Words, live stations, household NPCs or working conditions.
            foreach (var field in p.Fields)
            {
                if (new[] { "id", "kit", "defType", "piece", "at", "z", "dir", "opts", "variants", "sortY", "layer", "walk", "why", "new" }.Contains(field.Key)) continue;
                AddNmc(row, field.Key, NmcMapped(field.Value, beside));
            }
            AddNmc(row, "ownerScene", scene.Id);
            AddNmc(row, "type", type);
            if (def?.Get("household") is Json household)
                AddNmc(row, "household", household.Str("household"));
            // Words remain empty until the words lane installs approved trade labels and their anchors.
            // The source words contract is retained above; no origin or proper-name line is made player-facing.
            return row;
        }

        static void NmcHouseholds(Scene scene, Json source, Beside beside)
        {
            Json layout = source.Get("layout");
            Json fleet = layout?.Get("fleet");
            if (fleet != null)
                foreach (Json entry in Items(fleet, "camp households"))
                {
                    Json household = entry.Get("household");
                    if (household == null) continue;
                    string pitch = NmcId(Need(entry, "pitch", "camp household").Str("pitch"), beside);
                    string unit = entry.Get("unit")?.Get("id")?.Str("unit id");
                    Row row = unit == null ? scene.Rows.FirstOrDefault(r => r.Options.Any(o => o.Key == "nmc.pitch" && o.Value == SystemQuote(pitch)))
                        : scene.Rows.FirstOrDefault(r => r.Id == NmcId(unit, beside));
                    if (row == null) throw new Refusal("household has no lot/piece: " + pitch);
                    AddNmc(row, "household", household.Str("household"));
                    AddNmc(row, "householdOwner", "H13; names slate first; exact visit windows remain data");
                }
            foreach (Row row in scene.Rows.Where(r => r.Options.Any(o => o.Key == "nmc.household")))
            {
                string text = row.Options.First(o => o.Key == "nmc.household").Value;
                if (text.Contains("August")) AddNmc(row, "visitMapping", "High Summer; exact window H13");
                if (text.Contains("weekend") && text.Contains("May to October"))
                    AddNmc(row, "visitMapping", "Early Spring through The Turn, rest days; exact window H13");
            }
        }
    }
}
#endif
