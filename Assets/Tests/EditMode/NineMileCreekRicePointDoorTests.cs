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
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using Doors = HiddenHarbours.App.Editor.NineMileCreekRicePointDoors;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE CREEK'S HALVES OF RICE POINT'S SEA AND STRAND DOORS</b> (NR1a). Rice Point (#956) lies west of the
    /// creek and authored its halves in its plan; these are the creek's: a sea door that takes a boat, rowed or
    /// sailed, and a strand door that takes only a walker, each mirrored across the seam from Rice Point's, each
    /// asking Rice Point for the way in its plan names, and the two ways in Rice Point's doors ask the creek for.
    /// Rice Point's third door, Route 91, stays shut until NR1b opens both its halves together.
    ///
    /// <para>Text first, with no import and no engine: the halves against Rice Point's plan; every way in beyond
    /// every band on its side; the strand's on-foot policy; Route 91's closure; step 12, the bake's text patch,
    /// adding exactly its documents and editing exactly four; no def, no doors; the crossing card's name; the save's
    /// region id. Then in the editor: the builder's door section placing what step 12 writes; a truck, an ATV and a
    /// boat refused at the strand by the game's own switcher; the shut band; the loader's list with and without the
    /// def.</para>
    /// </summary>
    public class NineMileCreekRicePointDoorTests
    {
        const int GameObjectClass = 1, CircleCollider2DClass = 58, BoxCollider2DClass = 61, MonoBehaviourClass = 114,
                  SceneRootsClass = 1660057539;
        const string PlanPath = "Assets/_Project/Data/Terrain/RicePointGround/RicePointPlan.asset";
        const string RegionsFolder = "Assets/_Project/Data/Regions/";
        const string CreekRegionPath = RegionsFolder + "NineMileCreek.asset";
        const string RicePointScenePath = "Assets/_Project/Scenes/RicePoint.unity";
        const string PlayerSource = "Assets/_Project/Code/Player/PlayerWalkController.cs";
        const string OverlaySource = "Assets/_Project/Code/UI/RegionFadeOverlay.cs";
        const string AnchorSource = "Assets/_Project/Code/App/RegionAnchor.cs";
        const string RestSaveSource = "Assets/_Project/Code/Core/Save/RestSaveResponder.cs";
        const string SeaId = "door.rpt_sea", StrandId = "door.rpt_strand", Route91Id = "door.rpt_route_91";
        const string RicePointId = "region.rice_point";

        /// <summary>The hulls that cross by the sea door: the dory rowed, the sloop sailed, the Cape Islander motored.</summary>
        static readonly string[] HullsThatCross = { "Dory", "Sloop30", "CapeIslander" };

        // ---- the halves, against Rice Point's plan ----------------------------------------------------------------

        [Test]
        public void TheCreeksHalves_MirrorRicePointsAcrossTheSeam_SizedKeyedAndOnFootAsItsPlanAsks()
        {
            Rect creek = RegionRect(CreekRegionPath), ricePoint = RegionRect(Doors.RegionAssetPath);
            float seam = creek.xMin;
            Assert.That(ricePoint.xMax - NineMileCreekMainland.RicePointFrameOffset.x, Is.EqualTo(seam).Within(1e-4f),
                "premise: Rice Point's east edge, carried into the creek's frame, is the creek's west edge, the seam both halves meet on");
            List<PlanDoor> theirs = PlanDoors();
            string creekGuid = GuidOf(CreekRegionPath);
            Assert.That(theirs.Where(d => d.TargetGuid != creekGuid).Select(d => d.Id), Is.Empty, "every Rice Point door leads to the creek");
            Assert.That(Doors.Halves.Select(h => h.Name), Is.EqualTo(new[] { Doors.SeaDoorName, Doors.StrandDoorName }));

            foreach (Doors.CreekHalf half in Doors.Halves)
            {
                PlanDoor their = theirs.Single(d => d.OwnArrival == half.ArrivalKey);
                Rect ours = Band(half.Position, half.Size);
                TestContext.WriteLine($"{half.Name}: band at {At2(half.Position)}, {Shown(half.Size.x)} x {Shown(half.Size.y)} m " +
                                      $"(x {Shown(ours.xMin)}..{Shown(ours.xMax)}, y {Shown(ours.yMin)}..{Shown(ours.yMax)}), asks Rice Point " +
                                      $"for '{half.ArrivalKey}'{(half.OnFootOnly ? ", on foot only" : "")}; its other half, {their.Id}, " +
                                      $"is at {At2(their.Centre)} in Rice Point's frame and asks the creek for '{their.TargetArrival}'");
                Assert.That(half.Position.z, Is.EqualTo(0f), $"{half.Name} stands on the ground plane");
                Assert.That(Vector2.Distance(half.Position, Mirrored(their.Centre, seam)), Is.LessThan(1e-4f),
                    $"{half.Name} is {their.Id} mirrored across the seam");
                Assert.That(Vector2.Distance(half.Size, their.Size), Is.LessThan(1e-4f), $"{half.Name} is {their.Id}'s size");
                Assert.That(ours.xMin, Is.EqualTo(seam).Within(1e-4f), $"{half.Name} meets the seam");
                Assert.That(ours.yMin >= creek.yMin && ours.yMax <= creek.yMax && ours.xMax <= creek.xMax, Is.True,
                    $"{half.Name} lies inside the creek's frame");
                Assert.That(half.OnFootOnly, Is.EqualTo(their.OnFootOnly), $"{half.Name} takes whom {their.Id} takes");
                Assert.That(their.Shut, Is.False, $"{their.Id} is open, so the creek places its half");
                Assert.That(Doors.WaysIn.Select(w => w.Key), Does.Contain(their.TargetArrival),
                    $"the creek authors the way in {their.Id} asks for");
            }
        }

        [Test]
        public void EveryWayIn_StandsBeyondEveryBandOnItsSide_SoNoArrivalReFiresADoor()
        {
            float seam = RegionRect(CreekRegionPath).xMin;
            List<PlanDoor> theirs = PlanDoors();
            var hulls = HullsThatCross.ToDictionary(n => n, n => Hull($"Assets/_Project/Data/Boats/{n}.asset"));

            // The creek's side: the ways in Rice Point's doors ask for, against both halves and every door the scene holds.
            var creekBands = Doors.Halves.Select(h => (Who: h.Name, Band: Band(h.Position, h.Size)))
                .Concat(Bands(Scene).Where(b => Doors.Halves.All(h => h.Name != b.Who))).ToList();
            var walker = PlayerCircle(Scene);
            foreach (Doors.WayIn way in Doors.WaysIn)
            {
                PlanDoor their = theirs.Single(d => d.TargetArrival == way.Key);
                Assert.That(way.Walker.z, Is.EqualTo(0f), $"{way.Key}'s walker stands on the ground plane");
                Assert.That(Vector2.Distance(way.Walker, Mirrored(their.Disembark, seam)), Is.LessThan(1e-4f),
                    $"{way.Key}'s walker lands where {their.Id}'s disembark point mirrors to");
                if (their.OnFootOnly)
                    Assert.That(way.Boat.HasValue, Is.False,
                        $"{way.Key} is a walk-in: it sets no boat point, so the boat lies at the creek's own berth, as over the bar");
                else
                {
                    Assert.That(way.Boat.HasValue, Is.True, $"{way.Key} brings a boat");
                    Assert.That(Vector2.Distance(way.Boat.Value, Mirrored(their.Arrival, seam)), Is.LessThan(1e-4f),
                        $"{way.Key}'s boat lies where {their.Id}'s arrival point mirrors to");
                }
                Clear($"the creek's '{way.Key}'", way.Walker, way.Boat, creekBands, walker, hulls);
            }

            // Rice Point's side: the ways in the creek's halves ask for, where Rice Point's plan puts them, against its three bands.
            var riceBands = theirs.Select(d => (Who: d.Id, Band: Band(d.Centre, d.Size))).ToList();
            var riceWalker = PlayerCircle(RicePointScene);
            foreach (Doors.CreekHalf half in Doors.Halves)
            {
                PlanDoor their = theirs.Single(d => d.OwnArrival == half.ArrivalKey);
                Clear($"Rice Point's '{their.OwnArrival}'", their.Disembark, their.OnFootOnly ? (Vector3?)null : (Vector3)their.Arrival,
                      riceBands, riceWalker, hulls);
            }
        }

        /// <summary>A way in stands clear of every band: the walker's circle outside each, and the boat's whole reach.</summary>
        static void Clear(string way, Vector3 walkerAt, Vector3? boatAt, List<(string Who, Rect Band)> bands,
                          (float Radius, Vector2 Offset) walker,
                          Dictionary<string, (float Length, float Draught, float HalfBeam)> hulls)
        {
            float nearestFeet = float.MaxValue, nearestHull = float.MaxValue;
            foreach (var (who, band) in bands)
            {
                float feet = GapTo(band, (Vector2)walkerAt + walker.Offset);
                nearestFeet = Mathf.Min(nearestFeet, feet);
                Assert.That(feet, Is.GreaterThan(walker.Radius),
                    $"{way}: a walker landing at {At2(walkerAt)} stands {Shown(feet)} m from {who}'s band, inside their own " +
                    $"circle ({Shown(walker.Radius)} m): only the re-fire guard would stand between them and a crossing straight back");
                if (!boatAt.HasValue) continue;
                float gap = GapTo(band, boatAt.Value);
                nearestHull = Mathf.Min(nearestHull, gap);
                foreach (var (name, hull) in hulls)
                    Assert.That(gap, Is.GreaterThan(Reach(hull)),
                        $"{way}: a {name} laid at {At2(boatAt.Value)} reaches {Shown(Reach(hull))} m any way she lies, " +
                        $"and {who}'s band is {Shown(gap)} m off");
            }
            TestContext.WriteLine($"{way}: the walker's feet {Shown(nearestFeet)} m from the nearest band" +
                                  (boatAt.HasValue ? $", the boat's point {Shown(nearestHull)} m (reach: " +
                                   string.Join(", ", hulls.Select(h => $"{h.Key} {Shown(Reach(h.Value))} m")) + ")" : ", no boat"));
        }

        // ---- who takes the strand ---------------------------------------------------------------------------------

        [Test]
        public void OnlyAWalkerTakesTheStrand_ADriverAHelmsmanAndADeckhandAreRefused()
        {
            var who = new Dictionary<ControlMode, string>
            {
                [ControlMode.OnFoot] = "a walker",
                [ControlMode.Driving] = "a truck's driver or an ATV's rider",
                [ControlMode.Aboard] = "a boat's helmsman",
                [ControlMode.OnDeck] = "a hand on a boat's deck",
            };
            Assert.That(who.Keys, Is.EquivalentTo((ControlMode[])Enum.GetValues(typeof(ControlMode))), "every way of moving is named");
            foreach (var mode in who)
                Assert.That(LandPassageAccess.Allows(true, mode.Key), Is.EqualTo(mode.Key == ControlMode.OnFoot),
                    $"the strand's policy with {mode.Value} at the controls");
            Assert.That(LandPassageAccess.Allows(false, ControlMode.OnFoot), Is.False, "no known controller, no passage");
            Assert.That(Doors.Halves.Single(h => h.Name == Doors.StrandDoorName).OnFootOnly, Is.True, "the strand carries the policy");
            Assert.That(Doors.Halves.Single(h => h.Name == Doors.SeaDoorName).OnFootOnly, Is.False, "the sea door carries every boat");
        }

        // ---- Route 91 waits on NR1b -------------------------------------------------------------------------------

        [Test]
        public void RicePointsPlan_KeepsRoute91Shut_UntilNR1b_TheSeaAndStrandOpen()
        {
            List<PlanDoor> doors = PlanDoors();
            Assert.That(doors.Select(d => d.Id), Is.EqualTo(new[] { SeaId, StrandId, Route91Id }), "Rice Point's three doors");
            Assert.That(doors.Where(d => d.Shut).Select(d => d.Id), Is.EqualTo(new[] { Route91Id }),
                "Route 91 waits shut for H4's road and NR1b's half; the sea and the strand open now");
            PlanDoor road = doors.Single(d => d.Id == Route91Id);
            Assert.That(Doors.Halves.Select(h => h.ArrivalKey), Does.Not.Contain(road.OwnArrival), "the creek places no half of Route 91 yet");
            Assert.That(Doors.WaysIn.Select(w => w.Key), Does.Not.Contain(road.TargetArrival), "nor the way in it would ask for");
        }

        /// <summary>The scene follows its plan: Rice Point's builder writes a shut door's band off. Phase B's refresh of
        /// RicePoint.unity writes the one line this needs; until then this names the scene as unrefreshed.</summary>
        [Test]
        public void RicePointsScene_HoldsEachDoorsBandAsItsPlanSays_Route91Off()
        {
            SceneYaml s = RicePointScene;
            foreach (PlanDoor door in PlanDoors())
            {
                Doc go = s.Docs.Single(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == door.Id);
                Doc box = s.ComponentsOf(go).Single(c => c.ClassId == BoxCollider2DClass);
                TestContext.WriteLine($"{door.Id}: plan {(door.Shut ? "shut" : "open")}, band {(Enabled(box) ? "on" : "off")}");
                Assert.That(Active(go), Is.True, $"{door.Id} stays placed");
                Assert.That(Enabled(box), Is.EqualTo(!door.Shut),
                    $"RicePoint.unity's {door.Id} band is as its plan says; if not, the builder has not refreshed the scene since the plan changed");
            }
        }

        // ---- step 12: the bake, as text ---------------------------------------------------------------------------

        [Test]
        public void TheStep_PlansExactlyTheHalvesTheLoadersListAndTheWaysIn_OrNothingOnceBaked()
        {
            string before = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            Doors.StepAssets assets = Doors.StepAssets.Read();
            Assert.That(assets, Is.Not.Null, "Rice Point's RegionDef is on main, so the step has a region to join");
            List<LayerPatch> patches = Doors.PlanAll(before, assets, out string after);
            foreach (LayerPatch p in patches) TestContext.WriteLine(p.Summary());
            Assert.That(patches.Select(p => p.Root),
                Is.EqualTo(new[] { Doors.SeaDoorName, Doors.StrandDoorName, Doors.LoaderName, NineMileCreekGroundSteps.AnchorName }),
                "one patch per root: each half, the loader's list, the anchor's ways in");
            List<LayerPatch> again = Doors.PlanAll(after, assets, out string twice);
            Assert.That(again.Where(p => !p.IsEmpty).Select(p => p.Root), Is.Empty, "a second plan, on what the step writes, changes nothing");
            Assert.That(twice == after, Is.True, "and writes the same scene");

            SceneYaml was = SceneYaml.Parse(before), now = SceneYaml.Parse(after);
            StepShape shape = Holds(now, assets);
            bool baked = was.Docs.Any(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == Doors.SeaDoorName);
            if (baked)
            {
                TestContext.WriteLine("the committed scene carries step 12");
                Assert.That(patches.Where(p => !p.IsEmpty).Select(p => p.Root), Is.Empty, "a baked scene plans nothing");
                Assert.That(after == before, Is.True, "and is written unchanged");
                return;
            }

            var wasIds = new HashSet<long>(was.Docs.Select(d => d.FileId));
            List<long> added = now.Docs.Where(d => !wasIds.Contains(d.FileId)).Select(d => d.FileId).ToList();
            List<long> edited = was.Docs.Where(d => now.Contains(d.FileId) && now.Get(d.FileId).Text != d.Text).Select(d => d.FileId).ToList();
            TestContext.WriteLine($"the committed scene waits on step 12 (Phase B's bake): {added.Count} documents to add, {edited.Count} to edit");
            Assert.That(wasIds.Where(id => !now.Contains(id)), Is.Empty, "step 12 deletes nothing");
            Assert.That(added, Is.EquivalentTo(shape.Written), "step 12 adds the halves and the points, and nothing else");
            Assert.That(edited, Is.EquivalentTo(new[] { shape.RootsDoc, shape.Loader, shape.AnchorTransform, shape.Anchor }),
                "step 12 edits the roots list, the loader, the anchor's transform and the anchor, and nothing else");
            Changed(was, now, shape.RootsDoc, new string[0], shape.Roots, "SceneRoots lists the two halves, after the roots it holds");
            Changed(was, now, shape.Loader, new string[0], new[] { shape.Region }, "the loader lists Rice Point, after the regions it lists");
            bool hadPoints = was.Get(shape.AnchorTransform).FieldRefs("m_Children").Count > 0;
            Changed(was, now, shape.AnchorTransform, hadPoints ? new string[0] : new[] { "  m_Children: []" },
                    hadPoints ? shape.Children : new[] { "  m_Children:" }.Concat(shape.Children).ToList(),
                    "the anchor holds Rice Point's points, after any it holds");
            Changed(was, now, shape.Anchor, new string[0], shape.Ways, "the anchor names Rice Point's ways in, after the bar's");
        }

        [Test]
        public void WithoutRicePointsDef_TheStepFindsNoAssets_AndPlansNothing()
        {
            string nowhere = Path.Combine(Path.GetTempPath(), "hh-no-project-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(nowhere), Is.False, "premise: a project root with no assets at all");
            Assert.That(Doors.StepAssets.Read(nowhere), Is.Null, "no Rice Point RegionDef, no step assets");
            Assert.That(Doors.StepAssets.Read(), Is.Not.Null, "control: this project's own def is found");
            string scene = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            Assert.That(Doors.PlanAll(scene, null, out string after), Is.Empty, "the step plans nothing without the def");
            Assert.That(after == scene, Is.True, "and leaves the scene as it is: never a dangling door");
        }

        // ---- the name on the card, and the save --------------------------------------------------------------------

        [Test]
        public void TheCrossingCard_NamesRicePoint_FromTheSceneItLoads_ReadingNoRegistrar()
        {
            string def = File.ReadAllText(Doors.RegionAssetPath);
            string scene = AssetLine(def, "SceneName"), shown = AssetLine(def, "DisplayName");
            Assert.That(File.Exists($"Assets/_Project/Scenes/{scene}.unity"), Is.True, "the scene Rice Point's def loads is on main");
            Assert.That(RegionFade.ArrivalTitle(scene), Is.EqualTo(shown).And.EqualTo("Rice Point"),
                "the card reads the loaded scene's name, which gives Rice Point's own");
            string overlay = Code(OverlaySource);
            string[] writes = Regex.Matches(overlay, @"_arrivalLabel\.text\s*=\s*([^;]+);").Cast<Match>()
                                   .Select(m => m.Groups[1].Value.Trim()).ToArray();
            Assert.That(writes, Is.EqualTo(new[] { "RegionFade.ArrivalTitle(next.name)" }),
                "the card has one writer, and it reads the scene the crossing loads");
            Assert.That(overlay, Does.Not.Contain("RegionDisplayNames"),
                "the overlay reads no registrar, so no registrar must name Rice Point before the card draws");
        }

        [Test]
        public void RicePointsAnchor_GivesARestSaveItsDefsId_AndTheCreeksLoaderResolvesIt()
        {
            string id = AssetLine(File.ReadAllText(Doors.RegionAssetPath), "Id");
            Assert.That(id, Is.EqualTo(RicePointId), "Rice Point's def carries its id");
            List<Doc> anchors = RicePointScene.Docs.Where(d => d.IsScript("HiddenHarbours.App.RegionAnchor")).ToList();
            Assert.That(anchors.Count, Is.EqualTo(1), "Rice Point's scene has one anchor");
            Assert.That(anchors[0].Field("_regionId"), Is.EqualTo(id), "its anchor reports Rice Point's id as the current region");
            Assert.That(Code(AnchorSource), Does.Match(@"GameServices\.CurrentRegionId\s*=\s*_regionId\s*;"),
                "an enabled anchor publishes its id");
            Assert.That(Code(RestSaveSource), Does.Match(@"AnchorFor\(\s*request\s*,\s*GameServices\.CurrentRegionId\s*\)"),
                "and a rest save stamps that id");
            Doors.PlanAll(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), Doors.StepAssets.Read(), out string after);
            SceneYaml creek = SceneYaml.Parse(after);
            Doc loader = creek.ComponentsOf(creek.RootNamed(Doors.LoaderName)).Single(d => d.IsScript("HiddenHarbours.World.RegionSceneLoader"));
            Assert.That(ListLines(loader, "_regions").Select(l => RefGuid(l)), Does.Contain(GuidOf(Doors.RegionAssetPath)),
                "the creek's loader, once step 12 stands, lists the def that carries the id");
        }

        // ---- in the editor: the builder's sections, the switcher, the template, the loader ------------------------

        [Test]
        public void TheBuildersDoorSection_PlacesTheObjectsTheStepWrites()
        {
            var ricePoint = AssetDatabase.LoadAssetAtPath<RegionDef>(Doors.RegionAssetPath);
            Assert.That(ricePoint, Is.Not.Null, "Rice Point's RegionDef is on main");
            var loader = Track(new GameObject(Doors.LoaderName)).AddComponent<RegionSceneLoader>();
            Transform anchor = Track(new GameObject(NineMileCreekGroundSteps.AnchorName)).transform;
            RegionPassage[] placed = Doors.PlaceDoors(ricePoint, loader);
            foreach (RegionPassage p in placed) Track(p.gameObject);
            NamedArrival[] ways = Doors.PlaceArrivals(ricePoint, anchor);

            Doors.PlanAll(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), Doors.StepAssets.Read(), out string after);
            SceneYaml s = SceneYaml.Parse(after);
            Assert.That(placed.Select(p => p.name), Is.EqualTo(Doors.Halves.Select(h => h.Name)), "the builder places both halves");
            foreach (RegionPassage live in placed)
            {
                Doc go = s.RootNamed(live.name);
                List<Doc> parts = s.ComponentsOf(go);
                Assert.That(live.GetComponents<Component>().Select(c => Kind(c)), Is.EqualTo(parts.Select(d => Kind(d))),
                    $"{live.name}: the builder's components are the step's, in its order");
                Assert.That(live.gameObject.activeSelf, Is.EqualTo(Active(go)), $"{live.name} is active as the step writes it");
                Assert.That(live.transform.parent, Is.Null, $"{live.name} is a root, as the step writes it");
                Assert.That(live.transform.localPosition, Is.EqualTo(ParseVec3(parts[0].Field("m_LocalPosition"))), $"{live.name} stands where the step puts it");
                Assert.That(live.transform.localRotation, Is.EqualTo(Quaternion.identity), $"{live.name} is unturned");
                Assert.That(live.transform.localScale, Is.EqualTo(Vector3.one), $"{live.name} is unscaled");

                var box = live.GetComponent<BoxCollider2D>();
                Assert.That(box.enabled, Is.EqualTo(Enabled(parts[1])), $"{live.name}'s band is on, as the step writes it");
                Assert.That(box.isTrigger, Is.EqualTo(parts[1].Field("m_IsTrigger") == "1"), $"{live.name}'s band is a trigger");
                Assert.That(box.offset, Is.EqualTo(Vector(parts[1], "m_Offset")), $"{live.name}'s band is centred on it");
                Assert.That(box.size, Is.EqualTo(Vector(parts[1], "m_Size")), $"{live.name}'s band is the step's size");

                Doc passage = parts[2];
                Assert.That(live.enabled, Is.EqualTo(Enabled(passage)), $"{live.name}'s passage is enabled");
                Assert.That(GuidOfAsset(MonoScript.FromMonoBehaviour(live)), Is.EqualTo(RefGuid(passage.Field("m_Script"))));
                Assert.That(GuidOfAsset(live.Target), Is.EqualTo(RefGuid(passage.Field("_target"))), $"{live.name} leads to Rice Point");
                Assert.That(live.ArrivalKey, Is.EqualTo(passage.Field("_arrivalKey")), $"{live.name} asks for the step's way in");
                var saved = new SerializedObject(live);
                Assert.That(saved.FindProperty("_reentryCooldownSeconds").floatValue, Is.EqualTo(Number(passage, "_reentryCooldownSeconds")),
                    $"{live.name} keeps the passage's own re-fire guard");
                Assert.That(saved.FindProperty("_loader").objectReferenceValue, Is.SameAs(loader), $"{live.name} travels by the scene's loader");
                Object policy = saved.FindProperty("_accessPolicy").objectReferenceValue;
                if (passage.FieldRef("_accessPolicy") == 0)
                    Assert.That(policy, Is.Null, $"{live.name} carries no policy");
                else
                {
                    var access = live.GetComponent<LandPassageAccess>();
                    Assert.That(policy, Is.SameAs(access), $"{live.name}'s policy is its own on-foot one");
                    Assert.That(access.enabled, Is.EqualTo(Enabled(parts[3])));
                    Assert.That(GuidOfAsset(MonoScript.FromMonoBehaviour(access)), Is.EqualTo(RefGuid(parts[3].Field("m_Script"))));
                }
            }

            Doc anchorTr = s.TransformOf(s.RootNamed(NineMileCreekGroundSteps.AnchorName));
            Assert.That(ways.Select(w => w.Key), Is.EqualTo(Doors.WaysIn.Select(w => w.Key)), "the builder names Rice Point's ways in");
            foreach (NamedArrival way in ways)
                foreach (var (point, name) in new[] { (way.ArrivalPoint, way.Key), (way.DisembarkPoint, way.Key + Doors.ShoreSuffix) })
                {
                    Assert.That(point != null, Is.EqualTo(NamesUnder(s, anchorTr).Contains(name)), $"{name}: the builder and the step agree it exists");
                    if (point == null) continue;
                    Doc doc = s.ChildNamed(anchorTr, name);
                    Assert.That(point.name, Is.EqualTo(name));
                    Assert.That(point.parent, Is.SameAs(anchor), $"{name} hangs under the anchor");
                    Assert.That(point.localPosition, Is.EqualTo(ParseVec3(s.TransformOf(doc).Field("m_LocalPosition"))), $"{name} stands where the step puts it");
                    Assert.That(point.GetComponents<Component>().Length, Is.EqualTo(s.ComponentsOf(doc).Count), $"{name} is a bare point");
                }
        }

        /// <summary>What the player sees: a truck, an ATV or a boat that runs into the strand's band gets nothing, no
        /// fade and no card, and runs on toward the seam as if the band were not there. The sea door, 45 m south
        /// along the seam, carries every boat.</summary>
        [Test]
        public void TheStrandRefusesATruckSeatAnAtvAndABoat_TheSeaDoorRefusesNone()
        {
            Reset();
            var ricePoint = AssetDatabase.LoadAssetAtPath<RegionDef>(Doors.RegionAssetPath);
            var loader = Go(Doors.LoaderName, Vector3.zero).AddComponent<RegionSceneLoader>();
            RegionPassage[] doors = Doors.PlaceDoors(ricePoint, loader);
            foreach (RegionPassage d in doors) Track(d.gameObject);
            RegionPassage sea = doors.Single(d => d.name == Doors.SeaDoorName), strand = doors.Single(d => d.name == Doors.StrandDoorName);

            // EventBusHelmTests' rig: a walker beside a dory at a dock, boarded through the public interaction.
            var player = Go("Player", new Vector3(0, -11.5f, 0));
            var walk = player.AddComponent<PlayerWalkController>();
            player.AddComponent<DeckWalkController>().enabled = false;
            var boatGo = Go("Boat", new Vector3(0, -13.8f, 0));
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = Track(ScriptableObject.CreateInstance<BoatHullDef>());
            hull.Id = "boat.strand_test"; hull.CameraWorldHeightMeters = 14f;
            boat.SetHull(hull);
            boat.enabled = false; input.enabled = false;
            var sw = Go("Switcher", Vector3.zero).AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, Go("Dock", new Vector3(0, -12, 0)).transform, 3f, Go("Exit", new Vector3(0, -10.5f, 0)).transform);
            Assert.That(Object.FindObjectsByType<ControlSwitcher>(FindObjectsSortMode.None), Has.Length.EqualTo(1),
                "premise: the policy finds the one switcher there is");

            void Holds(string who, ControlMode mode, bool strandCrosses)
            {
                TestContext.WriteLine($"{who} ({sw.Mode}): the strand {(strand.TravelAllowed ? "crosses" : "refuses")}, " +
                                      $"the sea door {(sea.TravelAllowed ? "crosses" : "refuses")}");
                Assert.That(sw.Mode, Is.EqualTo(mode), $"premise: {who} is {mode}");
                Assert.That(strand.TravelAllowed, Is.EqualTo(strandCrosses), $"the strand with {who}");
                Assert.That(sea.TravelAllowed, Is.True, $"the sea door with {who}");
            }

            Holds("a walker", ControlMode.OnFoot, true);
            foreach (var (who, astride) in new[] { ("a truck's driver", false), ("an ATV's rider", true) })
            {
                var seat = new TestSeat(who, Go(who, player.transform.position).transform, astride);
                Assert.That(sw.TryEnterDriving(seat), Is.True, $"{who} takes the wheel through the public gate");
                Holds(who, ControlMode.Driving, false);
                Assert.That(sw.LeaveDriving(), Is.True, $"{who} gets out");
                Holds("a walker again", ControlMode.OnFoot, true);
            }
            player.transform.position = new Vector3(0, -11.5f, 0);
            Assert.That(sw.TryInteract(), Is.True, "the walker boards through the public interaction");
            Holds("a hand on the dory's deck", ControlMode.OnDeck, false);
            player.transform.position = sw.HelmWorldPosition;
            Assert.That(sw.TryInteract(), Is.True, "and takes the oars");
            Holds("the dory's oarsman", ControlMode.Aboard, false);
        }

        [Test]
        public void AShutDoor_IsPlacedAndWired_WithItsBandOff_Route91()
        {
            LandScenePlanDef plan = Track(Object.Instantiate(RicePointBuilder.LoadPlan()));
            GameObject root = Track(new GameObject("RicePointDoorsUnderTest"));
            var loader = root.AddComponent<RegionSceneLoader>();
            LandSceneTemplate.AddDoors(plan, root.transform, loader);
            RegionPassage[] placed = root.GetComponentsInChildren<RegionPassage>(true);
            Assert.That(placed.Select(p => p.name), Is.EqualTo(plan.Doors.Select(d => d.Id)), "every door is placed, a shut one too");
            foreach (LandDoor door in plan.Doors)
            {
                RegionPassage passage = placed.Single(p => p.name == door.Id);
                Assert.That(passage.gameObject.activeInHierarchy && passage.enabled, Is.True, $"{door.Id} is placed and wired");
                Assert.That(passage.Target, Is.SameAs(door.Target), $"{door.Id} leads where its plan says");
                Assert.That(passage.ArrivalKey, Is.EqualTo(door.TargetArrival), $"{door.Id} asks for its plan's way in");
                Assert.That(passage.GetComponent<BoxCollider2D>().enabled, Is.EqualTo(!door.Shut), $"{door.Id}: a shut door's band is off");
            }
            Assert.That(plan.Doors.Where(d => d.Shut).Select(d => d.Id), Is.EqualTo(new[] { Route91Id }), "only Route 91 waits shut, on NR1b");
        }

        [Test]
        public void TheLoadersRegions_GainRicePointOnlyWithItsDef_AndResolveItsId()
        {
            RegionDef Load(string name) => AssetDatabase.LoadAssetAtPath<RegionDef>(RegionsFolder + name + ".asset");
            RegionDef creek = Load("NineMileCreek"), cove = Load("CoddleCove"), stPeters = Load("StPeters"),
                      west = Load("WestWater"), ricePoint = Load(Doors.RegionAssetName);
            Object[] with = Doors.LoaderRegions(creek, cove, stPeters, west, ricePoint);
            Object[] without = Doors.LoaderRegions(creek, cove, stPeters, west, null);
            Assert.That(with, Is.EqualTo(new Object[] { creek, cove, stPeters, west, ricePoint }), "with the def, Rice Point closes the list");
            Assert.That(without, Is.EqualTo(new Object[] { creek, cove, stPeters, west }), "without it, the list has no hole");
            foreach (var (list, resolves) in new[] { (with, true), (without, false) })
            {
                var loader = Track(new GameObject("Loader")).AddComponent<RegionSceneLoader>();
                var saved = new SerializedObject(loader);
                SerializedProperty regions = saved.FindProperty("_regions");
                regions.arraySize = list.Length;
                for (int i = 0; i < list.Length; i++) regions.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                saved.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(loader.Registry.TryGet(RicePointId, out RegionDef found), Is.EqualTo(resolves),
                    $"a loader listing {list.Length} regions {(resolves ? "resolves" : "cannot resolve")} Rice Point's id");
                if (resolves) Assert.That(found, Is.SameAs(ricePoint));
            }

            Transform anchor = Track(new GameObject("Anchor")).transform;
            Assert.That(Doors.PlaceDoors(null, null), Is.Empty, "no def, no doors");
            Assert.That(Doors.PlaceArrivals(null, anchor), Is.Empty, "no def, no ways in");
            Assert.That(anchor.childCount, Is.Zero, "and no points under the anchor");
        }

        // ---- helpers ------------------------------------------------------------------------------------------------

        sealed class PlanDoor
        {
            public string Id, TargetGuid, TargetArrival, OwnArrival;
            public Vector2 Centre, Size, Arrival, Disembark;
            public bool OnFootOnly, Shut;
        }

        /// <summary>Rice Point's doors as its plan writes them: each "  - " entry and the four-space fields under it.</summary>
        static List<PlanDoor> PlanDoors()
        {
            string[] lines = File.ReadAllText(PlanPath).Replace("\r", "").Split('\n');
            int at = Array.IndexOf(lines, "  Doors:");
            Assert.That(at, Is.GreaterThan(0), "Rice Point's plan lists its doors");
            var entries = new List<Dictionary<string, string>>();
            for (int i = at + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("  - ", StringComparison.Ordinal)) entries.Add(new Dictionary<string, string>());
                else if (!line.StartsWith("    ", StringComparison.Ordinal)) break;
                int colon = line.IndexOf(": ", StringComparison.Ordinal);
                entries[entries.Count - 1][line.Substring(4, colon - 4)] = line.Substring(colon + 2);
            }
            return entries.Select(e =>
            {
                string Value(string key)
                {
                    Assert.That(e.ContainsKey(key), Is.True, $"a door in Rice Point's plan writes no {key}");
                    return e[key];
                }
                return new PlanDoor
                {
                    Id = Value("Id"), TargetGuid = RefGuid(Value("Target")), TargetArrival = Value("TargetArrival"),
                    OwnArrival = Value("OwnArrival"), Centre = Vector(Value("Centre")), Size = Vector(Value("Size")),
                    Arrival = Vector(Value("Arrival")), Disembark = Vector(Value("Disembark")),
                    OnFootOnly = Value("OnFootOnly") == "1", Shut = Value("Shut") == "1",
                };
            }).ToList();
        }

        /// <summary>A region's frame, as its RegionDef writes it.</summary>
        static Rect RegionRect(string assetPath)
        {
            string text = File.ReadAllText(assetPath);
            Vector2 centre = Vector(AssetLine(text, "WorldCenter")), size = Vector(AssetLine(text, "WorldSizeMeters"));
            return new Rect(centre - size * 0.5f, size);
        }

        static string AssetLine(string text, string key)
        {
            Match m = Regex.Match(text, @"(?m)^  " + key + @": (.*?)\r?$");
            Assert.That(m.Success, Is.True, $"no {key}");
            return m.Groups[1].Value;
        }

        /// <summary>A point in Rice Point's frame carried into the creek's and mirrored across the seam: where the creek
        /// places its half of whatever Rice Point authors at <paramref name="ricePoint"/>.</summary>
        static Vector2 Mirrored(Vector2 ricePoint, float seam)
        {
            Vector2 inCreek = ricePoint - NineMileCreekMainland.RicePointFrameOffset;
            return new Vector2(2f * seam - inCreek.x, inCreek.y);
        }

        static Rect Band(Vector3 centre, Vector2 size) => new Rect((Vector2)centre - size * 0.5f, size);

        /// <summary>The gap between a point and a band, zero inside it.</summary>
        static float GapTo(Rect band, Vector2 p)
        {
            float dx = Mathf.Max(band.xMin - p.x, p.x - band.xMax, 0f);
            float dy = Mathf.Max(band.yMin - p.y, p.y - band.yMax, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>How far a hull reaches from her point, any way she lies: half her length by half her beam.</summary>
        static float Reach((float Length, float Draught, float HalfBeam) hull) =>
            Mathf.Sqrt(hull.Length * hull.Length / 4f + hull.HalfBeam * hull.HalfBeam);

        static string At2(Vector2 v) => $"({Shown(v.x)}, {Shown(v.y)})";

        static SceneYaml _ricePoint;
        static SceneYaml RicePointScene => _ricePoint ??= SceneYaml.Parse(File.ReadAllText(RicePointScenePath));

        /// <summary>Every door's band in a scene: its passage's GameObject, where it stands, and its box.</summary>
        static List<(string Who, Rect Band)> Bands(SceneYaml s) =>
            s.Docs.Where(d => !d.Stripped && d.IsScript("HiddenHarbours.World.RegionPassage")).Select(p =>
            {
                Doc go = s.Require(p.FieldRef("m_GameObject"), "a door");
                Doc box = s.ComponentsOf(go).Single(c => c.ClassId == BoxCollider2DClass);
                return (Name(go), Band(Placed(s, go) + (Vector3)Vector(box, "m_Offset"), Vector(box, "m_Size")));
            }).ToList();

        /// <summary>Where a GameObject stands in a scene: its local position composed up its parents, none of which (nor
        /// it) may turn or scale what it holds.</summary>
        static Vector3 Placed(SceneYaml s, Doc go)
        {
            Vector3 at = Vector3.zero;
            for (Doc tr = s.TransformOf(go); tr != null;)
            {
                Assert.That(tr.Field("m_LocalRotation") + " " + tr.Field("m_LocalScale"),
                    Is.EqualTo("{x: 0, y: 0, z: 0, w: 1} {x: 1, y: 1, z: 1}"), $"&{tr.FileId} turns or scales what it holds");
                at += ParseVec3(tr.Field("m_LocalPosition"));
                long up = tr.FieldRef("m_Father");
                tr = up == 0 ? null : s.Require(up, "a parent transform");
            }
            return at;
        }

        /// <summary>The walker's circle as a scene authors it: the one PlayerWalkController's CircleCollider2D.</summary>
        static (float Radius, Vector2 Offset) PlayerCircle(SceneYaml s)
        {
            string walker = GuidOf(PlayerSource);
            Doc player = s.Docs.Where(d => d.ClassId == MonoBehaviourClass && ScriptGuid(d) == walker)
                          .Select(d => s.Require(d.FieldRef("m_GameObject"), "the player")).Single();
            Doc circle = s.ComponentsOf(player).Single(d => d.ClassId == CircleCollider2DClass);
            return (Number(circle, "m_Radius"), Vector(circle, "m_Offset"));
        }

        static string ScriptGuid(Doc behaviour)
        {
            Match m = Regex.Match(behaviour.Field("m_Script") ?? "", @"guid: ([0-9a-f]{32})");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>The names of the GameObjects directly under a transform (prefab instances skipped).</summary>
        static List<string> NamesUnder(SceneYaml s, Doc transform) =>
            transform.FieldRefs("m_Children").Select(id => s.Require(id, "a child transform")).Where(t => !t.Stripped)
                     .Select(t => Name(s.Require(t.FieldRef("m_GameObject"), "a child GameObject"))).ToList();

        /// <summary>The lines of a two-space list field: its "  - " entries and the four-space lines under them.</summary>
        static string[] ListLines(Doc doc, string key)
        {
            string[] lines = doc.Lines;
            int at = Array.FindIndex(lines, l => l == "  " + key + ":" || l == "  " + key + ": []");
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"&{doc.FileId} has no {key}");
            return lines.Skip(at + 1).TakeWhile(l => l.StartsWith("  - ", StringComparison.Ordinal) ||
                                                     l.StartsWith("    ", StringComparison.Ordinal)).ToArray();
        }

        static string Kind(Doc d) =>
            d.ClassId == TransformClass ? "Transform" :
            d.ClassId == BoxCollider2DClass ? "BoxCollider2D" :
            d.ClassId == MonoBehaviourClass ? ClassOf(d.Field("m_EditorClassIdentifier")) : "class " + d.ClassId;

        static string Kind(Component c) => c is Transform ? "Transform" : c.GetType().Name;

        static string ClassOf(string identifier) =>
            identifier == null ? "a script with no class" : identifier.Substring(identifier.LastIndexOf('.') + 1);

        static string Ref(long id) => "{fileID: " + id + "}";

        static string GuidOfAsset(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        /// <summary>One source file with its comments removed, so a scan reads what the compiler reads.</summary>
        static string Code(string path)
        {
            Assert.That(File.Exists(path), Is.True, "missing source: " + path);
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(text, @"//[^\r\n]*", " ");
        }

        /// <summary>What step 12 leaves in a scene, read back field by field: the documents it writes, and the lines
        /// its four edits add (the roots list's, the loader's, the anchor transform's and the anchor's).</summary>
        sealed class StepShape
        {
            public readonly HashSet<long> Written = new HashSet<long>();
            public readonly List<string> Roots = new List<string>(), Children = new List<string>(), Ways = new List<string>();
            public long RootsDoc, Loader, AnchorTransform, Anchor;
            public string Region;
        }

        static StepShape Holds(SceneYaml s, Doors.StepAssets assets)
        {
            var shape = new StepShape();
            Doc rootsDoc = s.Docs.Single(d => d.ClassId == SceneRootsClass);
            List<long> roots = rootsDoc.FieldRefs("m_Roots");
            Doc loader = s.ComponentsOf(s.RootNamed(Doors.LoaderName)).Single(d => d.IsScript("HiddenHarbours.World.RegionSceneLoader"));
            shape.RootsDoc = rootsDoc.FileId;
            shape.Loader = loader.FileId;

            foreach (Doors.CreekHalf half in Doors.Halves)
            {
                Doc go = s.RootNamed(half.Name);
                List<Doc> parts = s.ComponentsOf(go);
                string[] kinds = half.OnFootOnly
                    ? new[] { "Transform", "BoxCollider2D", "RegionPassage", "LandPassageAccess" }
                    : new[] { "Transform", "BoxCollider2D", "RegionPassage" };
                Assert.That(parts.Select(d => Kind(d)), Is.EqualTo(kinds), $"{half.Name} carries the builder's components, in its order");
                Doc tr = parts[0], box = parts[1], passage = parts[2];
                Assert.That(Active(go), Is.True, $"{half.Name} is active");
                Assert.That(Placed(s, go), Is.EqualTo(half.Position), $"{half.Name} stands where the builder puts it");
                Assert.That(tr.FieldRef("m_Father"), Is.EqualTo(0L), $"{half.Name} is a root");
                Assert.That(roots, Does.Contain(tr.FileId), $"{half.Name} is listed among the scene's roots");
                Assert.That(Enabled(box) && box.Field("m_IsTrigger") == "1", Is.True, $"{half.Name}'s band is an enabled trigger");
                Assert.That(Vector(box, "m_Offset"), Is.EqualTo(Vector2.zero), $"{half.Name}'s band is centred on it");
                Assert.That(Vector(box, "m_Size"), Is.EqualTo(half.Size), $"{half.Name}'s band is its size");
                Assert.That(Enabled(passage), Is.True, $"{half.Name}'s passage is enabled");
                Assert.That(RefGuid(passage.Field("m_Script")), Is.EqualTo(GuidOf(Doors.StepAssets.PassageScriptPath)));
                Assert.That(RefGuid(passage.Field("_target")), Is.EqualTo(GuidOf(Doors.RegionAssetPath)), $"{half.Name} leads to Rice Point");
                Assert.That(passage.Field("_target"), Is.EqualTo(assets.RicePoint.Yaml), $"{half.Name} points at Rice Point's def");
                Assert.That(passage.FieldRef("_loader"), Is.EqualTo(loader.FileId), $"{half.Name} travels by the scene's loader");
                Assert.That(passage.Field("_arrivalKey"), Is.EqualTo(half.ArrivalKey), $"{half.Name} asks for its way in");
                Assert.That(Number(passage, "_reentryCooldownSeconds"), Is.EqualTo(Doors.PassageGuardSeconds), $"{half.Name} keeps the re-fire guard");
                long policy = passage.FieldRef("_accessPolicy");
                if (half.OnFootOnly)
                {
                    Doc access = parts[3];
                    Assert.That(policy, Is.EqualTo(access.FileId), $"{half.Name} answers to its own on-foot policy");
                    Assert.That(Enabled(access), Is.True, $"{half.Name}'s policy is enabled");
                    Assert.That(RefGuid(access.Field("m_Script")), Is.EqualTo(GuidOf(Doors.StepAssets.AccessScriptPath)));
                }
                else Assert.That(policy, Is.EqualTo(0L), $"{half.Name} carries no policy");
                shape.Written.Add(go.FileId);
                foreach (Doc part in parts) shape.Written.Add(part.FileId);
                shape.Roots.Add("  - " + Ref(tr.FileId));
            }

            string[] regions = ListLines(loader, "_regions").Select(l => RefGuid(l)).ToArray();
            string[] listed = new[] { "NineMileCreek", "CoddleCove", "StPeters", "WestWater", Doors.RegionAssetName }
                .Select(n => GuidOf(RegionsFolder + n + ".asset")).ToArray();
            Assert.That(regions, Is.EqualTo(listed), "the loader lists the creek's regions in the builder's order, Rice Point last");
            shape.Region = "  - " + assets.RicePoint.Yaml;

            Doc anchorGo = s.RootNamed(NineMileCreekGroundSteps.AnchorName);
            Doc anchorTr = s.TransformOf(anchorGo);
            Doc anchor = s.ComponentsOf(anchorGo).Single(d => d.IsScript("HiddenHarbours.App.RegionAnchor"));
            Assert.That(Placed(s, anchorGo), Is.EqualTo(Vector3.zero), "the anchor stands at the origin, so its points' local places are the world's");
            shape.AnchorTransform = anchorTr.FileId;
            shape.Anchor = anchor.FileId;
            foreach (Doors.WayIn way in Doors.WaysIn)
            {
                long boat = 0;
                if (way.Boat.HasValue) boat = Point(s, anchorTr, way.Key, way.Boat.Value, shape);
                else Assert.That(NamesUnder(s, anchorTr), Does.Not.Contain(way.Key), $"{way.Key} is a walk-in: no boat point");
                long walker = Point(s, anchorTr, way.Key + Doors.ShoreSuffix, way.Walker, shape);
                shape.Ways.AddRange(new[] { "  - Key: " + way.Key, "    ArrivalPoint: " + Ref(boat), "    DisembarkPoint: " + Ref(walker) });
            }
            string[] arrivals = ListLines(anchor, "_arrivals");
            Assert.That(arrivals.Skip(Math.Max(0, arrivals.Length - shape.Ways.Count)), Is.EqualTo(shape.Ways),
                "Rice Point's ways in close the anchor's list");
            foreach (Doors.WayIn way in Doors.WaysIn)
                Assert.That(arrivals.Count(l => l == "  - Key: " + way.Key), Is.EqualTo(1), $"the anchor names {way.Key} once");
            return shape;
        }

        /// <summary>One point under the anchor, by name: a bare GameObject where the builder puts it. Returns its transform.</summary>
        static long Point(SceneYaml s, Doc anchorTr, string name, Vector3 at, StepShape shape)
        {
            Doc go = s.ChildNamed(anchorTr, name);
            List<Doc> parts = s.ComponentsOf(go);
            Assert.That(parts.Select(d => Kind(d)), Is.EqualTo(new[] { "Transform" }), $"{name} is a bare point");
            Assert.That(Active(go), Is.True, $"{name} is active");
            Assert.That(Placed(s, go), Is.EqualTo(at), $"{name} stands where the builder puts it");
            shape.Written.Add(go.FileId);
            shape.Written.Add(parts[0].FileId);
            shape.Children.Add("  - " + Ref(parts[0].FileId));
            return parts[0].FileId;
        }

        /// <summary>An edit takes out exactly <paramref name="removed"/> and puts in exactly <paramref name="inserted"/>:
        /// the lines between the two documents' common head and common tail.</summary>
        static void Changed(SceneYaml was, SceneYaml now, long id, IReadOnlyList<string> removed, IReadOnlyList<string> inserted, string what)
        {
            string[] a = was.Get(id).Lines, b = now.Get(id).Lines;
            int head = 0;
            while (head < a.Length && head < b.Length && a[head] == b[head]) head++;
            int tail = 0;
            while (tail < a.Length - head && tail < b.Length - head && a[a.Length - 1 - tail] == b[b.Length - 1 - tail]) tail++;
            Assert.That(a.Skip(head).Take(a.Length - head - tail), Is.EqualTo(removed), $"{what}: the lines it takes out of &{id}");
            Assert.That(b.Skip(head).Take(b.Length - head - tail), Is.EqualTo(inserted), $"{what}: the lines it puts in &{id}");
        }

        /// <summary>A seat to drive from: a truck's cab, or an ATV's saddle, which has a door each side and shows its rider.</summary>
        sealed class TestSeat : IDriveSeat
        {
            readonly bool _astride;
            public TestSeat(string who, Transform root, bool astride) { VehicleId = "vehicle.test." + who.Replace(' ', '_'); Root = root; _astride = astride; }
            public Transform Root { get; }
            public string VehicleId { get; }
            public bool IsDrivable => true;
            public bool IsAlive => Root != null;
            public float CameraWorldHeightMeters => 18f;
            public Vector2 DoorWorldPosition => Root.position;
            public bool HasAltDoor => _astride;
            public Vector2 AltDoorWorldPosition => Root.position;
            public bool ShowsDriver => _astride;
            public Vector2 DriverSeatWorldPosition => Root.position;
            public float DriverSeatHeightMeters => 0f;
            public void TakeControls() { }
            public void SetDriveInput(float throttle, float steer, bool brake) { }
            public void ReleaseControls() { }
        }

        readonly List<Object> _spawned = new();
        T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }
        GameObject Go(string name, Vector3 at) { var g = Track(new GameObject(name)); g.transform.position = at; return g; }

        [TearDown]
        public void TearDown()
        {
            if (_spawned.Count == 0) return;   // the text tests touch no engine state
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            Reset();
        }

        static void Reset()
        {
            EventBus.Clear<ActiveBoatChanged>(); EventBus.Clear<ActiveVehicleChanged>(); EventBus.Clear<ControlModeChanged>();
            GameServices.Reset(); ShellFlow.Reset();
            Interactables.Clear(); InteractVerb.Reset(); InteractionGate.Reset();
            InteractActionClaim.Reset(); InteractOffer.Reset(); InteractActorProbe.Reset();
        }
    }
}
