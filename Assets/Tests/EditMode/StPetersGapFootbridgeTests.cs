using System.IO;
using System.Linq;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The ground by the cottage above the Whelps: the cliff walk passes in front of the house and not under
    /// it, a spur goes to the door, the walk crosses the Gap brook on a plank bridge that is data, and nothing is
    /// planted on any of it.</summary>
    public class StPetersGapFootbridgeTests
    {
        const string BridgeId = "bridge.stp_gap_footbridge";
        const string KeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_stp_gap_bridge.asset";
        const string BrookPath = "Assets/_Project/Data/Terrain/StPetersPlan/Stream_GapBrook.asset";
        const string PlanPath = "Assets/_Project/Data/Terrain/StPetersPlan/TerrainPlan_StPeters.asset";

        static PathDef Path(string asset) => AssetDatabase.LoadAssetAtPath<PathDef>(asset);

        [Test]
        public void TheBridge_IsData_TheKeyScenesPiece_AndDrawnForItsSize()
        {
            var defs = LoadFootbridgeDefs();
            Assert.That(defs.Select(d => d.Id), Is.EqualTo(new[] { BridgeId }));
            FootbridgeDef def = defs[0];
            var keyScene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(KeyScenePath);
            Assert.That(keyScene, Is.Not.Null);
            Assert.That(def.KeySceneId, Is.EqualTo(keyScene.Id));
            var piece = keyScene.Pieces.Single(p => p.Id == def.PieceId);
            Assert.That(Vector2.Distance(piece.At, def.At), Is.LessThan(0.01f), "the bridge stands where the key scene puts it");
            Assert.That(def.GroundMetres, Is.EqualTo(piece.Z).Within(0.001f), "on the ground the key scene's footings ask for");

            FootbridgeContract drawn = LoadFootbridgeContract();
            Assert.That(drawn.spanMetres, Is.EqualTo(def.SpanMetres).Within(0.001f));
            Assert.That(drawn.widthMetres, Is.EqualTo(def.WidthMetres).Within(0.001f));
            var bridge = LoadFootbridges(drawn).Single();
            var deck = AssetDatabase.LoadAssetAtPath<Sprite>(FootbridgeArt + "/" + drawn.deck + ".png");
            Assert.That(deck.pixelsPerUnit, Is.EqualTo(drawn.pixelsPerMetre));
            Assert.That(deck.rect.width, Is.EqualTo(def.SpanMetres * drawn.pixelsPerMetre + 2).Within(0.5f), "the planks are as long as the span");
            Assert.That(bridge.Footprint, Is.EqualTo(def.Footprint));
            Assert.That(bridge.Standing.yMin, Is.EqualTo(def.Footprint.yMin), "the standing ground starts at the planks' near edge");
            Assert.That(bridge.Standing.yMax, Is.EqualTo(def.Footprint.yMax + def.StandingReachMetres).Within(0.0001f),
                "and reaches landward as far as a walker's body is read from his feet");
            Assert.That(bridge.DeckMetres, Is.EqualTo(def.GroundMetres + def.DeckRiseMetres).Within(0.0001f));
        }

        [Test]
        public void TheScene_HoldsExactlyTheBridgeItsStepWouldWrite()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var bridges = LoadFootbridges(LoadFootbridgeContract());
            LayerPatch patch = Footbridges(scene, bridges, new EditorFootbridgeAssets());
            Assert.That(patch.IsEmpty, Is.True, "StPeters.unity is not what the footbridges step writes:\n" + patch.Summary());

            var owned = scene.SubtreeOf(scene.RootNamed(FootbridgesRootName).FileId);
            var platform = scene.Docs.Single(d => owned.Contains(d.FileId) && d.IsScript(typeof(StandablePlatform).FullName));
            Assert.That(platform.Field("_id"), Is.EqualTo(BridgeId));
            Assert.That(ParseFloat(platform.Field("_deckElevation")), Is.EqualTo(bridges[0].DeckMetres).Within(0.0001f));
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 212), Is.EqualTo(3), "the planks and the two rails");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 60), Is.EqualTo(2), "only the rails are walls");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == FootbridgeDeckName),
                Is.EqualTo(1), "the planks carry the name the sun's rules leave alone");
            var rules = Resources.Load<HiddenHarbours.Art.SunCasterRules>(nameof(HiddenHarbours.Art.SunCasterRules));
            Assert.That(rules.ExcludedNames, Does.Contain("*Floor*"), "the rules no longer leave a floor alone");

            // The rails lie just outside the deck's two long edges: the whole width of the planks is walked, a walker
            // on them is north of the near rail and south of the far one, and neither closes the ends.
            Rect foot = bridges[0].Footprint;
            Vector2[] nearRail = FootbridgeRailLine(bridges[0], near: true), farRail = FootbridgeRailLine(bridges[0], near: false);
            Assert.That(nearRail.Max(p => p.y) + bridges[0].At.y, Is.EqualTo(foot.yMin).Within(0.0001f));
            Assert.That(farRail.Min(p => p.y) + bridges[0].At.y, Is.EqualTo(foot.yMax).Within(0.0001f));
            foreach (Vector2[] rail in new[] { nearRail, farRail })
            {
                Assert.That(rail.Min(p => p.x) + bridges[0].At.x, Is.GreaterThan(foot.xMin));
                Assert.That(rail.Max(p => p.x) + bridges[0].At.x, Is.LessThan(foot.xMax));
            }
            int Order(string name) => ParseInt(scene.ComponentsOf(scene.Docs.Single(d => owned.Contains(d.FileId) && d.ClassId == 1 &&
                d.Field("m_Name") == name)).Single(d => d.ClassId == 212).Field("m_SortingOrder"));
            Assert.That(Order(FootbridgeDeckName), Is.LessThan(Order(FootbridgeFarRailName)), "the planks lie under the far rail");
            Assert.That(Order(FootbridgeFarRailName), Is.LessThan(Order(FootbridgeNearRailName)), "the far rail is drawn before the near one");
        }

        [Test]
        public void TheCliffWalk_CrossesTheBrookOnThePlanks()
        {
            FootbridgeDef def = LoadFootbridgeDefs().Single();
            Rect deck = def.Footprint;
            PathDef walk = Path(TroddenPathAssets[0]);
            Assert.That(walk.Id, Is.EqualTo("path.stp_cliff_walk"));
            int on = -1;
            for (int i = 0; i + 1 < walk.Points.Length; i++)
            {
                Vector2 a = walk.Points[i], b = walk.Points[i + 1];
                if (Mathf.Abs(a.y - def.At.y) < 0.01f && Mathf.Abs(b.y - def.At.y) < 0.01f &&
                    Mathf.Min(a.x, b.x) <= deck.xMin + 0.06f && Mathf.Max(a.x, b.x) >= deck.xMax - 0.06f) on = i;
            }
            Assert.That(on, Is.GreaterThanOrEqualTo(0), "no stretch of the cliff walk runs the deck from end to end along its middle");

            // The brook runs under the deck: one of its stretches crosses the deck's middle line inside the span.
            var brook = AssetDatabase.LoadAssetAtPath<StreamDef>(BrookPath);
            Assert.That(brook, Is.Not.Null);
            bool under = false;
            for (int i = 0; i + 1 < brook.Points.Length; i++)
            {
                Vector2 a = brook.Points[i], b = brook.Points[i + 1];
                if ((a.y - def.At.y) * (b.y - def.At.y) > 0f) continue;
                float x = Mathf.Lerp(a.x, b.x, Mathf.InverseLerp(a.y, b.y, def.At.y));
                if (x > deck.xMin + 0.5f && x < deck.xMax - 0.5f) under = true;
            }
            Assert.That(under, Is.True, "the brook does not run under the planks");
        }

        [Test]
        public void ThePaths_PassTheCottage_AndGoToItsDoor_NotUnderIt()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            StPetersBuildingClearance.Zone cottage = KeeperCottageZones(scene).Single();
            var plan = AssetDatabase.LoadAssetAtPath<RegionTerrainPlanDef>(PlanPath);
            foreach (string asset in TroddenPathAssets)
            {
                PathDef path = Path(asset);
                Assert.That(path, Is.Not.Null, asset);
                Assert.That(plan.Paths, Does.Contain(path), path.Id + " is not in the island's plan");
                bool door = path.Id == "path.stp_whelps_cottage_door";
                for (int i = 0; i + 1 < path.Points.Length; i++)
                for (float t = 0f; t <= 1f; t += 0.05f)
                {
                    Vector2 p = Vector2.Lerp(path.Points[i], path.Points[i + 1], t);
                    if (!TroddenGroundWindow.Contains(p)) continue;
                    float clear = cottage.DistanceTo(p) - path.Width * 0.5f;
                    if (door) Assert.That(cottage.DistanceTo(p), Is.GreaterThan(0f), "the door path runs into the house");
                    else Assert.That(clear, Is.GreaterThan(StPetersBuildingClearance.ClearanceMetres),
                        $"{path.Id} at {p} is {clear:0.00} m off the cottage: it runs under the house or its porch");
                }
            }
            PathDef spur = Path(TroddenPathAssets[3]);
            Assert.That(cottage.DistanceTo(spur.Points[0]), Is.LessThan(2.5f), "the door path does not reach the porch");
            PathDef walk = Path(TroddenPathAssets[0]);
            Vector2 end = spur.Points[spur.Points.Length - 1];
            Assert.That(walk.Points.Min(p => Vector2.Distance(p, end)), Is.LessThan(0.05f), "the door path does not meet the cliff walk");
        }

        [Test]
        public void NothingIsPlantedOnTheWalkedGround()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var ground = TroddenGround(TroddenPathAssets.Select(Path), LoadFootbridgeDefs());
            Assert.That(ground.Count, Is.GreaterThan(8), "the walk, the spurs and the bridge");
            var sites = CommittedPlantSites(scene);
            Assert.That(sites.Count, Is.GreaterThan(1000), "an empty field would pass for nothing");
            var on = sites.Where(s => TroddenGroundWindow.Contains(s) && ground.Any(g => g.Holds(s))).ToList();
            Assert.That(on.Count, Is.EqualTo(0), "plants root on the walked ground, the first at " + (on.Count > 0 ? on[0].ToString() : ""));
        }
    }
}
