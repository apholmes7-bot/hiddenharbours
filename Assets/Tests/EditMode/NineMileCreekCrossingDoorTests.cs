using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE CREEK'S DOOR TO ST PETERS, AS BIG AS THE OWNER ASKED</b> (ruling of 2026-10-17: a larger door
    /// each way, and no change to the water). The crossing's band at the bar's tip is 6 × 120 m, wide enough to
    /// be a door for a boat beside the bar as well as for the walker on it, and it asks the island for its
    /// <c>bar</c> landing, so a walker arriving either way lands on the far bar. The bar still floods at high
    /// water: the tests that hold that gate stand as they are.
    ///
    /// <para>Four claims. The scene's door and landing are the builder's numbers. The landing stands outside the
    /// door it arrives by. A door that asks for a landing the far side does not author arrives exactly where a
    /// door asking for nothing does, which is how the crossing behaves until St Peters answers to the name. And
    /// nothing else that moves can reach the bigger door: a door fires for any collider with a body behind it,
    /// so every body, every moored boat, every scheduled trip and every fleet is held clear of it, and a gull
    /// carries neither a collider nor a body.</para>
    /// </summary>
    public class NineMileCreekCrossingDoorTests
    {
        const int MonoBehaviourClass = 114, Rigidbody2DClass = 50, CircleCollider2DClass = 58, BoxCollider2DClass = 61;
        const string DoorName = "PassageToStPeters", CoveDoorName = "PassageToCoddleCove", LandingName = "NineMileCreekBarLanding";
        const string StPetersRegionAsset = "Assets/_Project/Data/Regions/StPeters.asset";
        const string MooredBoatSource = "Assets/_Project/Code/Boats/MooredBoat.cs";
        const string PlayerSource = "Assets/_Project/Code/Player/PlayerWalkController.cs";
        const string ScheduledTripSource = "Assets/_Project/Code/Vehicles/ScheduledTrip.cs";

        /// <summary>The door's re-fire guard, which the owner's ruling keeps.</summary>
        const float GuardSeconds = 1.5f;

        /// <summary>How far every other mover must stand from the door's band. Nothing in the creek comes near it:
        /// the nearest is a moored boat 168 m away.</summary>
        const float FarFromTheDoor = 100f;

        static Rect Band(Vector3 centre, Vector2 size) => new Rect((Vector2)centre - size * 0.5f, size);
        static Rect TheDoor => Band(NineMileCreekBuilder.ToStPetersPassagePos, NineMileCreekBuilder.StPetersPassageBandSize);

        /// <summary>The gap between a point and a band, zero inside it.</summary>
        static float GapTo(Rect band, Vector2 p)
        {
            float dx = Mathf.Max(band.xMin - p.x, p.x - band.xMax, 0f);
            float dy = Mathf.Max(band.yMin - p.y, p.y - band.yMax, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static string At2(Vector2 v) => $"({Shown(v.x)}, {Shown(v.y)})";
        static Doc GameObjectOf(Doc component) => Scene.Require(component.FieldRef("m_GameObject"), "a component's GameObject");

        // ---- the door and the landing, the builder's numbers ------------------------------------------------------

        [Test]
        public void TheDoorInTheScene_IsTheBuildersDoor_SixByAHundredAndTwenty_AskingForTheIslandsBar()
        {
            Doc door = At(DoorName);
            Assert.That(Live(door), Is.True, "the crossing door is live");
            Assert.That(Vector2.Distance(WorldPosition(door), NineMileCreekBuilder.ToStPetersPassagePos), Is.LessThan(1e-4f),
                "the door stands where it stood, at the bar's tip");

            Doc box = Components(door, BoxCollider2DClass).Single();
            Assert.That(Enabled(box), Is.True, "the band is live");
            Assert.That(box.Field("m_IsTrigger"), Is.EqualTo("1"), "the band is a trigger, not a wall");
            Assert.That(Vector(box, "m_Offset"), Is.EqualTo(Vector2.zero), "the band is centred on the door");
            Assert.That(Vector(box, "m_Size"), Is.EqualTo(NineMileCreekBuilder.StPetersPassageBandSize),
                "the scene's band is the builder's");
            Assert.That(NineMileCreekBuilder.StPetersPassageBandSize, Is.EqualTo(new Vector2(6f, 120f)),
                "the owner's size for the doors between the creek and St Peters: 6 m deep, 120 m across");

            Doc passage = Script(door, "HiddenHarbours.World.RegionPassage");
            Assert.That(Enabled(passage), Is.True, "the passage is live");
            Assert.That(RefGuid(passage.Field("_target")), Is.EqualTo(GuidOf(StPetersRegionAsset)), "the door leads to St Peters");
            Assert.That(passage.Field("_arrivalKey"), Is.EqualTo(NineMileCreekBuilder.StPetersBarArrivalKey),
                "the door asks the island for its bar landing");
            Assert.That(NineMileCreekBuilder.StPetersBarArrivalKey, Is.EqualTo(NineMileCreekBuilder.BarArrivalKey),
                "one bar, one word, both ways");
            Assert.That(Number(passage, "_reentryCooldownSeconds"), Is.EqualTo(GuardSeconds), "the re-fire guard is kept");

            // The whole band inside the region: half hanging off the edge, the camera's clamp cannot follow a boat in.
            var region = new Rect(NineMileCreekMainland.RegionWorldCenter - NineMileCreekMainland.RegionWorldSize * 0.5f,
                                  NineMileCreekMainland.RegionWorldSize);
            Rect band = TheDoor;
            TestContext.WriteLine($"the door's band: x {Shown(band.xMin)}..{Shown(band.xMax)}, y {Shown(band.yMin)}..{Shown(band.yMax)}; " +
                                  $"the region: x {Shown(region.xMin)}..{Shown(region.xMax)}, y {Shown(region.yMin)}..{Shown(region.yMax)}");
            Assert.That(region.Contains(band.min) && region.Contains(band.max), Is.True, "the band lies inside the region");
        }

        [Test]
        public void TheCoddleCoveDoor_KeepsTheRegionsPassageBand()
        {
            Doc cove = At(CoveDoorName);
            Assert.That(Vector2.Distance(WorldPosition(cove), NineMileCreekBuilder.ToCovePassagePos), Is.LessThan(1e-4f),
                "the cove door stays put");
            Assert.That(Vector(Components(cove, BoxCollider2DClass).Single(), "m_Size"), Is.EqualTo(NineMileCreekBuilder.PassageBandSize),
                "the scene's cove door is the builder's");
            Assert.That(NineMileCreekBuilder.PassageBandSize, Is.EqualTo(new Vector2(6f, 40f)),
                "PassageBandSize still sizes the Coddle Cove door, unchanged by the crossing's own");
        }

        [Test]
        public void TheBarLanding_StandsOnTheCrest_OutsideTheDoorItArrivesBy()
        {
            Doc landing = At(LandingName);
            Vector2 at = WorldPosition(landing);
            Assert.That(Vector2.Distance(at, (Vector2)NineMileCreekMainland.WalkArrivalPos), Is.LessThan(1e-4f),
                "the scene's landing is the builder's");

            Vector2 from = NineMileCreekMainland.BarFrom, to = NineMileCreekMainland.BarTo;
            float offAxis = DistanceToSegment(at, from, to);
            float alongPastTheTip = Vector2.Dot(at - to, (to - from).normalized);
            Assert.That(offAxis, Is.LessThan(0.05f), "the landing stands on the bar's crest");
            Assert.That(alongPastTheTip, Is.LessThan(0f), "on the creek's side of the tip");

            // The player's own circle, as the scene authors it.
            string walker = GuidOf(PlayerSource);
            Doc player = Scene.Docs.Where(d => d.ClassId == MonoBehaviourClass && ScriptGuid(d) == walker)
                              .Select(GameObjectOf).Single();
            Doc circle = Components(player, CircleCollider2DClass).Single();
            float radius = Number(circle, "m_Radius");
            Vector2 feet = at + Vector(circle, "m_Offset");
            float gap = GapTo(TheDoor, feet);
            TestContext.WriteLine($"landing {At2(at)}: {Shown(-alongPastTheTip)} m short of the tip, {Shown(offAxis)} m off the axis; " +
                                  $"the player's circle (radius {Shown(radius)} m) stands {Shown(gap)} m from the door's band");
            Assert.That(gap, Is.GreaterThan(radius),
                "a walker who lands here must stand OUTSIDE the door they came in by: inside it, only the re-fire " +
                "guard would stand between a slow first frame and a crossing straight back");
        }

        // ---- a door asking for a landing the far side lacks -------------------------------------------------------

        readonly List<Object> _spawned = new();
        GameObject New(string n) { var g = new GameObject(n); _spawned.Add(g); return g; }
        Transform Place(string n, Vector3 p) { var g = New(n); g.transform.position = p; return g.transform; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        /// <summary>A real rig over an island at St Peters' own points: its arrival, dock and step-ashore, and one
        /// named way in, from the east water, but no landing named for the bar.</summary>
        ControlSwitcher IslandRig(out Transform player, out Transform boat, out RegionAnchor island)
        {
            var playerGo = New("Player");
            var walk = playerGo.AddComponent<PlayerWalkController>();
            player = playerGo.transform;

            var boatGo = New("Boat");
            var controller = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test"; hull.CameraWorldHeightMeters = 14f; hull.DraughtMeters = 0.3f;
            _spawned.Add(hull);
            controller.SetHull(hull);
            boat = boatGo.transform;

            island = New("StPetersRegionAnchor").AddComponent<RegionAnchor>();
            island.Configure("region.st_peters", Place("StPetersArrival", StPetersBuilder.ArrivalPos),
                             Place("StPetersDockZone", StPetersBuilder.DockZonePos), Place("StPetersDisembark", StPetersBuilder.DisembarkPos));
            island.ConfigureArrivals(new NamedArrival(StPetersBuilder.FromEastWaterArrivalKey,
                                                      Place("StPetersEastWaterArrival", StPetersBuilder.FromEastWaterArrivalPos), null));

            var switcher = New("Switcher").AddComponent<ControlSwitcher>();
            switcher.Configure(walk, controller, input, null, 3.5f, null);
            return switcher;
        }

        [Test]
        public void ADoorAskingForALandingTheFarSideLacks_ArrivesWhereADoorAskingForNothingDoes()
        {
            var switcher = IslandRig(out Transform player, out Transform boat, out RegionAnchor island);
            string key = NineMileCreekBuilder.StPetersBarArrivalKey;
            Assert.That(island.HasArrival(key), Is.False, "precondition: this island does not answer to the name");

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, island);
            Vector3 keylessPlayer = player.position, keylessBoat = boat.position;
            player.position = boat.position = new Vector3(-999f, -999f, 0f);

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, island, key);
            Assert.AreEqual(keylessPlayer, player.position,
                "asking for a landing the island lacks puts the walker where a door asking for nothing does");
            Assert.AreEqual(keylessBoat, boat.position, "…and the boat too");
            Assert.AreEqual(StPetersBuilder.DisembarkPos, player.position,
                "which is the island's own step-ashore: where the crossing landed before its door named anything");
            Assert.AreEqual(StPetersBuilder.ArrivalPos, boat.position, "…and the island's own arrival point");
        }

        // ---- nothing else that moves reaches the bigger door -----------------------------------------------------

        [Test]
        public void NoFleetSailsTheCreek_SoNoAmbientBoatCanReachItsDoor()
        {
            string creek = Path.GetFileNameWithoutExtension(NineMileCreekLayerRefresh.ScenePath);
            var library = Resources.Load<AmbientFleetLibrary>(AmbientFleetLibrary.ResourcesPath);
            Assert.That(library, Is.Not.Null, "the fleet library loads as the presenter loads it");
            Assert.That(library.Fleets, Is.Not.Empty, "control: the library holds St Peters' fleet");
            foreach (AmbientFleetDef fleet in library.Fleets)
            {
                Assert.That(fleet, Is.Not.Null, "a hole in the fleet library");
                TestContext.WriteLine($"{fleet.name} sails '{fleet.RegionSceneName}'");
                Assert.That(fleet.RegionSceneName, Is.Not.Empty,
                    $"{fleet.name} names no scene, so it sails every scene, the creek and its door included");
                Assert.That(string.Equals(fleet.RegionSceneName, creek, StringComparison.OrdinalIgnoreCase), Is.False,
                    $"{fleet.name} sails the creek: its spots and legs would need keeping clear of the crossing door");
            }
        }

        [Test]
        public void NothingThatMovesStandsWithinAHundredMetresOfTheDoor()
        {
            Rect band = TheDoor;
            List<(string who, Vector2 at)> movers = Movers();
            Assert.That(movers.Count(m => m.who.StartsWith("Rigidbody2D", StringComparison.Ordinal)), Is.GreaterThan(0),
                "control: the scan finds the scene's bodies (the player has one)");
            Assert.That(movers.Count(m => m.who.StartsWith("MooredBoat", StringComparison.Ordinal)), Is.GreaterThan(0),
                "control: the scan finds the scene's moored boats");
            foreach (var (who, at) in movers.OrderBy(m => GapTo(band, m.at)).Take(5))
                TestContext.WriteLine($"{Shown(GapTo(band, at))} m: {who} at {At2(at)}");
            foreach (var (who, at) in movers)
                Assert.That(GapTo(band, at), Is.GreaterThanOrEqualTo(FarFromTheDoor),
                    $"{who} at {At2(at)} stands {Shown(GapTo(band, at))} m from the crossing door");

            // The machines that drive themselves: every scheduled trip in the scene, and the trips the builder hangs.
            string tripGuid = GuidOf(ScheduledTripSource);
            var routes = new List<(string who, Vector2[] route)>();
            foreach (Doc trip in Scene.Docs.Where(d => d.ClassId == MonoBehaviourClass && ScriptGuid(d) == tripGuid))
                foreach (string leg in new[] { "_outbound", "_return" })
                    routes.Add(($"{Name(GameObjectOf(trip))} {leg}", Points(trip, leg)));
            routes.Add(("the buyer's run out", NineMileCreekTrips.OutboundRoute()));
            routes.Add(("the buyer's run home", NineMileCreekTrips.ReturnRoute()));
            routes.Add(("the chandler's run out", NineMileCreekTrips.ToTheForecourt(NineMileCreekTrips.ThroughRoad, NineMileCreekTrips.ChandleryPost())));
            routes.Add(("the chandler's run home", NineMileCreekTrips.FromTheForecourt(NineMileCreekTrips.ThroughRoad, NineMileCreekTrips.ChandleryPost())));
            routes.Add(("the outboard man's run out", NineMileCreekTrips.ToTheForecourt(NineMileCreekTrips.WharfRoad, NineMileCreekTrips.DoryYardPost())));
            routes.Add(("the outboard man's run home", NineMileCreekTrips.FromTheForecourt(NineMileCreekTrips.WharfRoad, NineMileCreekTrips.DoryYardPost())));
            foreach (var (who, route) in routes)
            {
                Assert.That(route.Length, Is.GreaterThan(1), $"{who} is a route");
                float nearest = float.MaxValue;
                for (int i = 1; i < route.Length; i++)
                    nearest = Mathf.Min(nearest, LegGap(band, route[i - 1], route[i]));
                TestContext.WriteLine($"{who}: {route.Length} points, nearest {Shown(nearest)} m from the door");
                Assert.That(nearest, Is.GreaterThanOrEqualTo(FarFromTheDoor), $"{who} passes {Shown(nearest)} m from the crossing door");
            }
        }

        [Test]
        public void AGullCannotFireADoor_ItCarriesNeitherAColliderNorABody()
        {
            // Gulls belong to places (#957), and the creek's breakwater and wharf birds fly their away lines across
            // this door, as they cross the 6 x 40 m door it replaced. What keeps a gull from firing it is what it is
            // made of: a picture and its shadow, with nothing a trigger can feel. This scan reads the code; the
            // PlayMode case in NineMileCreekCrossingDoorPlayTests builds a flock and looks at every object it made.
            var physical = new Regex(@"\b(\w*Collider2D|Collider|Rigidbody2D|Rigidbody|Physics2D)\b");
            var added = new Regex(@"AddComponent\s*<\s*(\w+)\s*>|typeof\s*\(\s*(\w+)\s*\)");
            Assert.That(physical.IsMatch("var c = go.AddComponent<CircleCollider2D>();"), Is.True, "control: the scan sees a collider");
            Assert.That(physical.IsMatch("var b = go.AddComponent<Rigidbody2D>();"), Is.True, "control: the scan sees a body");

            var allowed = new[] { "GullFlock", "SpriteRenderer", "SpriteShadow" };
            foreach (string file in new[] { "Art/GullFlock.cs", "Art/SpriteShadow.cs", "World/GullPerch.cs" })
            {
                string code = CodeOf(file);
                Assert.That(physical.IsMatch(code), Is.False, $"{file} names a collider or a body: a gull that carries one can fire a door");
                string[] types = added.Matches(code).Cast<Match>()
                                      .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct().ToArray();
                TestContext.WriteLine($"{file} adds or requires: {string.Join(", ", types)}");
                Assert.That(types, Is.SubsetOf(allowed), $"{file} adds a component a gull does not need");
            }
            Assert.That(added.Matches(CodeOf("Art/GullFlock.cs")).Cast<Match>().Select(m => m.Groups[1].Value),
                Has.Member("SpriteRenderer"), "control: the scan reads the flock's own AddComponent calls");
        }

        // ---- helpers ---------------------------------------------------------------------------------------------

        static string ScriptGuid(Doc behaviour)
        {
            Match m = Regex.Match(behaviour.Field("m_Script") ?? "", @"guid: ([0-9a-f]{32})");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>A serialized Vector2 array (<c>- {x: …, y: …}</c> rows; <c>[]</c> is the empty one).</summary>
        static Vector2[] Points(Doc doc, string key)
        {
            Assert.That(doc.Field(key), Is.Not.Null, $"&{doc.FileId} has no {key}");
            string[] lines = doc.Lines;
            int at = Array.IndexOf(lines, "  " + key + ":");
            var points = new List<Vector2>();
            for (int i = at + 1; at >= 0 && i < lines.Length && lines[i].StartsWith("  - ", StringComparison.Ordinal); i++)
                points.Add(Vector(lines[i].Substring(4)));
            return points.ToArray();
        }

        /// <summary>The nearest a straight leg from <paramref name="a"/> to <paramref name="b"/> comes to a band.</summary>
        static float LegGap(Rect band, Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
            float nearest = float.MaxValue;
            for (int i = 0; i <= steps; i++)
                nearest = Mathf.Min(nearest, GapTo(band, Vector2.Lerp(a, b, i / (float)steps)));
            return nearest;
        }

        /// <summary>Every mover the creek's scene holds, with where it stands: each Rigidbody2D (the one thing that lets
        /// a collider fire a trigger) and each moored boat, on the scene's own GameObjects or inside a prefab instance.</summary>
        static List<(string who, Vector2 at)> Movers()
        {
            string moored = GuidOf(MooredBoatSource);
            var found = new List<(string, Vector2)>();
            foreach (Doc d in Scene.Docs)
            {
                if (d.Stripped) continue;
                if (d.ClassId == Rigidbody2DClass)
                    found.Add(($"Rigidbody2D on {PathOf(GameObjectOf(d))}", ToWorld(GameObjectOf(d), Vector2.zero)));
                else if (d.ClassId == MonoBehaviourClass && ScriptGuid(d) == moored)
                    found.Add(($"MooredBoat {PathOf(GameObjectOf(d))}", ToWorld(GameObjectOf(d), Vector2.zero)));
                else if (d.ClassId == PrefabInstanceClass)
                {
                    string guid = RefGuid(d.Field("m_SourcePrefab"));
                    string held = MoverInPrefab(guid, moored, 0);
                    if (held != null) found.Add(($"{held} in prefab instance &{d.FileId}", InstanceRoot(d, guid)));
                }
            }
            return found;
        }

        static string PathOf(Doc go)
        {
            var names = new List<string>();
            for (Doc tr = Scene.TransformOf(go); tr != null;)
            {
                if (tr.Stripped) { names.Add("(a prefab instance)"); break; }
                names.Add(Name(Scene.Require(tr.FieldRef("m_GameObject"), "a GameObject")));
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : Scene.Require(father, "a parent transform");
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static Dictionary<string, string> _prefabPaths;

        static SceneYaml PrefabFile(string guid)
        {
            // Unity skips a folder whose name starts with a dot or ends in a tilde, and so does this map.
            _prefabPaths ??= Directory.EnumerateFiles("Assets", "*.prefab.meta", SearchOption.AllDirectories)
                .Where(meta => !meta.Split('/', '\\').Any(part => part.StartsWith(".", StringComparison.Ordinal) ||
                                                                  part.EndsWith("~", StringComparison.Ordinal)))
                .Select(meta => (meta, m: Regex.Match(File.ReadAllText(meta), @"(?m)^guid: ([0-9a-f]{32})\r?$")))
                .Where(x => x.m.Success)
                .ToDictionary(x => x.m.Groups[1].Value, x => x.meta.Substring(0, x.meta.Length - ".meta".Length));
            if (!_prefabPaths.TryGetValue(guid, out string path)) throw new FileNotFoundException("no prefab has the guid " + guid);
            return SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
        }

        /// <summary>What in the prefab can move, or null: a Rigidbody2D or a moored boat, in it or in any prefab it nests.</summary>
        static string MoverInPrefab(string guid, string moored, int depth)
        {
            if (depth > 8) throw new InvalidOperationException("prefabs nest deeper than eight at " + guid);
            foreach (Doc d in PrefabFile(guid).Docs)
            {
                if (d.Stripped) continue;
                if (d.ClassId == Rigidbody2DClass) return "Rigidbody2D";
                if (d.ClassId == MonoBehaviourClass && ScriptGuid(d) == moored) return "MooredBoat";
                if (d.ClassId == PrefabInstanceClass && MoverInPrefab(RefGuid(d.Field("m_SourcePrefab")), moored, depth + 1) is string inner)
                    return inner;
            }
            return null;
        }

        /// <summary>Where a prefab instance's root stands: the prefab root's own position under the instance's
        /// overrides, carried out through the scene parent it hangs from.</summary>
        static Vector2 InstanceRoot(Doc instance, string guid)
        {
            Doc root = PrefabFile(guid).Docs.Single(d => !d.Stripped && (d.ClassId == TransformClass || d.ClassId == RectTransformClass) &&
                                                         d.FieldRef("m_Father") == 0);
            Vector3 p = ParseVec3(root.Field("m_LocalPosition"));
            string target = "    - target: {fileID: " + root.FileId + ", guid: " + guid + ",";
            string[] lines = instance.Lines;
            for (int i = 0; i + 2 < lines.Length; i++)
            {
                if (!lines[i].StartsWith(target, StringComparison.Ordinal)) continue;
                string path = lines[i + 1].Trim(), value = lines[i + 2].Trim();
                if (!value.StartsWith("value: ", StringComparison.Ordinal)) continue;
                if (path == "propertyPath: m_LocalPosition.x") p.x = ParseFloat(value.Substring("value: ".Length));
                else if (path == "propertyPath: m_LocalPosition.y") p.y = ParseFloat(value.Substring("value: ".Length));
            }
            const string parentKey = "    m_TransformParent: ";
            long parent = ParseRef(lines.Single(l => l.StartsWith(parentKey, StringComparison.Ordinal)).Substring(parentKey.Length));
            if (parent == 0) return p;
            Doc parentTransform = Scene.Require(parent, "an instance's parent transform");
            if (parentTransform.Stripped)
                throw new NotSupportedException($"&{instance.FileId} hangs from another prefab instance, which this test cannot place.");
            return ToWorld(Scene.Require(parentTransform.FieldRef("m_GameObject"), "a parent GameObject"), p);
        }

        /// <summary>One source file under Code/ with its comments removed, so a scan reads what the compiler reads.</summary>
        static string CodeOf(string file)
        {
            string path = Path.Combine("Assets", "_Project", "Code", file);
            Assert.That(File.Exists(path), Is.True, "missing source: " + path);
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"//[^\r\n]*", " ");
            Assert.That(text.Trim().Length, Is.GreaterThan(200), file + ": nothing survived the comment strip");
            return text;
        }
    }
}
