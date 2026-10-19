using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;
using G = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>WHERE THE PLAYER LANDS AT NINE MILE CREEK, AND THE LOOP THEY WALK</b> (NMC H2, PR 2): the activated
    /// scene's ground readers, the berth the boat is laid alongside, the counters the first loop visits and the walk
    /// between them, the marks that lead in from the sea, and the Otter parked on the yard, held to G3. Every fixture
    /// reads NineMileCreek.unity as N1 writes it (<see cref="NineMileCreekLiveGround.Scene"/>) and probes G3 through
    /// PR 1's seam (<see cref="NineMileCreekLiveGround.Plan"/>), so what passes is what ships.
    ///
    /// <para><b>Whose numbers.</b> The scope's (PR 2 rows 0 and 10 to 14), measured by Phase A on G3 before the scene
    /// moved; each test prints what it measured. The walk is Phase A's walker (<see cref="Walker"/>) on the live
    /// scene's solids: a grid search, so it proves a clear, dry way exists and how long it is. The player's own
    /// steering along it, the boarding rail and the talk at the counters are PlayMode's.</para>
    /// </summary>
    public class NineMileCreekSceneArrivalTests
    {
        static float High => NineMileCreekMainland.SpringHighWater;
        static float Low => NineMileCreekMainland.SpringLowWater;
        static float Ground(Vector2 at) => Plan.ElevationAt(at);
        static string At2(Vector2 v) => $"({v.x:0.######}, {v.y:0.######})";
        static Doc GameObjectOf(Doc component) => Scene.Require(component.FieldRef("m_GameObject"), "a component's GameObject");

        /// <summary>The scene's persistent providers: the GameObject whose proxies hand every seller the player's
        /// wallet and hold.</summary>
        const long Providers = 957678902;
        const long PlayerGameObject = 992535032, PlayerTransform = 992535033;
        const int CircleCollider2DClass = 58, Rigidbody2DClass = 50;

        /// <summary>A kept component on its kept, named GameObject (and, unless <paramref name="transform"/> is 0, its
        /// kept transform), running the named script.</summary>
        static Doc KeptComponent(long id, long go, long transform, string name, string script)
        {
            Doc c = Scene.Require(id, name + "'s " + script);
            Doc owner = GameObjectOf(c);
            Assert.That(owner.FileId, Is.EqualTo(go), $"&{id} stays on {name}");
            Assert.That(Name(owner), Is.EqualTo(name), $"&{go} keeps its name");
            if (transform != 0) Assert.That(Scene.TransformOf(owner).FileId, Is.EqualTo(transform), $"{name} keeps its transform");
            Assert.That(c.IsScript(script), Is.True, $"&{id} is {name}'s {script}");
            Assert.That(Live(owner) && Enabled(c), Is.True, $"{name}'s {script} runs");
            return c;
        }

        /// <summary>A GameObject standing where it stood, on ground that is dry at spring high.</summary>
        static void Stands(Doc go, Vector2 at)
        {
            Vector2 p = WorldPosition(go);
            Assert.That(Vector2.Distance(p, at), Is.LessThan(1e-3f), $"{Name(go)} stands where it stood, {At2(at)}: {At2(p)}");
            Assert.That(Ground(p), Is.GreaterThan(High), $"{Name(go)} stands on dry ground at spring high");
        }

        /// <summary>The catalog the counters sell from.</summary>
        const string Catalog = "Assets/_Project/Data/Resources/Catalog/";

        /// <summary>A seller's counter: its seller id, its offer and the player's wallet, where it stood.</summary>
        static void Counter(Doc seller, string sellerId, string offerField, string offer, Vector2 at)
        {
            Doc go = GameObjectOf(seller);
            Assert.That(seller.Field("_sellerId"), Is.EqualTo(sellerId), $"{Name(go)} sells as {sellerId}");
            Assert.That(RefGuid(seller.Field(offerField)), Is.EqualTo(GuidOf(offer)),
                $"{Name(go)} still sells {Path.GetFileNameWithoutExtension(offer)}");
            Assert.That(seller.FieldRef("_walletProvider"), Is.EqualTo(Providers), $"{Name(go)} takes the player's wallet");
            Stands(go, at);
            TestContext.WriteLine($"{Name(go)} ({sellerId}): at {At2(WorldPosition(go))}, G3 {Ground(at):0.######}");
        }

        // ---- row 0: the ground the scene reads -----------------------------------------------------------------------

        static Vector2 AssetVector(string text, string field)
        {
            Match m = Regex.Match(text, @"(?m)^  " + field + @": (\{[^}]*\})\r?$");
            if (!m.Success) throw new FormatException("no vector " + field);
            return Vector(m.Groups[1].Value);
        }

        static void DecodesG3(Doc doc, string min, string max, string centre, string size)
        {
            Assert.That(Number(doc, min), Is.EqualTo(NineMileCreekGround.Minimum), $"&{doc.FileId} decodes G3's range");
            Assert.That(Number(doc, max), Is.EqualTo(NineMileCreekGround.Maximum), $"&{doc.FileId} decodes G3's range");
            Assert.That(Vector(doc, centre), Is.EqualTo(NineMileCreekMainland.RegionWorldCenter), $"&{doc.FileId} lays G3 over the region");
            Assert.That(Vector(doc, size), Is.EqualTo(NineMileCreekMainland.RegionWorldSize), $"&{doc.FileId} lays G3 over the region");
        }

        /// <summary>The three readers of the region's ground, on the ids the scope keeps: the tidal terrain answers
        /// from PR 1's painted G3 map, and the splat and the sea decode G3's height over its own range and field.</summary>
        [Test]
        public void TheGroundUnderTheSceneIsG3_ItsThreeReadersOnTheirKeptIds()
        {
            string height = GuidOf(NineMileCreekGround.HeightPath);
            Doc terrain = KeptComponent(581129739, 581129738, 581129740, NineMileCreekGroundSteps.TerrainRootName,
                                        "HiddenHarbours.World.PaintedTidalTerrain");
            Assert.That(RefGuid(terrain.Field("m_Script")), Is.EqualTo(GuidOf(NineMileCreekGroundSteps.PaintedTerrainScriptPath)),
                "the terrain runs the painted terrain's script");
            Assert.That(RefGuid(terrain.Field("_map")), Is.EqualTo(GuidOf(NineMileCreekGround.MapPath)), "the terrain reads PR 1's G3 map");
            Assert.That(Number(terrain, "_fallbackElevation"), Is.LessThan(NineMileCreekGround.Minimum),
                "a terrain that lost its map answers deeper than any of G3: never dry land");
            Assert.That(Scene.Docs.Count(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain")), Is.Zero,
                "the analytic mainland ground stands down");
            Assert.That(Scene.Docs.Count(d => d.IsScript("HiddenHarbours.World.PaintedTidalTerrain")), Is.EqualTo(1), "one tidal terrain");

            string map = File.ReadAllText(NineMileCreekGround.MapPath);
            Assert.That(GuidField(map, "_heightTexture"), Is.EqualTo(height), "the map's height is G3's");
            Assert.That(GuidField(map, "_stillLevelTexture"), Is.EqualTo(GuidOf(NineMileCreekGround.StillPath)), "the map's still water is G3's");
            Assert.That(AssetNumber(map, "_minElevation"), Is.EqualTo(NineMileCreekGround.Minimum), "the map decodes G3's range");
            Assert.That(AssetNumber(map, "_maxElevation"), Is.EqualTo(NineMileCreekGround.Maximum), "the map decodes G3's range");
            Assert.That(AssetVector(map, "_worldCenter"), Is.EqualTo(NineMileCreekMainland.RegionWorldCenter), "the map lies over the region");
            Assert.That(AssetVector(map, "_worldSize"), Is.EqualTo(NineMileCreekMainland.RegionWorldSize), "the map lies over the region");

            Doc splat = KeptComponent(1396913515, 1396913514, 1396913516, NineMileCreekGroundSteps.SplatRootName,
                                      "HiddenHarbours.Art.TerrainSplatSurface");
            Assert.That(RefGuid(splat.Field("_heightTexture")), Is.EqualTo(height), "the splat paints by G3's height");
            DecodesG3(splat, "_heightMin", "_heightMax", "_worldCenter", "_worldSize");

            Doc sea = KeptComponent(1575258830, 1575258828, 1575258832, NineMileCreekGroundSteps.SeaRootName,
                                    "HiddenHarbours.Art.WaterSurface");
            Assert.That(ParseInt(sea.Field("_depthSource")), Is.EqualTo((int)HiddenHarbours.Art.WaterSurface.DepthSource.PaintedHeightMap),
                "the sea's depth comes from the painted height, as St Peters' does");
            Assert.That(sea.Field("_bakeHeightMap"), Is.EqualTo("1"), "the sea bakes its depth from that height");
            Assert.That(RefGuid(sea.Field("_paintedHeightTex")), Is.EqualTo(height), "the sea's depth is G3's");
            DecodesG3(sea, "_heightMin", "_heightMax", "_heightWorldCenter", "_heightWorldSize");
        }

        // ---- rows 10 and 11: the counters the loop visits ----------------------------------------------------------

        static readonly (long id, long go, long transform, string name, string script, string sellerId, string offerField, string offer, Vector2 at)[]
            PlateauCounters =
            {
                (291587380, 291587379, 291587384, "GeneralStore", "HiddenHarbours.Economy.GearShop", "seller.nmc_chandlery",
                 "_offer", Catalog + "Gear/Rod.asset", new Vector2(-206f, 116f)),
                (2136210945, 2136210944, 2136210949, "HarbourmasterOffice", "HiddenHarbours.Economy.LicenseVendor", "seller.nmc_harbourmaster",
                 "_license", Catalog + "Licenses/CodLicense.asset", new Vector2(-150f, 118f)),
            };

        /// <summary>The harbourmaster's office and the chandlery: PR 2 moves neither (their move is a later wave's).
        /// Each keeps its seller, its offer and the player's wallet where it stood, on the plateau's flat top: the
        /// scope's row 10, +6 with no ground delta. Their approach steps are the walk's.</summary>
        [Test]
        public void TheChandleryAndTheHarbourmasterKeepTheirCounters_OnThePlateau()
        {
            Doc providers = Scene.Require(Providers, "the persistent providers");
            Assert.That(providers.ClassId == 1 && Name(providers) == "PersistentProviders" && Live(providers), Is.True,
                "the sellers' wallet is the live persistent providers");
            Assert.That(Scene.ComponentsOf(providers).Count(c => c.IsScript("HiddenHarbours.App.PersistentWalletProxy") && Enabled(c)),
                Is.EqualTo(1), "the providers hand on the player's wallet");
            foreach (var c in PlateauCounters)
            {
                Doc seller = KeptComponent(c.id, c.go, c.transform, c.name, c.script);
                Counter(seller, c.sellerId, c.offerField, c.offer, c.at);
                Assert.That(Ground(c.at), Is.EqualTo(NineMileCreekGround.Maximum).Within(NineMileCreekGround.CodeMetres),
                    $"{c.name} stands on the plateau's top, as before");
            }
        }

        static readonly (long id, long go, long transform, string name, string sellerId, string offer, Vector2 at)[] YardSellers =
        {
            (1268888617, 1268888616, 1268888618, "UsedOutboardSeller", "seller.hectors_barrel", Catalog + "Shipwright/DoryOutboardOffer.asset",
             new Vector2(66.3862f, 111.25431f)),
            (1499338002, 1499338001, 1499338006, "ShipwrightDoryYard", "seller.nmc_yard", Catalog + "Shipwright/DamagedDoryOffer.asset",
             new Vector2(64f, 112f)),
            (2028377147, 2028377143, 2028377151, "ShipwrightShed", "seller.nmc_yard", Catalog + "Shipwright/PuntOffer.asset",
             new Vector2(-108f, 64f)),
        };

        /// <summary>Where the walk stands to use each counter: Phase A's approach steps, 3.5 m in front of the office
        /// and the store, and at the clam buyer's and the dory yard's speakers.</summary>
        static readonly Vector2 LicenceStep = new Vector2(-150f, 114.5f), RodStep = new Vector2(-206f, 112.5f),
                                ClamStep = new Vector2(90.5f, 109.5f), DoryStep = new Vector2(68.182f, 111.25f);

        static readonly (long sprite, long go, long transform, long interactable, string name, Vector2 at, Vector2 step)[] Speakers =
        {
            (413417729, 413417726, 413417727, 413417731, "WendellArsenault", new Vector2(89.36788f, 109.38445f), ClamStep),
            (1871746044, 1871746041, 1871746046, 1871746042, "HectorBernard", new Vector2(66.3862f, 111.25431f), DoryStep),
        };

        /// <summary>The clam counter and the yard's sellers keep their places: the scope's row 11 retains every XY.
        /// The counter stays one object, its buyer, market and sell point sharing the player's hold and wallet. The
        /// two speakers the loop talks to stand within the interactor's reach of the walk's steps: the dory yard's by
        /// about 4 mm, Phase A's narrow approach.</summary>
        [Test]
        public void TheClamCounterAndTheYardsSellersKeepTheirPlaces_EachSpeakerWithinReach()
        {
            Doc sell = KeptComponent(1176619498, 1176619496, 1176619504, "FishBuyerStall", "HiddenHarbours.Economy.WharfSellPoint");
            Doc buyer = KeptComponent(1176619499, 1176619496, 1176619504, "FishBuyerStall", "HiddenHarbours.Economy.FishBuyer");
            Doc market = Scene.Require(buyer.FieldRef("_market"), "the clam buyer's market");
            Assert.That(market.FileId == 1176619500 && market.IsScript("HiddenHarbours.Economy.Market") &&
                        market.FieldRef("m_GameObject") == 1176619496 && Enabled(market), Is.True, "the counter's market stands on the counter");
            Assert.That(sell.FieldRef("_buyer"), Is.EqualTo(buyer.FileId), "the sell point sells to the counter's buyer");
            Assert.That(sell.FieldRef("_holdProvider"), Is.EqualTo(Providers), "the sell point empties the player's hold");
            Assert.That(sell.FieldRef("_walletProvider"), Is.EqualTo(Providers), "the sell point pays the player's wallet");
            Stands(GameObjectOf(sell), new Vector2(88f, 110f));

            foreach (var y in YardSellers)
                Counter(KeptComponent(y.id, y.go, y.transform, y.name, "HiddenHarbours.Economy.Shipwright"), y.sellerId, "_offer", y.offer, y.at);

            Doc interactor = KeptComponent(1912614191, 1912614190, 0, "WorldInteractor", "HiddenHarbours.World.WorldInteractor");
            Assert.That(interactor.FieldRef("_player"), Is.EqualTo(PlayerTransform), "the interactor reaches from the player");
            float reach = Number(interactor, "_radius");
            List<long> talks = interactor.FieldRefs("_interactables");
            foreach (var s in Speakers)
            {
                KeptComponent(s.sprite, s.go, s.transform, s.name, "HiddenHarbours.Core.IsoCharacterSprite");
                Doc speaker = Scene.Require(s.go, s.name);
                Stands(speaker, s.at);
                KeptComponent(s.interactable, s.go, s.transform, s.name, "HiddenHarbours.World.Interactable");
                Assert.That(talks, Does.Contain(s.interactable), $"the interactor talks to {s.name}");
                float d = Vector2.Distance(s.step, WorldPosition(speaker));
                TestContext.WriteLine($"{s.name}: {d:0.######} m from the walk's step {At2(s.step)}; reach {reach:0.###}, slack {reach - d:0.######}");
                Assert.That(d, Is.LessThanOrEqualTo(reach), $"{s.name} is within the interactor's reach of the walk's step");
            }
        }

        // ---- row 12: the arrival ----------------------------------------------------------------------------------

        /// <summary>The hulls berth 0 takes, each with the keel clearance at spring low the scope measured (the
        /// outboard dory shares the dory's hull).</summary>
        static readonly (string hull, float clearance)[] BerthHulls =
        {
            ("Assets/_Project/Data/Boats/CapeIslander.asset", 0.680504f),
            ("Assets/_Project/Data/Boats/Dory.asset", 1.808446f),
            ("Assets/_Project/Data/Boats/DoryOutboard.asset", 1.808446f),
        };

        /// <summary>One of the anchor's markers: the anchor's <paramref name="field"/> names the kept transform on its
        /// kept, named GameObject, standing at <paramref name="at"/>.</summary>
        static Doc Marker(Doc anchor, string field, long transform, long go, string name, Vector2 at)
        {
            Assert.That(anchor.FieldRef(field), Is.EqualTo(transform), $"the anchor's {field} is {name}'s kept transform");
            Doc t = Scene.Require(transform, name);
            Assert.That(t.FieldRef("m_GameObject"), Is.EqualTo(go), $"{name} keeps its GameObject");
            Doc g = Scene.Require(go, name);
            Assert.That(Name(g), Is.EqualTo(name), $"&{go} keeps its name");
            Assert.That(Live(g), Is.True, $"{name} is live");
            Vector2 p = WorldPosition(g);
            Assert.That(Vector2.Distance(p, at), Is.LessThan(1e-4f), $"{name} stands at {At2(at)}: {At2(p)}");
            return g;
        }

        /// <summary>The boat comes in alongside berth 0, heading east along the face: the arrival is laid for the design
        /// hull, so each hull the berth takes lies with her side on the fender line inside the slot (the coordinator's
        /// own <see cref="RegionAnchor.AlongsideCentre"/>) and floats at spring low by the scope's margin. The step-off
        /// stands clear and dry by the ladder that boarding and stepping off both choose, and the ways in from the sea
        /// keep their doors (the bar's landing, the sea door at y 38).</summary>
        [Test]
        public void TheArrivalLaysTheBoatAlongsideBerthZero_HeadingEast_AfloatAtSpringLow()
        {
            Doc anchor = KeptComponent(1030553595, 1030553594, 1030553596, NineMileCreekGroundSteps.AnchorName, "HiddenHarbours.App.RegionAnchor");
            Assert.That(anchor.Field("_regionId"), Is.EqualTo("region.nine_mile_creek"));
            Doc arrival = Marker(anchor, "_arrivalPoint", 1263013074, 1263013073, NineMileCreekGroundSteps.ArrivalName, G.ArrivalPos);
            Doc dock = Marker(anchor, "_dockZone", 692709123, 692709122, NineMileCreekGroundSteps.DockZoneName, G.DockZonePos);
            Doc step = Marker(anchor, "_disembarkPoint", 887353733, 887353732, NineMileCreekGroundSteps.DisembarkName, G.DisembarkPos);
            Quaternion heading = Rotation(arrival);
            Assert.That(Quaternion.Angle(heading, G.ArrivalRotation), Is.LessThan(0.01f), "she comes in on the berth's heading");
            Vector2 bow = heading * Vector3.up;
            Assert.That(bow.x, Is.EqualTo(1f).Within(1e-5f), "the berth's heading is east, along the face");
            Assert.That(anchor.Field("_alongsideBerth"), Is.EqualTo("1"), "berth 0 is an alongside berth");
            float design = Number(anchor, "_alongsideHalfBeamMetres");
            Assert.That(design, Is.EqualTo(G.DesignHalfBeamMetres), "the berth is laid for the design hull's beam");

            Vector2 arrivalAt = WorldPosition(arrival);
            foreach (var (path, scope) in BerthHulls)
            {
                var (length, draught, halfBeam) = Hull(path);
                string name = Path.GetFileNameWithoutExtension(path);
                Vector2 c = RegionAnchor.AlongsideCentre(arrivalAt, heading, design, halfBeam);
                var hull = new Rect(c.x - length / 2f, c.y - halfBeam, length, 2f * halfBeam);
                float bed = Highest(hull, 0.05f);
                float clearance = Low - draught - bed;
                TestContext.WriteLine($"{name}: {length:0.###} x {2f * halfBeam:0.###} m, draught {draught:0.###}, centre {At2(c)}, " +
                                      $"x {hull.xMin:0.###}..{hull.xMax:0.###}, highest bed {bed:0.######}, keel clearance at spring low {clearance:0.######}");
                Assert.That(c.y + halfBeam, Is.EqualTo(G.FenderLineY).Within(0.01f), $"{name} lies with her side on the fender line");
                Assert.That(hull.xMin >= G.BerthSlotWestX && hull.xMax <= G.BerthSlotEastX, Is.True, $"{name} lies inside berth 0's slot");
                Assert.That(clearance, Is.GreaterThanOrEqualTo(scope - 0.005f), $"{name} floats at spring low by the scope's margin");
            }

            Walker walker = Walker.OnTheScene();
            Vector2 off = WorldPosition(step);
            var (d, who) = walker.Clearance(off + walker.Offset);
            TestContext.WriteLine($"step-off {At2(off)}: the player's circle {d - walker.Radius:0.######} m off {who}; " +
                                  $"stands on {walker.Support(off):0.######}");
            Assert.That(d - walker.Radius, Is.GreaterThanOrEqualTo(walker.Allowance), "the step-off is clear of every solid");
            Assert.That(walker.Support(off), Is.GreaterThan(High), "the step-off is dry at spring high");

            var ladders = Scene.Docs.Where(l => l.IsScript("HiddenHarbours.World.WharfLadder") && Enabled(l) && Live(GameObjectOf(l))).ToList();
            foreach (var (what, at) in new[] { ("the step-off", off), ("the dock zone", WorldPosition(dock)) })
            {
                Doc ladder = ladders.OrderBy(l => Vector2.Distance(WorldPosition(GameObjectOf(l)), at)).First();
                Doc go = GameObjectOf(ladder);
                TestContext.WriteLine($"{what}: nearest ladder {Name(go)} at {Vector2.Distance(WorldPosition(go), at):0.###} m");
                Assert.That(Name(go), Is.EqualTo(G.BerthLadderName), $"{what} chooses berth 0's ladder");
                Assert.That(ladder.Field("_id"), Is.EqualTo(G.BerthLadderId), $"{what} chooses berth 0's ladder");
            }

            int ways = Array.IndexOf(anchor.Lines, "  _arrivals:");
            Assert.That(ways, Is.GreaterThan(0), "the anchor names its ways in");
            Assert.That(anchor.Lines.Skip(ways + 1).TakeWhile(l => !l.StartsWith("  _", StringComparison.Ordinal)).ToArray(),
                Is.EqualTo(new[] { "  - Key: bar", "    ArrivalPoint: {fileID: 0}", "    DisembarkPoint: {fileID: 600323448}" }),
                "one way in, over the bar, landing at the bar's own point");
            Doc bar = Scene.Require(Scene.Require(600323448, "the bar landing").FieldRef("m_GameObject"), "the bar landing");
            Assert.That(Vector2.Distance(WorldPosition(bar), new Vector2(350f, -200f)), Is.LessThan(1e-4f), "the bar's landing stays put");
            Doc sea = Scene.Docs.Where(p => p.IsScript("HiddenHarbours.World.RegionPassage")).Select(GameObjectOf)
                           .Single(p => Name(p) == "PassageToWestWater");
            Assert.That(WorldPosition(sea).y, Is.EqualTo(38f), "the sea door stays at y 38");
        }

        // ---- the loop walk ----------------------------------------------------------------------------------------

        /// <summary>The walk's box, a few metres beyond the walker's grid: still water there must lie under spring
        /// high for the walker's level to be the sea's.</summary>
        static readonly Rect WalkBox = Rect.MinMaxRect(-225f, 60f, 165f, 165f);

        /// <summary>From where the scene stands the player to the berth's step-off, up to the harbourmaster's counter
        /// and the chandlery's, back down to the clam buyer and the dory yard's speaker, and home to the berth: each
        /// leg with the length Phase A's walk measured on G3. No leg may grow by more than a metre: a detour is a
        /// change of the walk, not noise.</summary>
        [Test]
        public void TheLoopWalksFromTheBerthToEveryCounterAndHome_ClearOfEverySolid_DryAtSpringHigh()
        {
            var ground = NineMileCreekGroundMasks.ReadGround(".");
            float still = float.NegativeInfinity;
            for (int row = 0; row < NineMileCreekGround.Height; row++)
            for (int col = 0; col < NineMileCreekGround.Width; col++)
            {
                var at = new Vector2(NineMileCreekMainland.RegionWorldCenter.x - NineMileCreekMainland.RegionWorldSize.x / 2f + (col + 0.5f) / 2f,
                                     NineMileCreekMainland.RegionWorldCenter.y + NineMileCreekMainland.RegionWorldSize.y / 2f - (row + 0.5f) / 2f);
                if (WalkBox.Contains(at))
                    still = Mathf.Max(still, (float)NineMileCreekGroundMasks.HeightOfCode(ground.Still[row * NineMileCreekGround.Width + col]));
            }
            TestContext.WriteLine($"still water over the walk's box: highest {still:0.######}");
            Assert.That(still, Is.LessThanOrEqualTo(High), "no still water over the walk stands above spring high");

            Doc player = Scene.Require(PlayerGameObject, "the Player");
            Assert.That(Name(player) == "Player" && Scene.TransformOf(player).FileId == PlayerTransform, Is.True, "the Player keeps its ids");
            Walker walker = Walker.OnTheScene();
            TestContext.WriteLine($"walker: circle {walker.Radius:0.###} m at {At2(walker.Offset)}, {walker.OutlineCount} solid outlines, " +
                                  $"{walker.DeckCount} decks, level {walker.Level:0.###}");
            Vector2 otter = WorldPosition(Scene.Require(194637439, "the Otter"));
            var legs = new (string name, Vector2 from, Vector2 to, float phaseA)[]
            {
                ("the player's spot to the berth", WorldPosition(player), G.DisembarkPos, 12.457f),
                ("berth to the licence counter", G.DisembarkPos, LicenceStep, 247.434f),
                ("licence to the rod counter", LicenceStep, RodStep, 56.828f),
                ("rod to the clam buyer", RodStep, ClamStep, 304.127f),
                ("clam buyer to the dory yard", ClamStep, DoryStep, 24.854f),
                ("dory yard home to the berth", DoryStep, G.DisembarkPos, 31.403f),
            };
            foreach (var (name, from, to, phaseA) in legs)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Walker.Route r = walker.Path(from, to);
                TestContext.WriteLine($"{name} {At2(from)} -> {At2(to)}: {r.Reason}; {r.Points.Count} points, {r.Length:0.###} m " +
                                      $"(Phase A {phaseA:0.###}), clearance {r.ClearanceMin:0.######} off {r.ClearanceWho}, " +
                                      $"wet {r.WetMax:0.######}, {r.Visited} nodes in {clock.ElapsedMilliseconds} ms" +
                                      (r.Reached ? $", nearest the Otter {r.Points.Min(p => Vector2.Distance(p, otter)):0.##} m" : ""));
                Assert.That(r.Reached, Is.True, $"{name}: {r.Reason}");
                Assert.That(r.ClearanceMin, Is.GreaterThanOrEqualTo(walker.Allowance), $"{name} keeps clear of every solid");
                Assert.That(r.WetMax, Is.LessThanOrEqualTo(Walker.DryAllowance), $"{name} stays dry at spring high");
                Assert.That(r.Length, Is.LessThanOrEqualTo(phaseA + 1f), $"{name} walks Phase A's way, not a detour");
            }
        }

        // ---- row 13: the marks ------------------------------------------------------------------------------------

        /// <summary>The scope's kept mark instances, by their stable ids.</summary>
        static readonly (long instance, string id)[] KeptMarks =
        {
            (875841738, "channel.nmc_entrance.p0"), (1057366139, "channel.nmc_entrance.s0"), (508014158, "channel.nmc_entrance.s1"),
            (1078554119, "channel.nmc_entrance.p2"), (519935955, "channel.nmc_entrance.s2"), (1303868689, "channel.nmc_entrance.p1"),
            (939297788, "channel.nmc_bar_gut.p0"), (1209194584, "channel.nmc_bar_gut.s0"), (145410075, "channel.nmc_bar_gut.p1"),
            (1882927730, "channel.nmc_bar_gut.s1"), (235807725, "mark.nmc_breakwater_head"), (1752502719, "mark.nmc_shoal_northeast"),
        };

        /// <summary>The marks the plan no longer places: the entrance's old p1 and the two retired cardinals.</summary>
        static IEnumerable<string> StoodDown => NineMileCreekNavMarks.RetiredIds.Append("channel.nmc_entrance.p1");

        /// <summary>Every mark def under the defs' folder, by its mark type: its GUID and, per size rung, the sprites its
        /// facings wear.</summary>
        static Dictionary<string, (string guid, Dictionary<string, List<string>> facings)> MarkDefs()
        {
            var defs = new Dictionary<string, (string, Dictionary<string, List<string>>)>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(NineMileCreekNavMarks.DefFolder, "*.asset"))
            {
                string[] lines = File.ReadAllText(file).Replace("\r\n", "\n").Split('\n');
                string type = null, size = null;
                var facings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("  MarkType: ", StringComparison.Ordinal)) type = lines[i].Substring(12).Trim();
                    else if (lines[i].StartsWith("  - SizeId: ", StringComparison.Ordinal)) size = lines[i].Substring(12).Trim();
                    else if (lines[i] == "    Facings:" && size != null)
                    {
                        var sprites = new List<string>();
                        while (i + 1 < lines.Length && lines[i + 1].StartsWith("    - {", StringComparison.Ordinal))
                            sprites.Add(lines[++i].Substring("    - ".Length));
                        facings[size] = sprites;
                    }
                }
                if (type != null) defs.Add(type, (GuidOf(file.Replace('\\', '/')), facings));
            }
            return defs;
        }

        /// <summary>The marks stand where G3's plan puts them: each planned mark is one active instance of the buoy
        /// prefab named for its stable id, at the planned spot, turned to the planned facing and dressed in its def's
        /// sprite for that facing and rung. Every mark the plan leaves out stands down on its kept id (the old p1 and
        /// both retired cardinals), and the plan never names a retired id.</summary>
        [Test]
        public void TheMarksStandWhereG3sPlanPutsThem_TheRetiredStandDownOnTheirIds()
        {
            var plan = NineMileCreekNavMarks.Plan(Plan);
            Assert.That(plan.Refusals, Is.Empty, "G3 takes every planned mark");
            Assert.That(plan.Marks.Select(m => m.Id).Intersect(NineMileCreekNavMarks.RetiredIds), Is.Empty, "the plan names no retired id");

            SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(NineMileCreekNavMarks.PrefabPath).Replace("\r\n", "\n"));
            Doc buoy = prefab.RootNamed("NavBuoy"), buoyTr = prefab.TransformOf(buoy);
            Doc visual = prefab.ComponentsOf(buoy).Single(d => d.IsScript("HiddenHarbours.Boats.NavBuoyVisual"));
            Doc renderer = prefab.Docs.Single(d => d.ClassId == SpriteRendererClass);
            string prefabGuid = GuidOf(NineMileCreekNavMarks.PrefabPath);
            var defs = MarkDefs();

            Doc root = At(NineMileCreekNavMarks.RootName), rootTr = Scene.TransformOf(root);
            Assert.That(rootTr.FileId, Is.EqualTo(273312153), "the marks' root keeps its transform");
            Assert.That(rootTr.Field("m_LocalPosition") == "{x: 0, y: 0, z: 0}" && rootTr.Field("m_LocalRotation") == "{x: 0, y: 0, z: 0, w: 1}" &&
                        rootTr.Field("m_LocalScale") == "{x: 1, y: 1, z: 1}", Is.True, "the marks' root lays them in world space");

            var marks = new Dictionary<string, (Doc instance, Func<long, string, string, string> of)>(StringComparer.Ordinal);
            foreach (long transform in rootTr.FieldRefs("m_Children"))
            {
                Doc instance = Scene.Require(Scene.Require(transform, "a mark's transform").FieldRef("m_PrefabInstance"), "a mark");
                Assert.That(RefGuid(instance.Field("m_SourcePrefab")), Is.EqualTo(prefabGuid), $"&{instance.FileId} is a buoy");
                var overrides = Overrides(instance);
                string Of(long target, string path, string loaded) => overrides.TryGetValue((target, path), out string v) ? v : loaded;
                string name = Of(buoy.FileId, "m_Name", Name(buoy));
                Assert.That(marks.ContainsKey(name), Is.False, $"one mark named {name}");
                marks.Add(name, (instance, Of));
            }
            foreach (var (instance, id) in KeptMarks)
                Assert.That(marks[NavMarkNamePrefix + id].instance.FileId, Is.EqualTo(instance), $"{id} keeps its instance");

            foreach (var m in plan.Marks)
            {
                string name = NavMarkNamePrefix + m.Id;
                Assert.That(marks.ContainsKey(name), Is.True, $"{name} stands in the scene");
                var of = marks[name].of;
                var at = new Vector2(ParseFloat(of(buoyTr.FileId, "m_LocalPosition.x", null)), ParseFloat(of(buoyTr.FileId, "m_LocalPosition.y", null)));
                int facing = Mathf.Clamp(m.Facing, 0, 7);
                var def = defs[m.MarkType.ToString()];
                List<string> sprites = def.facings[m.SizeId];
                TestContext.WriteLine($"{m.Id} {m.MarkType} {m.SizeId}: planned {At2(m.At)}, stands {At2(at)}, facing {facing}");
                Assert.That(of(buoy.FileId, "m_IsActive", buoy.Field("m_IsActive")), Is.EqualTo("1"), $"{name} is active");
                Assert.That(Vector2.Distance(at, m.At), Is.LessThan(1e-4f), $"{name} stands where G3's plan puts it");
                Assert.That(ParseInt(of(visual.FileId, "_facing", visual.Field("_facing"))), Is.EqualTo(facing), $"{name} turns to the plan's facing");
                Assert.That(RefGuid(of(visual.FileId, "_def", visual.Field("_def"))), Is.EqualTo(def.guid), $"{name} is a {m.MarkType}");
                Assert.That(of(visual.FileId, "_sizeId", visual.Field("_sizeId")), Is.EqualTo(m.SizeId), $"{name} is a {m.SizeId}");
                Assert.That(of(renderer.FileId, "m_Sprite", renderer.Field("m_Sprite")), Is.EqualTo(sprites[Mathf.Clamp(facing, 0, sprites.Count - 1)]),
                    $"{name} wears its def's sprite for that facing");
            }
            var unplanned = marks.Keys.Except(plan.Marks.Select(m => NavMarkNamePrefix + m.Id)).ToList();
            Assert.That(unplanned, Is.EquivalentTo(StoodDown.Select(id => NavMarkNamePrefix + id)), "only the old p1 and the retired stand down");
            foreach (string name in unplanned)
                Assert.That(marks[name].of(buoy.FileId, "m_IsActive", buoy.Field("m_IsActive")), Is.EqualTo("0"), $"{name} stands down, its id kept");
        }

        // ---- row 14: the Otter ------------------------------------------------------------------------------------

        /// <summary>The scope's measures of her yard: every metre within 18 m above spring high, 40 m of dry yard ahead,
        /// and 14.6 m off the paved network (the turning pad's edge; Wharf Road's is 23 m).</summary>
        const float OtterDryRadius = 18f, OtterDryAhead = 40f, OtterOffTheRoads = 14.6f;

        /// <summary>The Otter stays parked where she landed, drivable, turned to face inland (the owner's ruling: somewhere
        /// safe for now; her launch is a later wave's). She stands on dry yard: all G3 within 18 m of her above spring
        /// high and 40 m of it ahead, off every paved way and inside no solid.</summary>
        [Test]
        public void TheOtterStandsParkedOnDryYard_OffTheRoads_FacingInland()
        {
            Doc otter = Scene.Require(194637439, "the Otter");
            Doc tr = Scene.TransformOf(otter);
            Assert.That(Name(otter), Is.EqualTo(NineMileCreekOtterLanding.OtterName), "she keeps her name");
            Assert.That(tr.FileId == 194637444 && tr.FieldRef("m_Father") == 0, Is.True, "she keeps her transform, at the scene's root");
            Assert.That(Live(otter), Is.True, "she stays active");
            string def = GuidOf(NineMileCreekOtterLanding.OtterDefPath);
            Doc parked = KeptComponent(194637442, 194637439, 194637444, NineMileCreekOtterLanding.OtterName, "HiddenHarbours.Vehicles.ParkedVehicle");
            Assert.That(RefGuid(parked.Field("_vehicle")), Is.EqualTo(def), "she parks as the Otter");
            Assert.That(parked.Field("_drivable"), Is.EqualTo("1"), "she stays drivable");
            Doc controller = KeptComponent(194637441, 194637439, 194637444, NineMileCreekOtterLanding.OtterName, "HiddenHarbours.Vehicles.VehicleController");
            Assert.That(RefGuid(controller.Field("_vehicle")), Is.EqualTo(def), "she drives as the Otter");
            KeptComponent(194637440, 194637439, 194637444, NineMileCreekOtterLanding.OtterName, "HiddenHarbours.Vehicles.VehicleDoor");
            Assert.That(Components(otter, Rigidbody2DClass).Select(c => c.FileId), Is.EqualTo(new[] { 194637443L }), "she keeps her body");
            Assert.That(Regex.Match(File.ReadAllText(NineMileCreekOtterLanding.OtterDefPath), @"(?m)^  Id: (\S+)\r?$").Groups[1].Value,
                Is.EqualTo("vehicle.otter_8x8"));

            Vector2 at = WorldPosition(otter);
            Assert.That(Vector2.Distance(at, G.OtterParkPos), Is.LessThan(1e-4f), $"she parks at {At2(G.OtterParkPos)}: {At2(at)}");
            Assert.That(Quaternion.Angle(Rotation(otter), G.OtterParkRotation), Is.LessThan(0.01f), "she faces the plan's heading");
            Assert.That(ParseVec3(tr.Field("m_LocalEulerAnglesHint")).z, Is.EqualTo(G.OtterParkHeadingDegrees), "her hint agrees");

            float lowest = float.PositiveInfinity;
            Vector2 lowAt = at;
            for (float dx = -OtterDryRadius; dx <= OtterDryRadius; dx += 0.5f)
            for (float dy = -OtterDryRadius; dy <= OtterDryRadius; dy += 0.5f)
            {
                if (dx * dx + dy * dy > OtterDryRadius * OtterDryRadius) continue;
                float e = Ground(at + new Vector2(dx, dy));
                if (e < lowest) { lowest = e; lowAt = at + new Vector2(dx, dy); }
            }
            Vector2 ahead = Rotation(otter) * Vector3.up;
            float aheadLowest = float.PositiveInfinity;
            for (float s = 0f; s <= OtterDryAhead; s += 0.25f) aheadLowest = Mathf.Min(aheadLowest, Ground(at + ahead * s));
            float astern = float.NaN;
            for (float s = 0f; s <= 60f && float.IsNaN(astern); s += 0.25f) if (Ground(at - ahead * s) <= High) astern = s;
            TestContext.WriteLine($"the Otter at {At2(at)} on G3 {Ground(at):0.######}: lowest within {OtterDryRadius} m {lowest:0.######} " +
                                  $"at {At2(lowAt)}; lowest {OtterDryAhead} m ahead {aheadLowest:0.######}; first ground at spring high {astern:0.##} m astern");
            Assert.That(lowest, Is.GreaterThan(High), $"all ground within {OtterDryRadius} m of her is dry at spring high");
            Assert.That(aheadLowest, Is.GreaterThan(High), $"{OtterDryAhead} m of dry yard lies ahead of her");

            float offTheRoads = float.PositiveInfinity;
            foreach (var way in G.Ways())
            {
                float d = float.PositiveInfinity;
                for (int i = 1; i < way.Route.Length; i++) d = Mathf.Min(d, DistanceToSegment(at, way.Route[i - 1], way.Route[i]));
                TestContext.WriteLine($"  {way.Name}: {d - way.HalfWidth:0.###} m off its edge");
                offTheRoads = Mathf.Min(offTheRoads, d - way.HalfWidth);
            }
            Assert.That(offTheRoads, Is.GreaterThanOrEqualTo(OtterOffTheRoads - 0.05f), "she parks off every paved way by the scope's margin");
            var outlines = SolidOutlines(out var polygons);
            Assert.That(SolidAt(at, outlines, polygons), Is.Null, "she parks inside no solid");
        }

        // ---- the walker -------------------------------------------------------------------------------------------

        /// <summary>
        /// Phase A's walker, on the live scene: the player's own circle (the scene's Player's collider) moved over a
        /// half-metre grid, each step checked at tenth-of-a-metre stations against every solid the scene loads
        /// (<see cref="NineMileCreekLiveGround.SolidOutlines"/>, the player's own circle left out) and against the
        /// ground at spring high. A station is <b>clear</b> when the circle keeps <see cref="Allowance"/> off every
        /// solid outline and lies inside no closed one; it is <b>dry</b> when what the player stands on (the highest
        /// live <c>StandablePlatform</c> deck whose footprint holds them, else G3) lies no more than
        /// <see cref="DryAllowance"/> under spring high. A* finds the shortest clear, dry way between two points.
        /// </summary>
        public sealed class Walker
        {
            /// <summary>The grid's cell, the stations' spacing and the solids' bucket, in metres.</summary>
            const float Step = 0.5f, Station = 0.1f, Bucket = 2f;
            /// <summary>Phase A's grid: x -220..160, y 65..160.</summary>
            const float MinX = -220f, MinY = 65f;
            const int Columns = 761, Rows = 191;
            public const float DryAllowance = 0.001f;

            readonly List<(Vector2 a, Vector2 b, float radius, string who)> _outlines;
            readonly List<(Vector2[] path, string who, Rect box)> _polygons = new List<(Vector2[], string, Rect)>();
            readonly Dictionary<(int, int), List<int>> _cells = new Dictionary<(int, int), List<int>>();
            readonly Dictionary<(int, int), List<int>> _polygonCells = new Dictionary<(int, int), List<int>>();
            readonly List<(Rect footprint, float deck)> _decks;
            readonly int[] _stamp;
            int _tick;

            public readonly float Radius, Allowance = 0.05f, Level;
            public readonly Vector2 Offset;
            public int OutlineCount => _outlines.Count;
            public int DeckCount => _decks.Count;

            /// <summary>The walker as the live scene makes it: the Player's circle, every other solid, every live deck,
            /// at spring high.</summary>
            public static Walker OnTheScene()
            {
                Doc circle = Components(Scene.Require(PlayerGameObject, "the Player"), CircleCollider2DClass).Single();
                Assert.That(Enabled(circle) && circle.Field("m_IsTrigger") == "0", Is.True, "the player's circle is solid");
                string own = " &" + circle.FileId;
                var outlines = SolidOutlines(out var polygons);
                var decks = Scene.Docs.Where(d => d.IsScript("HiddenHarbours.World.StandablePlatform") && Enabled(d) && Live(GameObjectOf(d)))
                                      .Select(d => (RectOf(d, "_footprint"), Number(d, "_deckElevation"))).ToList();
                return new Walker(outlines.Where(o => !o.who.EndsWith(own, StringComparison.Ordinal)).ToList(),
                                  polygons.Where(p => !p.who.EndsWith(own, StringComparison.Ordinal)).ToList(),
                                  decks, Number(circle, "m_Radius"), Vector(circle, "m_Offset"), High);
            }

            Walker(List<(Vector2 a, Vector2 b, float radius, string who)> outlines, List<(Vector2[] path, string who)> polygons,
                   List<(Rect, float)> decks, float radius, Vector2 offset, float level)
            {
                _outlines = outlines;
                _decks = decks;
                Radius = radius;
                Offset = offset;
                Level = level;
                _stamp = new int[outlines.Count];
                for (int i = 0; i < outlines.Count; i++)
                {
                    var o = outlines[i];
                    Index(_cells, i, Vector2.Min(o.a, o.b) - Vector2.one * o.radius, Vector2.Max(o.a, o.b) + Vector2.one * o.radius);
                }
                foreach (var (path, who) in polygons)
                {
                    Vector2 min = path.Aggregate(Vector2.Min), max = path.Aggregate(Vector2.Max);
                    _polygons.Add((path, who, Rect.MinMaxRect(min.x, min.y, max.x, max.y)));
                    Index(_polygonCells, _polygons.Count - 1, min, max);
                }
            }

            static void Index(Dictionary<(int, int), List<int>> cells, int i, Vector2 min, Vector2 max)
            {
                for (int x = Mathf.FloorToInt(min.x / Bucket); x <= Mathf.FloorToInt(max.x / Bucket); x++)
                for (int y = Mathf.FloorToInt(min.y / Bucket); y <= Mathf.FloorToInt(max.y / Bucket); y++)
                {
                    if (!cells.TryGetValue((x, y), out List<int> list)) cells[(x, y)] = list = new List<int>();
                    list.Add(i);
                }
            }

            /// <summary>How far <paramref name="c"/> lies from the nearest solid outline (a rounded edge's radius taken
            /// off), counted out to the bucket's 2 m, and which; -1 inside a closed outline.</summary>
            public (float d, string who) Clearance(Vector2 c)
            {
                int cx = Mathf.FloorToInt(c.x / Bucket), cy = Mathf.FloorToInt(c.y / Bucket);
                if (_polygonCells.TryGetValue((cx, cy), out List<int> inside))
                    foreach (int i in inside)
                        if (_polygons[i].box.Contains(c) && Inside(_polygons[i].path, c)) return (-1f, _polygons[i].who);
                float best = Bucket;
                string who = "nothing within 2 m";
                _tick++;
                for (int x = cx - 1; x <= cx + 1; x++)
                for (int y = cy - 1; y <= cy + 1; y++)
                {
                    if (!_cells.TryGetValue((x, y), out List<int> list)) continue;
                    foreach (int i in list)
                    {
                        if (_stamp[i] == _tick) continue;
                        _stamp[i] = _tick;
                        var o = _outlines[i];
                        float d = DistanceToSegment(c, o.a, o.b) - o.radius;
                        if (d < best) { best = d; who = o.who; }
                    }
                }
                return (best, who);
            }

            /// <summary>What the player stands on at <paramref name="p"/>: the highest live deck whose footprint holds
            /// them, else G3.</summary>
            public float Support(Vector2 p)
            {
                float deck = float.NegativeInfinity;
                foreach (var (footprint, e) in _decks)
                    if (footprint.Contains(p)) deck = Mathf.Max(deck, e);
                return float.IsNegativeInfinity(deck) ? Ground(p) : deck;
            }

            /// <summary>The stations from <paramref name="a"/> to <paramref name="b"/>: whether any is foul, the least
            /// clearance and its solid, and the deepest wet.</summary>
            (bool foul, float min, string who, float wet) Leg(Vector2 a, Vector2 b, bool stopAtFoul)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(a, b) / Station);
                bool foul = false;
                float min = float.PositiveInfinity, wet = 0f;
                string who = null;
                for (int i = 0; i <= n; i++)
                {
                    Vector2 p = n == 0 ? a : Vector2.Lerp(a, b, (float)i / n);
                    var (d, solid) = Clearance(p + Offset);
                    if (d - Radius < min) { min = d - Radius; who = solid; }
                    wet = Mathf.Max(wet, Level - Support(p));
                    foul |= d < Radius + Allowance || wet > DryAllowance;
                    if (foul && stopAtFoul) break;
                }
                return (foul, min, who, wet);
            }

            public sealed class Route
            {
                public bool Reached;
                public string Reason = "", ClearanceWho = "";
                public List<Vector2> Points = new List<Vector2>();
                public float Length, ClearanceMin = float.PositiveInfinity, WetMax;
                public int Visited;
            }

            static Vector2 Node(int k) => new Vector2(MinX + (k % Columns) * Step, MinY + (k / Columns) * Step);

            /// <summary>The nearest of the 25 grid nodes around <paramref name="p"/> that a clear, dry step reaches.</summary>
            int Nearest(Vector2 p)
            {
                int ix = Mathf.RoundToInt((p.x - MinX) / Step), iy = Mathf.RoundToInt((p.y - MinY) / Step);
                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    int x = ix + dx, y = iy + dy;
                    if (x < 0 || x >= Columns || y < 0 || y >= Rows) continue;
                    Vector2 q = Node(y * Columns + x);
                    if (Leg(p, q, true).foul) continue;
                    float d = Vector2.Distance(p, q);
                    if (d < bestD) { bestD = d; best = y * Columns + x; }
                }
                return best;
            }

            public Route Path(Vector2 start, Vector2 end)
            {
                var route = new Route();
                int s = Nearest(start), goal = Nearest(end);
                if (s < 0 || goal < 0)
                {
                    route.Reason = (s < 0 ? "the start" : "the end") + " has no clear, dry step onto the grid";
                    return route;
                }
                var g = new float[Columns * Rows];
                var prev = new int[Columns * Rows];
                var closed = new bool[Columns * Rows];
                for (int i = 0; i < g.Length; i++) { g[i] = float.PositiveInfinity; prev[i] = -1; }
                g[s] = 0f;
                var open = new Heap();
                open.Push(0f, s);
                while (open.Count > 0)
                {
                    int k = open.Pop();
                    if (closed[k]) continue;
                    if (k == goal) { route.Reached = true; break; }
                    closed[k] = true;
                    route.Visited++;
                    int x = k % Columns, y = k / Columns;
                    Vector2 a = Node(k);
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if ((dx == 0 && dy == 0) || xx < 0 || xx >= Columns || yy < 0 || yy >= Rows) continue;
                        int q = yy * Columns + xx;
                        if (closed[q]) continue;
                        Vector2 b = Node(q);
                        if (Leg(a, b, true).foul) continue;
                        float through = g[k] + Mathf.Sqrt(dx * dx + dy * dy) * Step;
                        if (through >= g[q]) continue;
                        g[q] = through;
                        prev[q] = k;
                        open.Push(through + Vector2.Distance(b, end), q);
                    }
                }
                if (!route.Reached) { route.Reason = "no clear, dry way between them on the grid"; return route; }
                var points = new List<Vector2> { end };
                for (int k = goal; k != -1; k = prev[k]) points.Add(Node(k));
                points.Add(start);
                points.Reverse();
                route.Points = points;
                for (int i = 1; i < points.Count; i++)
                {
                    var leg = Leg(points[i - 1], points[i], false);
                    if (leg.min < route.ClearanceMin) { route.ClearanceMin = leg.min; route.ClearanceWho = leg.who; }
                    route.WetMax = Mathf.Max(route.WetMax, leg.wet);
                    route.Length += Vector2.Distance(points[i - 1], points[i]);
                }
                route.Reason = "walked";
                return route;
            }

            /// <summary>A binary min-heap of grid nodes by their estimated length.</summary>
            sealed class Heap
            {
                readonly List<(float f, int k)> _items = new List<(float, int)>();
                public int Count => _items.Count;

                public void Push(float f, int k)
                {
                    _items.Add((f, k));
                    int i = _items.Count - 1;
                    while (i > 0)
                    {
                        int parent = (i - 1) >> 1;
                        if (_items[parent].f <= f) break;
                        _items[i] = _items[parent];
                        i = parent;
                    }
                    _items[i] = (f, k);
                }

                public int Pop()
                {
                    int top = _items[0].k;
                    var last = _items[_items.Count - 1];
                    _items.RemoveAt(_items.Count - 1);
                    if (_items.Count == 0) return top;
                    int i = 0;
                    while (2 * i + 1 < _items.Count)
                    {
                        int child = 2 * i + 1;
                        if (child + 1 < _items.Count && _items[child + 1].f < _items[child].f) child++;
                        if (_items[child].f >= last.f) break;
                        _items[i] = _items[child];
                        i = child;
                    }
                    _items[i] = last;
                    return top;
                }
            }
        }
    }
}
