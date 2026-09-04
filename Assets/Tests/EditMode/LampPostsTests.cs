using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;               // MainlandCoast — the route the pole line and the lamps share

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>LAMPS ON THE LAND</b> — the owner's 2026-09-03 ruling (<i>"yes i want lights on land"</i>) as
    /// placement tables, asserted the way every region table in this repo is: <b>against the pure data the
    /// builders consume</b>, never against a built scene. <i>Scene-wired is not builder-wired</i> — a lamp
    /// dragged into <c>StPeters.unity</c> survives exactly until the owner presses Build.
    ///
    /// <para><b>Art-independent where it can be.</b> Whether a lamp has pixels is a pipeline question; where
    /// it stands is world-content's, and only the second one is asserted here. The one test that reads the
    /// ISO pack's published heights says so and skips when the pack is not checked out.</para>
    /// </summary>
    public class LampPostsTests
    {
        // The four pieces LampPosts knows how to light, and the heights their own rig contracts publish.
        // Pinned rather than read so a re-bake that silently changes a lamp's height reddens here: the
        // height is not decoration, it is what sets every cast shadow's length off this lamp.
        private const string UtilityContract = "Assets/_Project/Art/Sprites/Utility/utilityIsoRig.contract.json";
        private const string DecorContract = "Assets/_Project/Art/Sprites/Wharf/Decor/wharfDecorRig.contract.json";

        // =============================================================================================
        //  WHAT A LAMP POST IS
        // =============================================================================================

        /// <summary>
        /// Every kit key maps to a preset, and the split is by HEAD HEIGHT: the two low posts pool warmly,
        /// the two tall poles flood. A yard light that came out as a <see cref="LightPresets.Kind.Lightpost"/>
        /// would be a seven-metre pole throwing a four-metre circle.
        /// </summary>
        [Test]
        public void EachKitPiece_TakesThePresetItsHeightAsksFor()
        {
            Assert.AreEqual(LightPresets.Kind.Lightpost, LampPosts.PresetFor(LampPosts.LanternPost));
            Assert.AreEqual(LightPresets.Kind.Lightpost, LampPosts.PresetFor(LampPosts.StreetLamp));
            Assert.AreEqual(LightPresets.Kind.Floodlight, LampPosts.PresetFor(LampPosts.YardLight));
            Assert.AreEqual(LightPresets.Kind.Floodlight, LampPosts.PresetFor(LampPosts.FloodMast));
        }

        /// <summary>
        /// Every preset a lamp post can carry is a night-gated RADIAL pool that actually emits. The gate
        /// itself is the shader's (ADR 0016) and no preset may touch it, so this is the whole of what a
        /// placed lamp promises.
        /// </summary>
        [Test]
        public void EveryLampPostPreset_IsARadialPoolThatEmits()
        {
            foreach (string key in new[]
                     { LampPosts.LanternPost, LampPosts.StreetLamp, LampPosts.YardLight, LampPosts.FloodMast })
            {
                var c = LightPresets.For(LampPosts.PresetFor(key));
                Assert.AreEqual(SceneLight.LightShape.Radial, c.Shape, $"{key} pools, it does not beam");
                Assert.Greater(c.Intensity, 0f, $"{key} must emit");
                Assert.Greater(c.Range, 1f, $"{key} must reach past its own post");
            }
        }

        /// <summary>
        /// <b>A lamp's head height comes from the PACK, not from this repo's opinion.</b> Left at
        /// <see cref="SceneLight.DefaultLampHeightMeters"/> every one of these would cast as a 2.5 m lamp —
        /// so a 7.8 m flood mast would light a yard like a mast and shadow it like a bollard.
        /// </summary>
        [Test]
        public void AHeadHeightIsTheKitsOwnPublishedHeight_NotTheSceneLightDefault()
        {
            if (!File.Exists(UtilityContract) || !File.Exists(DecorContract))
                Assert.Ignore("the ISO pack contracts are not checked out — placement is unaffected");

            Assert.AreEqual(2.46f, LampPosts.HeadHeightMetres(LampPosts.DecorFamily, LampPosts.LanternPost), 0.01f);
            Assert.AreEqual(4.48f, LampPosts.HeadHeightMetres(LampPosts.UtilityFamily, LampPosts.StreetLamp), 0.01f);
            Assert.AreEqual(7.26f, LampPosts.HeadHeightMetres(LampPosts.UtilityFamily, LampPosts.YardLight), 0.01f);
            Assert.AreEqual(7.80f, LampPosts.HeadHeightMetres(LampPosts.UtilityFamily, LampPosts.FloodMast), 0.01f);

            // The three tall pieces are the ones the default would have lied about.
            foreach (string key in new[] { LampPosts.StreetLamp, LampPosts.YardLight, LampPosts.FloodMast })
                Assert.Greater(LampPosts.HeadHeightMetres(LampPosts.UtilityFamily, key),
                               SceneLight.DefaultLampHeightMeters,
                               $"{key} is taller than the default it would otherwise have used");
        }

        /// <summary>
        /// A site declared on a DECK is checked against the planks, never against the terrain. The St Peters
        /// pier stands over a slip dredged to −1.0 m: a dryness check there rejects a lamp for standing in
        /// water it is six metres above, and this is the field that stops it.
        /// </summary>
        [Test]
        public void ADeckSite_KnowsItStandsOnPlanks_AndAGroundSiteDoesNot()
        {
            var deck = LampPosts.OnDeck(LampPosts.DecorFamily, LampPosts.LanternPost,
                                        Vector2.zero, 180f, new Rect(-5f, -5f, 10f, 10f), "on planks");
            var ground = LampPosts.OnGround(LampPosts.UtilityFamily, LampPosts.StreetLamp,
                                            Vector2.zero, 180f, "on soil");

            Assert.IsTrue(deck.StandsOnDeck);
            Assert.IsFalse(ground.StandsOnDeck, "a zero-width deck rect means 'on the ground'");
        }

        // =============================================================================================
        //  ST PETERS — two lanterns on the one dock
        // =============================================================================================

        /// <summary>
        /// Both lamps stand ON the pier, on its NORTH row, and clear of every fitting on the mooring face.
        /// The south lip is where the bollards, the fenders and the ladder are, and a post among them is a
        /// post in the way of a line — which is what <see cref="StPetersWharf.NorthFaceY"/> already says the
        /// back of the pier is for.
        /// </summary>
        [Test]
        public void TheStPetersLamps_StandOnThePlanks_OnTheBackRow_ClearOfTheGear()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            IReadOnlyList<LampPosts.Site> lamps = StPetersWharf.LampPostSites();

            Assert.AreEqual(2, lamps.Count, "two lit ends, not a run — see the siting note");

            foreach (var lamp in lamps)
            {
                Assert.IsTrue(lamp.StandsOnDeck, "a pier lamp is checked against the planks");
                Assert.IsTrue(deck.Contains(lamp.Position),
                    $"{lamp.Key} at {lamp.Position} must be on the deck, not beside it");
                Assert.AreEqual(StPetersWharf.MaxCellY + 0.5f, lamp.Position.y, 1e-4f,
                    "the north row: the working edge is the south one");

                foreach (var fitting in StPetersWharf.Fittings())
                    Assert.Greater(Vector2.Distance(lamp.Position, fitting.Position), 2f,
                        $"{lamp.Key} stands {Vector2.Distance(lamp.Position, fitting.Position):0.0} m from " +
                        $"the {fitting.Name} — a post needs room round the gear it shares a deck with");
            }
        }

        /// <summary>
        /// The head lamp takes the LADDER's own x rather than a chosen one, so it lights the two places a
        /// person is in the dark at this end: the ladder she climbs, and — straight across six metres of
        /// planks — the north pilehead the starting dory lies against. Re-site the ladder and the lamp
        /// follows it; a typed coordinate would not.
        /// </summary>
        [Test]
        public void TheHeadLamp_IsDerivedFromTheLadder_NotTyped()
        {
            var head = StPetersWharf.LampPostSites()[0];
            Assert.AreEqual(StPetersWharf.LadderPosition().x, head.Position.x, 1e-4f);

            // And its pool actually crosses the deck to the dory's berth.
            float reach = LightPresets.For(LampPosts.PresetFor(head.Key)).Range;
            float toDory = Vector2.Distance(head.Position,
                new Vector2(StPetersBuilder.DoryMooredX, StPetersBuilder.DoryMooredY));
            Assert.Less(toDory, reach,
                $"the dory lies {toDory:0.0} m off a lamp that reaches {reach:0.0} m — she should be lit");
        }

        // =============================================================================================
        //  NINE MILE CREEK — the quay, the road, the yards
        // =============================================================================================

        /// <summary>
        /// <b>The yard light is placed ONCE, and it is a light now.</b> It stood at the wharf entrance from
        /// #462 described as <i>"the only lit thing out here at night"</i> and emitted nothing at all. It
        /// keeps the site the plan chose and moves to the table that knows how to light it — leaving it in
        /// both would draw two poles on one spot.
        /// </summary>
        [Test]
        public void TheYardLight_LeftTheDecorTable_AndIsNowALamp()
        {
            Assert.IsFalse(NineMileCreekDressing.Services().Any(p => p.Key == LampPosts.YardLight),
                "the unlit decor copy is gone");

            var entrance = NineMileCreekDressing.WharfEntrance() + new Vector2(0f, 2f);
            var lit = NineMileCreekDressing.Lamps().Where(l => l.Key == LampPosts.YardLight).ToList();

            Assert.AreEqual(2, lit.Count, "the wharf entrance and the Route 91 forecourt");
            Assert.AreEqual(1, lit.Count(l => Vector2.Distance(l.Position, entrance) < 1e-3f),
                "and one of them is on #462's own entrance site, unmoved");
        }

        /// <summary>
        /// The quay lamps stand on the row the tall things go on and clear of what is already there. Berths
        /// 2, 6, 7 and 11 are taken along the back by the wood stack, the standpipe, the net frame and the
        /// winter stack; a lamp on top of any of them is two objects on one metre of deck.
        /// </summary>
        [Test]
        public void TheQuayLamps_AreOnTheBackRow_ClearOfWhatIsAlreadyOnIt()
        {
            var onTheBack = NineMileCreekDressing.QuayGear()
                .Concat(NineMileCreekDressing.Services())
                .Where(p => Mathf.Abs(p.Position.y - NineMileCreekDressing.BackRowY) < 0.5f)
                .ToList();

            Assert.IsNotEmpty(onTheBack, "the back row really does carry gear — otherwise this proves nothing");

            var quayLamps = NineMileCreekDressing.Lamps()
                .Where(l => Mathf.Abs(l.Position.y - NineMileCreekDressing.BackRowY) < 0.5f)
                .ToList();
            Assert.AreEqual(2, quayLamps.Count, "two along eighty-four metres of wall");

            foreach (var lamp in quayLamps)
                foreach (var prop in onTheBack)
                    Assert.Greater(Vector2.Distance(lamp.Position, prop.Position), 4f,
                        $"a lamp at {lamp.Position.x:0.#} is on top of the {prop.Key}");
        }

        /// <summary>
        /// <b>A road lamp goes where the wire is, and never on a pole.</b> Both take Wharf Road's published
        /// route and the SAME north offset <c>NineMileCreekMainland</c> §12 gives the pole line — and both
        /// sit exactly half a pole-spacing from their neighbours, because the first draft put one at node 0,
        /// which is precisely where pole 0 stands.
        /// </summary>
        [Test]
        public void TheRoadLamps_RideThePoleLine_AndLandInTheGapsBetweenPoles()
        {
            Vector2[] road = NineMileCreekMainland.WharfRoad;
            float spacing = NineMileCreekMainland.UtilityPoleSpacingMetres;
            float offset = NineMileCreekMainland.UtilityPoleOffsetMetres;

            var poles = NineMileCreekDressing.Poles().Select(p => p.Position).ToList();
            var roadLamps = NineMileCreekDressing.Lamps()
                .Where(l => l.Key == LampPosts.StreetLamp
                            && Mathf.Abs(l.Position.y - NineMileCreekDressing.BackRowY) > 0.5f)
                .ToList();

            Assert.AreEqual(2, roadLamps.Count, "two lamps on 322 m of gravel — varied, not regular");

            foreach (var lamp in roadLamps)
            {
                float nearestPole = poles.Min(p => Vector2.Distance(p, lamp.Position));
                Assert.AreEqual(spacing * 0.5f, nearestPole, 1.5f,
                    $"a lamp sits in the GAP, but this one is {nearestPole:0.0} m from a pole");

                // At the pole line's OWN offset from the road, on the pole line's OWN side of it.
                float toRoad = DistanceToRoute(road, lamp.Position);
                Assert.AreEqual(offset, toRoad, 0.5f,
                    $"a lamp stands off the centre-line by the pole line's {offset} m, not {toRoad:0.0} m");
                Assert.Greater(lamp.Position.y, NearestPointOn(road, lamp.Position).y,
                    "north of the centre-line, the side §12 put the wire on");
            }
        }

        /// <summary>Nearest point on a polyline, by dense sampling — cheap, and it needs no second copy of
        /// the projection maths the route helpers already own.</summary>
        private static Vector2 NearestPointOn(Vector2[] route, Vector2 p)
        {
            float length = NineMileCreekMainland.RouteLength(route);
            Vector2 best = MainlandCoast.PositionAt(route, 0f);
            float bestD = Vector2.Distance(best, p);
            for (float s = 0f; s <= length; s += 0.5f)
            {
                Vector2 q = MainlandCoast.PositionAt(route, s);
                float d = Vector2.Distance(q, p);
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }

        private static float DistanceToRoute(Vector2[] route, Vector2 p) =>
            Vector2.Distance(NearestPointOn(route, p), p);

        /// <summary>
        /// The flood mast stands OFF the laydown apron. A mast inside the pavement is either in a bay a
        /// machine reverses into or in the lane it drives down; the yard's own rectangle is what says which
        /// ground is spoken for.
        /// </summary>
        [Test]
        public void TheFloodMast_StandsOffTheLaydownPavement()
        {
            Rect apron = NineMileCreekLaydown.ApronArea();
            var mast = NineMileCreekDressing.Lamps().Single(l => l.Key == LampPosts.FloodMast);

            Assert.IsFalse(apron.Contains(mast.Position),
                $"the mast at {mast.Position} is standing on the yard it is meant to light from the edge of");
            for (int bay = 0; bay < NineMileCreekLaydown.BayCount; bay++)
                Assert.IsFalse(NineMileCreekLaydown.BayArea(bay).Contains(mast.Position),
                    $"and not in bay {bay}");
            Assert.IsFalse(NineMileCreekLaydown.LaneArea().Contains(mast.Position), "and not in the lane");

            // Close enough to be the yard's light rather than a lamp in a field.
            Assert.Less(Vector2.Distance(mast.Position, new Vector2(apron.center.x, apron.yMin)),
                        LightPresets.For(LightPresets.Kind.Floodlight).Range,
                        "it must still reach the pavement it stands beside");
        }

        /// <summary>
        /// Every lamp this region places names a piece the preset table knows. A typo would otherwise fall
        /// through <see cref="LampPosts.PresetFor"/>'s default and ship a flood mast with a lantern's pool.
        /// </summary>
        [Test]
        public void EveryLampNamesAPieceThePresetTableKnows()
        {
            var known = new[]
                { LampPosts.LanternPost, LampPosts.StreetLamp, LampPosts.YardLight, LampPosts.FloodMast };

            foreach (var lamp in NineMileCreekDressing.Lamps().Concat(StPetersWharf.LampPostSites()))
            {
                CollectionAssert.Contains(known, lamp.Key, $"unknown lamp piece '{lamp.Key}'");
                Assert.IsNotEmpty(lamp.Reason, $"{lamp.Key} must say what it is for");
            }
        }

        /// <summary>
        /// Nine on the two regions together, which is the number the lamp-shadow budget was reasoned about
        /// with: the pool is 24 nearest pairs, and every one of these posts would have taken a slot for its
        /// own foot without <c>LampShadowSystem</c>'s carrier rule.
        /// </summary>
        [Test]
        public void TheWholeGameGetsNineLamps()
        {
            Assert.AreEqual(7, NineMileCreekDressing.Lamps().Count);
            Assert.AreEqual(2, StPetersWharf.LampPostSites().Count);
        }
    }
}
