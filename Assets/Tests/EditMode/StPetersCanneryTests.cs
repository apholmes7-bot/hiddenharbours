using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE DERELICT ST PETERS — the shut cannery beside the pier, and the three broken outbuildings
    /// on Aunt Ginny's plot.</b>
    ///
    /// <para>The owner ruled on 2026-08-19 that St Peters had a cannery, that it closed when the work
    /// went to the mainland, and that restarting it is a long-arc player goal. This fixture holds the
    /// SCENERY half of that — where the building stands and that the world can actually draw it. The
    /// restart itself is M3 and lives in <c>backlog/building-lifecycle-gameplay.md</c>; nothing here asserts a
    /// mechanic, because there is not one.</para>
    ///
    /// <para><b>⚠️ Everything positional is asserted against the AUTHORED TERRAIN, never against the
    /// constants</b> — the #345 rule, and it earns its keep twice over here. The pier root measures
    /// +5.35 m and two cells further east is +3.15 m, because the dredged berth and approach pull the
    /// ground down a long way inland of where a map says they stop. A cannery site picked by eye off
    /// those coordinates is exactly the kind that floods.</para>
    /// </summary>
    public class StPetersCanneryTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        const float SpringHighWater = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   // 2.2

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("StPetersTerrain_CanneryTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // =====================================================================================
        //  the site
        // =====================================================================================

        /// <summary>
        /// The cannery stands on ground that is dry at every tide — <b>and the margin is not thin</b>,
        /// which is the part worth asserting. Measured, not stated: the same check at the pier root two
        /// cells to the east would report about half of this.
        /// </summary>
        [Test]
        public void TheCanneryStandsOnGroundThatIsDryAtEveryTide()
        {
            float ground = _terrain.ElevationAt(StPetersCannery.Site);

            Assert.Greater(ground, SpringHighWater,
                $"the cannery is sited at {StPetersCannery.Site} where the ground is {ground:0.00} m, " +
                $"at or under spring high water ({SpringHighWater:0.00} m).");

            // The plateau is 6 m; anything much under that means a dredge carve is reaching the site.
            Assert.Greater(ground, SpringHighWater + 2f,
                $"the cannery has only {ground - SpringHighWater:0.00} m of freeboard. That is the " +
                "signature of the berth or approach carve reaching the site — the ground beside those " +
                "cuts falls off smoothly and a long way. Move the building, do not lower the tide.");

            Debug.Log($"[stpeters-cannery] site {StPetersCannery.Site} stands at {ground:0.00} m " +
                      $"({ground - SpringHighWater:0.00} m of freeboard over spring high water).");
        }

        /// <summary>
        /// It is a WATERFRONT building: near enough the pier to be fed by it, and clear enough of the
        /// deck to stand beside it rather than on it.
        ///
        /// <para>This is the assertion the site exists to satisfy. A cannery loads boats — put it on the
        /// village's south beach, where there is no pier and no dredged water, and it is a factory its
        /// own boats cannot reach.</para>
        /// </summary>
        [Test]
        public void TheCanneryHasWharfFrontage_AndDoesNotStandOnTheDeck()
        {
            var pierRoot = new Vector2(StPetersWharf.RootCellX + 0.5f, 0f);
            float toPier = Vector2.Distance(StPetersCannery.Site, pierRoot);

            Assert.Less(toPier, 40f,
                $"the cannery is {toPier:0.0} m from the pier root. A fish plant that far from its own " +
                "wharf is a building beside a road, not on a waterfront.");

            // …and not IN the pier. The deck runs x ∈ [RootCellX, HeadCellX], y ∈ [−3, +3] at one metre
            // a cell; the building reserves a circle, so the test is edge-to-edge.
            float reach = StPetersCannery.FootprintRadiusMetres > 0f
                ? StPetersCannery.FootprintRadiusMetres
                : StPetersCannery.UnbakedFootprintRadiusMetres;

            float dx = Mathf.Max(0f, Mathf.Max(StPetersWharf.RootCellX - StPetersCannery.Site.x,
                                               StPetersCannery.Site.x - (StPetersWharf.HeadCellX + 1f)));
            float dy = Mathf.Max(0f, Mathf.Abs(StPetersCannery.Site.y) - StPetersWharf.HalfWidthCells);
            float toDeck = Mathf.Sqrt(dx * dx + dy * dy);

            Assert.Greater(toDeck, reach,
                $"the cannery's {reach:0.0} m footprint reaches onto the wharf deck ({toDeck:0.0} m " +
                "away at its nearest edge). A building standing in the planking sorts against the deck's " +
                "own row order and reads as broken art.");

            Debug.Log($"[stpeters-cannery] {toPier:0.0} m from the pier root, {toDeck:0.0} m clear of " +
                      $"the deck, reserving {reach:0.0} m.");
        }

        /// <summary>
        /// It does not stand in anything else that is already sited — the village clearing, Ginny's
        /// land, or any of the three woodland lots.
        /// </summary>
        [Test]
        public void TheCanneryStandsClearOfEverythingElseOnTheIsland()
        {
            float reach = StPetersCannery.FootprintRadiusMetres > 0f
                ? StPetersCannery.FootprintRadiusMetres
                : StPetersCannery.UnbakedFootprintRadiusMetres;

            float toGinny = Vector2.Distance(StPetersCannery.Site, StPetersGinnyPlot.CottagePos)
                            - StPetersGinnyPlot.ClearingRadius - reach;
            Assert.Greater(toGinny, 0f,
                $"the cannery reaches {-toGinny:0.0} m into Ginny's declared clearing");

            float toVillage = Vector2.Distance(StPetersCannery.Site,
                                               (Vector2)StPetersBuilder.VillageHearthPos)
                              - StPetersWoods.VillageClearingRadius - reach;
            Assert.Greater(toVillage, 0f,
                $"the cannery reaches {-toVillage:0.0} m into the village clearing");

            foreach (var lot in StPetersWoodlandZones.Lots)
            {
                // `From` IS the centre for a circular zone — Circle() sets both spine ends to it.
                float gap = Vector2.Distance(StPetersCannery.Site, lot.From) - lot.Radius - reach;
                Assert.Greater(gap, 0f,
                    $"the cannery overlaps the '{lot.Name}' wood by {-gap:0.0} m — trees would plant " +
                    "through its wall");
            }
        }

        // =====================================================================================
        //  the world knows it is there
        // =====================================================================================

        /// <summary>
        /// The woods hold off the cannery, from a DECLARED clearing rather than by luck.
        ///
        /// <para>The site already falls inside the wharf's 30 m dock clearance, so no tree plants there
        /// today with or without this row. That is the reason the row exists: a clearing that only works
        /// because a neighbouring constant happens to cover it is one that breaks silently the day that
        /// constant is retuned.</para>
        /// </summary>
        [Test]
        public void TheWoodsAreHeldOffTheCannery_ByItsOwnDeclaredClearing()
        {
            StPetersWoodlandZones.InvalidateCache();

            // Require, not FirstOrDefault: WoodlandZone is a readonly STRUCT, so a missing row comes
            // back as a zero-radius zone at the origin and an IsNotNull check on it passes forever.
            var row = WoodlandZones.Require(StPetersWoodlandZones.Zones,
                                            StPetersWoodlandZones.CanneryZone);

            float reach = StPetersCannery.FootprintRadiusMetres > 0f
                ? StPetersCannery.FootprintRadiusMetres
                : StPetersCannery.UnbakedFootprintRadiusMetres;

            Assert.GreaterOrEqual(row.Radius, reach,
                $"the cannery's clearing is {row.Radius:0.0} m but the building reaches {reach:0.0} m — " +
                "a spruce would stand inside its wall");

            Assert.IsTrue(StPetersWoodlandZones.IsCleared(StPetersCannery.Site),
                "the cannery's own site does not read as cleared ground");

            // A point on the far wall, not just the centre: a radius that covered only the middle would
            // pass the line above and still plant a tree through the corrugated.
            Assert.IsTrue(
                StPetersWoodlandZones.IsCleared(StPetersCannery.Site + new Vector2(reach * 0.9f, 0f)),
                "the far edge of the cannery's footprint is not cleared ground");
        }

        /// <summary>
        /// The meadow does not grow through the cannery's floor.
        ///
        /// <para>This is the post-office bug (2026-08-12) wearing a different building: a floor sprite
        /// sorts BELOW the tuft band, so grass placed on a building's site draws over it. Every placed
        /// building has to be in <c>StPetersGrass.BuildingSites</c> and a derelict is no exception — the
        /// weeds a ruin deserves are drawn into its own cell by the lifecycle pass.</para>
        /// </summary>
        [Test]
        public void TheMeadowIsHeldOffTheCannerysFloor()
        {
            var keepout = StPetersGrass.BuildingKeepouts
                .FirstOrDefault(k => Vector2.Distance(k.Position, StPetersCannery.Site) < 0.01f);

            Assert.Greater(keepout.RadiusMetres, 0f,
                "the cannery has no meadow keepout, so the grass grows through it");

            // ⭐ AND IT IS THE ONE SITE ON THE ISLAND THAT NEEDS MORE THAN THE 7 m FLOOR. Its 9.06 m of
            // half-diagonal is what made that radius per site instead of one constant — see
            // StPetersGrass.MeadowKeepout.
            float reach = StPetersCannery.FootprintRadiusMetres;
            if (reach > 0f)
                Assert.GreaterOrEqual(keepout.RadiusMetres, reach,
                    $"the cannery reserves {reach:0.00} m but clears only {keepout.RadiusMetres:0.00} m " +
                    "of meadow — a quarter-turned building would have grass through its corner");
        }

        // =====================================================================================
        //  the art the world asks for exists
        // =====================================================================================

        /// <summary>
        /// Every build key the world names is one the kit declares.
        ///
        /// <para>Two files agreeing about a set of strings, checked by neither the compiler nor the bake:
        /// a shed row naming a build that is not there places a greybox marker and logs a warning nobody
        /// reads. This makes it red instead.</para>
        /// </summary>
        [Test]
        public void EveryBuildKeyTheWorldNames_IsOneTheKitDeclares()
        {
            foreach (var shed in StPetersGinnyPlot.Sheds)
            {
                Assert.IsFalse(string.IsNullOrEmpty(shed.BuildKey),
                    $"the {shed.Key} names no build, so it can only ever be a greybox marker");
                Assert.IsNotNull(VillageBuildingKit.FindBuild(shed.BuildKey),
                    $"the {shed.Key} asks for build '{shed.BuildKey}', which the kit does not declare");
            }

            Assert.IsNotNull(VillageBuildingKit.FindBuild(StPetersCannery.BuildKey),
                $"the cannery asks for build '{StPetersCannery.BuildKey}', which the kit does not declare");
        }

        /// <summary>
        /// The three derelicts are in three DIFFERENT states, and none of them is "finished".
        ///
        /// <para>Ginny's plot describes them in prose — one still standing squarest, one furthest gone,
        /// one with its back broken — and the art was chosen to match those sentences rather than the
        /// other way round. Three buildings in one state would be one shed copied about, which is what
        /// the greybox markers already were.</para>
        /// </summary>
        [Test]
        public void TheThreeDerelicts_AreThreeDifferentStates_AndNoneOfThemIsSound()
        {
            var builds = StPetersGinnyPlot.Sheds
                                          .Select(s => VillageBuildingKit.FindBuild(s.BuildKey))
                                          .Where(b => b.HasValue)
                                          .Select(b => b.Value)
                                          .ToArray();

            Assert.AreEqual(StPetersGinnyPlot.Sheds.Count, builds.Length,
                "a shed names a build that is not in the kit");

            foreach (var b in builds)
                Assert.IsTrue(b.HasLifecycle,
                    $"'{b.Key}' is one of Ginny's derelicts but is baked in repair");

            CollectionAssert.AllItemsAreUnique(builds.Select(b => b.StateTag).ToArray(),
                "two of the three sheds are in the same state, so they read as one shed copied about");

            CollectionAssert.AllItemsAreUnique(builds.Select(b => b.Preset).ToArray(),
                "two of the three sheds are the same preset, so they are the same building twice");

            Debug.Log("[stpeters-cannery] Ginny's derelicts: " +
                      string.Join(", ", builds.Select(b => $"{b.Key}={b.Preset}@{b.StateTag}")));
        }

        // =====================================================================================
        //  it actually stands up
        // =====================================================================================

        /// <summary>
        /// <c>StPetersCannery.Place</c> builds a cannery, at the site, sorted into the world.
        ///
        /// <para>A claim about what the BUILDER produces, not about what the kit declares — scene-wired
        /// is not builder-wired, and only one of those two ships.</para>
        /// </summary>
        [Test]
        public void ThePlacerStandsTheCannery_AtItsSite()
        {
            var greybox = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            GameObject built = null;
            try
            {
                Assert.IsTrue(StPetersCannery.Place(null, _terrain, greybox),
                    "the placer reported that it built nothing");

                built = GameObject.Find(StPetersCannery.RootName);
                Assert.IsNotNull(built, "no cannery in the scene after Place");

                Assert.That(Vector2.Distance(built.transform.position, StPetersCannery.Site),
                            Is.LessThan(0.01f),
                            "the cannery is not standing at its own site — the rig's pivot IS the ground " +
                            "centre, so the site position is the position and there is no offset to apply");

                var sr = built.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(sr, "the cannery has no renderer");
                Assert.IsNotNull(sr.sprite, "the cannery has a renderer with no sprite");

                // Baked or greybox, it layers by world Y like everything else you walk around (ADR 0032).
                Assert.IsNotNull(built.GetComponent<HiddenHarbours.Art.YSortSprite>(),
                    "the cannery does not Y-sort, so the player walks behind it at every position");
            }
            finally
            {
                if (built != null) Object.DestroyImmediate(built);
                Object.DestroyImmediate(greybox);
            }
        }
    }
}
