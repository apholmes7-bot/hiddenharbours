using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.World;               // MainlandCoast, MainlandTidalTerrain — the route the pole line and the lamps share

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
        private GameObject _terrainHost;

        [TearDown]
        public void TearDown()
        {
            if (_terrainHost != null) Object.DestroyImmediate(_terrainHost);
            _terrainHost = null;
        }

        /// <summary>Nine Mile Creek's own authored terrain — the same one the dressing tests measure
        /// against, and the one the lamp siting walks along looking for dry ground.</summary>
        private ITidalTerrain CreekTerrain()
        {
            if (_terrainHost == null)
                _terrainHost = new GameObject("lampTerrain") { hideFlags = HideFlags.HideAndDontSave };
            var t = _terrainHost.GetComponent<MainlandTidalTerrain>();
            if (t == null)
            {
                t = _terrainHost.AddComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(t);
            }
            return t;
        }

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
                Assert.Greater(LightPresets.ReachMetres(LampPosts.PresetFor(key)), 1f,
                    $"{key} must LIGHT past its own post — the reach, since 2026-09-04 the bloom is the " +
                    "lantern and is supposed to be small");
            }
        }

        // ---- the LIT FITTING: how big the lamp LOOKS (the owner's ruling, 2026-09-04) ----------------------

        /// <summary>
        /// <b>Every kit piece glows at its own lit fitting, and no two of them are the same size.</b> The
        /// owner's complaint was that a dock light is <i>"just a round glow"</i> — a bloom drawn at the size
        /// of the POOL. These four numbers are the widths of the things that actually glow, read off the
        /// rigs; the ORDER is the part that carries meaning, because it is the difference between a
        /// hand-sized hurricane lantern on a quay post and a three-lamp array on a mast.
        /// </summary>
        [Test]
        public void EachKitPiece_BloomsAtItsOwnLitFitting_InTheRightOrder()
        {
            Assert.AreEqual(0.14f, LampPosts.FittingWidthMetres(LampPosts.LanternPost), 1e-6f,
                "the glazed lantern box, wharfDecorRig.js:993");
            Assert.AreEqual(0.40f, LampPosts.FittingWidthMetres(LampPosts.StreetLamp), 1e-6f,
                "the pendant lantern's lens, utilityIsoRig.js:361");
            Assert.AreEqual(0.58f, LampPosts.FittingWidthMetres(LampPosts.YardLight), 1e-6f,
                "the cobra head's lens slab, utilityIsoRig.js:339");
            Assert.AreEqual(1.49f, LampPosts.FittingWidthMetres(LampPosts.FloodMast), 1e-6f,
                "three flood cans on a bar, utilityIsoRig.js:373-376");

            Assert.Less(LampPosts.FittingWidthMetres(LampPosts.LanternPost),
                        LampPosts.FittingWidthMetres(LampPosts.StreetLamp),
                "a quay lantern is a smaller lamp than a road lamp and must look like one");
            Assert.Less(LampPosts.FittingWidthMetres(LampPosts.StreetLamp),
                        LampPosts.FittingWidthMetres(LampPosts.YardLight));
            Assert.Less(LampPosts.FittingWidthMetres(LampPosts.YardLight),
                        LampPosts.FittingWidthMetres(LampPosts.FloodMast));

            Assert.AreEqual(0f, LampPosts.FittingWidthMetres("noSuchPiece"), 1e-6f,
                "an unmeasured piece must fall back to the preset's archetype, not to darkness");
        }

        /// <summary>
        /// <b>The guard, as a RATIO of the piece rather than as an absolute.</b> A bloom is the source; a
        /// source is a fitting on a lamp. Two things are asserted for every placed piece: it never glows
        /// bigger than the thing that glows, and it never approaches the pool it lights — which is the
        /// picture the owner refused, and the one an absolute ceiling would let creep back in the next time
        /// somebody retunes a reach.
        /// </summary>
        [Test]
        public void NoLampPostGlowsBiggerThanItsOwnFitting_NorAnywhereNearItsPool()
        {
            foreach (string key in new[]
                     { LampPosts.LanternPost, LampPosts.StreetLamp, LampPosts.YardLight, LampPosts.FloodMast })
            {
                float fitting = LampPosts.FittingWidthMetres(key);
                float bloom = LightPresets.BloomForFitting(fitting);
                float reach = LightPresets.ReachMetres(LampPosts.PresetFor(key));

                Assert.LessOrEqual(bloom, fitting + 1e-6f,
                    $"{key} blooms at {bloom:0.00} m off a {fitting:0.00} m fitting — the halo is allowed to " +
                    "be the fitting's own width and no more");
                Assert.Less(bloom / reach, 0.25f,
                    $"{key} blooms at {bloom / reach:0.000} of the {reach:0.0} m it lights — a lamp drawn at " +
                    "anything approaching its own pool is the flat cream disc the owner ruled against");
            }
        }

        /// <summary>
        /// <b>The measured width actually reaches the light — and RIDES on the component, so waking cannot
        /// undo it.</b> <see cref="PreconfiguredLight"/> re-stamps its preset on every <c>Awake</c> and
        /// <c>OnEnable</c>, so a bloom written only onto the <see cref="SceneLight"/> at build time would be
        /// saved into the scene correctly and then overwritten by the archetype the first time the object
        /// woke in play: right in the editor, wrong in the game, and invisible to any test that only
        /// inspects the built object.
        /// </summary>
        [Test]
        public void ThePlacedLamp_CarriesItsFittingWidth_SoAWakeCannotStampItBack()
        {
            var go = new GameObject("lampFitting") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                SceneLight light = LampPosts.Light(go, LampPosts.DecorFamily, LampPosts.LanternPost);

                Assert.NotNull(light);
                Assert.AreEqual(0.14f, light.Range, 1e-4f, "the wharf lantern's own glazed box, stamped");

                var carried = go.GetComponent<PreconfiguredLight>();
                Assert.NotNull(carried, "the placed lamp carries its preset");
                Assert.AreEqual(0.14f, carried.FittingWidthMetres, 1e-4f,
                    "and carries its FITTING, or the next Awake stamps the archetype over it");

                // Prove the re-stamp is harmless rather than asserting the field and hoping: setting the
                // preset re-runs exactly the code Awake runs.
                carried.Preset = LampPosts.PresetFor(LampPosts.LanternPost);
                Assert.AreEqual(0.14f, light.Range, 1e-4f,
                    "a re-stamp must not put the preset's archetype back over the measured fitting");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// <b>⚠️⚠️ SHRINKING THE BLOOM MUST NOT SWITCH THE LAMP SHADOWS OFF — and before this test it did.</b>
        ///
        /// <para><c>LampShadowSystem</c> pairs a lamp with the casters inside it by a RADIUS, and until
        /// 2026-09-04 that radius was <c>light.Range</c> — correctly, because Range then WAS the pool a lamp
        /// lights. This PR split the two: Range became the BLOOM and a lantern post's fell from 3.6 m to
        /// 0.14 m. A bollard three and a half metres away is not within fourteen centimetres of anything, so
        /// the pairing loop silently stopped finding it. No error, no warning: every 02:00 plate simply came
        /// back with no lamp shadows in it, and the whole of #698 was off.</para>
        ///
        /// <para>⭐ <b>The lesson is the reusable part: when a number stops meaning what it meant, the bug is
        /// not where you changed it — it is in every OTHER reader of the old meaning.</b> Grep the readers
        /// before shipping the split. This one was found by a plate, three PRs later, and only because
        /// something else needed the same number.</para>
        ///
        /// <para>The assertion is on the pairing REACH rather than on a rendered shadow, because that is
        /// where the defect lived and it needs no GPU: the pier's bollards must be inside the radius the
        /// shadow system pairs its lamps by.</para>
        /// </summary>
        [Test]
        public void ALampPostStillThrowsShadowsOffTheBollards_AfterTheBloomShrank()
        {
            var go = new GameObject("shadowReach") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                SceneLight light = LampPosts.Light(go, LampPosts.DecorFamily, LampPosts.LanternPost);

                Assert.AreEqual(0.14f, light.Range, 1e-4f,
                    "the bloom is the lantern — that is this PR");
                Assert.AreEqual(LightPresets.ReachMetres(LightPresets.Kind.Lightpost), light.ReachMetres, 1e-4f,
                    "and the REACH rides on the light, or nothing downstream can tell the two apart");

                // The distance the shadow system actually pairs by must still cover the working edge.
                float nearest = float.MaxValue;
                float row = StPetersWharf.LampRowY;
                foreach (var f in StPetersWharf.MooringFittings())
                    nearest = Mathf.Min(nearest, Mathf.Abs(f.Position.y - row));

                Assert.Less(nearest, light.ReachMetres,
                    $"the nearest mooring fitting is {nearest:0.00} m off the lamp row and the lamp reaches " +
                    $"{light.ReachMetres:0.00} m — this is the pairing that puts a bollard's rake on the planks");
                Assert.Greater(nearest, light.Range,
                    $"and it is COMFORTABLY outside the {light.Range:0.00} m bloom, which is exactly why " +
                    "pairing by the bloom found nothing at all");
            }
            finally { Object.DestroyImmediate(go); }
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
        /// Both lamps stand ON the pier, on the row <see cref="StPetersWharf.LampRowY"/> derives, on the
        /// NORTH half, and clear of every fitting on the mooring face. The south lip is where the bollards,
        /// the fenders and the ladder are, and a post among them is a post in the way of a line.
        /// </summary>
        [Test]
        public void TheStPetersLamps_StandOnThePlanks_OnTheDerivedRow_ClearOfTheGear()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            IReadOnlyList<LampPosts.Site> lamps = StPetersWharf.LampPostSites();

            Assert.AreEqual(2, lamps.Count, "two lit ends, not a run — see the siting note");

            foreach (var lamp in lamps)
            {
                Assert.IsTrue(lamp.StandsOnDeck, "a pier lamp is checked against the planks");
                Assert.IsTrue(deck.Contains(lamp.Position),
                    $"{lamp.Key} at {lamp.Position} must be on the deck, not beside it");
                Assert.AreEqual(StPetersWharf.LampRowY, lamp.Position.y, 1e-4f,
                    "the row the pool's reach allows — back from the working edge, but not so far back " +
                    "that it stops covering it");
                Assert.Greater(lamp.Position.y, 0f,
                    "and still on the NORTH half: the mooring gear is all on the south lip");

                // 1.5 m is twice the deepest footprint either kit publishes for a piece this size
                // (lanternPost is 0.46 x 0.73 m), so it is a bar on CO-SITING, not on neighbourliness:
                // the head lamp really does stand 2.0 m from the corner pilehead and that is fine.
                foreach (var fitting in StPetersWharf.Fittings())
                    Assert.Greater(Vector2.Distance(lamp.Position, fitting.Position), 1.5f,
                        $"{lamp.Key} stands {Vector2.Distance(lamp.Position, fitting.Position):0.0} m from " +
                        $"the {fitting.Name} — a post needs room round the gear it shares a deck with");
            }
        }

        /// <summary>
        /// <b>⭐ The lamps actually reach the gear they are for.</b> This is the assertion the first draft
        /// failed: on the back row at y = 2.5 the pool fell 0.4 m short of the bollards at y = −2.5, so the
        /// pier's 02:00 plate had two pools of light and not one lamp shadow off a fitting. A lamp that
        /// lights only the planks it stands on is a lamp in the wrong row.
        /// </summary>
        [Test]
        public void EachStPetersLamp_ReachesTheNearestMooringFitting()
        {
            float reach = LightPresets.ReachMetres(LightPresets.Kind.Lightpost);

            foreach (var lamp in StPetersWharf.LampPostSites())
            {
                float nearest = StPetersWharf.MooringFittings()
                    .Min(f => Vector2.Distance(lamp.Position, f.Position));
                Assert.Less(nearest, reach,
                    $"the nearest tie-off is {nearest:0.00} m from this lamp, which reaches {reach:0.00} m " +
                    "— the mooring edge is the one place a rope is worked in the dark");
            }
        }

        /// <summary>
        /// And the row is DERIVED from that reach, not typed: widen the preset and the lamps must step
        /// further back, because the whole point of the row is to be as far out of the working edge as the
        /// pool can afford. A hard-coded row would silently stop tracking.
        ///
        /// <para><b>⚠ REACH, and the second half of this test is why.</b> When the bloom came down to the
        /// size of the lit lantern (2026-09-04), <c>For(Lightpost).Range</c> went from 3.6 m to 0.40 m. Had
        /// <c>LampRowY</c> kept reading it, both St Peters lamps would have marched forward onto the
        /// mooring lip — into the bollards, in the way of a line — chasing a glow that was never the light.
        /// So the row is pinned at the value it shipped with as well as derived, because "it still tracks
        /// the number it reads" is not the same claim as "it reads the right number".</para>
        /// </summary>
        [Test]
        public void TheLampRow_FollowsThePresetsReach_RatherThanBeingChosen()
        {
            float reach = LightPresets.ReachMetres(LightPresets.Kind.Lightpost);
            float fittingRow = StPetersWharf.MinCellY + 0.5f;

            Assert.LessOrEqual(StPetersWharf.LampRowY, fittingRow + reach,
                "the row must be inside the pool's reach of the fitting row");
            Assert.Greater(StPetersWharf.LampRowY + 1f, fittingRow + reach,
                "and it must be the FURTHEST BACK row that is — one row further north would be outside");

            Assert.AreEqual(0.5f, StPetersWharf.LampRowY, 1e-6f,
                "the row the 02:00 plates were shot on — the fitting ruling must move what is DRAWN and " +
                "nothing about where anything STANDS");
        }

        /// <summary>
        /// The head lamp takes the LADDER's own x rather than a chosen one, so it lights the place a person
        /// is actually in the dark at this end: the ladder she climbs at low water, and the bollards abreast
        /// of it. Re-site the ladder and the lamp follows; a typed coordinate would not.
        ///
        /// <para>⚠ It does NOT reach the dory on the north face, and that is a deliberate trade rather than
        /// an oversight. She lies 4.25 m off the deck's centre line and the pool is 3.6 m; a lamp far enough
        /// north to cover her is one that no longer covers the mooring edge, which is the working side. She
        /// carries her own anchor light (boat-lights PR 2a), so the berth is not dark — the wharf's lamp does
        /// the wharf's job.</para>
        /// </summary>
        [Test]
        public void TheHeadLamp_IsDerivedFromTheLadder_NotTyped()
        {
            var head = StPetersWharf.LampPostSites()[0];
            Assert.AreEqual(StPetersWharf.LadderPosition().x, head.Position.x, 1e-4f);

            float reach = LightPresets.ReachMetres(LampPosts.PresetFor(head.Key));
            float toLadder = Vector2.Distance(head.Position, StPetersWharf.LadderPosition());
            Assert.Less(toLadder, reach,
                $"the ladder is {toLadder:0.00} m from a lamp that reaches {reach:0.00} m — the one fitting " +
                "whose whole job is getting a person between a boat and the planks in the dark");
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
            var lit = NineMileCreekDressing.Lamps(CreekTerrain()).Where(l => l.Key == LampPosts.YardLight).ToList();

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
        public void TheQuayLamps_AreAtTheWorkingEdge_ClearOfWhatIsAlreadyOnTheQuay()
        {
            var onTheQuay = NineMileCreekDressing.QuayGear()
                .Concat(NineMileCreekDressing.Services())
                .ToList();
            Assert.IsNotEmpty(onTheQuay, "the quay really does carry gear — otherwise this proves nothing");

            var quayLamps = NineMileCreekDressing.Lamps(CreekTerrain())
                .Where(l => Mathf.Abs(l.Position.y - NineMileCreekDressing.LampRowY) < 0.5f)
                .ToList();
            Assert.AreEqual(2, quayLamps.Count, "two along eighty-four metres of wall");

            foreach (var lamp in quayLamps)
            {
                // Out of the working strip, which is what the strip is for.
                Assert.GreaterOrEqual(lamp.Position.y, NineMileCreekDressing.GearBandMinY,
                    "a post in the working strip is a post in the way of a landing");

                // ⭐ And its pool actually reaches the mooring edge — the reason it is at the FRONT of the
                // gear band and not against the yard. On a ten-metre quay a 3.6 m pool cannot do both.
                float reach = LightPresets.ReachMetres(LightPresets.Kind.Lightpost);
                float toLip = lamp.Position.y - NineMileCreekWharf.DeckFootprint().yMin;
                Assert.Less(toLip, reach,
                    $"the berths are {toLip:0.00} m from this lamp, which reaches {reach:0.00} m — a wharf " +
                    "lamp that does not light the berth lights the wrong thing");

                foreach (var prop in onTheQuay)
                    Assert.Greater(Vector2.Distance(lamp.Position, prop.Position), 2f,
                        $"a lamp at {lamp.Position.x:0.#},{lamp.Position.y:0.#} is on top of the {prop.Key}");
            }
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
            // ⚠ The quay lamps are streetLamps too, so "a streetLamp" is not the filter — "a streetLamp
            // that is not on the quay's own lamp row" is.
            var roadLamps = NineMileCreekDressing.Lamps(CreekTerrain())
                .Where(l => l.Key == LampPosts.StreetLamp
                            && Mathf.Abs(l.Position.y - NineMileCreekDressing.LampRowY) > 0.5f)
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
            var mast = NineMileCreekDressing.Lamps(CreekTerrain()).Single(l => l.Key == LampPosts.FloodMast);

            Assert.IsFalse(apron.Contains(mast.Position),
                $"the mast at {mast.Position} is standing on the yard it is meant to light from the edge of");
            for (int bay = 0; bay < NineMileCreekLaydown.BayCount; bay++)
                Assert.IsFalse(NineMileCreekLaydown.BayArea(bay).Contains(mast.Position),
                    $"and not in bay {bay}");
            Assert.IsFalse(NineMileCreekLaydown.LaneArea().Contains(mast.Position), "and not in the lane");

            // Close enough to be the yard's light rather than a lamp in a field.
            Assert.Less(Vector2.Distance(mast.Position, new Vector2(apron.center.x, apron.yMin)),
                        LightPresets.ReachMetres(LightPresets.Kind.Floodlight),
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

            foreach (var lamp in NineMileCreekDressing.Lamps(CreekTerrain()).Concat(StPetersWharf.LampPostSites()))
            {
                CollectionAssert.Contains(known, lamp.Key, $"unknown lamp piece '{lamp.Key}'");
                Assert.IsNotEmpty(lamp.Reason, $"{lamp.Key} must say what it is for");
            }
        }

        /// <summary>
        /// <b>⭐ Every ground lamp stands on ground that is dry at every tide.</b> The builder already refuses
        /// a wet site loudly, but a console error on a rebuild is not a gate — this is, and it is the test
        /// that caught the first draft: the second road lamp was anchored at Wharf Road's node 4, <i>"stepping
        /// onto the spit"</i>, which snapped to a point five metres north of the centre-line where the ground
        /// is <b>−0.16 m</b>. The road crosses the neck between the barachois and the marsh pool, and beside
        /// it there is water.
        ///
        /// <para>Measured against the region's OWN authored terrain, so a terrain edit that floods a lamp
        /// reddens here instead of shipping a post standing in a marsh.</para>
        /// </summary>
        [Test]
        public void EveryGroundLamp_StandsOnGroundDryAtEveryTide()
        {
            ITidalTerrain terrain = CreekTerrain();
            float springHigh = NineMileCreekMainland.SpringHighWater;

            foreach (var lamp in NineMileCreekDressing.Lamps(terrain))
            {
                if (lamp.StandsOnDeck) continue;   // planks are checked against the deck, not the seabed
                float ground = terrain.ElevationAt(lamp.Position);
                Assert.Greater(ground, springHigh,
                    $"'{lamp.Key}' at {lamp.Position} stands on {ground:0.00} m, at or under spring high " +
                    $"water ({springHigh:0.0} m). {lamp.Reason}");
            }
        }

        /// <summary>
        /// <b>And the walk really is the terrain's doing, not a coordinate that happens to be dry.</b> Handed
        /// NO terrain the second road lamp takes its raw anchor — along = 220 m, the wet notch at the neck —
        /// so the site that ships is demonstrably the one the ground chose. Without this the dryness test
        /// above would pass just as well against a hard-coded number.
        /// </summary>
        [Test]
        public void TheRoadLampsSite_IsChosenByTheGround_NotByACoordinate()
        {
            var unchecked_ = NineMileCreekDressing.Lamps(null)
                .Where(l => l.Key == LampPosts.StreetLamp
                            && Mathf.Abs(l.Position.y - NineMileCreekDressing.LampRowY) > 0.5f)
                .ToList();
            var walked = NineMileCreekDressing.Lamps(CreekTerrain())
                .Where(l => l.Key == LampPosts.StreetLamp
                            && Mathf.Abs(l.Position.y - NineMileCreekDressing.LampRowY) > 0.5f)
                .ToList();

            Assert.AreEqual(2, unchecked_.Count);
            Assert.AreEqual(2, walked.Count);
            Assert.AreEqual(unchecked_[0].Position, walked[0].Position,
                "the town-end lamp's anchor is already dry, so the terrain moves it nowhere");
            Assert.AreNotEqual(unchecked_[1].Position, walked[1].Position,
                "the spit lamp's anchor is UNDER water, so the terrain must have moved it");

            var terrain = CreekTerrain();
            Assert.LessOrEqual(terrain.ElevationAt(unchecked_[1].Position),
                               NineMileCreekMainland.SpringHighWater,
                               "the unwalked anchor really is wet — otherwise this proves nothing");
            Assert.Greater(terrain.ElevationAt(walked[1].Position), NineMileCreekMainland.SpringHighWater);
        }

        /// <summary>
        /// Nine on the two regions together, which is the number the lamp-shadow budget was reasoned about
        /// with: the pool is 24 nearest pairs, and every one of these posts would have taken a slot for its
        /// own foot without <c>LampShadowSystem</c>'s carrier rule.
        /// </summary>
        [Test]
        public void TheWholeGameGetsNineLamps()
        {
            Assert.AreEqual(7, NineMileCreekDressing.Lamps(CreekTerrain()).Count);
            Assert.AreEqual(2, StPetersWharf.LampPostSites().Count);
        }
    }
}
