using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ THE ST PETERS COAST STANDS UP — the sector plan, the cliff height statements, and the
    /// tide-qualified walk that makes a low-tide ledge a place rather than a texture.
    ///
    /// <para><b>Every number here is DERIVED from the plan the builder actually pushes</b>
    /// (<see cref="StPetersBuilder.CoastSectors"/> → the component's own
    /// <see cref="TidalTerrain.CoastClassAt"/>), never from a literal copied out of it. That is the
    /// house's derived-bounds law and it earns its keep twice over on this feature: a classifier that
    /// silently died would move the measured cliff share by a FACTOR, not by a percent, and a bound
    /// written down by hand would have gone on passing.</para>
    ///
    /// <para><see cref="StPetersLayoutTests"/> is the sibling that asserts the island's geometry;
    /// <see cref="StPetersTerrainTests"/> asserts the tide's behaviour on it. This asserts the coast
    /// BETWEEN them: which stretches stand as rock, and what the water does to each.</para>
    /// </summary>
    public class StPetersCoastTests
    {
        private GameObject _go;
        private TidalTerrain _terrain;
        private GameConfig _config;

        const float SpringHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;   //  2.2
        const float SpringLow  = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // -2.2

        private float WadeDepth => _config.WadeDepth;

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _go = new GameObject("StPetersTerrain_CoastTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
            StandableSurfaces.Clear();
            GameServices.Reset();
        }

        // =========================================================================================
        //  the shoreline, measured
        // =========================================================================================

        /// <summary>A point on the plateau-edge ellipse at a bearing — the reference curve every length
        /// measure below runs along. Chosen because it is the ONE curve common to all six classes and it
        /// is where the classification is decided; a cliff's waterline sits on it exactly, and a beach's
        /// sits further out, which if anything flatters the beaches.</summary>
        private static Vector2 ShorePoint(float bearingDegrees)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            return StPetersBuilder.IslandCenter
                   + new Vector2(StPetersBuilder.IslandRadius * Mathf.Sin(r),
                                 StPetersBuilder.IslandRadiusY * Mathf.Cos(r));
        }

        /// <summary>A point <paramref name="outMetres"/> of ELLIPTICAL distance seaward of the plateau
        /// edge on a bearing — the frame the profile is actually written in, so <c>outMetres</c> means
        /// the same thing at the island's ends as along its flanks. (In world metres it does not: a metre
        /// of northing is 120/70 of a metre of elliptical distance on this island, which is exactly the
        /// amplification that made an earlier version of the ledge test measure the wrong thing.)</summary>
        private static Vector2 ShorePointOut(float bearingDegrees, float outMetres)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            float d = StPetersBuilder.IslandRadius + outMetres;
            return StPetersBuilder.IslandCenter
                   + new Vector2(d * Mathf.Sin(r),
                                 d * Mathf.Cos(r) * StPetersBuilder.IslandRadiusY
                                   / StPetersBuilder.IslandRadius);
        }

        /// <summary>Arc length per radian of bearing at a bearing — the ellipse's own speed. Degrees are
        /// NOT metres on a 120 × 70 island: one near due south buys 120 m/rad of coast where one near the
        /// harbour buys 70, which is why every share below is measured in metres.</summary>
        private static float ShoreSpeed(float bearingDegrees)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            return new Vector2(StPetersBuilder.IslandRadius * Mathf.Cos(r),
                               StPetersBuilder.IslandRadiusY * Mathf.Sin(r)).magnitude;
        }

        const int ShoreSamples = 7200;          // 0.05° — finer than the narrowest sector by ~150×

        /// <summary>Walk the whole shoreline through the LIVE decider and total the metres each class
        /// owns. This is the measurement the cliff share, the aspect law and the crossings are all
        /// asserted from.</summary>
        private Dictionary<CoastClass, float> MeasureCoast(out float total)
        {
            var metres = new Dictionary<CoastClass, float>();
            float step = 360f / ShoreSamples;
            total = 0f;
            for (int i = 0; i < ShoreSamples; i++)
            {
                float bearing = (i + 0.5f) * step;
                float length = ShoreSpeed(bearing) * step * Mathf.Deg2Rad;
                CoastClass c = _terrain.CoastClassAt(ShorePoint(bearing));
                metres.TryGetValue(c, out float had);
                metres[c] = had + length;
                total += length;
            }
            return metres;
        }

        private float CliffMetres(Dictionary<CoastClass, float> byClass)
        {
            float m = 0f;
            foreach (var kv in byClass) if (CoastPlan.IsCliff(kv.Key)) m += kv.Value;
            return m;
        }

        // =========================================================================================
        //  1. the cliff share — and the arithmetic that decides what it CAN be
        // =========================================================================================

        /// <summary>
        /// ⭐ THE HEADLINE: about half the coast is rock — measured, from the same decider the builder
        /// runs, against bounds DERIVED from the aspect law rather than written down.
        ///
        /// <para>The owner asked for ~50%. That is not expressible on this island and this test is where
        /// the arithmetic lives: the kit authors no north-facing wall, so a cliff may stand only where the
        /// outward normal snaps to one of five facings, which on a 120 × 70 m ellipse is 55.6% of the
        /// shoreline — and the sandbar's landing and the berth's cove must both be walkable shore inside
        /// that arc. Whatever is left is the ceiling, and the plan is asserted to reach most of it.</para>
        /// </summary>
        [Test]
        public void TheCliffShareIsAboutHalfTheCoast_AndSitsUnderTheAspectLawsOwnCeiling()
        {
            var byClass = MeasureCoast(out float total);
            float cliff = CliffMetres(byClass);
            float share = cliff / total;

            // The LEGAL arc, re-derived here from the normal-azimuth law rather than from the plan — so
            // this bound moves if the island is ever re-ruled a different shape, and does not move if
            // somebody merely edits a sector.
            float legal = 0f;
            float step = 360f / ShoreSamples;
            for (int i = 0; i < ShoreSamples; i++)
            {
                float bearing = (i + 0.5f) * step;
                if (CoastPlan.CliffIsLegalAt(bearing, StPetersBuilder.IslandRadius,
                                             StPetersBuilder.IslandRadiusY))
                    legal += ShoreSpeed(bearing) * step * Mathf.Deg2Rad;
            }

            Assert.LessOrEqual(cliff, legal + 1e-3f,
                $"cliff runs where the kit cannot draw it: {cliff:F1} m of cliff on {legal:F1} m of " +
                "legal arc. Some sector's outward normal is more than 22.5° from every authored aspect.");

            // A dead classifier answers ONE class everywhere: 0% (all beach) or ~55.6% (all cliff, which
            // the bound above would already catch). Both are far outside this band; a sector nudged by a
            // few degrees is well inside it.
            Assert.That(share, Is.InRange(0.34f, 0.47f),
                $"the cliff share is {share:P2} ({cliff:F1} m of {total:F1} m). Expected roughly two " +
                "fifths of the coast — see the derivation on StPetersBuilder.CoastSectors.");

            // …and it should USE the arc it is allowed, or the plan is timid rather than constrained.
            Assert.Greater(cliff / legal, 0.65f,
                $"only {cliff / legal:P0} of the legal arc stands up — the aspect law is not what is " +
                "limiting this coast, the plan is.");

            var report = new StringBuilder();
            report.AppendLine($"[coast] shoreline {total:F1} m (plateau-edge ellipse)");
            foreach (var kv in byClass)
                report.AppendLine($"        {kv.Key,-16} {kv.Value,7:F1} m  {kv.Value / total:P2}");
            report.AppendLine($"        CLIFF (3 classes) {cliff:F1} m = {share:P2}");
            report.AppendLine($"        aspect-law legal arc {legal:F1} m = {legal / total:P2} " +
                              $"— the plan uses {cliff / legal:P1} of it");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// THE ASPECT LAW, pointwise: every metre classified as cliff must face a direction the kit
        /// actually authors. The kit draws W · SW · S · SE · E and no north, because a north-facing wall
        /// shows its shadowed back to the camera and reads as a hole punched in the coast.
        ///
        /// <para>Tested on the outward NORMAL, never on the bearing — on this ellipse they differ by up
        /// to 14°, and testing the bearing would pass a coast the kit cannot draw.</para>
        /// </summary>
        [Test]
        public void EveryCliffMetreFacesADirectionTheKitAuthors()
        {
            float step = 360f / ShoreSamples;
            float worst = 0f;
            float worstBearing = 0f;
            for (int i = 0; i < ShoreSamples; i++)
            {
                float bearing = (i + 0.5f) * step;
                if (!CoastPlan.IsCliff(_terrain.CoastClassAt(ShorePoint(bearing)))) continue;
                float error = CoastPlan.AspectSnapError(
                    CoastPlan.OutwardNormalAzimuth(bearing, StPetersBuilder.IslandRadius,
                                                   StPetersBuilder.IslandRadiusY));
                if (error > worst) { worst = error; worstBearing = bearing; }
            }
            Assert.LessOrEqual(worst, CoastPlan.AspectSnapToleranceDegrees + 1e-3f,
                $"a cliff at bearing {worstBearing:F1}° faces {worst:F2}° from the nearest authored " +
                "aspect — further than the kit's 22.5° snap can absorb.");
            Debug.Log($"[coast] worst aspect-snap error over every cliff metre: {worst:F2}° " +
                      $"(law {CoastPlan.AspectSnapToleranceDegrees}°) at bearing {worstBearing:F1}°");
        }

        /// <summary>
        /// The ring is well formed, and every sector is wide enough to BE itself. A sector narrower than
        /// twice the blend feather never reaches its own profile at any bearing — it would be authored,
        /// counted, and invisible, which is the quietest way to lose an access trail.
        /// </summary>
        [Test]
        public void ThePlanIsAClosedRing_AndNoSectorIsNarrowerThanItsOwnBlend()
        {
            CoastSector[] plan = _terrain.CoastSectors;
            Assert.IsNotNull(plan);
            Assert.Greater(plan.Length, 1, "a one-sector plan is not a coast");

            for (int i = 1; i < plan.Length; i++)
                Assert.Greater(plan[i].FromBearing, plan[i - 1].FromBearing,
                    $"sector {i} starts at {plan[i].FromBearing}°, before sector {i - 1} " +
                    $"({plan[i - 1].FromBearing}°) — the ring must be ordered clockwise");

            float feather = _terrain.CoastBlendDegrees;
            Assert.Greater(feather, 0f, "an unfeathered plan puts a 6.5 m step across the shore");
            for (int i = 0; i < plan.Length; i++)
            {
                float width = CoastPlan.SectorWidth(plan, i);
                Assert.Greater(width, 2f * feather,
                    $"sector {i} ({plan[i].Class}) is {width:F2}° wide against a {feather}° feather — " +
                    "it never reaches its own profile, so it is authored but not present");
            }
        }

        /// <summary>The two crossings survive the reshape. The sandbar is the tide-gated WALK to Nine
        /// Mile Creek and the berth is the ONE door in the reef; a cliff across either would be a
        /// region-breaking bug that no rendering test would ever catch.</summary>
        [Test]
        public void TheSandbarLandingAndTheBerthCoveAreNotCliff()
        {
            foreach (var (name, worldPos) in new (string, Vector2)[]
            {
                ("the sandbar's island end", StPetersBuilder.SandbarFrom),
                ("the berth's shore end",    StPetersBuilder.BerthTo),
                ("the dock zone",            StPetersBuilder.DockZonePos),
                ("the disembark step",       StPetersBuilder.DisembarkPos),
            })
            {
                CoastClass c = _terrain.CoastClassAt(worldPos);
                Assert.IsFalse(CoastPlan.IsCliff(c),
                    $"{name} sits on a {c} — the crossing it carries cannot be reached over a wall");
            }

            // …and the whole width the bar actually BARES across, not just its centre-line.
            float halfBared = StPetersBuilder.SandbarHalfWidth * 0.6f;
            for (float y = -halfBared; y <= halfBared; y += 2f)
            {
                var p = new Vector2(StPetersBuilder.SandbarFrom.x, y);
                Assert.IsFalse(CoastPlan.IsCliff(_terrain.CoastClassAt(p)),
                    $"the bar's landing is cliff {y:F0} m off its centre-line");
            }
        }

        // =========================================================================================
        //  2. the additive guarantee
        // =========================================================================================

        /// <summary>
        /// ⭐ THE WIDENING IS ADDITIVE — the condition the coordinator attached to the sanctioned
        /// <see cref="TidalTerrain.IslandProfile(float)"/> edit. The radial signature stays, and it still
        /// answers the pre-cliff chain EXACTLY: plateau → beach → shelf → drop-off → floor.
        ///
        /// <para>Asserted against an independent restatement of the old chain rather than against the
        /// shipped method, so a bug introduced INTO that method is caught rather than mirrored.</para>
        /// </summary>
        [Test]
        public void TheRadialProfileStillAnswersThePreCliffChain_ByteForByte()
        {
            float rx = StPetersBuilder.IslandRadius;
            float fall = StPetersBuilder.IslandFalloff;
            float shelfW = StPetersBuilder.ReefShelfWidth;
            float beachEnd = rx + fall;
            float shelfEnd = beachEnd + shelfW;

            float Old(float d)
            {
                if (d <= rx) return StPetersBuilder.IslandElevation;
                if (d <= beachEnd)
                    return Ease(d, rx, fall, StPetersBuilder.IslandElevation,
                                StPetersBuilder.ReefShelfInnerElevation);
                if (d <= shelfEnd)
                    return Ease(d, beachEnd, shelfW, StPetersBuilder.ReefShelfInnerElevation,
                                StPetersBuilder.ReefShelfOuterElevation);
                return Ease(d, shelfEnd, Mathf.Max(1f, fall),
                            StPetersBuilder.ReefShelfOuterElevation, StPetersBuilder.DeepHarbourElevation);
            }

            for (float d = 0f; d <= shelfEnd + 200f; d += 0.25f)
                Assert.AreEqual(Old(d), _terrain.IslandProfile(d), 1e-4f,
                    $"the radial profile moved at d = {d:F2} m — the widening was supposed to be " +
                    "additive, and every caller that never heard of cliffs reads this overload");

            // And the same is true of the class the soft coast actually uses.
            for (float d = 0f; d <= shelfEnd + 50f; d += 0.5f)
            {
                Assert.AreEqual(_terrain.IslandProfile(d), _terrain.IslandProfile(d, CoastClass.Beach), 1e-6f);
                Assert.AreEqual(_terrain.IslandProfile(d), _terrain.IslandProfile(d, CoastClass.Dune), 1e-6f,
                    "a dune is a beach wearing marram — if its HEIGHT diverges, the north coast, the " +
                    "clam field and every decor pin on them move for no reason anybody chose");
                Assert.AreEqual(_terrain.IslandProfile(d), _terrain.IslandProfile(d, CoastClass.Access), 1e-6f);
            }
        }

        /// <summary>The old <c>Lerped</c> easing, restated — smoothstep from inner to outer across the
        /// band, held at outer beyond it.</summary>
        private static float Ease(float distance, float flat, float falloff, float inner, float outer)
        {
            if (distance <= flat) return inner;
            if (falloff <= 0f) return outer;
            float u = Mathf.Clamp01((distance - flat) / falloff);
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
        }

        /// <summary>SABOTAGE: a coast with no plan must be the coast that predates cliffs, everywhere.
        /// This is what makes the feature safe to land — every OTHER region carries no sector ring, and
        /// none of them may move by a millimetre.</summary>
        [Test]
        public void Sabotage_APlanlessTerrainIsBitIdenticalToThePreCliffCoast()
        {
            var bare = new GameObject("PlanlessTerrain");
            try
            {
                var plain = bare.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(plain);
                plain.CoastSectors = new CoastSector[0];

                // With no plan the bearing-aware profile must BE the radial one, everywhere and exactly.
                // Compared against IslandProfile rather than against ElevationAt, because ElevationAt also
                // composes the bar, the channel and the berth — features this widening never touched, and
                // whose presence would make a difference here mean nothing.
                int moved = 0;
                for (float bearing = 0f; bearing < 360f; bearing += 1.7f)
                for (float d = 0f; d < 200f; d += 3f)
                {
                    Vector2 p = ShorePoint(bearing);
                    if (Mathf.Abs(plain.IslandProfileAt(d, p) - plain.IslandProfile(d)) > 0f) moved++;
                }
                Assert.AreEqual(0, moved,
                    $"{moved} samples of a PLANLESS coast disagree with the radial profile — the " +
                    "widening leaked into regions that never authored a sector");

                // …and the shipped plan does NOT agree with it, or none of this does anything.
                //
                // ⚠ Sampled SEAWARD of the plateau edge, not on it. On the edge itself both coasts
                // answer the plateau height — IslandProfile returns it before it ever dispatches on
                // class — so the first version of this check compared the one place on the whole island
                // where a cliff and a beach are guaranteed to agree, and reported 0 differences on a
                // coast that had changed completely.
                int differ = 0;
                for (float bearing = 100f; bearing < 250f; bearing += 1.7f)
                for (float outM = 2f; outM <= 10f; outM += 4f)
                {
                    Vector2 p = ShorePointOut(bearing, outM);
                    if (Mathf.Abs(_terrain.ElevationAt(p) - plain.ElevationAt(p)) > 0.5f) differ++;
                }
                Assert.Greater(differ, 20,
                    "SABOTAGE NOT DETECTED — the planned coast matches the planless one along the whole " +
                    "south shore, so the sector plan is doing nothing and every test above is vacuous");
            }
            finally { Object.DestroyImmediate(bare); }
        }

        // =========================================================================================
        //  3. the three cliff classes are three HEIGHT statements
        // =========================================================================================

        /// <summary>
        /// The tide reference the fraction elevations are authored against is the region's OWN tide.
        /// If these drift, every ledge in the region silently moves relative to the water that is
        /// supposed to work it — the exact failure the 2026-08-01 pacing ruling was written to prevent.
        /// </summary>
        [Test]
        public void TheTerrainsTideReferenceIsTheRegionsAuthoredTide()
        {
            Assert.AreEqual(StPetersBuilder.TideMean, _terrain.TideMean, 1e-4f);
            Assert.AreEqual(StPetersBuilder.TideAmplitude, _terrain.TideAmplitude, 1e-4f);
            Assert.AreEqual(SpringLow, _terrain.SpringLowWater, 1e-4f);
        }

        /// <summary>
        /// ⭐ THE LEDGE IS AUTHORED AS A TIDE FRACTION, NOT AS A METRE — and this is what proves it.
        /// Retune the amplitude and the bench must MOVE WITH THE WATER; an absolute elevation would sit
        /// still and be drowned (or stranded) by the owner's next pacing pass, silently.
        /// </summary>
        [Test]
        public void RetuningTheTideMovesTheLedgeWithIt()
        {
            float benchAt2_2 = _terrain.LedgeBenchElevation;
            float toeAt2_2 = _terrain.CliffToeElevation;
            Assert.Greater(benchAt2_2, _terrain.SpringLowWater,
                "the bench must sit ABOVE the lowest water or it never bares at all");

            var so = new UnityEditor.SerializedObject(_terrain);
            so.FindProperty("_tideAmplitude").floatValue = StPetersBuilder.TideAmplitude * 2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // "Moved by more than a tolerance" is Greater-on-the-magnitude, NOT AreNotEqual-with-a-delta:
            // NUnit gives AreEqual a tolerance overload and AreNotEqual none, so the third argument there
            // is a MESSAGE and the delta form does not compile.
            Assert.Greater(Mathf.Abs(_terrain.LedgeBenchElevation - benchAt2_2), 0.05f,
                "the ledge bench did not move when the amplitude doubled — it is authored as an " +
                "ABSOLUTE elevation, which the tide-pacing ruling forbids");
            Assert.Greater(_terrain.LedgeBenchElevation, _terrain.SpringLowWater,
                "the bench must stay above the lowest water at ANY amplitude — that is the whole " +
                "point of stating it as a fraction");
            Assert.Less(_terrain.CliffToeElevation, _terrain.SpringLowWater,
                "a plain cliff's foot must stay BELOW the lowest water at any amplitude");
            Assert.Greater(Mathf.Abs(_terrain.CliffToeElevation - toeAt2_2), 0.05f,
                "the cliff toe did not move with the amplitude either");
        }

        /// <summary>
        /// The three cliff classes are three different offers to a boat and to a walker, and each one is
        /// a statement about where its FOOT sits relative to the water:
        /// deep-shore below everything (lie alongside) · plain below the lowest water (nothing bares) ·
        /// ledge just above it (a bench the tide lends you).
        /// </summary>
        [Test]
        public void TheThreeCliffFeetSitAtThreeDifferentPlacesInTheTide()
        {
            Assert.AreEqual(StPetersBuilder.DeepHarbourElevation, _terrain.DeepShoreToeElevation, 1e-4f,
                "a deep-shore face must reach the harbour floor, or a hull cannot lie close alongside");
            Assert.Less(_terrain.CliffToeElevation, SpringLow,
                "a plain cliff's foot bares at spring low — then it is a ledge, and the class is a lie");
            Assert.Greater(_terrain.LedgeBenchElevation, SpringLow,
                "the ledge bench never bares — then it is a plain cliff, and the class is a lie");
            Assert.Less(_terrain.LedgeBenchElevation, StPetersBuilder.TideMean,
                "a bench above mean water is a beach, not a foreshore the tide works");

            Assert.Less(_terrain.DeepShoreToeElevation, _terrain.CliffToeElevation,
                "the ladder must be strictly ordered: deep-shore < plain < ledge");
            Assert.Less(_terrain.CliffToeElevation, _terrain.LedgeBenchElevation);
        }

        /// <summary>A cliff PLUNGES and a beach does not — stated as the thing a player sees, the
        /// gradient, and derived from the two profiles rather than asserted as a number.</summary>
        [Test]
        public void ACliffFallsAtLeastFiveTimesAsSteeplyAsTheBeachBesideIt()
        {
            float rx = StPetersBuilder.IslandRadius;
            float Gradient(CoastClass c)
            {
                float top = _terrain.IslandProfile(rx + 0.05f, c);
                float bottom = _terrain.IslandProfile(rx + StPetersBuilder.CliffPlungeWidth, c);
                return (top - bottom) / Mathf.Max(StPetersBuilder.CliffPlungeWidth, 1e-4f);
            }
            float beach = Gradient(CoastClass.Beach);
            foreach (var c in new[] { CoastClass.Cliff, CoastClass.DeepShoreCliff, CoastClass.LedgeCliff })
                Assert.Greater(Gradient(c), beach * 5f,
                    $"{c} falls at {Gradient(c):F2} m/m against the beach's {beach:F2} — that is a " +
                    "slope with rock painted on it, which is the owner's actual complaint");
        }

        // =========================================================================================
        //  4. the tide-qualified walk — the acceptance that eyeballing cannot give
        // =========================================================================================

        /// <summary>Can a person stand here at this water level? The Core rule, unmodified — exposure and
        /// the wade band, read through the same seam the on-foot controller reads.</summary>
        private bool Walkable(Vector2 p, float waterLevel) =>
            TidalExposure.IsWalkable(waterLevel, _terrain.ElevationAt(p), WadeDepth);

        /// <summary>
        /// A flood fill over walkable ground from the village, at one state of tide. This is the
        /// acceptance the handoff asks for and the reason an eyeball cannot give it: "you can get there"
        /// is a question about a GRAPH, and a trail that looks connected can be severed by one metre of
        /// water nobody thought to stand in.
        /// </summary>
        private HashSet<Vector2Int> ReachableAt(float waterLevel, float cell = 2f)
        {
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            Vector2Int Key(Vector2 p) =>
                new Vector2Int(Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell));
            Vector2 World(Vector2Int k) => new Vector2(k.x * cell, k.y * cell);

            Vector2Int start = Key(StPetersBuilder.StartSpawnPos);
            Assert.IsTrue(Walkable(World(start), waterLevel),
                "the spawn itself is not walkable — the fill has nowhere to start");
            seen.Add(start);
            queue.Enqueue(start);

            // Bounded to the island and its apron; the fill must not wander the whole 760 × 520 m sea.
            float reach = StPetersBuilder.IslandRadius + StPetersBuilder.IslandFalloff
                          + StPetersBuilder.ReefShelfWidth + 20f;
            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = new Vector2Int(at.x + dx, at.y + dy);
                    if (seen.Contains(next)) continue;
                    Vector2 p = World(next);
                    if (TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter,
                            StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY) > reach) continue;
                    if (!Walkable(p, waterLevel)) continue;
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
            return seen;
        }

        /// <summary>True if any sample within <paramref name="radius"/> of a position was reached.</summary>
        private static bool Reached(HashSet<Vector2Int> fill, Vector2 target, float cell = 2f,
                                    float radius = 3f)
        {
            int r = Mathf.CeilToInt(radius / cell);
            var k = new Vector2Int(Mathf.RoundToInt(target.x / cell), Mathf.RoundToInt(target.y / cell));
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                if (fill.Contains(new Vector2Int(k.x + dx, k.y + dy))) return true;
            return false;
        }

        /// <summary>The seaward-most point of a sector class, at the bearing its arc is centred on —
        /// where a walker standing "on" that stretch of coast would actually be.</summary>
        private List<Vector2> SampleClass(CoastClass want, float outMetres)
        {
            var found = new List<Vector2>();
            CoastSector[] plan = _terrain.CoastSectors;
            for (int i = 0; i < plan.Length; i++)
            {
                if (plan[i].Class != want) continue;
                float mid = plan[i].FromBearing + CoastPlan.SectorWidth(plan, i) * 0.5f;
                found.Add(ShorePointOut(mid, outMetres));
            }
            return found;
        }

        /// <summary>
        /// ⭐ EVERY BEACH IS REACHABLE FROM THE VILLAGE AT ANY TIDE. A cliff coast that accidentally
        /// walls the player off from a shore is the single worst thing this change could do, and it is
        /// invisible in a screenshot.
        /// </summary>
        [Test]
        public void EveryBeachAndDuneStretchIsReachableFromTheVillageAtEveryTide()
        {
            foreach (float water in new[] { SpringLow, StPetersBuilder.TideMean, SpringHigh })
            {
                HashSet<Vector2Int> fill = ReachableAt(water);
                foreach (var want in new[] { CoastClass.Beach, CoastClass.Dune, CoastClass.Access })
                foreach (Vector2 p in SampleClass(want, 2f))
                {
                    // Only ground that is itself dry at this tide can be required to be reachable.
                    if (!Walkable(p, water)) continue;
                    Assert.IsTrue(Reached(fill, p),
                        $"a {want} stretch at {p} is dry at water {water:F2} m but cannot be walked to " +
                        "from the village — the reshape has walled off a piece of shore");
                }
            }
        }

        /// <summary>
        /// ⭐ THE LEDGE, BOTH DIRECTIONS. Reachable near dead low and NOT by land at high water — one
        /// assertion without the other is worthless: a ledge you can always reach is a beach, and one you
        /// can never reach is scenery.
        /// </summary>
        [Test]
        public void TheLowTideLedgesAreWalkableNearDeadLow_AndDrownedAtHighWater()
        {
            List<Vector2> benches = SampleClass(CoastClass.LedgeCliff,
                StPetersBuilder.CliffPlungeWidth + StPetersBuilder.LedgeBenchWidth * 0.5f);
            Assert.Greater(benches.Count, 0, "the plan authors no ledge runs at all");

            HashSet<Vector2Int> low = ReachableAt(SpringLow);
            foreach (Vector2 p in benches)
            {
                Assert.IsTrue(Walkable(p, SpringLow),
                    $"the ledge bench at {p} is under water even at dead low ({SpringLow:F2} m) — the " +
                    "bench is authored too low and the tide never hands it back");
                Assert.IsTrue(Reached(low, p, radius: 5f),
                    $"the ledge bench at {p} bares at dead low but cannot be WALKED to — the access " +
                    "trail beside it does not actually connect");
            }

            // ⚠ The OTHER direction is a DEPTH claim, not a flood-fill one, and getting that wrong cost
            // a CI round. A fill only ever contains WALKABLE cells, so a drowned bench can never be in
            // it — asking "was it reached" therefore measures how close the nearest dry ground is, which
            // on a 3 m-wide plunge is ~3 m, and says nothing whatever about the ledge. What actually
            // has to be true is that the tide has properly TAKEN the bench: not merely wet, but past the
            // swim limit into boat-only water. That is the claim a ledge authored too high would fail.
            foreach (Vector2 p in benches)
            {
                Assert.IsFalse(Walkable(p, SpringHigh),
                    $"the ledge bench at {p} is still walkable at high water ({SpringHigh:F2} m) — then " +
                    "it is a beach, and the tide is not lending the player anything");

                float depth = TidalExposure.WaterDepth(SpringHigh, _terrain.ElevationAt(p));
                Assert.Greater(depth, _config.SwimLimit,
                    $"the ledge bench at {p} carries only {depth:F2} m at high water, inside the " +
                    $"{_config.SwimLimit:F2} m swim band — the tide has not taken it away, it has just " +
                    "made it awkward, and the ledge stops reading as something the sea lends and reclaims");
            }
        }

        /// <summary>
        /// ⭐ WHAT A CLIFF DOES TO A WALKER — stated as what the terrain can actually promise.
        ///
        /// <para><b>⚠ An earlier version of this test was named "a cliff face is not a walkable surface"
        /// and it was overclaiming.</b> The walk gate is a comparison of ground against water
        /// (<see cref="TidalExposure.IsWalkable"/>) and has <b>no slope term</b>, so to the sim a 3 m
        /// plunge is a very steep RAMP, not a wall. The old test only ever sampled the cliff's FOOT — it
        /// passed, and it would have gone on passing on a coast whose faces were freely walkable.
        /// Enforcing a face properly needs a slope gate in the walk model (gameplay-systems' lane) or
        /// collision on the cliff geometry, and neither is in this slice.</para>
        ///
        /// <para>So this asserts the two things the height field genuinely delivers, and they are the
        /// two the player feels: a cliff's foot is never standable ground, and past the plateau edge a
        /// cliff gives you almost no shore where a beach gives you tens of metres of it.</para>
        /// </summary>
        [Test]
        public void ACliffLeavesAlmostNoShoreSeawardOfThePlateau_WhereABeachLeavesTens()
        {
            // The foot itself: never standable, at any tide. (True, and worth keeping — a cliff that
            // grew an apron would be a slope with rock painted on it.)
            foreach (var cls in new[] { CoastClass.Cliff, CoastClass.DeepShoreCliff })
            foreach (Vector2 p in SampleClass(cls, StPetersBuilder.CliffPlungeWidth + 1.5f))
            foreach (float water in new[] { SpringLow, StPetersBuilder.TideMean, SpringHigh })
                Assert.IsFalse(Walkable(p, water),
                    $"the foot of a {cls} at {p} is walkable at water {water:F2} m — a face with a " +
                    "standable apron is a slope, not a wall");

            // …and how far out you can actually get, measured in the profile's own frame.
            float limit = StPetersBuilder.IslandFalloff + StPetersBuilder.ReefShelfWidth;
            float Seaward(CoastClass c, float water)
            {
                float reach = 0f;
                for (float outM = 0f; outM <= limit; outM += 0.25f)
                {
                    float e = _terrain.IslandProfile(StPetersBuilder.IslandRadius + outM, c);
                    if (!TidalExposure.IsWalkable(water, e, WadeDepth)) break;
                    reach = outM;
                }
                return reach;
            }

            float beachLow = Seaward(CoastClass.Beach, SpringLow);
            Assert.Greater(beachLow, limit * 0.5f,
                $"a beach only gives {beachLow:F1} m of shore at dead low — the soft coast has stopped " +
                "being somewhere you can walk out onto, which no part of this change should have done");

            // The two classes whose feet are UNDER the lowest water give you nothing, ever.
            foreach (var cls in new[] { CoastClass.Cliff, CoastClass.DeepShoreCliff })
                Assert.Less(Seaward(cls, SpringLow), beachLow * 0.25f,
                    $"a {cls} gives {Seaward(cls, SpringLow):F1} m of walkable shore at dead low " +
                    $"against the beach's {beachLow:F1} m — that is not a cliff, it is a brisker beach");

            // ⭐ The LEDGE is deliberately NOT in that list — giving the player ground at low water is
            // the entire class, and asserting it gives almost none would contradict the feature. Its
            // contract is the pair below: real shore at dead low, and the beach's own share of nothing
            // once the tide is up. (It reaches further than its 4 m bench at low because the slope past
            // the bench is still under half a metre of water for another ~12 m — you wade out. That is
            // the coast being generous at dead low, which is the point.)
            float ledgeLow = Seaward(CoastClass.LedgeCliff, SpringLow);
            Assert.Greater(ledgeLow, StPetersBuilder.LedgeBenchWidth,
                $"a ledge gives only {ledgeLow:F1} m at dead low — narrower than its own {StPetersBuilder.LedgeBenchWidth} m " +
                "bench, so the tide is not actually handing the player anything to stand on");

            float beachHigh = Seaward(CoastClass.Beach, SpringHigh);
            Assert.Less(Seaward(CoastClass.LedgeCliff, SpringHigh), beachHigh * 0.25f,
                $"at high water a ledge still gives {Seaward(CoastClass.LedgeCliff, SpringHigh):F1} m " +
                $"against the beach's {beachHigh:F1} m — the sea is supposed to have taken it back");
        }

        // =========================================================================================
        //  5. what the boats and the chart see
        // =========================================================================================

        /// <summary>
        /// The depth ladder: a cliff base must read DEEP and a beach must stay approachable. Both are
        /// measured off the SAME field the sounder and the chart survey read
        /// (<see cref="ITidalTerrain.ElevationAt"/>), which is what makes "the chart agrees with the
        /// sounder" true by construction rather than by coincidence.
        /// </summary>
        [Test]
        public void BoatsCanReachEveryBeach_AndLieCloseUnderTheDeepShoreFaces()
        {
            const float DoryDraught = 0.6f;      // §5.1a's tier cut — the hull you learn on

            foreach (Vector2 p in SampleClass(CoastClass.DeepShoreCliff,
                                              StPetersBuilder.CliffPlungeWidth + 1f))
            {
                float depthAtLow = SpringLow - _terrain.ElevationAt(p);
                Assert.Greater(depthAtLow, DoryDraught,
                    $"a deep-shore face at {p} carries only {depthAtLow:F2} m at dead low — the whole " +
                    "point of the class is that a hull lies close alongside at any state of tide");
            }

            foreach (Vector2 p in SampleClass(CoastClass.Beach, StPetersBuilder.IslandFalloff))
            {
                float depthAtHigh = SpringHigh - _terrain.ElevationAt(p);
                Assert.Greater(depthAtHigh, DoryDraught,
                    $"a beach at {p} cannot float a {DoryDraught} m draught even at high water — a " +
                    "beach a boat can never approach is not a landing");
            }

            // The cliff bases must be DEEPER than the soft coast's apron, or the sounder tells the
            // player nothing new as they run along the shore.
            float cliffFoot = 0f, beachApron = 0f; int nc = 0, nb = 0;
            foreach (Vector2 p in SampleClass(CoastClass.DeepShoreCliff,
                                              StPetersBuilder.CliffPlungeWidth + 1f))
            { cliffFoot += _terrain.ElevationAt(p); nc++; }
            foreach (Vector2 p in SampleClass(CoastClass.Beach, StPetersBuilder.CliffPlungeWidth + 1f))
            { beachApron += _terrain.ElevationAt(p); nb++; }
            Assert.Greater(nc, 0); Assert.Greater(nb, 0);
            Assert.Less(cliffFoot / nc, beachApron / nb - 2f,
                "a cliff base sounds no deeper than a beach at the same distance off — the coast reads " +
                "flat on the instruments the player navigates by");
        }

        // =========================================================================================
        //  6. determinism
        // =========================================================================================

        /// <summary>Same seed, same config, same coast — twice, on two separately built components.
        /// The coast is recomputed geometry and nothing about it is saved (rule 5).</summary>
        [Test]
        public void TheCoastIsByteStableAcrossTwoIndependentBuilds()
        {
            var otherGo = new GameObject("StPetersTerrain_CoastTest_Twin");
            try
            {
                var twin = otherGo.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(twin);

                for (float bearing = 0f; bearing < 360f; bearing += 0.37f)
                for (float outM = -6f; outM <= 30f; outM += 3f)
                {
                    float r = bearing * Mathf.Deg2Rad;
                    var p = StPetersBuilder.IslandCenter
                            + new Vector2((StPetersBuilder.IslandRadius + outM) * Mathf.Sin(r),
                                          (StPetersBuilder.IslandRadiusY + outM) * Mathf.Cos(r));
                    Assert.AreEqual(_terrain.ElevationAt(p), twin.ElevationAt(p), 0f,
                        $"the coast is not reproducible at bearing {bearing:F2}°, {outM:F0} m out");
                    Assert.AreEqual(_terrain.CoastClassAt(p), twin.CoastClassAt(p),
                        $"the classifier is not reproducible at bearing {bearing:F2}°");
                }
            }
            finally { Object.DestroyImmediate(otherGo); }
        }

        /// <summary>
        /// The height field is CONTINUOUS across every sector join. Butted hard, a cliff meeting a beach
        /// is a 6.5 m step running out to sea — it aliases the seabed bake, tears the shader's waterline
        /// and puts an invisible precipice on the walk. The blend is what prevents it, and this is the
        /// measurement that proves the blend is actually on.
        /// </summary>
        [Test]
        public void NoSectorJoinLeavesAStepInTheHeightField()
        {
            const float sampleStep = 0.05f;      // degrees
            float worst = 0f; float worstAt = 0f;
            for (float outM = 0.5f; outM <= 12f; outM += 1.5f)
            {
                float previous = float.NaN;
                for (float bearing = 0f; bearing < 360f; bearing += sampleStep)
                {
                    float r = bearing * Mathf.Deg2Rad;
                    var p = StPetersBuilder.IslandCenter
                            + new Vector2((StPetersBuilder.IslandRadius + outM) * Mathf.Sin(r),
                                          (StPetersBuilder.IslandRadiusY + outM) * Mathf.Cos(r));
                    float e = _terrain.ElevationAt(p);
                    if (!float.IsNaN(previous) && Mathf.Abs(e - previous) > worst)
                    {
                        worst = Mathf.Abs(e - previous);
                        worstAt = bearing;
                    }
                    previous = e;
                }
            }
            // DERIVED, not chosen: the biggest drop a join can carry is plateau-to-deep-floor, spread by
            // the blend over 2 × the feather (each side eases from 0.5 to 1). Three times that per-sample
            // rate is generous for the profile's own curvature and still an order of magnitude below the
            // ~8.75 m an UNFEATHERED join would show — which is the failure this exists to catch.
            float fullDrop = StPetersBuilder.IslandElevation - StPetersBuilder.DeepHarbourElevation;
            float allowed = 3f * fullDrop / (2f * _terrain.CoastBlendDegrees) * sampleStep;
            Assert.Less(worst, allowed,
                $"a {worst:F2} m step in the height field at bearing {worstAt:F2}° against an allowance " +
                $"of {allowed:F3} m — two sectors are butted without a blend, which is a cliff running " +
                "out to sea rather than along the shore");
            Debug.Log($"[coast] worst alongshore step {worst:F3} m per {sampleStep}° at {worstAt:F2}°");
        }
    }
}
