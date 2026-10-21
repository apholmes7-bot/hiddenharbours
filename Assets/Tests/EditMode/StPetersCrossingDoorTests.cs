using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>ST PETERS' DOOR TO THE CREEK, AS BIG AS THE OWNER ASKED</b> (ruling of 2026-10-17: a larger door each
    /// way, and no change to the water). The walk door past the bar's west tip grows from 3 × 60 m, the bar's own
    /// width, to 6 × 120 m, and it asks the creek for its <c>bar</c> landing. The island gains a <c>bar</c> landing
    /// of its own on the crest, which the creek's door asks for, so a walker arriving either way lands on the far
    /// bar. The bar still floods at high water: the tests that hold that gate stand as they are.
    ///
    /// <para>St Peters is not rebuilt, so its scene is patched by hand, and these tests read the patched scene as
    /// text. Four claims. The scene's door, key and landing are the builder's numbers, and the anchor keeps the
    /// rows it had. The landing stands on the crest, outside the door it arrives by, and bares when the bar does.
    /// And nothing else that moves reaches the bigger door: a door fires for any collider with a body behind it,
    /// so every body on the island is held clear of it, the boat that brings the player in runs the island's far
    /// side, the fleet keeps clear of the band and carries no body, and neither a gull nor a villager carries a
    /// collider or a body.</para>
    /// </summary>
    public class StPetersCrossingDoorTests
    {
        const int TransformClass = 4, RectTransformClass = 224, MonoBehaviourClass = 114, Rigidbody2DClass = 50,
                  CircleCollider2DClass = 58, BoxCollider2DClass = 61, PrefabInstanceClass = 1001,
                  SceneRootsClass = 1660057539;

        /// <summary>The body and every 2D collider: what a trigger can feel.</summary>
        static readonly int[] PhysicsClasses = { Rigidbody2DClass, 58, 60, 61, 66, 68, 70 };

        const string DoorName = "PassageToNineMileCreek", AnchorName = "StPetersRegionAnchor",
                     LandingName = "StPetersBarLanding", EastWaterArrivalName = "StPetersEastWaterArrival";
        const string CreekRegionAsset = "Assets/_Project/Data/Regions/NineMileCreek.asset";
        const string FleetLibraryAsset = "Assets/_Project/Resources/AmbientFleetLibrary.asset";
        const string MooredBoatSource = "Assets/_Project/Code/Boats/MooredBoat.cs";
        const string PlayerSource = "Assets/_Project/Code/Player/PlayerWalkController.cs";
        const string ScheduledTripSource = "Assets/_Project/Code/Vehicles/ScheduledTrip.cs";
        const string ParkedVehicleSource = "Assets/_Project/Code/Vehicles/ParkedVehicle.cs";
        const string ArrivalOpeningSource = "Assets/_Project/Code/App/ArrivalOpening.cs";
        const string VillagerSource = "Assets/_Project/Code/World/Routines/VillagerRoutine.cs";
        const string GullPerchSource = "Assets/_Project/Code/World/GullPerch.cs";

        /// <summary>The door's re-fire guard, which the owner's ruling keeps.</summary>
        const float GuardSeconds = 1.5f;

        /// <summary>How far every other mover must stand from the door's band. Nothing on the island comes near
        /// it: the nearest is a rideable machine 350 m away.</summary>
        const float FarFromTheDoor = 100f;

        static SceneYaml _scene;

        /// <summary>StPeters.unity as committed, its documents parsed once per domain.</summary>
        static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));

        static Rect Band(Vector3 centre, Vector2 size) => new Rect((Vector2)centre - size * 0.5f, size);
        static Rect TheDoor => Band(StPetersBuilder.ToNineMileCreekPassagePos, StPetersBuilder.NineMileCreekPassageBandSize);

        /// <summary>The gap between a point and a band, zero inside it.</summary>
        static float GapTo(Rect band, Vector2 p)
        {
            float dx = Mathf.Max(band.xMin - p.x, p.x - band.xMax, 0f);
            float dy = Mathf.Max(band.yMin - p.y, p.y - band.yMax, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static string Shown(float v) => NineMileCreekLiveGround.Shown(v);
        static string At2(Vector2 v) => $"({Shown(v.x)}, {Shown(v.y)})";
        static string GuidOf(string assetPath) => NineMileCreekLiveGround.GuidOf(assetPath);
        static Vector2 Vector(string yaml) => NineMileCreekLiveGround.Vector(yaml);
        static Vector2 Vector(Doc doc, string key) => Vector(doc.Field(key));

        // ---- the door and the landing, the builder's numbers ------------------------------------------------------

        [Test]
        public void TheDoorInTheScene_IsTheBuildersDoor_SixByAHundredAndTwenty_AskingForTheCreeksBar()
        {
            Doc door = Scene.RootNamed(DoorName);
            Assert.That(Live(door), Is.True, "the crossing door is live");
            Assert.That(Vector2.Distance(WorldPosition(door), StPetersBuilder.ToNineMileCreekPassagePos), Is.LessThan(1e-4f),
                "the door stands where it stood, 6 m past the bar's west tip");

            Doc box = Components(door, BoxCollider2DClass).Single();
            Assert.That(Enabled(box), Is.True, "the band is live");
            Assert.That(box.Field("m_IsTrigger"), Is.EqualTo("1"), "the band is a trigger, not a wall");
            Assert.That(Vector(box, "m_Offset"), Is.EqualTo(Vector2.zero), "the band is centred on the door");
            Assert.That(Vector(box, "m_Size"), Is.EqualTo(StPetersBuilder.NineMileCreekPassageBandSize),
                "the scene's band is the builder's");
            Assert.That(StPetersBuilder.NineMileCreekPassageBandSize, Is.EqualTo(new Vector2(6f, 120f)),
                "the owner's size for the doors between the creek and St Peters: 6 m deep, 120 m across");

            Doc passage = Script(door, "HiddenHarbours.World.RegionPassage");
            Assert.That(Enabled(passage), Is.True, "the passage is live");
            Assert.That(NineMileCreekLiveGround.RefGuid(passage.Field("_target")), Is.EqualTo(GuidOf(CreekRegionAsset)),
                "the door leads to the creek");
            Assert.That(passage.Field("_arrivalKey"), Is.EqualTo(NineMileCreekBuilder.BarArrivalKey),
                "the door asks the creek for its bar landing");
            Assert.That(Number(passage, "_reentryCooldownSeconds"), Is.EqualTo(GuardSeconds), "the re-fire guard is kept");

            // The whole band inside the region: half hanging off the edge, the camera's clamp cannot follow a boat in.
            var region = new Rect(StPetersBuilder.RegionWorldCenter - StPetersBuilder.RegionWorldSize * 0.5f,
                                  StPetersBuilder.RegionWorldSize);
            Rect band = TheDoor;
            TestContext.WriteLine($"the door's band: x {Shown(band.xMin)}..{Shown(band.xMax)}, y {Shown(band.yMin)}..{Shown(band.yMax)}; " +
                                  $"the region: x {Shown(region.xMin)}..{Shown(region.xMax)}, y {Shown(region.yMin)}..{Shown(region.yMax)}");
            Assert.That(region.Contains(band.min) && region.Contains(band.max), Is.True, "the band lies inside the region");
        }

        [Test]
        public void TheAnchorAnswersTheBar_WithItsLanding_AndKeepsTheEastWatersRow()
        {
            Doc anchor = Script(Scene.RootNamed(AnchorName), "HiddenHarbours.App.RegionAnchor");
            Assert.That(anchor.Field("_regionId"), Is.EqualTo("region.st_peters"), "control: the island's own anchor");

            List<(string key, long arrival, long disembark)> rows = Arrivals(anchor);
            TestContext.WriteLine(string.Join("; ", rows.Select(r => $"{r.key}: arrival &{r.arrival}, disembark &{r.disembark}")));
            Assert.That(rows.Select(r => r.key),
                Is.EqualTo(new[] { StPetersBuilder.FromEastWaterArrivalKey, StPetersBuilder.FromNineMileCreekArrivalKey }),
                "the island answers two names: the east water's, as before, and now the bar's");

            var east = rows[0];
            Assert.That(Name(GameObjectAt(east.arrival)), Is.EqualTo(EastWaterArrivalName), "the east row keeps its point");
            Assert.That(Vector2.Distance(WorldPosition(GameObjectAt(east.arrival)), StPetersBuilder.FromEastWaterArrivalPos),
                Is.LessThan(1e-4f), "…where the builder puts it");
            Assert.That(east.disembark, Is.EqualTo(0L), "the east row still names only the boat's point");

            var bar = rows[1];
            Assert.That(bar.arrival, Is.EqualTo(0L),
                "the bar row names no boat's point: a boat that comes this way is laid where a keyless arrival lays her");
            Doc landing = GameObjectAt(bar.disembark);
            Assert.That(Name(landing), Is.EqualTo(LandingName), "the bar row puts the walker down at the bar landing");
            Assert.That(Scene.TransformOf(landing).FieldRef("m_Father"), Is.EqualTo(0L), "the landing is a root of its own");
            Assert.That(Vector2.Distance(WorldPosition(landing), StPetersBuilder.FromNineMileCreekWalkArrivalPos), Is.LessThan(1e-4f),
                "the scene's landing is the builder's");
            Assert.That(StPetersBuilder.FromNineMileCreekArrivalKey, Is.EqualTo(NineMileCreekBuilder.BarArrivalKey),
                "one bar, one word, both ways");
            Assert.That(NineMileCreekBuilder.BarArrivalKey, Is.EqualTo("bar"), "the name the owner approved");

            Doc roots = Scene.Docs.Single(d => d.ClassId == SceneRootsClass);
            Assert.That(roots.FieldRefs("m_Roots"), Has.Member(bar.disembark), "the landing is listed among the scene's roots");

            // The keyless way in is the one it was: the boat's arrival, the dock and the step ashore at the wharf.
            foreach (var (field, at) in new[] { ("_arrivalPoint", StPetersBuilder.ArrivalPos), ("_dockZone", StPetersBuilder.DockZonePos),
                                                ("_disembarkPoint", StPetersBuilder.DisembarkPos) })
                Assert.That(Vector2.Distance(WorldPosition(GameObjectAt(anchor.FieldRef(field))), at), Is.LessThan(1e-4f),
                    $"the anchor's {field} still stands at the builder's {At2(at)}");
        }

        [Test]
        public void TheBarLanding_StandsOnTheCrest_OutsideTheDoorItArrivesBy()
        {
            Doc landing = Scene.RootNamed(LandingName);
            Vector2 at = WorldPosition(landing);
            Assert.That(Vector2.Distance(at, StPetersBuilder.FromNineMileCreekWalkArrivalPos), Is.LessThan(1e-4f),
                "the scene's landing is the builder's");

            Vector2 from = StPetersBuilder.SandbarFrom, to = StPetersBuilder.SandbarTo;
            float offAxis = NineMileCreekLiveGround.DistanceToSegment(at, from, to);
            float alongPastTheTip = Vector2.Dot(at - to, (to - from).normalized);
            Assert.That(offAxis, Is.LessThan(0.05f), "the landing stands on the bar's crest");
            Assert.That(alongPastTheTip, Is.LessThan(0f), "on the island's side of the tip");

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

        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void TheLandingBaresAtLowWater_FloodsAtHigh_AndStandsNoLowerThanTheDoor()
        {
            var go = new GameObject("TidalTerrain_CrossingDoorTest");
            _spawned.Add(go);
            var terrain = go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);

            float low = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            float high = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
            float landing = terrain.ElevationAt(StPetersBuilder.FromNineMileCreekWalkArrivalPos);
            float door = terrain.ElevationAt(StPetersBuilder.ToNineMileCreekPassagePos);
            TestContext.WriteLine($"the landing stands at {Shown(landing)} m, the door at {Shown(door)} m; " +
                                  $"spring low {Shown(low)} m, spring high {Shown(high)} m");
            Assert.That(TidalExposure.IsExposed(low, landing), Is.True,
                "the landing bares at spring low: a walker who crossed the bared bar is put down on sand");
            Assert.That(TidalExposure.IsExposed(high, landing), Is.False,
                "…and floods at spring high, as the bar does: the gate stands");
            Assert.That(landing, Is.GreaterThanOrEqualTo(door), "the landing stands no lower than the door the walker came in by");
        }

        // ---- nothing else that moves reaches the bigger door -----------------------------------------------------

        [Test]
        public void NothingThatMovesStandsWithinAHundredMetresOfTheDoor()
        {
            Rect band = TheDoor;
            List<(string who, Vector2 at)> movers = Movers(out int instances);
            Assert.That(movers.Count(m => m.who.StartsWith("Rigidbody2D", StringComparison.Ordinal)), Is.GreaterThan(0),
                "control: the scan finds the scene's bodies (the player has one)");
            Assert.That(movers.Count(m => m.who.StartsWith("ParkedVehicle", StringComparison.Ordinal)), Is.GreaterThan(0),
                "control: the scan finds the island's machines, which take a body when they wake");
            Assert.That(instances, Is.GreaterThan(0), "control: the scan reads the scene's prefab instances");
            TestContext.WriteLine($"{movers.Count} movers; {instances} prefab instances read, " +
                                  $"{movers.Count(m => m.who.Contains("prefab instance"))} of them hold one");
            foreach (var (who, at) in movers.OrderBy(m => GapTo(band, m.at)).Take(6))
                TestContext.WriteLine($"{Shown(GapTo(band, at))} m: {who} at {At2(at)}");
            foreach (var (who, at) in movers)
                Assert.That(GapTo(band, at), Is.GreaterThanOrEqualTo(FarFromTheDoor),
                    $"{who} at {At2(at)} stands {Shown(GapTo(band, at))} m from the crossing door");

            // Nothing on the island drives itself but the boat that brings the player in.
            Assert.That(Behaviours(MooredBoatSource), Is.Empty,
                "the island moors a boat of its own: her swing must now be held clear of the crossing door");
            Assert.That(Behaviours(ScheduledTripSource), Is.Empty,
                "a scheduled trip runs on the island: its route must now be held clear of the crossing door");
            List<Doc> openings = Behaviours(ArrivalOpeningSource);
            Assert.That(openings, Has.Count.EqualTo(1), "control: the island has one arrival opening, the boat that brings the player in");
            foreach (Doc opening in openings)
            {
                Vector2[] route = Points(opening, "_route");
                Assert.That(route.Length, Is.GreaterThan(1), "the opening's route is a route");
                float nearest = GapTo(band, Vector(opening, "_berth"));
                for (int i = 1; i < route.Length; i++)
                    nearest = Mathf.Min(nearest, LegGap(band, route[i - 1], route[i]));
                TestContext.WriteLine($"the arrival boat's run: {route.Length} marks, {At2(route[0])} to {At2(route[route.Length - 1])}, " +
                                      $"nearest {Shown(nearest)} m from the door");
                Assert.That(nearest, Is.GreaterThanOrEqualTo(FarFromTheDoor),
                    $"the arrival boat's run passes {Shown(nearest)} m from the crossing door");
            }
        }

        [Test]
        public void TheFleetKeepsClearOfTheBiggerDoor_AndCarriesNoBodyToFireIt()
        {
            // Every fleet the library sails on this island, and the band each keeps clear. The spots and legs it
            // plans are held clear of that list by AmbientFleetGroundsTests.
            string island = Path.GetFileNameWithoutExtension(StPetersLayerRefresh.ScenePath);
            string[] fleets = RowsOf(File.ReadAllText(FleetLibraryAsset), "Fleets").Select(NineMileCreekLiveGround.RefGuid).ToArray();
            Assert.That(fleets, Is.Not.Empty, "control: the library holds St Peters' fleet");
            int here = 0;
            foreach (string guid in fleets)
            {
                string text = File.ReadAllText(AssetWithGuid(guid, "*.asset.meta")).Replace("\r\n", "\n");
                string sails = Regex.Match(text, @"(?m)^  RegionSceneName: ?(.*)$").Groups[1].Value.Trim();
                TestContext.WriteLine($"fleet {guid} sails '{sails}'");
                if (sails.Length > 0 && !string.Equals(sails, island, StringComparison.OrdinalIgnoreCase)) continue;
                here++;
                Rect kept = KeptBox(text, "Nine Mile Creek band");
                Assert.That(Vector2.Distance(kept.min, TheDoor.min) + Vector2.Distance(kept.max, TheDoor.max), Is.LessThan(1e-4f),
                    $"fleet {guid} keeps clear of x {Shown(kept.xMin)}..{Shown(kept.xMax)}, y {Shown(kept.yMin)}..{Shown(kept.yMax)}, " +
                    "not the door's band");
            }
            Assert.That(here, Is.GreaterThan(0), "control: a fleet sails St Peters");

            // And what a fleet boat is made of: a picture, its wave and its buoys, with nothing a trigger can feel.
            AssertNothingPhysical(new[] { "Boats/AmbientFleetPresenter.cs", "Boats/BoatWaveMotion.cs", "Boats/BuoyWaveVisual.cs" },
                                  new[] { "AmbientFleetPresenter", "SpriteRenderer", "BoatWaveMotion", "BuoyWaveVisual" });
            Assert.That(Added(CodeOf("Boats/AmbientFleetPresenter.cs")), Has.Member("SpriteRenderer"),
                "control: the scan reads the presenter's own AddComponent calls");
        }

        [Test]
        public void NeitherAGullNorAVillagerCarriesAColliderOrABody()
        {
            // In the scene: every GameObject that holds a villager's routine or a gull's perch, and all below it.
            List<Doc> villagers = Behaviours(VillagerSource), perches = Behaviours(GullPerchSource);
            Assert.That(villagers, Is.Not.Empty, "control: the scan finds the island's villagers");
            Assert.That(perches, Is.Not.Empty, "control: the scan finds the island's gull perches");
            foreach (Doc holder in villagers.Concat(perches))
                foreach (Doc go in SelfAndBelow(GameObjectOf(holder)))
                {
                    int[] physical = Scene.ComponentsOf(go).Select(c => c.ClassId).Where(id => PhysicsClasses.Contains(id)).ToArray();
                    Assert.That(physical, Is.Empty, $"{PathOf(go)} carries a collider or a body (class {string.Join(", ", physical)})");
                }

            // The perched gulls sit at the cannery and the landing stage, the island's length from the door.
            foreach (Doc p in perches)
            {
                Vector2 ground = Vector(p, "_groundPoint");
                TestContext.WriteLine($"{p.Field("_id")} perches at {At2(ground)}, {Shown(GapTo(TheDoor, ground))} m from the door");
                Assert.That(GapTo(TheDoor, ground), Is.GreaterThanOrEqualTo(FarFromTheDoor), $"{p.Field("_id")} perches by the crossing door");
            }

            // Gulls belong to places (#957), and no line the island's birds fly enters this door: the nearest, the
            // crossing mussels' away line, passes 11 m clear of its band. What keeps a gull from firing a door is what
            // it is made of: a picture and its shadow, with nothing a trigger can feel. The creek's
            // NineMileCreekCrossingDoorPlayTests builds a flock and looks at every object it made.
            AssertNothingPhysical(new[] { "Art/GullFlock.cs", "Art/SpriteShadow.cs", "World/GullPerch.cs" },
                                  new[] { "GullFlock", "SpriteRenderer", "SpriteShadow" });
            AssertNothingPhysical(new[] { "World/Routines/VillagerRoutine.cs", "World/Routines/RoutineLanes.cs" }, new string[0]);
            Assert.That(Added(CodeOf("Art/GullFlock.cs")), Has.Member("SpriteRenderer"),
                "control: the scan reads the flock's own AddComponent calls");
        }

        // ---- helpers: the scene ----------------------------------------------------------------------------------

        static Doc GameObjectOf(Doc component) => Scene.Require(component.FieldRef("m_GameObject"), "a component's GameObject");

        /// <summary>The GameObject of the transform a reference names.</summary>
        static Doc GameObjectAt(long transform) =>
            GameObjectOf(Scene.Require(transform, "a referenced transform"));

        static Doc Script(Doc go, string script) => Scene.ComponentsOf(go).Single(d => d.IsScript(script));
        static List<Doc> Components(Doc go, int classId) => Scene.ComponentsOf(go).Where(d => d.ClassId == classId).ToList();
        static string Name(Doc go) => go.Field("m_Name");
        static bool Active(Doc go) => go.Field("m_IsActive") == "1";
        static bool Enabled(Doc component) => component.Field("m_Enabled") == "1";
        static float Number(Doc doc, string key) => ParseFloat(doc.Field(key));

        static string ScriptGuid(Doc behaviour)
        {
            Match m = Regex.Match(behaviour.Field("m_Script") ?? "", @"guid: ([0-9a-f]{32})");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Every behaviour in the scene running the script at <paramref name="source"/>.</summary>
        static List<Doc> Behaviours(string source)
        {
            string guid = GuidOf(source);
            return Scene.Docs.Where(d => !d.Stripped && d.ClassId == MonoBehaviourClass && ScriptGuid(d) == guid).ToList();
        }

        /// <summary>Whether <paramref name="go"/> is active in the hierarchy: it and every GameObject above it.</summary>
        static bool Live(Doc go)
        {
            for (Doc tr = Scene.TransformOf(go); ;)
            {
                if (!Active(Scene.Require(tr.FieldRef("m_GameObject"), "a GameObject"))) return false;
                long father = tr.FieldRef("m_Father");
                if (father == 0) return true;
                tr = Scene.Require(father, "a parent transform");
            }
        }

        static readonly Regex QuatRx = new Regex(@"^\{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^,}]+)\}$",
                                                 RegexOptions.CultureInvariant);

        static Quaternion Turn(Doc transform)
        {
            Match m = QuatRx.Match(transform.Field("m_LocalRotation") ?? "");
            if (!m.Success) throw new FormatException($"&{transform.FileId} has no readable rotation.");
            return new Quaternion(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value),
                                  ParseFloat(m.Groups[3].Value), ParseFloat(m.Groups[4].Value));
        }

        /// <summary>A point in <paramref name="go"/>'s own space, carried out to the world through every transform
        /// above it (scale, then turn, then place).</summary>
        static Vector2 ToWorld(Doc go, Vector2 local) => Up(Scene.TransformOf(go), local);

        static Vector3 Up(Doc transform, Vector3 p)
        {
            for (Doc tr = transform; tr != null;)
            {
                if (tr.Stripped) throw new NotSupportedException($"&{tr.FileId} is a prefab instance's, which this test cannot place.");
                p = Turn(tr) * Vector3.Scale(p, ParseVec3(tr.Field("m_LocalScale"))) + ParseVec3(tr.Field("m_LocalPosition"));
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : Scene.Require(father, "a parent transform");
            }
            return p;
        }

        static Vector2 WorldPosition(Doc go) => ToWorld(go, Vector2.zero);

        /// <summary>The anchor's named ways in, as the scene lists them: each row's key and the transforms it names.</summary>
        static List<(string key, long arrival, long disembark)> Arrivals(Doc anchor)
        {
            string[] lines = anchor.Lines;
            int at = Array.IndexOf(lines, "  _arrivals:");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "the anchor lists its named ways in");
            var rows = new List<(string, long, long)>();
            for (int i = at + 1; i + 2 < lines.Length && lines[i].StartsWith("  - Key: ", StringComparison.Ordinal); i += 3)
            {
                Assert.That(lines[i + 1], Does.StartWith("    ArrivalPoint: "), $"the row at line {i - at} of the list");
                Assert.That(lines[i + 2], Does.StartWith("    DisembarkPoint: "), $"the row at line {i - at} of the list");
                rows.Add((lines[i].Substring("  - Key: ".Length), ParseRef(lines[i + 1]), ParseRef(lines[i + 2])));
            }
            return rows;
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

        /// <summary>Every mover the island's scene holds, with where it stands: each Rigidbody2D (the one thing that
        /// lets a collider fire a trigger), each parked machine (which takes a body when it wakes) and each moored
        /// boat, on the scene's own GameObjects or inside a prefab instance.</summary>
        static List<(string who, Vector2 at)> Movers(out int instances)
        {
            string moored = GuidOf(MooredBoatSource), parked = GuidOf(ParkedVehicleSource);
            var found = new List<(string, Vector2)>();
            instances = 0;
            foreach (Doc d in Scene.Docs)
            {
                if (d.Stripped) continue;
                string script = d.ClassId == MonoBehaviourClass ? ScriptGuid(d) : null;
                if (d.ClassId == Rigidbody2DClass)
                    found.Add(($"Rigidbody2D on {PathOf(GameObjectOf(d))}", WorldPosition(GameObjectOf(d))));
                else if (script != null && (script == moored || script == parked))
                    found.Add(($"{(script == moored ? "MooredBoat" : "ParkedVehicle")} {PathOf(GameObjectOf(d))}",
                               WorldPosition(GameObjectOf(d))));
                else if (d.ClassId == PrefabInstanceClass)
                {
                    instances++;
                    string guid = NineMileCreekLiveGround.RefGuid(d.Field("m_SourcePrefab"));
                    string held = MoverInPrefab(guid, moored, parked, 0);
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

        /// <summary><paramref name="go"/> and every GameObject below it (a nested prefab instance's are its own).</summary>
        static IEnumerable<Doc> SelfAndBelow(Doc go)
        {
            yield return go;
            foreach (long child in Scene.TransformOf(go).FieldRefs("m_Children"))
            {
                Doc tr = Scene.Require(child, "a child transform");
                if (tr.Stripped) continue;
                foreach (Doc below in SelfAndBelow(Scene.Require(tr.FieldRef("m_GameObject"), "a child GameObject")))
                    yield return below;
            }
        }

        // ---- helpers: prefabs and assets -------------------------------------------------------------------------

        static readonly Dictionary<string, Dictionary<string, string>> _byGuid = new();

        /// <summary>The asset whose <c>.meta</c> (matching <paramref name="metaPattern"/>) gives it <paramref name="guid"/>.</summary>
        static string AssetWithGuid(string guid, string metaPattern)
        {
            if (!_byGuid.TryGetValue(metaPattern, out var map))
                // Unity skips a folder whose name starts with a dot or ends in a tilde, and so does this map.
                _byGuid[metaPattern] = map = Directory.EnumerateFiles("Assets", metaPattern, SearchOption.AllDirectories)
                    .Where(meta => !meta.Split('/', '\\').Any(part => part.StartsWith(".", StringComparison.Ordinal) ||
                                                                      part.EndsWith("~", StringComparison.Ordinal)))
                    .Select(meta => (meta, m: Regex.Match(File.ReadAllText(meta), @"(?m)^guid: ([0-9a-f]{32})\r?$")))
                    .Where(x => x.m.Success)
                    .ToDictionary(x => x.m.Groups[1].Value, x => x.meta.Substring(0, x.meta.Length - ".meta".Length));
            if (!map.TryGetValue(guid, out string path)) throw new FileNotFoundException($"no {metaPattern} gives the guid {guid}");
            return path;
        }

        static SceneYaml PrefabFile(string guid) =>
            SceneYaml.Parse(File.ReadAllText(AssetWithGuid(guid, "*.prefab.meta")).Replace("\r\n", "\n"));

        /// <summary>What in the prefab can move, or null: a Rigidbody2D, a parked machine or a moored boat, in it or in
        /// any prefab it nests.</summary>
        static string MoverInPrefab(string guid, string moored, string parked, int depth)
        {
            if (depth > 8) throw new InvalidOperationException("prefabs nest deeper than eight at " + guid);
            foreach (Doc d in PrefabFile(guid).Docs)
            {
                if (d.Stripped) continue;
                if (d.ClassId == Rigidbody2DClass) return "Rigidbody2D";
                if (d.ClassId == MonoBehaviourClass && ScriptGuid(d) == moored) return "MooredBoat";
                if (d.ClassId == MonoBehaviourClass && ScriptGuid(d) == parked) return "ParkedVehicle";
                if (d.ClassId == PrefabInstanceClass &&
                    MoverInPrefab(NineMileCreekLiveGround.RefGuid(d.Field("m_SourcePrefab")), moored, parked, depth + 1) is string inner)
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

        /// <summary>The rows of a two-space list field in an asset's text (<c>- {fileID: …}</c> references).</summary>
        static List<string> RowsOf(string text, string key)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int at = Array.IndexOf(lines, "  " + key + ":");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"the asset has no {key} list");
            var rows = new List<string>();
            for (int i = at + 1; i < lines.Length && lines[i].StartsWith("  - ", StringComparison.Ordinal); i++)
                rows.Add(lines[i].Substring(4));
            return rows;
        }

        /// <summary>The keep-clear box a fleet's asset lists under <paramref name="name"/>.</summary>
        static Rect KeptBox(string fleetText, string name)
        {
            string[] lines = fleetText.Split('\n');
            int at = Array.IndexOf(lines, "  - Name: " + name);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"the fleet keeps no '{name}' clear");
            Assert.That(lines[at + 2], Is.EqualTo("    Shape: 1"), $"'{name}' is kept clear as a box");
            Assert.That(lines[at + 3], Does.StartWith("    A: "), $"'{name}' names its first corner");
            Assert.That(lines[at + 4], Does.StartWith("    B: "), $"'{name}' names its second corner");
            Vector2 a = Vector(lines[at + 3].Substring("    A: ".Length)), b = Vector(lines[at + 4].Substring("    B: ".Length));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ---- helpers: the code -----------------------------------------------------------------------------------

        static readonly Regex PhysicalRx = new Regex(@"\b(\w*Collider2D|Collider|Rigidbody2D|Rigidbody|Physics2D)\b");
        static readonly Regex AddedRx = new Regex(@"AddComponent\s*<\s*(\w+)\s*>|typeof\s*\(\s*(\w+)\s*\)");

        static string[] Added(string code) =>
            AddedRx.Matches(code).Cast<Match>().Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                   .Distinct().ToArray();

        /// <summary>Each file names no collider or body, and adds or requires nothing beyond <paramref name="allowed"/>.</summary>
        static void AssertNothingPhysical(string[] files, string[] allowed)
        {
            Assert.That(PhysicalRx.IsMatch("var c = go.AddComponent<CircleCollider2D>();"), Is.True, "control: the scan sees a collider");
            Assert.That(PhysicalRx.IsMatch("var b = go.AddComponent<Rigidbody2D>();"), Is.True, "control: the scan sees a body");
            foreach (string file in files)
            {
                string code = CodeOf(file);
                Assert.That(PhysicalRx.IsMatch(code), Is.False, $"{file} names a collider or a body: what carries one can fire a door");
                string[] types = Added(code);
                TestContext.WriteLine($"{file} adds or requires: {(types.Length == 0 ? "nothing" : string.Join(", ", types))}");
                Assert.That(types, Is.SubsetOf(allowed), $"{file} adds a component it did not before");
            }
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
