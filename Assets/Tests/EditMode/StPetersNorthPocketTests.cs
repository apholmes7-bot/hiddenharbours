using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    internal static class LandingDockEvidence
    {
        [Serializable] internal sealed class Record
        {
            public long[] protectedWharfIds;
            public string protectedWharfSha256, landingNonPlacementSha256, outsidePocketTexelsSha256;
            public string[] placementIds;
            public int expectedWharfDocuments, expectedOutsideTexels;
        }
        internal static Record Load() => JsonUtility.FromJson<Record>(File.ReadAllText(
            "Assets/Tests/EditMode/Fixtures/StPetersLandingDock.json"));
        internal static string Sha(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        // Survey fixture, independent of the builder's derivation.
        internal static readonly Rect NorthCut = new Rect(207.3f, 4.05220215f, 11.4f, 6.9f);
        internal static readonly Rect WalkingTop = new Rect(183f, 2.53345215f, 30.5f, 3.71875f);
        internal static IEnumerable<Vector2> PlankSamples()
        {
            Rect r = WalkingTop;
            for (float y = r.yMin; y <= r.yMax; y += .5f)
            for (float x = r.xMin; x <= r.xMax; x += .5f) yield return new Vector2(x, y);
            yield return r.max;
            yield return new Vector2(r.xMin, r.yMax);
            yield return new Vector2(r.xMax, r.yMin);
        }
    }

    public class StPetersNorthPocketTests
    {
        GameObject _go;
        TidalTerrain _terrain;
        [SetUp] public void SetUp()
        {
            _go = new GameObject("NorthPocketTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

        public static PaintedHeightField PaintedBed()
        {
            var image = TerrainPlanPng.Read(File.ReadAllBytes(StPetersNorthPocket.PngPath));
            Assert.That(image.BitDepth, Is.EqualTo(16));
            var heights = new float[image.Width * image.Height];
            ushort[] codes = image.Channel(0);
            for (int row = 0; row < image.Height; row++)
            for (int x = 0; x < image.Width; x++)
                heights[(image.Height - row - 1) * image.Width + x] = -4f + codes[row * image.Width + x] * (16f / ushort.MaxValue);
            return new PaintedHeightField(heights, image.Width, image.Height,
                StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize);
        }

        [Test]
        public void TheNorthPocketIsOnlyTheWholeDoryPlusTheExistingMarginAndShoulder()
        {
            Rect actual = StPetersBuilder.NorthPocketRectangle, frozen = LandingDockEvidence.NorthCut;
            Assert.That(actual.x, Is.EqualTo(frozen.x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(frozen.y).Within(1e-4f));
            Assert.That(actual.width, Is.EqualTo(frozen.width).Within(1e-4f));
            Assert.That(actual.height, Is.EqualTo(frozen.height).Within(1e-4f));
            Assert.That(StPetersBuilder.NorthPocketThalwegHalfWidth,
                Is.EqualTo(StPetersBuilder.DoryHalfBeamMetres + StPetersBuilder.BerthPocketMarginMetres));
            Assert.That(StPetersBuilder.NorthPocketBedElevation, Is.EqualTo(StPetersBuilder.ApproachBedElevation));
            Assert.That(StPetersBuilder.BerthPocketFrom, Is.EqualTo(new Vector2(205.05f, -5.8f)));
            Assert.That(StPetersBuilder.BerthPocketTo, Is.EqualTo(new Vector2(217.95f, -5.8f)));
        }

        [Test]
        public void PaintedPocketIsInstalledAndRepeatable_AndEveryOutsideTexelKeepsItsBaseBytes()
        {
            var image = TerrainPlanPng.Read(File.ReadAllBytes(StPetersNorthPocket.PngPath));
            ushort[] codes = image.Channel(0);
            ushort[] once = StPetersNorthPocket.PatchCodes(codes, image.Width, image.Height,
                StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize, -4, 12, _terrain);
            ushort[] twice = StPetersNorthPocket.PatchCodes(once, image.Width, image.Height,
                StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize, -4, 12, _terrain);
            CollectionAssert.AreEqual(codes, once, "the repeatable north cut is already installed");
            CollectionAssert.AreEqual(once, twice, "a second shoulder application cannot deepen it");
            using var outside = new MemoryStream();
            int count = 0;
            for (int row = 0; row < image.Height; row++)
            for (int x = 0; x < image.Width; x++)
            {
                Vector2 at = StPetersNorthPocket.TexelCentre(x, row, image.Width, image.Height,
                    StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize);
                if (LandingDockEvidence.NorthCut.Contains(at)) continue;
                int i = row * image.Width + x;
                Assert.That(once[i], Is.EqualTo(codes[i]), "outside texel " + x + "," + row);
                outside.WriteByte((byte)codes[i]); outside.WriteByte((byte)(codes[i] >> 8)); count++;
            }
            var proof = LandingDockEvidence.Load();
            Assert.That(count, Is.EqualTo(proof.expectedOutsideTexels));
            Assert.That(LandingDockEvidence.Sha(outside.ToArray()), Is.EqualTo(proof.outsidePocketTexelsSha256));
        }

        [Test]
        public void PaintedAndAnalyticBedsAgreeInsideTheNorthPocketAndOnItsShoulder()
        {
            PaintedHeightField painted = PaintedBed();
            Rect r = LandingDockEvidence.NorthCut;
            float peak = 0f; Vector2 worst = default;
            for (float y = r.yMin; y <= r.yMax; y += 0.125f)
            for (float x = r.xMin; x <= r.xMax; x += 0.125f)
            {
                Vector2 at = new Vector2(x, y);
                float error = Mathf.Abs(_terrain.ElevationAt(at) - painted.ElevationAt(at));
                if (error > peak) { peak = error; worst = at; }
            }
            // Measured 0.17447 m on the 0.5 m painted grid; R16's elevation quantum is 0.000244 m.
            Assert.That(peak, Is.LessThanOrEqualTo(0.18f), "paired bed error at " + worst);
            TestContext.WriteLine("north pocket paired peak: " + peak + " m at " + worst);
        }

        static IEnumerable<Vector2> HullAndMargin(HullFootprint hull, float margin)
        {
            for (int i = 0; i <= 26; i++)
            for (int j = 0; j <= 16; j++)
            {
                float u = Mathf.Lerp(-hull.HalfLength - margin, hull.HalfLength + margin, i / 26f);
                float v = Mathf.Lerp(-hull.HalfBeam - margin, hull.HalfBeam + margin, j / 16f);
                if (new Vector2(Mathf.Max(Mathf.Abs(u) - hull.HalfLength, 0), Mathf.Max(Mathf.Abs(v) - hull.HalfBeam, 0)).magnitude > margin + 1e-5f) continue;
                yield return hull.Center + hull.BowDirection * u + hull.StarboardDirection * v;
            }
            foreach (float su in new[] { -1f, 1f })
            foreach (float sv in new[] { -1f, 1f })
            for (int i = 0; i <= 12; i++)
            {
                float a = i / 12f * Mathf.PI * 0.5f;
                yield return hull.Center + hull.BowDirection * (su * (hull.HalfLength + margin * Mathf.Cos(a))) +
                    hull.StarboardDirection * (sv * (hull.HalfBeam + margin * Mathf.Sin(a)));
            }
        }

        [Test]
        public void SheCanLeaveTheNorthPocketAtSpringLow_OverHerWholeHullAndMargin()
            => AssertEscape(_terrain, PaintedBed());

        public static void AssertEscape(TidalTerrain terrain, PaintedHeightField painted)
        {
            Vector2 berth = StPetersBuilder.DoryMooredPos;
            // Surveyed reverse-out path. Z is mathematical heading (east=0), not an elevation.
            var marks = new[] { new Vector3(0,0,180), new Vector3(.4f,-.1f,170), new Vector3(.9f,-.4f,155),
                new Vector3(1.5f,-.9f,140), new Vector3(2.3f,-1.7f,135), new Vector3(3.2f,-2.7f,135),
                new Vector3(4.2f,-3.7f,135), new Vector3(5.2f,-4.7f,135), new Vector3(6,2f-berth.y,135) };
            float low = StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;
            float margin = StPetersBuilder.BerthPocketMarginMetres;
            float worst = float.PositiveInfinity;
            for (int leg = 0; leg < marks.Length - 1; leg++)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Vector2.Distance(marks[leg], marks[leg+1]) / .125f,
                    Mathf.Abs(marks[leg].z - marks[leg+1].z) / 2f));
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 pose = Vector3.Lerp(marks[leg], marks[leg+1], step / (float)steps);
                    var hull = HullFootprint.FromHeading(berth + (Vector2)pose, 90f - pose.z,
                        StPetersBuilder.DoryLengthMetres, StPetersBuilder.DoryHalfBeamMetres);
                    Rect floor = StPetersWharf.DeckFootprint();
                    var planks = HullFootprint.FromHeading(floor.center, 90f, floor.width, floor.height * .5f);
                    Assert.That(hull.SignedGapTo(planks), Is.GreaterThanOrEqualTo(0f), "escape must clear the physical planks");
                    foreach (Vector2 p in HullAndMargin(hull, margin))
                    {
                        float depth = low - Mathf.Max(terrain.ElevationAt(p), painted.ElevationAt(p));
                        worst = Mathf.Min(worst, depth);
                        Assert.That(depth, Is.GreaterThanOrEqualTo(.3f), "spring-low hull plus margin at " + p);
                        if (leg == marks.Length - 2 && step == steps)
                            Assert.That(low - terrain.ElevationAtWithoutNorthPocket(p), Is.GreaterThanOrEqualTo(.3f), "the exit reaches existing open water");
                    }
                }
            }
            TestContext.WriteLine("north-pocket escape minimum whole-outline plus margin depth: " + worst);
        }
    }
}
