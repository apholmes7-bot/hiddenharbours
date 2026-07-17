using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The skiff outboard's pure math: the remote-steer column mapping, the per-heading draw order, and the
    /// twin fit's exact clamp offsets. Everything here is a pure function — the same discipline (and the same
    /// clamp-don't-throw contract) as <c>DoryOarMathTests</c>.
    /// </summary>
    public class OutboardMotorMathTests
    {
        private const int Cols = OutboardMotorMath.SteerColumns;      // 9
        private const int Headings = OutboardMotorMath.HeadingCount;  // 8
        private const float Max = OutboardMotorMath.MaxSteerDegrees;  // 30

        // ---- the sheet's own mapping: angle(f) = -30 + 60*f/8 -------------------------------

        [Test]
        public void SteerDegreesForColumn_MatchesTheArtRigsAngleFunction()
        {
            for (int f = 0; f < Cols; f++)
            {
                float expected = -30f + (60f * f) / 8f;   // the rig's angle(f), verbatim
                Assert.AreEqual(expected, OutboardMotorMath.SteerDegreesForColumn(f, Cols, Max), 1e-4f,
                                $"col {f}");
            }
        }

        [Test]
        public void SteerDegreesForColumn_ExtremesAndDeadAhead()
        {
            Assert.AreEqual(-30f, OutboardMotorMath.SteerDegreesForColumn(0, Cols, Max), 1e-4f, "col 0 = full port");
            Assert.AreEqual(0f, OutboardMotorMath.SteerDegreesForColumn(4, Cols, Max), 1e-4f, "col 4 = dead ahead");
            Assert.AreEqual(30f, OutboardMotorMath.SteerDegreesForColumn(8, Cols, Max), 1e-4f, "col 8 = full starboard");
        }

        [Test]
        public void SteerDegreesForColumn_StepsAre7Point5Degrees()
        {
            for (int f = 1; f < Cols; f++)
            {
                float step = OutboardMotorMath.SteerDegreesForColumn(f, Cols, Max)
                           - OutboardMotorMath.SteerDegreesForColumn(f - 1, Cols, Max);
                Assert.AreEqual(7.5f, step, 1e-4f, $"step into col {f}");
            }
        }

        [Test]
        public void CenterColumn_IsDeadAheadForTheShippedSheet()
        {
            Assert.AreEqual(4, OutboardMotorMath.CenterColumn(Cols));
            Assert.AreEqual(0f, OutboardMotorMath.SteerDegreesForColumn(OutboardMotorMath.CenterColumn(Cols), Cols, Max), 1e-4f);
        }

        [Test]
        public void ColumnForSteerDegrees_RoundTripsEveryColumn()
        {
            for (int f = 0; f < Cols; f++)
            {
                float deg = OutboardMotorMath.SteerDegreesForColumn(f, Cols, Max);
                Assert.AreEqual(f, OutboardMotorMath.ColumnForSteerDegrees(deg, Cols, Max), $"round trip col {f}");
            }
        }

        [Test]
        public void ColumnForSteerDegrees_ClampsBeyondTheSheetsAuthority()
        {
            Assert.AreEqual(0, OutboardMotorMath.ColumnForSteerDegrees(-90f, Cols, Max), "hard over past full port pins");
            Assert.AreEqual(8, OutboardMotorMath.ColumnForSteerDegrees(90f, Cols, Max), "hard over past full starboard pins");
        }

        // ---- helm -> target column ----------------------------------------------------------

        [Test]
        public void TargetColumnForHelm_FullPortCentreFullStarboard()
        {
            Assert.AreEqual(0, OutboardMotorMath.TargetColumnForHelm(-1f, 0.05f, Cols, Max), "helm -1 = full port");
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(0f, 0.05f, Cols, Max), "helm 0 = dead ahead");
            Assert.AreEqual(8, OutboardMotorMath.TargetColumnForHelm(1f, 0.05f, Cols, Max), "helm +1 = full starboard");
        }

        [Test]
        public void TargetColumnForHelm_HalfHelmIsHalfSwivel()
        {
            Assert.AreEqual(6, OutboardMotorMath.TargetColumnForHelm(0.5f, 0.05f, Cols, Max), "+0.5 = +15deg = col 6");
            Assert.AreEqual(2, OutboardMotorMath.TargetColumnForHelm(-0.5f, 0.05f, Cols, Max), "-0.5 = -15deg = col 2");
        }

        [Test]
        public void TargetColumnForHelm_DeadzoneReadsAsCentred()
        {
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(0.04f, 0.05f, Cols, Max), "inside the deadzone");
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(-0.05f, 0.05f, Cols, Max), "exactly on it");
            Assert.AreNotEqual(4, OutboardMotorMath.TargetColumnForHelm(0.9f, 0.05f, Cols, Max), "clear of it");
        }

        [Test]
        public void TargetColumnForHelm_OverDrivenHelmPinsAtTheExtreme()
        {
            Assert.AreEqual(0, OutboardMotorMath.TargetColumnForHelm(-5f, 0.05f, Cols, Max));
            Assert.AreEqual(8, OutboardMotorMath.TargetColumnForHelm(5f, 0.05f, Cols, Max));
        }

        [Test]
        public void TargetColumnForHelm_NonFiniteHelmCentresRatherThanPropagates()
        {
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(float.NaN, 0.05f, Cols, Max));
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(float.PositiveInfinity, 0.05f, Cols, Max));
        }

        // ---- the ~8 fps swivel ---------------------------------------------------------------

        [Test]
        public void StepTowardColumn_RateLimitsTheSwivel()
        {
            // 8 columns/sec for a quarter second = 2 columns of travel, not the whole throw.
            float p = OutboardMotorMath.StepTowardColumn(4f, 8, 8f, 0.25f);
            Assert.AreEqual(6f, p, 1e-4f);
        }

        [Test]
        public void StepTowardColumn_ArrivesAndHolds()
        {
            float p = 4f;
            for (int i = 0; i < 60; i++) p = OutboardMotorMath.StepTowardColumn(p, 8, 8f, 1f / 30f);
            Assert.AreEqual(8f, p, 1e-4f, "arrives");
            Assert.AreEqual(8f, OutboardMotorMath.StepTowardColumn(p, 8, 8f, 1f / 30f), 1e-4f, "and does not overshoot");
        }

        [Test]
        public void StepTowardColumn_ZeroRateSnaps()
        {
            Assert.AreEqual(0f, OutboardMotorMath.StepTowardColumn(8f, 0, 0f, 0.016f), 1e-4f);
        }

        [Test]
        public void StepTowardColumn_NonFinitePositionReSeeds()
        {
            Assert.AreEqual(4f, OutboardMotorMath.StepTowardColumn(float.NaN, 4, 8f, 0.016f), 1e-4f);
            Assert.AreEqual(4f, OutboardMotorMath.StepTowardColumn(float.PositiveInfinity, 4, 8f, 0.016f), 1e-4f);
        }

        [Test]
        public void ColumnFromPosition_RoundsToNearestAndClamps()
        {
            Assert.AreEqual(4, OutboardMotorMath.ColumnFromPosition(4.4f, Cols), "rounds down");
            Assert.AreEqual(5, OutboardMotorMath.ColumnFromPosition(4.6f, Cols), "rounds up");
            Assert.AreEqual(0, OutboardMotorMath.ColumnFromPosition(-3f, Cols), "clamps low");
            Assert.AreEqual(8, OutboardMotorMath.ColumnFromPosition(99f, Cols), "clamps high");
        }

        // ---- grid index ----------------------------------------------------------------------

        [Test]
        public void MotorGridIndex_IsRowMajorOverTheWholeSheet()
        {
            Assert.AreEqual(0, OutboardMotorMath.MotorGridIndex(0, 0, Cols), "N, full port = slice 0");
            Assert.AreEqual(4, OutboardMotorMath.MotorGridIndex(0, 4, Cols), "N, dead ahead");
            Assert.AreEqual(9, OutboardMotorMath.MotorGridIndex(1, 0, Cols), "NE starts the second row");
            Assert.AreEqual(71, OutboardMotorMath.MotorGridIndex(7, 8, Cols), "NW, full starboard = the last slice");
        }

        [Test]
        public void MotorGridIndex_RoundTripsEveryCellExactlyOnce()
        {
            var seen = new bool[Headings * Cols];
            for (int row = 0; row < Headings; row++)
                for (int col = 0; col < Cols; col++)
                {
                    int i = OutboardMotorMath.MotorGridIndex(row, col, Cols);
                    Assert.IsFalse(seen[i], $"index {i} claimed twice");
                    seen[i] = true;
                    Assert.AreEqual(row, i / Cols, "row recovers");
                    Assert.AreEqual(col, i % Cols, "col recovers");
                }
            Assert.IsTrue(System.Array.TrueForAll(seen, s => s), "the whole 72-slice grid is covered");
        }

        // ---- per-heading draw order ----------------------------------------------------------

        [Test]
        public void LowerGoesUnderHull_OnlyForTheSternAwayHeadings()
        {
            // Art README + the rig's MOTOR.behind = [3,4,5]: SE, S, SW.
            Assert.IsFalse(OutboardMotorMath.LowerGoesUnderHull(0, Headings), "N");
            Assert.IsFalse(OutboardMotorMath.LowerGoesUnderHull(1, Headings), "NE");
            Assert.IsFalse(OutboardMotorMath.LowerGoesUnderHull(2, Headings), "E");
            Assert.IsTrue(OutboardMotorMath.LowerGoesUnderHull(3, Headings), "SE");
            Assert.IsTrue(OutboardMotorMath.LowerGoesUnderHull(4, Headings), "S");
            Assert.IsTrue(OutboardMotorMath.LowerGoesUnderHull(5, Headings), "SW");
            Assert.IsFalse(OutboardMotorMath.LowerGoesUnderHull(6, Headings), "W");
            Assert.IsFalse(OutboardMotorMath.LowerGoesUnderHull(7, Headings), "NW");
        }

        [Test]
        public void LowerGoesUnderHull_WrapsOutOfRangeRows()
        {
            Assert.AreEqual(OutboardMotorMath.LowerGoesUnderHull(4, Headings),
                            OutboardMotorMath.LowerGoesUnderHull(12, Headings), "row 12 == row 4");
            Assert.AreEqual(OutboardMotorMath.LowerGoesUnderHull(4, Headings),
                            OutboardMotorMath.LowerGoesUnderHull(-4, Headings), "row -4 == row 4");
        }

        [Test]
        public void SortingOrder_UpperIsAlwaysOverTheHullAtEveryHeading()
        {
            const int hull = 1;
            for (int row = 0; row < Headings; row++)
            {
                Assert.Greater(OutboardMotorMath.SortingOrder(hull, OutboardMotorMath.MotorPart.Upper, row, Headings, false),
                               hull, $"upper over hull at row {row}");
                Assert.Greater(OutboardMotorMath.SortingOrder(hull, OutboardMotorMath.MotorPart.Upper, row, Headings, true),
                               hull, $"far upper over hull at row {row}");
            }
        }

        [Test]
        public void SortingOrder_LowerFlipsUnderTheHullAcrossTheSternAwayArc()
        {
            const int hull = 1;
            for (int row = 0; row < Headings; row++)
            {
                int lower = OutboardMotorMath.SortingOrder(hull, OutboardMotorMath.MotorPart.Lower, row, Headings, false);
                if (row >= 3 && row <= 5) Assert.Less(lower, hull, $"lower UNDER hull at row {row} (stern away)");
                else Assert.Greater(lower, hull, $"lower OVER hull at row {row}");
            }
        }

        [Test]
        public void SortingOrder_UpperAlwaysOutranksItsOwnLower()
        {
            const int hull = 1;
            for (int row = 0; row < Headings; row++)
                foreach (bool far in new[] { false, true })
                    Assert.Greater(
                        OutboardMotorMath.SortingOrder(hull, OutboardMotorMath.MotorPart.Upper, row, Headings, far),
                        OutboardMotorMath.SortingOrder(hull, OutboardMotorMath.MotorPart.Lower, row, Headings, far),
                        $"row {row}, far {far}");
        }

        [Test]
        public void SortingOrder_FarEngineDrawsFirstWithinEveryLayer()
        {
            const int hull = 1;
            for (int row = 0; row < Headings; row++)
                foreach (var part in new[] { OutboardMotorMath.MotorPart.Lower, OutboardMotorMath.MotorPart.Upper })
                    Assert.Less(
                        OutboardMotorMath.SortingOrder(hull, part, row, Headings, isFarEngine: true),
                        OutboardMotorMath.SortingOrder(hull, part, row, Headings, isFarEngine: false),
                        $"far under near — {part} at row {row}");
        }

        [Test]
        public void SortingOrder_TracksTheHullsOwnOrder()
        {
            // The whole stack shifts with the hull rather than pinning to absolute numbers.
            int a = OutboardMotorMath.SortingOrder(1, OutboardMotorMath.MotorPart.Upper, 0, Headings, false);
            int b = OutboardMotorMath.SortingOrder(11, OutboardMotorMath.MotorPart.Upper, 0, Headings, false);
            Assert.AreEqual(10, b - a);
        }

        // ---- the twin fit --------------------------------------------------------------------

        [Test]
        public void MountOffset_AtNorthTheEnginesSeparatePurelyHorizontally()
        {
            // dir 0: th = 0 -> dy = 0. The rig's own sanity check.
            Vector2 port = OutboardMotorMath.MountOffset(0, -0.34f, Headings);
            Vector2 star = OutboardMotorMath.MountOffset(0, +0.34f, Headings);

            Assert.AreEqual(-0.34f, port.x, 1e-4f, "port sits a full 0.34 m to screen-left");
            Assert.AreEqual(+0.34f, star.x, 1e-4f, "starboard a full 0.34 m to screen-right");
            Assert.AreEqual(0f, port.y, 1e-4f, "no vertical component at N");
            Assert.AreEqual(0f, star.y, 1e-4f, "no vertical component at N");
        }

        [Test]
        public void MountOffset_AtEastTheEnginesSeparatePurelyVerticallyAndForeshortened()
        {
            // dir 2: th = 90deg -> dx = 0, dy = mx*sin(elev). The rig's other sanity check.
            Vector2 star = OutboardMotorMath.MountOffset(2, +0.34f, Headings);

            Assert.AreEqual(0f, star.x, 1e-4f, "no horizontal component at E");
            Assert.AreEqual(0.34f * Mathf.Sin(40f * Mathf.Deg2Rad), star.y, 1e-4f, "foreshortened by sin(40deg)");
            Assert.Less(Mathf.Abs(star.y), 0.34f, "foreshortening SHORTENS the separation");
        }

        [Test]
        public void MountOffset_ReproducesTheArtRigsMountOffsetAtEveryHeading()
        {
            // The rig, verbatim (image px, y-DOWN, S = 32 px/m):
            //   dx =  mx*cos(th)*S
            //   dy = -mx*sin(th)*sin(e)*S
            // Ours is metres in Unity axes (y-UP) at PPU 32, so: x = dx/32, y = -dy/32.
            const float S = 32f, ppu = 32f, mx = 0.34f;
            float e = 40f * Mathf.Deg2Rad;

            for (int dir = 0; dir < Headings; dir++)
            {
                float th = dir * Mathf.PI / 4f;
                float rigDx = mx * Mathf.Cos(th) * S;
                float rigDy = -mx * Mathf.Sin(th) * Mathf.Sin(e) * S;

                Vector2 ours = OutboardMotorMath.MountOffset(dir, mx, Headings);
                Assert.AreEqual(rigDx / ppu, ours.x, 1e-4f, $"x at dir {dir}");
                Assert.AreEqual(-rigDy / ppu, ours.y, 1e-4f, $"y at dir {dir} (image y-down -> Unity y-up)");
            }
        }

        [Test]
        public void MountOffset_IsAntisymmetricAboutTheCentreline()
        {
            for (int dir = 0; dir < Headings; dir++)
            {
                Vector2 port = OutboardMotorMath.MountOffset(dir, -0.34f, Headings);
                Vector2 star = OutboardMotorMath.MountOffset(dir, +0.34f, Headings);
                Assert.AreEqual(-star.x, port.x, 1e-4f, $"x at dir {dir}");
                Assert.AreEqual(-star.y, port.y, 1e-4f, $"y at dir {dir}");
            }
        }

        [Test]
        public void MountOffset_CentrelineEngineIsNeverOffset()
        {
            for (int dir = 0; dir < Headings; dir++)
                Assert.AreEqual(Vector2.zero, OutboardMotorMath.MountOffset(dir, 0f, Headings),
                                $"single engine at dir {dir}");
        }

        [Test]
        public void MountOffset_SeparationIsAlwaysTheFullGaugeAtNorthAndSouth()
        {
            // Beam-on to the camera at N/S; at E/W it is foreshortened. Never larger than the true gauge.
            for (int dir = 0; dir < Headings; dir++)
            {
                float sep = (OutboardMotorMath.MountOffset(dir, 0.34f, Headings)
                           - OutboardMotorMath.MountOffset(dir, -0.34f, Headings)).magnitude;
                Assert.LessOrEqual(sep, 0.68f + 1e-4f, $"dir {dir} never exceeds the true 0.68 m gauge");
            }
            Assert.AreEqual(0.68f, (OutboardMotorMath.MountOffset(0, 0.34f, Headings)
                                  - OutboardMotorMath.MountOffset(0, -0.34f, Headings)).magnitude, 1e-4f, "N is full gauge");
        }

        [Test]
        public void MountOffset_WrapsOutOfRangeRows()
        {
            Assert.AreEqual(OutboardMotorMath.MountOffset(2, 0.34f, Headings),
                            OutboardMotorMath.MountOffset(10, 0.34f, Headings), "row 10 == row 2");
            Assert.AreEqual(OutboardMotorMath.MountOffset(2, 0.34f, Headings),
                            OutboardMotorMath.MountOffset(-6, 0.34f, Headings), "row -6 == row 2");
        }

        // ---- far-engine ordering -------------------------------------------------------------

        [Test]
        public void IsFarEngine_TheHigherOnScreenEngineIsTheFarOne()
        {
            // "Far = drawn higher on screen" — so the far engine's MountOffset.y is the larger one, at every
            // heading where the two actually differ in depth.
            for (int dir = 0; dir < Headings; dir++)
            {
                if (dir == 0 || dir == 4) continue;   // abeam: equal depth, no overlap, order is free
                bool portIsFar = OutboardMotorMath.IsFarEngine(-0.34f, +0.34f, dir, Headings);
                float portY = OutboardMotorMath.MountOffset(dir, -0.34f, Headings).y;
                float starY = OutboardMotorMath.MountOffset(dir, +0.34f, Headings).y;

                if (portIsFar) Assert.Greater(portY, starY, $"dir {dir}: port is far so it draws higher");
                else Assert.Greater(starY, portY, $"dir {dir}: starboard is far so it draws higher");
            }
        }

        [Test]
        public void IsFarEngine_SwapsAcrossTheTurn()
        {
            // Turning through E vs W puts the other engine behind — the order is not a constant.
            Assert.IsFalse(OutboardMotorMath.IsFarEngine(-0.34f, +0.34f, 2, Headings), "at E starboard is far");
            Assert.IsTrue(OutboardMotorMath.IsFarEngine(-0.34f, +0.34f, 6, Headings), "at W port is far");
        }

        [Test]
        public void IsFarEngine_ExactlyOneOfThePairIsFar()
        {
            for (int dir = 0; dir < Headings; dir++)
            {
                bool portIsFar = OutboardMotorMath.IsFarEngine(-0.34f, +0.34f, dir, Headings);
                bool starIsFar = OutboardMotorMath.IsFarEngine(+0.34f, -0.34f, dir, Headings);
                Assert.AreNotEqual(portIsFar, starIsFar, $"dir {dir}: never both, never neither");
            }
        }

        [Test]
        public void IsFarEngine_IsStableWhenTheEnginesSitAbeam()
        {
            // N and S: equal depth. The answer must still be deterministic rather than order-dependent.
            foreach (int dir in new[] { 0, 4 })
            {
                Assert.IsTrue(OutboardMotorMath.IsFarEngine(-0.34f, +0.34f, dir, Headings), $"dir {dir}");
                Assert.IsFalse(OutboardMotorMath.IsFarEngine(+0.34f, -0.34f, dir, Headings), $"dir {dir} reversed");
            }
        }

        [Test]
        public void MountScreenDepth_IsZeroAbeamAndSignedThroughTheTurn()
        {
            Assert.AreEqual(0f, OutboardMotorMath.MountScreenDepth(0, 0.34f, Headings), 1e-4f, "N: abeam");
            Assert.AreEqual(0f, OutboardMotorMath.MountScreenDepth(4, 0.34f, Headings), 1e-4f, "S: abeam");
            Assert.Greater(OutboardMotorMath.MountScreenDepth(2, 0.34f, Headings), 0f, "E: starboard engine is far");
            Assert.Less(OutboardMotorMath.MountScreenDepth(6, 0.34f, Headings), 0f, "W: starboard engine is near");
        }

        // ---- determinism (rule 5) ------------------------------------------------------------

        [Test]
        public void EveryEntryPointIsDeterministic()
        {
            for (int dir = 0; dir < Headings; dir++)
                for (int col = 0; col < Cols; col++)
                {
                    Assert.AreEqual(OutboardMotorMath.MountOffset(dir, 0.34f, Headings),
                                    OutboardMotorMath.MountOffset(dir, 0.34f, Headings));
                    Assert.AreEqual(OutboardMotorMath.SteerDegreesForColumn(col, Cols, Max),
                                    OutboardMotorMath.SteerDegreesForColumn(col, Cols, Max));
                    Assert.AreEqual(OutboardMotorMath.SortingOrder(1, OutboardMotorMath.MotorPart.Lower, dir, Headings, false),
                                    OutboardMotorMath.SortingOrder(1, OutboardMotorMath.MotorPart.Lower, dir, Headings, false));
                }
        }

        // ---- degenerate input clamps rather than throws ---------------------------------------

        [Test]
        public void DegenerateCountsClampRatherThanThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                OutboardMotorMath.SteerDegreesForColumn(0, 0, Max);
                OutboardMotorMath.ColumnForSteerDegrees(0f, 0, Max);
                OutboardMotorMath.TargetColumnForHelm(1f, 0.05f, 0, Max);
                OutboardMotorMath.ColumnFromPosition(0f, 0);
                OutboardMotorMath.MountOffset(0, 0.34f, 0);
                OutboardMotorMath.MountScreenDepth(0, 0.34f, 0);
                OutboardMotorMath.LowerGoesUnderHull(0, 0);
                OutboardMotorMath.SortingOrder(1, OutboardMotorMath.MotorPart.Upper, 0, 0, false);
            });
        }

        [Test]
        public void SingleColumnSheetIsAlwaysDeadAhead()
        {
            Assert.AreEqual(0f, OutboardMotorMath.SteerDegreesForColumn(0, 1, Max), 1e-4f);
            Assert.AreEqual(0, OutboardMotorMath.TargetColumnForHelm(1f, 0.05f, 1, Max));
        }

        [Test]
        public void ZeroSteerAuthorityCentresRatherThanDividingByZero()
        {
            Assert.AreEqual(4, OutboardMotorMath.ColumnForSteerDegrees(10f, Cols, 0f));
            Assert.AreEqual(4, OutboardMotorMath.TargetColumnForHelm(1f, 0.05f, Cols, 0f));
        }
    }
}
