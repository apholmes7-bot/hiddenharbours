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
    /// it, a spur goes to the door, the walk crosses the Gap brook on a plank bridge that is data, set square to the
    /// brook and walked the way it is drawn, and nothing is planted on any of it.</summary>
    public class StPetersGapFootbridgeTests
    {
        const string BridgeId = "bridge.stp_gap_footbridge";
        const string KeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_stp_gap_bridge.asset";
        const string BrookPath = "Assets/_Project/Data/Terrain/StPetersPlan/Stream_GapBrook.asset";
        const string PlanPath = "Assets/_Project/Data/Terrain/StPetersPlan/TerrainPlan_StPeters.asset";

        static PathDef Path(string asset) => AssetDatabase.LoadAssetAtPath<PathDef>(asset);

        /// <summary>How far from square to the brook the bridge may stand, degrees.</summary>
        const float SquareToleranceDegrees = 1f;

        /// <summary>How far the planks as drawn, and the ground as walked, may stand from the bridge's axis, degrees.
        /// The drawn axis is measured off the picture's pixels, which the sills and the shade pull a little.</summary>
        const float DrawnAxisToleranceDegrees = 3f;

        /// <summary>The sharpest the cliff walk may turn where it comes off the planks onto the bank, degrees.</summary>
        const float GentlestBendDegrees = 30f;

        static StreamDef Brook() => AssetDatabase.LoadAssetAtPath<StreamDef>(BrookPath);

        /// <summary>The brook's stretch nearest the bridge's middle, and the nearest point on it.</summary>
        static (Vector2 A, Vector2 B, Vector2 Nearest) CrossingStretch(StreamDef brook, Vector2 at)
        {
            (Vector2, Vector2, Vector2) best = default;
            float bestD = float.MaxValue;
            for (int i = 0; i + 1 < brook.Points.Length; i++)
            {
                Vector2 a = brook.Points[i], b = brook.Points[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);
                Vector2 p = a + ab * t;
                float d = Vector2.Distance(p, at);
                if (d < bestD) { bestD = d; best = (a, b, p); }
            }
            return best;
        }

        static float AngleBetweenLines(Vector2 a, Vector2 b)
        {
            float deg = Vector2.Angle(a, b);
            return Mathf.Min(deg, 180f - deg);
        }

        [Test]
        public void TheBridge_IsData_TheKeyScenesPiece_AndDrawnForItsSizeAndAxis()
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
            Assert.That(drawn.axisDegrees, Is.EqualTo(def.AxisDegrees).Within(FootbridgeAxisToleranceDegrees), "the planks were drawn at the bridge's axis");
            var bridge = LoadFootbridges(drawn).Single();
            var deck = AssetDatabase.LoadAssetAtPath<Sprite>(FootbridgeArt + "/" + drawn.deck + ".png");
            Assert.That(deck.pixelsPerUnit, Is.EqualTo(drawn.pixelsPerMetre));
            Vector2 run = def.Axis * def.SpanMetres * drawn.pixelsPerMetre;
            Assert.That(deck.rect.width, Is.GreaterThanOrEqualTo(Mathf.Abs(run.x)), "the picture holds the planks' run across the screen");
            Assert.That(deck.rect.height, Is.GreaterThanOrEqualTo(Mathf.Abs(run.y)), "and up it");
            Assert.That(bridge.AxisDegrees, Is.EqualTo(def.AxisDegrees));
            Assert.That(bridge.SpanMetres, Is.EqualTo(def.SpanMetres));
            Assert.That(bridge.WidthMetres, Is.EqualTo(def.WidthMetres));
            Assert.That(bridge.StandingReachMetres, Is.EqualTo(def.StandingReachMetres));
            Assert.That(bridge.GuideConeDegrees, Is.EqualTo(def.GuideConeDegrees));
            Assert.That(bridge.GuideApproachMetres, Is.EqualTo(def.GuideApproachMetres));
            Assert.That(bridge.DeckMetres, Is.EqualTo(def.GroundMetres + def.DeckRiseMetres).Within(0.0001f));
        }

        /// <summary>The owner (11-26): a bridge crosses its river at a right angle. The deck's axis is square to the
        /// brook's course where it crosses, and the brook passes under the middle of the planks.</summary>
        [Test]
        public void TheBridge_CrossesTheBrook_AtARightAngle()
        {
            FootbridgeDef def = LoadFootbridgeDefs().Single();
            StreamDef brook = Brook();
            Assert.That(brook, Is.Not.Null);
            var (a, b, nearest) = CrossingStretch(brook, def.At);
            float offSquare = Mathf.Abs(90f - Vector2.Angle(def.Axis, b - a));
            Assert.That(offSquare, Is.LessThanOrEqualTo(SquareToleranceDegrees),
                $"the deck runs at {def.AxisDegrees:0.00} degrees and the brook at {Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg:0.00}: " +
                $"{offSquare:0.00} degrees off square");
            Assert.That(Vector2.Distance(nearest, def.At), Is.LessThan(0.1f), "the brook runs under the middle of the planks");
            // Bank to bank: the planks reach past the water on both sides, so each end sits on a sill on dry ground.
            float width = brook.Widths.Length > 0 ? brook.Widths.Max() : 0f;
            Assert.That(def.SpanMetres, Is.GreaterThan(width + 2f * brook.Bank.x), "the planks do not reach the banks");
        }

        /// <summary>The ground that is walked runs the way the planks are drawn: the scene's deck, its outline and its
        /// rails all lie along the axis the picture was drawn at, and the picture's own long axis is that axis.</summary>
        [Test]
        public void TheWalkedFootprint_LiesAlongTheDrawnAxis()
        {
            FootbridgeContract drawn = LoadFootbridgeContract();
            FootbridgeDef def = LoadFootbridgeDefs().Single();
            Vector2 axis = FootbridgeDef.AxisOf(drawn.axisDegrees);

            // The picture: the long axis of its opaque pixels (screen y flipped to the world's).
            var tex = new Texture2D(2, 2);
            try
            {
                Assert.That(tex.LoadImage(File.ReadAllBytes(FootbridgeArt + "/" + drawn.deck + ".png")), Is.True);
                double n = 0, mx = 0, my = 0, sxx = 0, syy = 0, sxy = 0;
                Color32[] px = tex.GetPixels32();
                for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    if (px[y * tex.width + x].a < 255) continue;
                    n++; mx += x; my += y; sxx += (double)x * x; syy += (double)y * y; sxy += (double)x * y;
                }
                Assert.That(n, Is.GreaterThan(1000), "the planks are not drawn");
                mx /= n; my /= n;
                double cxx = sxx / n - mx * mx, cyy = syy / n - my * my, cxy = sxy / n - mx * my;
                // GetPixels32 runs bottom row first, so its y is already the world's up.
                float drawnDegrees = (float)(0.5 * System.Math.Atan2(2 * cxy, cxx - cyy) * 180.0 / System.Math.PI);
                Assert.That(AngleBetweenLines(FootbridgeDef.AxisOf(drawnDegrees), axis), Is.LessThan(DrawnAxisToleranceDegrees),
                    $"the planks are drawn at {drawnDegrees:0.00} degrees, the contract says {drawn.axisDegrees:0.00}");
            }
            finally { Object.DestroyImmediate(tex); }

            // The walked ground: the scene's deck component, and the outline it answers.
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var owned = scene.SubtreeOf(scene.RootNamed(FootbridgesRootName).FileId);
            var deckDoc = scene.Docs.Single(d => owned.Contains(d.FileId) && d.IsScript(typeof(FootbridgeDeck).FullName));
            Assert.That(ParseFloat(deckDoc.Field("_axisDegrees")), Is.EqualTo(drawn.axisDegrees).Within(FootbridgeAxisToleranceDegrees));
            var go = new GameObject("deck");
            try
            {
                var standing = go.AddComponent<FootbridgeDeck>();
                standing.Configure(def.Id, def.At, ParseFloat(deckDoc.Field("_axisDegrees")), ParseFloat(deckDoc.Field("_spanMetres")),
                    ParseFloat(deckDoc.Field("_widthMetres")), ParseFloat(deckDoc.Field("_standingReachMetres")), def.DeckMetres, def.GuideConeDegrees,
                    def.GuideApproachMetres);
                var corners = new Vector2[4]; var heights = new float[4];
                Assert.That(standing.GetDrawnOutline(corners, heights), Is.EqualTo(4));
                Vector2 longEdge = corners[1] - corners[0], shortEdge = corners[3] - corners[0];
                Assert.That(longEdge.magnitude, Is.GreaterThan(shortEdge.magnitude), "the outline's first edge is its long one");
                Assert.That(AngleBetweenLines(longEdge, axis), Is.LessThan(0.01f), "the walked footprint's long axis is the drawn axis");
                Assert.That(longEdge.magnitude, Is.EqualTo(drawn.spanMetres).Within(0.001f));
                // Each end of the planks is stood on; feet a step past an end along the axis, or past a long edge, are
                // not on the planks. (A point past the raised east end can still be read as on the deck: it is up the
                // world from the planks, where a walker's body is drawn while his feet are on them. FootbridgeDeckTests.)
                foreach (float s in new[] { -1f, 1f })
                {
                    Assert.That(standing.TryGetDeckElevation(def.At + axis * (s * (def.SpanMetres * 0.5f - 0.05f)), out _), Is.True);
                    Assert.That(FootbridgeDeck.OnPlanks(def.At + axis * (s * (def.SpanMetres * 0.5f + 0.05f)), standing.At,
                        standing.AxisDegrees, standing.SpanMetres, standing.WidthMetres), Is.False);
                }
                Assert.That(standing.TryGetDeckElevation(def.At - axis * (def.SpanMetres * 0.5f + 0.05f), out _), Is.False,
                    "past the low west end is the bank");
                Assert.That(standing.TryGetDeckElevation(def.At - def.Across * (def.WidthMetres * 0.5f + 0.05f), out _), Is.False,
                    "south of the near rail is the brook, not the planks");
            }
            finally { Object.DestroyImmediate(go); }

            // The rails' walls run along that axis too.
            foreach (var wall in scene.Docs.Where(d => owned.Contains(d.FileId) && d.ClassId == 60))
            {
                Vector2[] pts = ColliderPoints(wall);
                Assert.That(pts.Length, Is.EqualTo(4));
                Assert.That(AngleBetweenLines(pts[1] - pts[0], axis), Is.LessThan(0.05f), "a rail stands across the walk");
            }
        }

        /// <summary>The first path of a PolygonCollider2D document's points.</summary>
        static Vector2[] ColliderPoints(Doc wall)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(wall.Text,
                         @"- \{x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+)\}"))
                pts.Add(new Vector2(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value)));
            return pts.ToArray();
        }

        [Test]
        public void TheScene_HoldsExactlyTheBridgeItsStepWouldWrite()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var bridges = LoadFootbridges(LoadFootbridgeContract());
            LayerPatch patch = Footbridges(scene, bridges, new EditorFootbridgeAssets());
            Assert.That(patch.IsEmpty, Is.True, "StPeters.unity is not what the footbridges step writes:\n" + patch.Summary());

            var owned = scene.SubtreeOf(scene.RootNamed(FootbridgesRootName).FileId);
            var platform = scene.Docs.Single(d => owned.Contains(d.FileId) && d.IsScript(typeof(FootbridgeDeck).FullName));
            Assert.That(platform.Field("_id"), Is.EqualTo(BridgeId));
            Assert.That(ParseFloat(platform.Field("_deckElevation")), Is.EqualTo(bridges[0].DeckMetres).Within(0.0001f));
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(StandablePlatform).FullName)), Is.EqualTo(0),
                "the upright rectangle the bridge used to be is gone");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 212), Is.EqualTo(3), "the planks and the two rails");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 60), Is.EqualTo(2), "only the rails are walls");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == FootbridgeDeckName),
                Is.EqualTo(1), "the planks carry the name the sun's rules leave alone");
            var rules = Resources.Load<HiddenHarbours.Art.SunCasterRules>(nameof(HiddenHarbours.Art.SunCasterRules));
            Assert.That(rules.ExcludedNames, Does.Contain("*Floor*"), "the rules no longer leave a floor alone");

            // The rails lie just outside the deck's two long edges: the whole width of the planks is walked, a walker
            // on them is landward of the near rail and seaward of the far one, and neither closes the ends.
            Footbridge b = bridges[0];
            float Across(Vector2 p) => Vector2.Dot(p, b.Across);
            float Along(Vector2 p) => Vector2.Dot(p, b.Axis);
            Vector2[] nearRail = FootbridgeRailLine(b, near: true), farRail = FootbridgeRailLine(b, near: false);
            Assert.That(nearRail.Max(Across), Is.EqualTo(-b.WidthMetres * 0.5f).Within(0.0001f));
            Assert.That(farRail.Min(Across), Is.EqualTo(b.WidthMetres * 0.5f).Within(0.0001f));
            foreach (Vector2[] rail in new[] { nearRail, farRail })
            {
                Assert.That(rail.Min(Along), Is.GreaterThan(-b.SpanMetres * 0.5f));
                Assert.That(rail.Max(Along), Is.LessThan(b.SpanMetres * 0.5f));
            }
            int Order(string name) => ParseInt(scene.ComponentsOf(scene.Docs.Single(d => owned.Contains(d.FileId) && d.ClassId == 1 &&
                d.Field("m_Name") == name)).Single(d => d.ClassId == 212).Field("m_SortingOrder"));
            Assert.That(Order(FootbridgeDeckName), Is.LessThan(Order(FootbridgeFarRailName)), "the planks lie under the far rail");
            Assert.That(Order(FootbridgeFarRailName), Is.LessThan(Order(FootbridgeNearRailName)), "the far rail is drawn before the near one");
            // The near rail sorts from its lowest end, the far one from its highest end and a body's height behind.
            Assert.That(FootbridgeRailSortOffset(b, near: true), Is.LessThan(0f));
            Assert.That(FootbridgeRailSortOffset(b, near: false), Is.GreaterThan(b.FarRailSortsBehindMetres));
        }

        /// <summary>The cliff walk comes onto the planks along the bridge's own axis and leaves them the same way, so
        /// a walker crosses straight; where it turns back to its course on each bank the bend is a gentle one.</summary>
        [Test]
        public void TheCliffWalk_MeetsBothEnds_AlongTheBridgesAxis()
        {
            FootbridgeDef def = LoadFootbridgeDefs().Single();
            PathDef walk = Path(TroddenPathAssets[0]);
            Assert.That(walk.Id, Is.EqualTo("path.stp_cliff_walk"));
            int on = -1;
            for (int i = 0; i + 1 < walk.Points.Length; i++)
            {
                Vector2 a = FootbridgeDeck.ToDeck(walk.Points[i], def.At, def.AxisDegrees);
                Vector2 b = FootbridgeDeck.ToDeck(walk.Points[i + 1], def.At, def.AxisDegrees);
                if (Mathf.Abs(a.y) < 0.02f && Mathf.Abs(b.y) < 0.02f &&
                    Mathf.Min(a.x, b.x) <= -def.SpanMetres * 0.5f && Mathf.Max(a.x, b.x) >= def.SpanMetres * 0.5f) on = i;
            }
            Assert.That(on, Is.GreaterThanOrEqualTo(1), "no stretch of the cliff walk runs the deck from end to end along its axis");
            Assert.That(on + 2, Is.LessThan(walk.Points.Length));
            Vector2 run = walk.Points[on + 1] - walk.Points[on];
            float before = Vector2.Angle(walk.Points[on] - walk.Points[on - 1], run);
            float after = Vector2.Angle(run, walk.Points[on + 2] - walk.Points[on + 1]);
            Assert.That(before, Is.LessThan(GentlestBendDegrees), "the walk turns hard onto the planks");
            Assert.That(after, Is.LessThan(GentlestBendDegrees), "the walk turns hard off the planks");
            foreach (int end in new[] { on, on + 1 })
                Assert.That(Mathf.Abs(FootbridgeDeck.ToDeck(walk.Points[end], def.At, def.AxisDegrees).x), Is.GreaterThan(def.SpanMetres * 0.5f),
                    "the walk bends on the planks, not on the bank");

            // The east gap path still leaves from the walk.
            PathDef gap = Path(TroddenPathAssets[2]);
            Vector2 start = gap.Points[0];
            float toWalk = float.MaxValue;
            for (int i = 0; i + 1 < walk.Points.Length; i++)
            {
                Vector2 a = walk.Points[i], ab = walk.Points[i + 1] - a;
                toWalk = Mathf.Min(toWalk, Vector2.Distance(start, a + ab * Mathf.Clamp01(Vector2.Dot(start - a, ab) / ab.sqrMagnitude)));
            }
            Assert.That(toWalk, Is.LessThan(0.05f), "the east gap path does not leave from the cliff walk");

            // The brook runs under the deck: it crosses the deck's axis inside the span.
            var (sa, sb, _) = CrossingStretch(Brook(), def.At);
            Vector2 da = FootbridgeDeck.ToDeck(sa, def.At, def.AxisDegrees), db = FootbridgeDeck.ToDeck(sb, def.At, def.AxisDegrees);
            Assert.That(da.y * db.y, Is.LessThanOrEqualTo(0f), "the brook does not cross the bridge's line");
            float crossesAt = Mathf.Lerp(da.x, db.x, Mathf.InverseLerp(da.y, db.y, 0f));
            Assert.That(Mathf.Abs(crossesAt), Is.LessThan(def.SpanMetres * 0.5f - 0.5f), "the brook does not run under the planks");
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
