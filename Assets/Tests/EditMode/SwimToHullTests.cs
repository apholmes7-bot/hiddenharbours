using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Player;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>SWIMMING UP TO A HULL — measured on the REAL St Peters seabed, not on a shore invented to
    /// make the rule look good.</b>
    ///
    /// <para>The owner, 2026-09-02: <i>"for now a player should be able to swim up to a hull and climb
    /// aboard anywhere"</i>. The obstacle was never the boarding gate — PR 2 established there is no swim
    /// check on it — it is the ratified water-travel model's boat-only soft wall, which keeps a person
    /// out of anything deeper than <c>GameConfig.SwimLimit</c>. Both boats at this pier lie over the
    /// dredged −4 m pocket, so the wall stood between the player and the two hulls she is supposed to be
    /// able to reach, on her own doorstep.</para>
    ///
    /// <para><b>What these hold.</b> That the wall really did refuse her (the <c>Before_</c> sabotage,
    /// in metres), that a registered hull opens it exactly as far as
    /// <c>GameConfig.SwimBoardReachMetres</c> and no further, and that the fairway 10 m off is still
    /// boat-only water — the owner's hard rule, which this relaxation is deliberately the narrowest
    /// possible dent in.</para>
    /// </summary>
    public class SwimToHullTests
    {
        private GameObject _terrainGo;
        private TidalTerrain _terrain;
        private GameObject _pierGo;
        private StandablePlatform _pier;
        private FixedTide _tide;

        private const string GameConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>The SHIPPED tunables, read off the owner's own asset. Never mirrored here: a test
        /// that restates 6.0 goes on passing after he tunes the reach to 3, and reports a berth as
        /// swimmable on a number the game no longer uses (the mirror this arc already deleted once —
        /// <c>StPetersDoryBerthTests.BoardReachMetres</c>).</summary>
        private static GameConfig ShippedConfig
        {
            get
            {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigAssetPath);
                Assert.IsNotNull(config, $"the shipped {GameConfigAssetPath} must exist — the swimmer's " +
                                         "reach is the owner's number, not this file's");
                return config;
            }
        }

        /// <summary>A still environment at a chosen water level — the tide, held where a test wants it.</summary>
        private sealed class FixedTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>A hull lying exactly where the region's own constants put her.</summary>
        private sealed class BerthedHull : IHullPresence
        {
            public HullFootprint Footprint { get; set; }
        }

        const float SpringHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;
        const float SpringLow = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        // Where a fisher stands on the planks to look over each face. 40 cm in from the lip, so the walk
        // model's own 0.5 m look-ahead probe lands unambiguously OFF the deck and in the berth: standing
        // ON the boundary would have the probe testing the plank edge itself, and the test would measure
        // a rounding decision instead of the sea.
        static float OnThePlanksNorth => StPetersWharf.NorthFaceY - 0.4f;
        static float OnThePlanksSouth => StPetersWharf.MooringFaceY + 0.4f;

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            HullPresences.Clear();

            _terrainGo = new GameObject("StPetersTerrain_SwimToHullTest");
            _terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);

            // Exactly what StPetersWharf.Place() registers, so standing on the planks reads as standing
            // on the planks rather than as standing in the berth they are built over.
            _pierGo = new GameObject("StPetersWharf_SwimToHullTest");
            _pier = _pierGo.AddComponent<StandablePlatform>();
            _pier.Configure(StPetersWharf.SurfaceId, StPetersWharf.DeckFootprint(),
                            StPetersWharf.DeckElevationFrom(_terrain));

            _tide = new FixedTide { Level = StPetersBuilder.TideMean };
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            HullPresences.Clear();
            if (_pierGo != null) Object.DestroyImmediate(_pierGo);
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        private IStandableSurface[] Deck => new IStandableSurface[] { _pier };

        private float DepthAt(Vector2 p)
            => TidalWalkability.DepthAt(_terrain, _tide, Deck, 0.0, p);

        /// <summary>The starter dory as she lies: alongside the pier's north face, on the pier's axis.</summary>
        private static HullFootprint TheDory() => HullFootprint.FromHeading(
            StPetersBuilder.DoryMooredPos, StPetersBuilder.DoryMooredHeadingDegrees,
            StPetersBuilder.DoryLengthMetres, StPetersBuilder.DoryHalfBeamMetres);

        /// <summary>The arriving cape islander on her berth alongside the south face.</summary>
        private static HullFootprint TheCape() => HullFootprint.FromHeading(
            StPetersBuilder.DockZonePos, StPetersArrivalOpening.BerthHeadingDegrees(),
            StPetersBuilder.ArrivalHullLengthMetres, StPetersBuilder.ArrivalHullHalfBeamMetres);

        private static IReadOnlyList<IHullPresence> Fleet(params HullFootprint[] hulls)
        {
            var list = new List<IHullPresence>();
            foreach (var h in hulls) list.Add(new BerthedHull { Footprint = h });
            return list;
        }

        /// <summary>
        /// One tick of the real walk model at <paramref name="from"/>, pushing along
        /// <paramref name="push"/>, with <paramref name="hulls"/> registered (null = none).
        /// </summary>
        private Vector2 Step(Vector2 from, Vector2 push, IReadOnlyList<IHullPresence> hulls)
        {
            GameConfig config = ShippedConfig;
            System.Func<Vector2, bool> alongside = hulls == null
                ? null
                : p => HullPresences.WithinReachOf(hulls, p, config.SwimBoardReachMetres);

            return PlayerWalkController.ApplyWaterEdge(push, from, DepthAt, 0.5f,
                                                       config.WadeDepth, config.SwimLimit,
                                                       config.WadeSlowFactor, config.SwimSlowFactor,
                                                       alongside);
        }

        // =================================================================================
        //  THE DEFECT, MEASURED
        // =================================================================================

        /// <summary>
        /// ⭐ <b>SABOTAGE ARM.</b> Standing on her own planks a body-length from her own boat, the model
        /// as it shipped refused to let her into the water.
        ///
        /// <para><b>⚠ MEASURED, not assumed — and the first draft of this test was wrong.</b> Her berth is
        /// dredged to −4 m, so at mean and spring high the water beside her reads 3.97 m and 6.17 m:
        /// boat-only, refused. At <b>spring low</b> it reads <b>1.77 m</b>, which is inside the slow-swim
        /// band, and the wall never refused her there at all. So the defect this relaxation closes is real
        /// for most of the tide and absent at the bottom of it. What is asserted is therefore the
        /// EQUIVALENCE rather than a flat refusal: wherever the water off her planks was boat-only, she was
        /// walled out of it.</para>
        /// </summary>
        [Test]
        public void Before_WhereverTheWaterOffHerPlanksWasBoatOnly_SheWasRefused()
        {
            Vector2 onThePlanks = new Vector2(StPetersBuilder.DoryMooredX, OnThePlanksNorth);
            Assert.Less(DepthAt(onThePlanks), 0f, "she is standing on the deck, dry");

            int refusals = 0;
            foreach (float level in new[] { SpringLow, StPetersBuilder.TideMean, SpringHigh })
            {
                _tide.Level = level;
                float inTheBerth = DepthAt(StPetersBuilder.DoryMooredPos);
                bool boatOnly = inTheBerth > ShippedConfig.SwimLimit;

                Vector2 stepped = Step(onThePlanks, new Vector2(0f, 3f), hulls: null);
                bool refused = Mathf.Approximately(stepped.y, 0f);

                Assert.AreEqual(boatOnly, refused,
                    $"at water level {level:0.00} m her berth reads {inTheBerth:0.00} m " +
                    $"(boat-only={boatOnly}) but the wall says refused={refused} — the soft wall and the " +
                    "band it enforces are supposed to be one law wearing two hats");
                if (refused) refusals++;
            }

            Assert.AreEqual(2, refusals,
                "at TWO of the three tide states the step off her own planks toward her own boat was " +
                "refused — mean and spring high; at spring low the berth is 1.77 m, inside the swim band. " +
                "If this ever reads 0 the wall is gone and the rest of this file is measuring nothing");
        }

        // =================================================================================
        //  THE FIX: ALONGSIDE A HULL, THE WALL STEPS ASIDE
        // =================================================================================

        /// <summary>
        /// The fix, at every state of the tide. ⚠ At spring low this passes for a second reason as well —
        /// her berth is 1.77 m then, inside the swim band, so the wall was not refusing her there anyway
        /// (see <see cref="Before_WhereverTheWaterOffHerPlanksWasBoatOnly_SheWasRefused"/>). The two tide
        /// states where the wall really stood are the ones this earns.
        /// </summary>
        [Test]
        public void FromThePlanksBesideHerOwnDory_SheMayNowGoIn()
        {
            Vector2 onThePlanks = new Vector2(StPetersBuilder.DoryMooredX, OnThePlanksNorth);

            foreach (float level in new[] { SpringLow, StPetersBuilder.TideMean, SpringHigh })
            {
                _tide.Level = level;
                var went = Step(onThePlanks, new Vector2(0f, 3f), Fleet(TheDory()));
                Assert.Greater(went.y, 0f,
                    $"at water level {level:0.00} m, with the dory registered, she goes over the edge " +
                    "into the water beside her own boat");
            }
        }

        [Test]
        public void AndTheCapeOnHerBerthIsReachableFromThePlanksToo()
        {
            Vector2 onThePlanks = new Vector2(StPetersBuilder.AlongsideBerthX, OnThePlanksSouth);
            _tide.Level = StPetersBuilder.TideMean;

            float toHer = HullPresences.DistanceToNearestOutline(Fleet(TheCape()), onThePlanks);
            Assert.LessOrEqual(toHer, ShippedConfig.SwimBoardReachMetres,
                $"the cape's outline is {toHer:0.00} m from the point the arrival sets the player down " +
                $"on, inside the {ShippedConfig.SwimBoardReachMetres:0.0#} m reach — so the water beside " +
                "the boat she just got off is water she may get back into");

            var went = Step(onThePlanks, new Vector2(0f, -3f), Fleet(TheCape()));
            Assert.Less(went.y, 0f, "…and the step south off the planks toward her is allowed");
        }

        /// <summary>
        /// The charter's own pin, in the region's numbers: 5 m off the dory the wall is open. Measured to
        /// her OUTLINE — see <see cref="HullPresencesTests"/> for why a root reading is two opposite
        /// wrong answers.
        /// </summary>
        [Test]
        public void FiveMetresOffTheDory_TheWallIsOpen()
        {
            var hulls = Fleet(TheDory());
            Vector2 fiveOff = TheDory().ClosestPoint(new Vector2(StPetersBuilder.DoryMooredX, 100f))
                              + new Vector2(0f, 5f);

            Assert.AreEqual(5f, HullPresences.DistanceToNearestOutline(hulls, fiveOff), 1e-3f,
                "the probe point really is 5 m off her rail");
            Assert.IsTrue(HullPresences.WithinReachOf(hulls, fiveOff, ShippedConfig.SwimBoardReachMetres),
                "5 m off a hull is alongside her, so the boat-only wall does not apply there");
        }

        // =================================================================================
        //  ⭐ EVERYWHERE ELSE THE WALL STANDS — the owner's hard rule, undamaged
        // =================================================================================

        /// <summary>
        /// ⭐ <b>The other half of the ruling, and the half a relaxation loses first.</b> Water travel is
        /// boats only (the 2026-07-05 ratification). Out of reach of every hull the sea is exactly as
        /// closed as it was before this seam existed.
        ///
        /// <para><b>⚠ The spot is SWEPT, not chosen.</b> The first draft stood her 10 m west along the
        /// north lip and asserted a refusal — and measured <b>−0.66 m</b>: that far inshore the pier runs
        /// over ground the tide bares, so there was no wall there to test and the "control" would have
        /// passed for the wrong reason. This walks both faces of the pier and tests every plank point that
        /// actually poses the question — she is standing dry, the water off the lip is boat-only, and no
        /// hull is within reach.</para>
        /// </summary>
        [Test]
        public void OutOfReachOfEveryHull_TheSeaIsStillBoatOnlyWater()
        {
            var hulls = Fleet(TheDory(), TheCape());
            float reach = ShippedConfig.SwimBoardReachMetres;
            _tide.Level = StPetersBuilder.TideMean;

            int examined = 0, refused = 0;
            float nearestHullExamined = float.PositiveInfinity;

            foreach (float lip in new[] { OnThePlanksNorth, OnThePlanksSouth })
            {
                float outward = lip > 0f ? 1f : -1f;
                for (float x = StPetersWharf.RootCellX; x <= StPetersWharf.HeadCellX + 1f; x += 0.5f)
                {
                    var p = new Vector2(x, lip);
                    if (DepthAt(p) > 0f) continue;                                 // she must be standing dry
                    if (DepthAt(p + new Vector2(0f, outward * 0.5f)) <= ShippedConfig.SwimLimit) continue;
                    float toHull = HullPresences.DistanceToNearestOutline(hulls, p);
                    if (toHull <= reach) continue;                                 // alongside IS the feature

                    examined++;
                    if (toHull < nearestHullExamined) nearestHullExamined = toHull;
                    if (Mathf.Approximately(Step(p, new Vector2(0f, outward * 3f), hulls).y, 0f)) refused++;
                }
            }

            Assert.Greater(examined, 0,
                "this pier must have SOMEWHERE that poses the question — dry underfoot, boat-only water off " +
                "the lip, no hull within reach. A sweep that examines nothing proves nothing");
            Assert.AreEqual(examined, refused,
                $"⭐ {examined} plank points, the nearest of them {nearestHullExamined:0.0} m from any hull, " +
                "and every one still refuses the step into the sea. If this starts admitting steps the " +
                "relaxation has stopped being narrow and the owner's boats-only rule is gone");
        }

        /// <summary>
        /// The reach is a RADIUS round a boat, not a corridor joining one to the next: with both hulls
        /// registered there must still be water between the berths that nobody may swim in, or the
        /// relaxation quietly becomes "swim anywhere there are boats".
        /// </summary>
        [Test]
        public void TheEntranceFairwayIsNotASwimmingLane()
        {
            var hulls = Fleet(TheDory(), TheCape());
            float reach = ShippedConfig.SwimBoardReachMetres;

            // Due east of the pier head, out along the entrance's final leg — the water the cape sails in
            // on, which is the last place a person on foot should be able to get to. Started clear of the
            // cape's own bow (her outline reaches x 219.95) rather than at the head, because the metres
            // right off her stem ARE hers and are supposed to be swimmable.
            int examined = 0, walled = 0;
            for (float x = StPetersWharf.HeadCellX + 14f; x <= StPetersWharf.HeadCellX + 36f; x += 1f)
            {
                examined++;
                if (!HullPresences.WithinReachOf(hulls, new Vector2(x, 0f), reach)) walled++;
            }
            Assert.AreEqual(23, examined, "the sweep must actually sweep — a vacuous loop proves nothing");
            Assert.AreEqual(examined, walled,
                "every metre of the fairway from 14 m off the pier head outward is out of reach of both " +
                "berthed hulls — the entrance is not a swimming lane");
        }
    }
}
