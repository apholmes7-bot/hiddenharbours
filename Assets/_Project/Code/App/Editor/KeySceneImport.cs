#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>CD'S PACKAGE, INTO THE KEY SCENES (O3).</b> Reads Claude Design's one-scene package (its three parts
    /// unzipped into one folder) and writes one <see cref="KeySceneDef"/> asset per scene, every piece under the
    /// id CD gave it. A pure function from the package to the assets' text: it reads through a delegate, is
    /// handed the folder's assets as they stand, and returns what to write, so run twice it changes nothing.
    ///
    /// <para><b>What it refuses, each a STOP for the owner and never a guess.</b> A package whose
    /// <c>SHA256SUMS</c> does not hold; a scene file the island does not list; a defType outside the type map;
    /// a mount text the mount table cannot read, or a height off its anchor's; a frozen piece the owner table
    /// does not name; an id that changes kit or kind; an id a scene once retired. The village's file is never
    /// read: its pieces stay with the village plan (V1/V3).</para>
    ///
    /// <para><b>What it reads beside the package (amendment 1).</b> The id map (<see cref="IdMapFile"/>): every
    /// id it reads, a row's own or a host's, goes from old to new, an id the map does not hold passes as it is,
    /// and one that holds a position the map does not rename is a STOP for the Art desk. The hosts' table
    /// (<see cref="HostsFile"/>): the Art desk's answers, each by the piece's id and its mount words as the
    /// package writes them, so a new wording still refuses.</para>
    ///
    /// <para><b>What it records and never places.</b> A frozen piece names the code that places it
    /// (<see cref="KeyScenePiece.Owner"/>); what comes and goes keeps its variants without today's; a light is
    /// kept on its scene. A new scene comes in with <see cref="KeySceneDef.Placed"/> off, and its wave turns
    /// it on when its art is in the game.</para>
    ///
    /// <para><b>By id, and only by id.</b> A re-import moves a row whose numbers changed, adds a new id, and
    /// retires an id the package no longer has. A row that differs in anything but its height is listed in
    /// the report, never silently taken.</para>
    /// </summary>
    public static partial class KeySceneImport
    {
        /// <summary>A STOP: nothing is written, and the message says what the owner must rule.</summary>
        public sealed class Refusal : Exception
        {
            public Refusal(string message) : base("[KeySceneImport] " + message) { }
        }

        // =====================================================================================
        //  the tables (lead-architect, O3)
        // =====================================================================================

        /// <summary>The village's file. Its pieces stay with the village plan (V1/V3), so it is never read.</summary>
        public const string VillageSceneId = "keyscene.stp_village";

        public const string SumsFile = "SHA256SUMS";
        public const string IslandFile = "island.json";
        public const string IslandSchema = "hidden-harbours/island@1";

        /// <summary>Where a dry run writes what it would write, with its report (ignored by git).</summary>
        public const string DryRunFolder = "artifacts/key-scene-import";

        /// <summary>The id map (amendment 1, 4.2), byte for byte as the Planner wrote it: each id that holds a
        /// position (<c>@x,y</c>), old to new. CD adopts it in its next issue.</summary>
        public const string IdMapFile = "docs/design/st-peters-key-scenes/key-scene-id-map.json";
        public const string IdMapSchema = "hidden-harbours/key-scene-id-map@1";

        /// <summary>The hosts' table (amendment 1, 4.6), beside the map. It retires when CD's next issue names
        /// every host by id.</summary>
        public const string HostsFile = "docs/design/st-peters-key-scenes/key-scene-hosts.json";
        public const string HostsSchema = "hidden-harbours/key-scene-hosts@1";

        /// <summary>What marks an id that holds a position: <c>plant.SpeckledAlder@110.9,56.4</c>.</summary>
        public const char PositionMark = '@';

        const string SceneIdPrefix = "keyscene.";
        const string ContextPrefix = "context.";
        const string GuidSeed = "HiddenHarbours.KeySceneImport/";
        const string CoastForms = "CoastFormField (PR 5b)";
        const string Afloat = "afloat";
        const string TheTide = "the tide";

        /// <summary>How far a mount's height may stand from the height its text names: 5 cm, the step's own.</summary>
        const decimal AnchorTolerance = 0.05m;

        /// <summary>Who places each frozen piece, by its id. Every frozen piece must be named here and everything
        /// named here must be frozen: the table and the package agree, or it is a STOP.</summary>
        public static readonly IReadOnlyDictionary<string, string> FrozenOwners = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "structure.stp_cannery", "StPetersCannery" },
            { "station.st_peters.slip_lantern", "StPetersWharf.LampPostSites" },
            { "station.st_peters.head_lamp", "StPetersWharf.LampPostSites" },
            { "building.stp_ginny_cottage", "StPetersGinnyPlot" },
            { "structure.stp_ginny_woodshed", "StPetersGinnyPlot" },
            { "structure.stp_ginny_net_store", "StPetersGinnyPlot" },
            { "structure.stp_ginny_lean_to", "StPetersGinnyPlot" },
            { "prop.stp_gp_freezer", "StPetersGinnyPlot" },
            { "home.ginny_lot_camper", "StPetersCamperLot" },
            { "rock.stp_whelp_1", CoastForms },
            { "rock.stp_whelp_1_b", CoastForms },
            { "rock.stp_whelp_1_c", CoastForms },
            { "rock.stp_whelp_2", CoastForms },
            { "rock.stp_whelp_2_b", CoastForms },
            { "rock.stp_whelp_3", CoastForms },
            { "rock.stp_whelp_3_b", CoastForms },
            { "rock.stp_east_gap_skerry", CoastForms },
        };

        /// <summary>Who places a kind of piece, frozen or not: the nav marks place the cardinals.</summary>
        public static readonly IReadOnlyDictionary<string, string> KindOwners = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { KeySceneDef.KindNavMark, "StPetersNavMarks.Cardinals" },
        };

        /// <summary>A host no scene holds that other work brings, with the work that brings it.</summary>
        public static readonly IReadOnlyDictionary<string, string> HostsFromOtherWork = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "form.stp_harbour_stack", "PR 5b (part 2's coast forms)" },
            { "form.stp_lantern_stack", "PR 5's forms (Form_LanternStack.asset, on main)" },
        };

        /// <summary>The owner's rulings that make a later board's variant today's, by scene: the north-east
        /// light stands as the tower (28 Sept).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> RuledToday = new[]
        {
            new KeyValuePair<string, string>("keyscene.stp_ne_light", "tower"),
        };

        /// <summary>What one mount text says: what the piece stands on, its host's id and the host's anchor.</summary>
        public sealed class MountRule
        {
            public readonly string StandsOn;
            public readonly string Host;
            public readonly string Anchor;

            public MountRule(string standsOn, string host, string anchor)
            {
                StandsOn = standsOn; Host = host; Anchor = anchor;
            }
        }

        static MountRule Deck() => new MountRule(KeySceneDef.StandsOnDeck, "", "");
        static MountRule Ground() => new MountRule(KeySceneDef.StandsOnGround, "", "");
        static MountRule Water() => new MountRule(KeySceneDef.StandsOnWater, "", "");
        static MountRule On(string host, string anchor) => new MountRule(KeySceneDef.StandsOnMount, host, anchor);

        /// <summary>The mount table: every mount text CD wrote, read exactly. A text not here is a STOP: a new
        /// text is a new ruling. A host left empty is one CD names by no id; the anchor is the mount contract's
        /// own name for the spot. The pieces the hosts' table answers (<see cref="HostsFile"/>) are read there,
        /// by their ids and words, and never here.</summary>
        public static readonly IReadOnlyDictionary<string, MountRule> Mounts = new Dictionary<string, MountRule>(StringComparer.Ordinal)
        {
            { "context.wharf deck, at the pier root by the slip (WharfRig2 pier, frozen), +5.35", Deck() },
            { "context.wharf deck, at the pier head (WharfRig2 pier, frozen), +5.35", Deck() },
            { "context.wharf deck (WharfRig2 pier, frozen), +5.35", Deck() },
            { "the cannery's door (anchor door, +1.2 m floor), +7.20", On("structure.stp_cannery", "door") },
            { "a ledge on the Head\u2019s west face (cliffs: the Head, ground.stp_ne_head), +6.40", On("ground.stp_ne_head", "westFaceLedge") },
            { "a ledge on the Head\u2019s west face (cliffs: the Head, ground.stp_ne_head), +8.30", On("ground.stp_ne_head", "westFaceLedge") },
            { "a ledge on the Head\u2019s south face (cliffs: the Head, ground.stp_ne_head), +4.60", On("ground.stp_ne_head", "southFaceLedge") },
            { "afloat on its mooring: it rides the tide (its z is the tide)", Water() },
            { "the camper\u2019s roof (+2.84 m); it sorts with the roof it sits on, +8.84", On("home.ginny_lot_camper", "roof") },
            { "the plunge pool\u2019s surface (pond.stp_alder_plunge in the ground file\u2019s still water), +2.05", On("pond.stp_alder_plunge", "surface") },
            { "rock.stp_af_flat_stone\u2019s top (it follows the stone), +2.33", On("rock.stp_af_flat_stone", "top") },
            { "the wreck\u2019s stem (structure.stp_cx_wreck; it follows the wreck), +0.22", On("structure.stp_cx_wreck", "stem") },
            { "on the ground", Ground() },
            { "rides the water", Water() },
            { "the Harbour Stack\u2019s top (form.stp_harbour_stack, +6.2): it sorts with the stack", On("form.stp_harbour_stack", "top") },
            { "the base beside Whelp 2\u2019s skerry, on the ground", Ground() },
            { "its two footings (ground.stp_gap_bridge, the pads levelled at +5.28), +5.28", On("ground.stp_gap_bridge", "footings") },
            { "its own foot on the dry sand at (10.4, -63.8): the kit stands the flight at its foot\u2019s ground and its head meets the dune\u2019s top, +2.78", Ground() },
        };

        /// <summary>The type map (lead-architect, O3): the package's defType to the piece's kind. The seagull
        /// kit's pieces are gulls whatever their defType says; a defType not here is a STOP.</summary>
        public static string KindOf(string kit, string defType, string id)
        {
            if (kit == "seagull")
            {
                if (defType == "seagull" || defType == "SetPieceDef") return KeySceneDef.KindGull;
                throw new Refusal($"{id}: the seagull kit with defType '{defType}' is not in the type map.");
            }
            switch (defType)
            {
                case null:
                case "SetPieceDef":
                case "PropDef": return KeySceneDef.KindSetPiece;
                case "BuildingDef": return KeySceneDef.KindBuilding;
                case "NavBuoyDef": return KeySceneDef.KindNavMark;
                case "BoatDef": return KeySceneDef.KindBoat;
                case "RockFormDef": return KeySceneDef.KindRock;
                case "TreeDef": return KeySceneDef.KindTree;
                case "PlantDef": return KeySceneDef.KindPlant;
                case "WaterfallDef": return KeySceneDef.KindWaterfall;
                case "WildlifeDef": return KeySceneDef.KindWildlife;
                default: throw new Refusal($"{id}: defType '{defType}' is not in the type map. lead-architect names its type first.");
            }
        }

        /// <summary>The piece keys the row carries.</summary>
        static readonly HashSet<string> CarriedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "kit", "piece", "defType", "at", "z", "dir", "opts", "variants", "whenTide", "mount", "contact", "frozen",
            "taken", "boardOnly", "sortY", "foot", "layer", "walk", "collider", "words", "light",
        };

        /// <summary>CD's record that stays in the package, which each asset's Source names: why, the tide's
        /// reading, the art's state, the reseat's history and the rest.</summary>
        static readonly HashSet<string> PackageOnlyKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "name", "faces", "tide", "why", "new", "ground", "reseated", "art", "role", "family", "from", "site", "footprint",
            "plan", "isNew", "form", "animated", "look", "ask", "of", "drawnBy", "moved", "shell", "beam", "facing", "seat",
            "emits", "fx", "ruled", "build", "rig", "lights", "lightsNote", "gauge", "refuge", "wake", "span", "deck", "rail",
            "anchors", "replaces",
        };

        static readonly HashSet<string> LightKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "preset", "pool", "reachM", "at", "colour", "intensity", "when", "whoLights", "budget", "note",
        };

        /// <summary>The asset's fields in the Defs' own order. The writer writes them so, and a test holds the
        /// Defs to these lists.</summary>
        public static readonly string[] SceneFields = { "Id", "DisplayName", "RegionId", "Source", "Placed", "Pieces", "Lights", "RetiredIds", "PlacementIds" };

        public static readonly string[] PieceFields =
        {
            "Id", "Kind", "Owner", "Kit", "Piece", "State", "At", "Z", "StandsOn", "MountHost", "MountAnchor", "HasFoot", "Foot",
            "Dir", "HasSortY", "SortY", "Layer", "Walk", "Collider", "Variants", "WhenTide", "Words", "Options",
        };

        public static readonly string[] OptionFields = { "Key", "Value" };

        public static readonly string[] LightFields =
        {
            "PieceId", "Preset", "Pool", "ReachMetres", "At", "Colour", "Intensity", "When", "WhoLights", "Budget", "Note",
        };

        static readonly string[] UnityFields =
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled",
            "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
        };

        static readonly Regex SumLineRx = new Regex(@"^([0-9a-fA-F]{64}) [ *](.+)$", RegexOptions.CultureInvariant);
        static readonly Regex SceneFileRx = new Regex(@"^scenes/[^/]+/scene\.json$", RegexOptions.CultureInvariant);
        static readonly Regex AnchorHeightRx = new Regex(@"\+([0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        static readonly Regex GuidRx = new Regex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant);

        // =====================================================================================
        //  the import
        // =====================================================================================

        /// <summary>One scene's counts, for the report and §2's table.</summary>
        public sealed class Tally
        {
            /// <summary>Rows; rows in today's variant; rows another code places; frozen rows; rows kept as data
            /// (no today); rows with a mount text; rows standing on a host; lights.</summary>
            public int Pieces, Today, Owned, Frozen, Data, Mounted, OnHost, Lights;

            public readonly SortedSet<string> Kits = new SortedSet<string>(StringComparer.Ordinal);

            public override string ToString() =>
                $"pieces {Pieces}, today {Today}, owned {Owned} (frozen {Frozen}), data {Data}, mounts {Mounted} " +
                $"(on a host {OnHost}), lights {Lights}; kits {string.Join(" ", Kits)}";
        }

        /// <summary>What an import would write.</summary>
        public sealed class Result
        {
            /// <summary>Every imported scene's asset text, by file name.</summary>
            public readonly SortedDictionary<string, string> Assets = new SortedDictionary<string, string>(StringComparer.Ordinal);

            /// <summary>The .meta of each new asset, by its file name (<c>KeyScene_x.asset.meta</c>).</summary>
            public readonly SortedDictionary<string, string> Metas = new SortedDictionary<string, string>(StringComparer.Ordinal);

            /// <summary>The assets whose text the import changes, new ones included.</summary>
            public readonly SortedSet<string> Changed = new SortedSet<string>(StringComparer.Ordinal);

            /// <summary>Each scene's counts, by scene id.</summary>
            public readonly SortedDictionary<string, Tally> Tallies = new SortedDictionary<string, Tally>(StringComparer.Ordinal);

            /// <summary>Hosts that no scene holds and no other work brings, with the pieces that name them: a STOP
            /// for the Art desk on those pieces only.</summary>
            public readonly SortedDictionary<string, List<string>> UnheldHosts = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            public readonly List<string> Report = new List<string>();
        }

        sealed class Row
        {
            public string Id, Kind, Owner = "", Kit, Piece, State = "", StandsOn = KeySceneDef.StandsOnGround;
            public string MountHost = "", MountAnchor = "", Layer = "", Walk = "", Collider = "", WhenTide = "", MountText;
            public float X, Y, Z, FootX, FootY, SortY;
            public bool HasFoot, HasSortY, Frozen;
            public int Dir;
            public readonly List<string> Variants = new List<string>();
            public readonly List<string> Words = new List<string>();
            public readonly List<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();
        }

        sealed class Light
        {
            public string PieceId, Preset = "", Colour = "", When = "", WhoLights = "", Note = "";
            public bool Pool;
            public float Reach, X, Y, Z, Intensity;
            public int Budget;
        }

        sealed class Scene
        {
            public string Id, Name, Source;
            public readonly List<Row> Rows = new List<Row>();
            public readonly List<Light> Lights = new List<Light>();
            public readonly Tally Tally = new Tally();

            /// <summary>This scene's context keys as hosts (<c>context.wharf</c>): each scene's own, never another's.</summary>
            public readonly HashSet<string> Context = new HashSet<string>(StringComparer.Ordinal);
        }

        sealed class Existing
        {
            public string File;
            public List<KeyValuePair<string, YamlNode>> Top;
            public List<YamlNode> Rows;
            public bool Placed;
            public List<string> Retired;
        }

        /// <summary>One answer of the hosts' table: the piece's mount words as the package writes them, and what
        /// they stand it on.</summary>
        sealed class HostAnswer
        {
            public string Mount;
            public MountRule Rule;
        }

        /// <summary>What the importer reads beside the package: the id map and the hosts' table.</summary>
        sealed class Beside
        {
            public readonly Dictionary<string, string> Renames = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, HostAnswer> Hosts = new Dictionary<string, HostAnswer>(StringComparer.Ordinal);
            public readonly HashSet<string> Renamed = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Answered = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>An id as the game keeps it: the map's new id for an old one, any other id as it is. An id
            /// that holds a position, and that the map does not rename, is a STOP.</summary>
            public string Id(string id, string what)
            {
                if (Renames.TryGetValue(id, out string renamed))
                {
                    Renamed.Add(id);
                    return renamed;
                }
                if (id.IndexOf(PositionMark) >= 0)
                    throw new Refusal($"{what}: {id} holds a position, and the id map does not rename it. A STOP for the Art desk, through the seat.");
                return id;
            }
        }

        /// <summary>
        /// Import the package. <paramref name="read"/> gives a package file's bytes by its path in the package
        /// (null when it is not there); <paramref name="existing"/> is the key scene folder's assets as they stand,
        /// file name to text; <paramref name="idMap"/> and <paramref name="hosts"/> are the bytes of the id map and
        /// the hosts' table (<see cref="IdMapFile"/>, <see cref="HostsFile"/>). Nothing is written: the result
        /// holds what to write.
        /// </summary>
        public static Result Import(string packageName, Func<string, byte[]> read, IReadOnlyDictionary<string, string> existing,
                                    string regionId, string scriptGuid, byte[] idMap, byte[] hosts)
        {
            if (string.IsNullOrEmpty(packageName)) throw new Refusal("the package has no name.");
            if (string.IsNullOrEmpty(regionId)) throw new Refusal("no region id.");
            if (scriptGuid == null || !GuidRx.IsMatch(scriptGuid)) throw new Refusal($"'{scriptGuid}' is not KeySceneDef's script guid.");

            var result = new Result();
            result.Report.Add($"package: {packageName}");
            Beside beside = ReadBeside(idMap, hosts, result);
            Dictionary<string, byte[]> files = CheckSums(read, result);

            Json island = ParseFile(files, IslandFile);
            string schema = island.Get("schema")?.Str("island.json's schema");
            if (schema != IslandSchema) throw new Refusal($"island.json's schema is '{schema}', not {IslandSchema}.");

            // What a mount's host may be: a piece of any imported scene, an id in the ground file, or a key of the
            // piece's own scene's context (Scene.Context).
            var held = new Dictionary<string, string>(StringComparer.Ordinal);
            string ground = island.Get("ground")?.Str("island.json's ground");
            if (!string.IsNullOrEmpty(ground))
            {
                string groundFile = ground.Split(' ')[0];
                CollectIds(ParseFile(files, groundFile), "the ground file", held, beside);
            }

            var scenes = new List<Scene>();
            var sceneJson = new List<Json>();
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (Json entry in Items(Need(island, "scenes", "island.json"), "island.json's scenes"))
            {
                string id = Need(entry, "id", "an island scene").Str("an island scene's id");
                Json file = entry.Get("file");
                if (file != null) named.Add(file.Str(id + "'s file"));
                if (id == VillageSceneId)
                {
                    result.Report.Add($"refused: {id} ({file?.Text}): its pieces stay with the village plan (V1/V3); never read");
                    continue;
                }
                if (file == null)
                {
                    result.Report.Add($"no file: {id} (the island names it; nothing to import)");
                    continue;
                }
                if (!id.StartsWith(SceneIdPrefix, StringComparison.Ordinal) || id.Length == SceneIdPrefix.Length)
                    throw new Refusal($"island.json names '{id}' with a file; a key scene's id is keyscene.<name>.");
                Json json = ParseFile(files, file.Text);
                string inner = Need(json, "id", file.Text).Str(file.Text + "'s id");
                if (inner != id) throw new Refusal($"{file.Text} holds {inner}, and island.json says {id}.");
                byte[] bytes = files[file.Text];
                var added = new Scene
                {
                    Id = id,
                    Name = Need(json, "name", file.Text).Str(file.Text + "'s name"),
                    Source = $"{packageName}, {file.Text}, {bytes.Length} bytes, sha256 {Sha256(bytes)}",
                };
                scenes.Add(added);
                sceneJson.Add(json);
                Json context = json.Get("context");
                if (context != null && context.Kind == Json.Of.Object)
                    foreach (KeyValuePair<string, Json> c in context.Fields) added.Context.Add(ContextPrefix + c.Key);
            }
            foreach (string path in files.Keys)
                if (SceneFileRx.IsMatch(path) && !named.Contains(path))
                    throw new Refusal($"{path} is in the package and island.json does not list it. The importer takes only the island's scenes.");

            // Every piece's id through the map, before any is read: a host is found by its new id.
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var ids = new List<List<string>>();
            for (int s = 0; s < scenes.Count; s++)
            {
                ids.Add(new List<string>());
                foreach (Json p in Items(Need(sceneJson[s], "pieces", scenes[s].Id), scenes[s].Id + "'s pieces"))
                {
                    string cd = Need(p, "id", scenes[s].Id + "'s piece").Str(scenes[s].Id + "'s piece id");
                    string id = beside.Id(cd, scenes[s].Id + "'s piece");
                    if (id != cd) result.Report.Add($"renamed: {scenes[s].Id} {cd} -> {id}");
                    if (seen.TryGetValue(id, out string other)) throw new Refusal($"{id} is in {other} and in {scenes[s].Id}. An id names one piece.");
                    seen[id] = scenes[s].Id;
                    held[id] = "a piece of " + scenes[s].Id;
                    ids[s].Add(id);
                }
            }

            for (int s = 0; s < scenes.Count; s++)
            {
                List<Json> pieces = sceneJson[s].Get("pieces").Items;
                for (int i = 0; i < pieces.Count; i++)
                    scenes[s].Rows.Add(RowOf(scenes[s], pieces[i], ids[s][i], held, beside, result));
            }
            foreach (string piece in beside.Hosts.Keys)
                if (!beside.Answered.Contains(piece))
                    throw new Refusal($"{HostsFile} answers {piece}, and no scene holds a piece of that id with a mount. A stale answer is a STOP for the Art desk.");
            foreach (string old in beside.Renames.Keys)
                if (!beside.Renamed.Contains(old)) result.Report.Add($"the id map renames {old}, and the package holds no such id");

            Dictionary<string, Existing> byId = ReadExisting(existing, scriptGuid);
            foreach (Scene scene in scenes)
                Write(scene, byId, existing, regionId, scriptGuid, result);

            foreach (KeyValuePair<string, Existing> e in byId)
                if (!scenes.Any(s => s.Id == e.Key)) result.Report.Add($"kept as it stands: {e.Value.File} ({e.Key} is not in the package)");
            foreach (KeyValuePair<string, List<string>> h in result.UnheldHosts)
                result.Report.Add($"STOP for the Art desk: the host {h.Key} is held by no scene and no other work brings it; it stops {string.Join(", ", h.Value)}");
            result.Report.Add($"changed: {result.Changed.Count} of {result.Assets.Count} assets; new metas {result.Metas.Count}");
            return result;
        }

        static Dictionary<string, byte[]> CheckSums(Func<string, byte[]> read, Result result)
        {
            byte[] sums = read(SumsFile) ?? throw new Refusal($"no {SumsFile}: the package cannot be checked.");
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string line in Text(sums, SumsFile).Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0) continue;
                Match m = SumLineRx.Match(line);
                if (!m.Success) throw new Refusal($"{SumsFile}: '{line}' is not a sum.");
                string path = m.Groups[2].Value;
                if (files.ContainsKey(path)) throw new Refusal($"{SumsFile} lists {path} twice.");
                byte[] bytes = read(path) ?? throw new Refusal($"{path} is in {SumsFile} and not in the package.");
                if (Sha256(bytes) != m.Groups[1].Value.ToLowerInvariant())
                    throw new Refusal($"{path}: its sha256 is not {SumsFile}'s. The package is not the one CD sent.");
                files[path] = bytes;
            }
            if (files.Count == 0) throw new Refusal($"{SumsFile} lists nothing.");
            result.Report.Add($"sums: {files.Count} files hold");
            return files;
        }

        /// <summary>The id map and the hosts' table, each held to its schema: a rename is one step, to an id that
        /// holds no position, and given once; an answer names its piece once, and stands it on a host by id and
        /// anchor, or on its own ground.</summary>
        static Beside ReadBeside(byte[] idMap, byte[] hosts, Result result)
        {
            var beside = new Beside();
            if (idMap == null) throw new Refusal($"no id map ({IdMapFile}).");
            Json map = Json.Parse(Text(idMap, IdMapFile), IdMapFile);
            Only(map, IdMapFile, "schema", "renames");
            string schema = Need(map, "schema", IdMapFile).Str(IdMapFile + "'s schema");
            if (schema != IdMapSchema) throw new Refusal($"{IdMapFile}'s schema is '{schema}', not {IdMapSchema}.");
            var news = new HashSet<string>(StringComparer.Ordinal);
            foreach (Json r in Items(Need(map, "renames", IdMapFile), IdMapFile + "'s renames"))
            {
                Only(r, IdMapFile + "'s rename", "old", "new");
                string old = Need(r, "old", IdMapFile + "'s rename").Str(IdMapFile + "'s old id");
                string renamed = Need(r, "new", IdMapFile + "'s rename").Str(IdMapFile + "'s new id");
                if (old.Length == 0 || renamed.Length == 0 || old == renamed) throw new Refusal($"{IdMapFile}: '{old}' to '{renamed}' is not a rename.");
                if (renamed.IndexOf(PositionMark) >= 0) throw new Refusal($"{IdMapFile}: {renamed} holds a position, and a new id never does.");
                if (beside.Renames.ContainsKey(old)) throw new Refusal($"{IdMapFile} renames {old} twice.");
                if (!news.Add(renamed)) throw new Refusal($"{IdMapFile} gives {renamed} to two ids.");
                beside.Renames[old] = renamed;
            }
            foreach (string old in beside.Renames.Keys)
                if (news.Contains(old)) throw new Refusal($"{IdMapFile}: {old} is an old id and a new one. A rename is one step.");
            result.Report.Add($"id map: {IdMapFile}, {beside.Renames.Count} renames, {idMap.Length} bytes, sha256 {Sha256(idMap)}");

            if (hosts == null) throw new Refusal($"no hosts' table ({HostsFile}).");
            Json table = Json.Parse(Text(hosts, HostsFile), HostsFile);
            Only(table, HostsFile, "schema", "about", "hosts");
            schema = Need(table, "schema", HostsFile).Str(HostsFile + "'s schema");
            if (schema != HostsSchema) throw new Refusal($"{HostsFile}'s schema is '{schema}', not {HostsSchema}.");
            foreach (Json h in Items(Need(table, "hosts", HostsFile), HostsFile + "'s hosts"))
            {
                Only(h, HostsFile + "'s answer", "piece", "mount", "standsOn", "host", "anchor", "answer");
                string piece = beside.Id(Need(h, "piece", HostsFile + "'s answer").Str(HostsFile + "'s piece"), HostsFile);
                string what = HostsFile + ": " + piece;
                string mount = Need(h, "mount", what).Str(what + "'s mount");
                string standsOn = Need(h, "standsOn", what).Str(what + "'s standsOn");
                string host = Need(h, "host", what).Str(what + "'s host");
                string anchor = Need(h, "anchor", what).Str(what + "'s anchor");
                if (mount.Length == 0 || Need(h, "answer", what).Str(what + "'s answer").Length == 0)
                    throw new Refusal($"{what}: an answer gives the piece's mount words and whose answer it is.");
                if (standsOn == KeySceneDef.StandsOnMount)
                {
                    if (host.Length == 0 || anchor.Length == 0) throw new Refusal($"{what}: on a mount, it names its host by id and the host's anchor.");
                }
                else if (standsOn == KeySceneDef.StandsOnGround)
                {
                    if (host.Length > 0 || anchor.Length > 0) throw new Refusal($"{what}: on its own ground, it names no host.");
                }
                else throw new Refusal($"{what}: it stands on '{standsOn}'; an answer stands a piece on a host or on its own ground.");
                if (beside.Hosts.ContainsKey(piece)) throw new Refusal($"{HostsFile} answers {piece} twice.");
                beside.Hosts[piece] = new HostAnswer
                {
                    Mount = mount,
                    Rule = new MountRule(standsOn, host.Length == 0 ? "" : beside.Id(host, what + "'s host"), anchor),
                };
            }
            result.Report.Add($"hosts' table: {HostsFile}, {beside.Hosts.Count} answers, {hosts.Length} bytes, sha256 {Sha256(hosts)}");
            return beside;
        }

        /// <summary>A mapping that holds no key but those named: a new key is the importer's to learn.</summary>
        static void Only(Json o, string what, params string[] keys)
        {
            if (o.Kind != Json.Of.Object) throw new Refusal($"{what} is not a mapping.");
            foreach (KeyValuePair<string, Json> f in o.Fields)
                if (Array.IndexOf(keys, f.Key) < 0) throw new Refusal($"{what}: the key '{f.Key}' is new to the importer.");
        }

        static Row RowOf(Scene scene, Json p, string id, Dictionary<string, string> held, Beside beside, Result result)
        {
            string what = scene.Id + " " + id;
            foreach (KeyValuePair<string, Json> f in p.Fields)
                if (!CarriedKeys.Contains(f.Key) && !PackageOnlyKeys.Contains(f.Key))
                    throw new Refusal($"{what}: the key '{f.Key}' is new to the importer. Teach it to carry the key, or to leave it in the package.");

            var row = new Row { Id = id };
            row.Kit = Need(p, "kit", what).Str(what + " kit");
            row.Piece = Need(p, "piece", what).Str(what + " piece");
            Json defType = p.Get("defType");
            row.Kind = KindOf(row.Kit, defType == null || defType.Kind == Json.Of.Null ? null : defType.Str(what + " defType"), id);
            (row.X, row.Y) = Pair(Need(p, "at", what), what + " at");
            row.Dir = Need(p, "dir", what).Int(what + " dir");
            if (row.Dir < 0 || row.Dir > 7) throw new Refusal($"{what}: dir {row.Dir} is not one of the kit's eight (0..7).");

            Json mount = p.Get("mount");
            bool answered = beside.Hosts.TryGetValue(id, out HostAnswer answer);
            if (mount != null)
            {
                row.MountText = mount.Str(what + " mount");
                MountRule rule;
                if (answered)
                {
                    if (row.MountText != answer.Mount)
                        throw new Refusal($"{what}: its mount reads \"{row.MountText}\", and the hosts' table answers \"{answer.Mount}\". " +
                                          "A new wording is a new answer (the Art desk).");
                    rule = answer.Rule;
                    beside.Answered.Add(id);
                }
                else if (!Mounts.TryGetValue(row.MountText, out rule))
                    throw new Refusal($"{what}: the mount table cannot read \"{row.MountText}\". A new mount text is a new ruling (lead-architect).");
                row.StandsOn = rule.StandsOn;
                row.MountHost = rule.Host.Length == 0 ? "" : beside.Id(rule.Host, what + "'s host");
                row.MountAnchor = rule.Anchor;
            }
            else if (answered) throw new Refusal($"{what}: the hosts' table answers its mount, and the package gives it none.");
            Json contact = p.Get("contact");
            if (contact != null)
            {
                string c = contact.Str(what + " contact");
                if (c != Afloat) throw new Refusal($"{what}: contact '{c}' is new to the importer.");
                if (mount != null && row.StandsOn != KeySceneDef.StandsOnWater)
                    throw new Refusal($"{what}: it is afloat, and its mount stands it on {row.StandsOn}.");
                row.StandsOn = KeySceneDef.StandsOnWater;
            }

            Json z = Need(p, "z", what);
            if (z.Kind == Json.Of.String)
            {
                if (z.Text != TheTide || row.StandsOn != KeySceneDef.StandsOnWater)
                    throw new Refusal($"{what}: z is \"{z.Text}\"; only a piece afloat reads its height off the tide.");
                row.Z = 0f;
            }
            else
            {
                row.Z = z.Float(what + " z");
                Match m = row.MountText == null ? Match.Empty : AnchorHeightRx.Match(row.MountText);
                if (m.Success)
                {
                    decimal anchor = decimal.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                    decimal height = decimal.Parse(z.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (Math.Abs(anchor - height) > AnchorTolerance)
                        throw new Refusal($"{what}: z {z.Text} stands off its mount's +{m.Groups[1].Value} by more than 5 cm.");
                }
            }

            Json foot = p.Get("foot");
            if (foot != null)
            {
                row.HasFoot = true;
                (row.FootX, row.FootY) = Pair(foot, what + " foot");
            }
            Json sortY = p.Get("sortY");
            if (sortY != null)
            {
                row.HasSortY = true;
                row.SortY = sortY.Float(what + " sortY");
            }
            row.Layer = p.Get("layer")?.Str(what + " layer") ?? "";
            row.Walk = p.Get("walk")?.Str(what + " walk") ?? "";
            // A collider is a string ("polygon 0.40 x 0.42 m"), or a shape the kit builds from (the gap bridge's deck,
            // walk, rail and turn), kept as compact JSON, as a tide rule and an option are.
            Json collider = p.Get("collider");
            row.Collider = collider == null ? "" : collider.Kind == Json.Of.String ? collider.Text : collider.Compact();

            Json variants = p.Get("variants");
            if (variants == null) row.Variants.Add(KeySceneDef.Today);
            else
            {
                foreach (Json v in Items(variants, what + " variants"))
                {
                    string name = v.Str(what + " variant");
                    if (name.Length == 0 || row.Variants.Contains(name)) throw new Refusal($"{what}: the variant '{name}' is empty or twice.");
                    row.Variants.Add(name);
                }
                if (row.Variants.Count == 0) throw new Refusal($"{what}: it stands in no variant.");
            }
            foreach (KeyValuePair<string, string> ruled in RuledToday)
                if (ruled.Key == scene.Id && row.Variants.Contains(ruled.Value) && !row.Variants.Contains(KeySceneDef.Today))
                {
                    row.Variants.Add(KeySceneDef.Today);
                    result.Report.Add($"ruled: {what} stands today as well as in '{ruled.Value}' (the owner's ruling)");
                }

            Json taken = p.Get("taken");
            Json boardOnly = p.Get("boardOnly");
            if ((taken != null && !Bool(taken, what + " taken")) || (boardOnly != null && Bool(boardOnly, what + " boardOnly")))
                if (row.Variants.Contains(KeySceneDef.Today))
                    throw new Refusal($"{what}: it is not taken (or the boards' only), and it stands in today's variant.");

            Json whenTide = p.Get("whenTide");
            if (whenTide != null) row.WhenTide = whenTide.Compact();

            Json words = p.Get("words");
            if (words != null)
                foreach (Json w in Items(words, what + " words"))
                    row.Words.Add(beside.Id(w.Kind == Json.Of.Object ? Need(w, "id", what + " words").Str(what + " words' id") : w.Str(what + " words"),
                                            what + " words"));

            Json opts = p.Get("opts");
            if (opts != null)
            {
                if (opts.Kind != Json.Of.Object) throw new Refusal($"{what}: opts is not a mapping.");
                foreach (KeyValuePair<string, Json> o in opts.Fields)
                {
                    row.Options.Add(new KeyValuePair<string, string>(o.Key, o.Value.Kind == Json.Of.String ? o.Value.Text : o.Value.Compact()));
                    if (o.Key == "state") row.State = o.Value.Str(what + " opts.state");
                }
            }

            Json frozen = p.Get("frozen");
            row.Frozen = frozen != null && Bool(frozen, what + " frozen");
            if (FrozenOwners.TryGetValue(id, out string owner))
            {
                if (!row.Frozen) throw new Refusal($"{what}: the owner table names {owner} for it, and the package does not freeze it.");
                row.Owner = owner;
            }
            else if (row.Frozen) throw new Refusal($"{what}: it is frozen, and the owner table names no code that places it.");
            if (row.Owner.Length == 0 && KindOwners.TryGetValue(row.Kind, out string kindOwner)) row.Owner = kindOwner;

            Json light = p.Get("light");
            if (light != null) scene.Lights.Add(LightOf(id, light, what));

            Tally t = scene.Tally;
            t.Pieces++;
            t.Kits.Add(row.Kit);
            bool today = row.Variants.Contains(KeySceneDef.Today);
            if (today) t.Today++;
            else t.Data++;
            if (row.Owner.Length > 0) t.Owned++;
            if (row.Frozen) t.Frozen++;
            if (light != null) t.Lights++;
            if (row.MountText != null) t.Mounted++;
            if (row.StandsOn == KeySceneDef.StandsOnMount)
            {
                t.OnHost++;
                string host = row.MountHost;
                bool context = host.StartsWith(ContextPrefix, StringComparison.Ordinal);
                bool unheld = host.Length > 0 && (context ? !scene.Context.Contains(host) : !held.ContainsKey(host) && !HostsFromOtherWork.ContainsKey(host));
                string how = host.Length == 0 ? "names no host by id"
                    : unheld ? "HELD BY NO SCENE"
                    : context ? "held: " + scene.Id + "'s context"
                    : held.TryGetValue(host, out string by) ? "held: " + by
                    : "brought by " + HostsFromOtherWork[host];
                if (unheld)
                {
                    if (!result.UnheldHosts.TryGetValue(host, out List<string> ids)) result.UnheldHosts[host] = ids = new List<string>();
                    ids.Add(id);
                }
                result.Report.Add($"mount: {what} on {(host.Length == 0 ? "(none)" : host)} at {row.MountAnchor}, z {Num(row.Z)}; host {how}" +
                                  (answered ? "; the hosts' table" : ""));
            }
            else if (answered) result.Report.Add($"mount: {what} stands on its own {row.StandsOn}, z {Num(row.Z)}, on no host; the hosts' table");
            return row;
        }

        static Light LightOf(string pieceId, Json light, string what)
        {
            if (light.Kind != Json.Of.Object) throw new Refusal($"{what}: its light is not a mapping.");
            foreach (KeyValuePair<string, Json> f in light.Fields)
                if (!LightKeys.Contains(f.Key)) throw new Refusal($"{what}: the light's key '{f.Key}' is new to the importer.");
            var l = new Light { PieceId = pieceId };
            l.Preset = light.Get("preset")?.Str(what + " light preset") ?? "";
            Json pool = light.Get("pool");
            l.Pool = pool != null && Bool(pool, what + " light pool");
            l.Reach = light.Get("reachM")?.Float(what + " light reachM") ?? 0f;
            Json at = light.Get("at");
            if (at != null)
            {
                List<Json> xyz = Items(at, what + " light at");
                if (xyz.Count != 3) throw new Refusal($"{what}: the light's at is not x, y, z.");
                l.X = xyz[0].Float(what + " light at");
                l.Y = xyz[1].Float(what + " light at");
                l.Z = xyz[2].Float(what + " light at");
            }
            l.Colour = light.Get("colour")?.Str(what + " light colour") ?? "";
            l.Intensity = light.Get("intensity")?.Float(what + " light intensity") ?? 0f;
            l.When = light.Get("when")?.Str(what + " light when") ?? "";
            l.WhoLights = light.Get("whoLights")?.Str(what + " light whoLights") ?? "";
            l.Budget = light.Get("budget")?.Int(what + " light budget") ?? 0;
            l.Note = light.Get("note")?.Str(what + " light note") ?? "";
            return l;
        }

        static Dictionary<string, Existing> ReadExisting(IReadOnlyDictionary<string, string> existing, string scriptGuid)
        {
            var byId = new Dictionary<string, Existing>(StringComparer.Ordinal);
            if (existing == null) return byId;
            foreach (KeyValuePair<string, string> e in existing.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                List<KeyValuePair<string, YamlNode>> top = ReadAsset(e.Value, e.Key);
                foreach (KeyValuePair<string, YamlNode> f in top)
                    if (Array.IndexOf(UnityFields, f.Key) < 0 && Array.IndexOf(SceneFields, f.Key) < 0)
                        throw new Refusal($"{e.Key}: '{f.Key}' is not a field of KeySceneDef.");
                string script = Get(top, "m_Script")?.Scalar ?? "";
                if (script.IndexOf("guid: " + scriptGuid + ",", StringComparison.Ordinal) < 0)
                    throw new Refusal($"{e.Key} is not a KeySceneDef (its script: {script}).");
                string id = Get(top, "Id")?.Scalar;
                if (string.IsNullOrEmpty(id)) throw new Refusal($"{e.Key} has no Id.");
                if (byId.TryGetValue(id, out Existing twin)) throw new Refusal($"{twin.File} and {e.Key} both hold {id}.");
                var old = new Existing { File = e.Key, Top = top, Rows = Get(top, "Pieces")?.Items ?? new List<YamlNode>() };
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (YamlNode r in old.Rows)
                {
                    if (r.Map == null) throw new Refusal($"{e.Key}: a piece that is not a mapping.");
                    foreach (KeyValuePair<string, YamlNode> f in r.Map)
                        if (Array.IndexOf(PieceFields, f.Key) < 0) throw new Refusal($"{e.Key}: '{f.Key}' is not a field of KeyScenePiece.");
                    string rid = r.Get("Id")?.Scalar;
                    if (string.IsNullOrEmpty(rid) || !ids.Add(rid)) throw new Refusal($"{e.Key}: a piece with no id, or {rid} twice.");
                }
                string placed = Get(top, "Placed")?.Scalar;
                if (placed != null && placed != "0" && placed != "1") throw new Refusal($"{e.Key}: Placed is '{placed}'.");
                old.Placed = placed != "0";
                old.Retired = (Get(top, "RetiredIds")?.Items ?? new List<YamlNode>()).Select(n => n.Scalar).ToList();
                byId[id] = old;
            }
            return byId;
        }

        static void Write(Scene scene, Dictionary<string, Existing> byId, IReadOnlyDictionary<string, string> existing,
                          string regionId, string scriptGuid, Result result)
        {
            byId.TryGetValue(scene.Id, out Existing old);
            string file = old?.File ?? "KeyScene_" + scene.Id.Substring(SceneIdPrefix.Length) + ".asset";
            if (old == null && existing != null && existing.ContainsKey(file))
                throw new Refusal($"{file} is there and holds another id; {scene.Id} needs its name.");

            var ids = new HashSet<string>(scene.Rows.Select(r => r.Id), StringComparer.Ordinal);
            var retired = new List<string>(old?.Retired ?? new List<string>());
            foreach (string r in retired)
                if (ids.Contains(r)) throw new Refusal($"{scene.Id}: {r} was retired, and the package uses it again. A retired id is never reused.");
            var newlyRetired = new List<string>();
            var oldRows = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
            if (old != null)
                foreach (YamlNode r in old.Rows)
                {
                    string rid = r.Get("Id").Scalar;
                    oldRows[rid] = r;
                    if (!ids.Contains(rid))
                    {
                        newlyRetired.Add(rid);
                        continue;
                    }
                    Row incoming = scene.Rows.First(x => x.Id == rid);
                    string kit = r.Get("Kit")?.Scalar ?? "";
                    string kind = r.Get("Kind")?.Scalar ?? KeySceneDef.KindSetPiece;
                    if (kit != incoming.Kit) throw new Refusal($"{scene.Id} {rid}: its kit changes ({kit} to {incoming.Kit}). An id that changes kit is a STOP for the owner, not a move.");
                    if (kind != incoming.Kind) throw new Refusal($"{scene.Id} {rid}: its kind changes ({kind} to {incoming.Kind}). An id that changes kind is a STOP for the owner, not a move.");
                }
            retired.AddRange(newlyRetired);

            var selection = (old == null ? null : Get(old.Top, "PlacementIds")?.Items)?.Select(n => n.Scalar).ToList();
            if (selection != null && selection.Any(id => !ids.Contains(id)))
                throw new Refusal($"{scene.Id}: a selected placement id is no longer in the package; review the wave selection.");
            string text = AssetText(scene, file, regionId, scriptGuid, old?.Placed ?? false, retired, selection);
            result.Assets[file] = text;
            result.Tallies[scene.Id] = scene.Tally;
            if (old == null) result.Metas[file + ".meta"] = MetaText(GuidFor(scene.Id));
            string was = null;
            if (existing == null || !existing.TryGetValue(file, out was) || was != text) result.Changed.Add(file);

            result.Report.Add($"scene {scene.Id} -> {file} ({(old == null ? "new, Placed 0" : "existing, Placed " + (old.Placed ? "1" : "0"))}" +
                              $"{(was == text ? ", unchanged" : "")}): {scene.Tally}");
            if (old == null) return;

            // The diff against the asset as it stands, field by field on the values (not the text).
            List<KeyValuePair<string, YamlNode>> now = ReadAsset(text, file);
            foreach (string f in new[] { "DisplayName", "RegionId", "Source" })
                if (Get(old.Top, f)?.Scalar != Get(now, f)?.Scalar)
                    result.Report.Add($"  {f}: {Get(old.Top, f)?.Scalar} -> {Get(now, f)?.Scalar}");
            var additions = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string f in SceneFields)
                if (Get(old.Top, f) == null) additions.Add(f);
            var kept = new List<string>();
            foreach (YamlNode r in Get(now, "Pieces")?.Items ?? new List<YamlNode>())
            {
                string rid = r.Get("Id").Scalar;
                if (!oldRows.TryGetValue(rid, out YamlNode before))
                {
                    result.Report.Add($"  added: {rid}");
                    continue;
                }
                kept.Add(rid);
                var changes = new List<string>();
                foreach (KeyValuePair<string, YamlNode> f in r.Map)
                {
                    YamlNode b = before.Get(f.Key);
                    if (b == null)
                    {
                        additions.Add(f.Key);
                        continue;
                    }
                    if (b.Canon() != f.Value.Canon()) changes.Add($"{f.Key} {Clip(b.Canon())} -> {Clip(f.Value.Canon())}");
                }
                if (changes.Count == 1 && changes[0].StartsWith("Z ", StringComparison.Ordinal)) result.Report.Add($"  height: {rid}: {changes[0]}");
                else if (changes.Count > 0) result.Report.Add($"  MORE THAN HEIGHT: {rid}: {string.Join("; ", changes)}");
            }
            foreach (string r in newlyRetired) result.Report.Add($"  retired: {r}");
            List<string> oldOrder = old.Rows.Select(r => r.Get("Id").Scalar).Where(kept.Contains).ToList();
            if (!oldOrder.SequenceEqual(kept)) result.Report.Add($"  ORDER: the kept rows' order changes ({string.Join(", ", kept)})");
            if (additions.Count > 0) result.Report.Add($"  fields new to the asset: {string.Join(", ", additions)}");
        }

        static string Clip(string s) => s.Length <= 120 ? s : s.Substring(0, 117) + "...";

        // =====================================================================================
        //  the asset's text
        // =====================================================================================

        /// <summary>The Def's class identifier, as Unity writes it on a ScriptableObject asset.</summary>
        static string ClassIdentifier => typeof(KeySceneDef).Assembly.GetName().Name + "::" + typeof(KeySceneDef).FullName;

        static string AssetText(Scene s, string file, string regionId, string scriptGuid, bool placed, List<string> retired,
                                List<string> selection = null)
        {
            var w = new YamlWriter();
            w.Line("%YAML 1.1");
            w.Line("%TAG !u! tag:unity3d.com,2011:");
            w.Line("--- !u!114 &11400000");
            w.Line("MonoBehaviour:");
            w.Line("  m_ObjectHideFlags: 0");
            w.Line("  m_CorrespondingSourceObject: {fileID: 0}");
            w.Line("  m_PrefabInstance: {fileID: 0}");
            w.Line("  m_PrefabAsset: {fileID: 0}");
            w.Line("  m_GameObject: {fileID: 0}");
            w.Line("  m_Enabled: 1");
            w.Line("  m_EditorHideFlags: 0");
            w.Line("  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}");
            w.Str(2, "m_Name", Path.GetFileNameWithoutExtension(file));
            w.Str(2, "m_EditorClassIdentifier", ClassIdentifier);
            w.Str(2, "Id", s.Id);
            w.Str(2, "DisplayName", s.Name);
            w.Str(2, "RegionId", regionId);
            w.Str(2, "Source", s.Source);
            w.Raw(2, "Placed", placed ? "1" : "0");
            if (w.Items(2, "Pieces", s.Rows.Count))
                foreach (Row r in s.Rows)
                {
                    w.Str(4, "Id", r.Id, item: true);
                    w.Str(4, "Kind", r.Kind);
                    w.Str(4, "Owner", r.Owner);
                    w.Str(4, "Kit", r.Kit);
                    w.Str(4, "Piece", r.Piece);
                    w.Str(4, "State", r.State);
                    w.Raw(4, "At", Vec(r.X, r.Y));
                    w.Raw(4, "Z", Num(r.Z));
                    w.Str(4, "StandsOn", r.StandsOn);
                    w.Str(4, "MountHost", r.MountHost);
                    w.Str(4, "MountAnchor", r.MountAnchor);
                    w.Raw(4, "HasFoot", r.HasFoot ? "1" : "0");
                    w.Raw(4, "Foot", Vec(r.FootX, r.FootY));
                    w.Raw(4, "Dir", r.Dir.ToString(CultureInfo.InvariantCulture));
                    w.Raw(4, "HasSortY", r.HasSortY ? "1" : "0");
                    w.Raw(4, "SortY", Num(r.SortY));
                    w.Str(4, "Layer", r.Layer);
                    w.Str(4, "Walk", r.Walk);
                    w.Str(4, "Collider", r.Collider);
                    w.List(4, "Variants", r.Variants);
                    w.Str(4, "WhenTide", r.WhenTide);
                    w.List(4, "Words", r.Words);
                    if (w.Items(4, "Options", r.Options.Count))
                        foreach (KeyValuePair<string, string> o in r.Options)
                        {
                            w.Str(6, "Key", o.Key, item: true);
                            w.Str(6, "Value", o.Value);
                        }
                }
            if (w.Items(2, "Lights", s.Lights.Count))
                foreach (Light l in s.Lights)
                {
                    w.Str(4, "PieceId", l.PieceId, item: true);
                    w.Str(4, "Preset", l.Preset);
                    w.Raw(4, "Pool", l.Pool ? "1" : "0");
                    w.Raw(4, "ReachMetres", Num(l.Reach));
                    w.Raw(4, "At", $"{{x: {Num(l.X)}, y: {Num(l.Y)}, z: {Num(l.Z)}}}");
                    w.Str(4, "Colour", l.Colour);
                    w.Raw(4, "Intensity", Num(l.Intensity));
                    w.Str(4, "When", l.When);
                    w.Str(4, "WhoLights", l.WhoLights);
                    w.Raw(4, "Budget", l.Budget.ToString(CultureInfo.InvariantCulture));
                    w.Str(4, "Note", l.Note);
                }
            w.List(2, "RetiredIds", retired);
            w.List(2, "PlacementIds", selection ?? new List<string>());
            return w.ToString();
        }

        static string Vec(float x, float y) => $"{{x: {Num(x)}, y: {Num(y)}}}";

        /// <summary>A float as Unity writes one in an asset: the shortest digits that read back to it (the step's
        /// <see cref="StPetersLayerRefresh.F"/>), always in fixed notation. F's own text turns a round number to an
        /// exponent (170 to 1.7E+02), which Unity reads but writes back as 170, so a re-save would change the file.</summary>
        static string Num(float v)
        {
            if (v != 0f && (Math.Abs(v) < 1e-4f || Math.Abs(v) >= 1e7f))
                throw new Refusal($"the number {v.ToString("R", CultureInfo.InvariantCulture)} is out of a key scene's range (metres, 0.0001 to 10,000,000).");
            string s = StPetersLayerRefresh.F(v);
            return s.IndexOf('E') < 0 ? s : decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A new asset's .meta, as Unity writes a ScriptableObject's.</summary>
        public static string MetaText(string guid) =>
            "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n" +
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        /// <summary>A new asset's guid, from its scene's id: the same scene gets the same guid on every run.</summary>
        public static string GuidFor(string sceneId)
        {
            using (MD5 md5 = MD5.Create())
                return Hex(md5.ComputeHash(Encoding.UTF8.GetBytes(GuidSeed + sceneId)));
        }

        // =====================================================================================
        //  small helpers
        // =====================================================================================

        static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(bytes));
        }

        static string Hex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static string Text(byte[] bytes, string what)
        {
            try
            {
                string s = new UTF8Encoding(false, true).GetString(bytes);
                return s.Length > 0 && s[0] == '\uFEFF' ? s.Substring(1) : s;
            }
            catch (DecoderFallbackException)
            {
                throw new Refusal($"{what} is not UTF-8.");
            }
        }

        static Json ParseFile(Dictionary<string, byte[]> files, string path)
        {
            if (!files.TryGetValue(path, out byte[] bytes))
                throw new Refusal($"{path} is not in {SumsFile}: the importer reads only what the sums hold.");
            return Json.Parse(Text(bytes, path), path);
        }

        static void CollectIds(Json v, string where, Dictionary<string, string> held, Beside beside)
        {
            if (v.Kind == Json.Of.Object)
            {
                Json id = v.Get("id");
                if (id != null && id.Kind == Json.Of.String) held[beside.Id(id.Text, where)] = where;
                foreach (KeyValuePair<string, Json> f in v.Fields) CollectIds(f.Value, where, held, beside);
            }
            else if (v.Kind == Json.Of.Array)
                foreach (Json i in v.Items) CollectIds(i, where, held, beside);
        }

        static Json Need(Json o, string key, string what) =>
            o.Get(key) ?? throw new Refusal($"{what}: no '{key}'.");

        static List<Json> Items(Json v, string what) =>
            v.Kind == Json.Of.Array ? v.Items : throw new Refusal($"{what} is not a list.");

        static (float, float) Pair(Json v, string what)
        {
            List<Json> xy = Items(v, what);
            if (xy.Count != 2) throw new Refusal($"{what} is not x, y.");
            return (xy[0].Float(what), xy[1].Float(what));
        }

        static bool Bool(Json v, string what) =>
            v.Kind == Json.Of.Bool ? v.Text == "true" : throw new Refusal($"{what} is not true or false.");

        static YamlNode Get(List<KeyValuePair<string, YamlNode>> map, string key)
        {
            foreach (KeyValuePair<string, YamlNode> f in map) if (f.Key == key) return f.Value;
            return null;
        }

        // =====================================================================================
        //  the editor's entry (tools-editor)
        // =====================================================================================

        const string MenuRoot = "Hidden Harbours/World/Key Scenes/";

        [MenuItem(MenuRoot + "Import CD's Package (dry run)...")]
        static void DryRunMenu() => FromMenu(apply: false);

        [MenuItem(MenuRoot + "Import CD's Package...")]
        static void ApplyMenu() => FromMenu(apply: true);

        static void FromMenu(bool apply)
        {
            string folder = EditorUtility.OpenFolderPanel("CD's package: its parts unzipped into one folder", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                Debug.Log(Run(folder, apply));
            }
            catch (Refusal r)
            {
                Debug.LogError(r.Message);
            }
        }

        /// <summary>For <c>-executeMethod</c>: <c>-keySceneImportPackage &lt;folder&gt;</c>, and
        /// <c>-keySceneImportApply</c> to write the assets (else a dry run). A refusal fails the run.</summary>
        public static void RunBatch()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-keySceneImportPackage");
            if (at < 0 || at + 1 >= args.Length) throw new Refusal("-keySceneImportPackage <folder> is missing.");
            Debug.Log(Run(args[at + 1], Array.IndexOf(args, "-keySceneImportApply") >= 0));
        }

        /// <summary>Import the package in <paramref name="folder"/>, through the committed id map and hosts' table:
        /// always the report and, for a dry run, the assets it would write, into <see cref="DryRunFolder"/>; with
        /// <paramref name="apply"/>, the changed assets and the new metas into the key scene folder.</summary>
        public static string Run(string folder, bool apply)
        {
            byte[] idMap = File.Exists(IdMapFile) ? File.ReadAllBytes(IdMapFile) : null;
            byte[] hosts = File.Exists(HostsFile) ? File.ReadAllBytes(HostsFile) : null;
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Func<string, byte[]> read = rel =>
            {
                string full = Path.GetFullPath(Path.Combine(root, rel));
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new Refusal($"'{rel}' leaves the package's folder.");
                return File.Exists(full) ? File.ReadAllBytes(full) : null;
            };
            var existing = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(StPetersLayerRefresh.KeySceneFolder, "*.asset"))
                existing[Path.GetFileName(path)] = File.ReadAllText(path);

            Result result = Import(PackageName(root), read, existing, StPetersLayerRefresh.StPetersRegionId, ScriptGuid(), idMap, hosts);

            var utf8 = new UTF8Encoding(false);
            string target = apply ? StPetersLayerRefresh.KeySceneFolder : DryRunFolder;
            Directory.CreateDirectory(DryRunFolder);
            File.WriteAllText(Path.Combine(DryRunFolder, "report.txt"), string.Join("\n", result.Report) + "\n", utf8);
            foreach (string file in result.Changed) File.WriteAllText(Path.Combine(target, file), result.Assets[file], utf8);
            foreach (KeyValuePair<string, string> meta in result.Metas)
            {
                string path = Path.Combine(target, meta.Key);
                if (!File.Exists(path)) File.WriteAllText(path, meta.Value, utf8);
            }
            if (apply) AssetDatabase.Refresh();
            return $"[KeySceneImport] {(apply ? "wrote" : "dry run, into " + DryRunFolder + ":")} {result.Changed.Count} of " +
                   $"{result.Assets.Count} key scene assets changed, {result.Metas.Count} new. The report: {DryRunFolder}/report.txt";
        }

        /// <summary>The package's name for each asset's Source: PARTS.json's own, else the folder's.</summary>
        static string PackageName(string root)
        {
            string parts = Path.Combine(root, "PARTS.json");
            if (File.Exists(parts))
            {
                Json name = Json.Parse(Text(File.ReadAllBytes(parts), "PARTS.json"), "PARTS.json").Get("package");
                if (name != null && name.Kind == Json.Of.String && name.Text.Length > 0) return name.Text;
            }
            return Path.GetFileName(root);
        }

        static string ScriptGuid()
        {
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
                if (script != null && script.GetClass() == typeof(KeySceneDef))
                    return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            throw new Refusal("KeySceneDef's script is not in the project.");
        }
    }
}
#endif
