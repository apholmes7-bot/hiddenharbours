using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode.Wildlife
{
    /// <summary>
    /// The seals on the Whelps: who is out is a pure function of (seed, time, tide); no seal ever lies on a
    /// wet rock; the approach puzzle (alert, creep, slip, return); fewer at night and in a rough sea; and
    /// the content agrees with the key scene it was taken from.
    /// </summary>
    public class SealHauloutTests
    {
        const int Seed = 1234567;
        static readonly TideProfile StPeters = new TideProfile { MeanLevel = 0f, Amplitude = 2.2f, PhaseHours = 0f };

        GameConfig _config;

        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<GameConfig>();
        [TearDown] public void TearDown() { Object.DestroyImmediate(_config); WildlifePresences.Reset(); }

        sealed class ModelTide : ISealTideSource
        {
            public TideProfile Profile; public GameConfig Config;
            public float LevelAt(double t) => TideModel.Height(t, Profile, Config);
            public long CycleAt(double t) => TideModel.CycleIndex(t, Profile, Config);
        }

        double Period => _config.TidalPeriodHours * _config.SecondsPerHour;

        static SealHauloutDef Whelps()
        {
            var def = AssetDatabase.LoadAssetAtPath<SealHauloutDef>(StPetersLayerRefresh.WhelpsHauloutPath);
            Assert.IsNotNull(def, StPetersLayerRefresh.WhelpsHauloutPath + " is missing.");
            return def;
        }

        static SealTuning Defaults()
        {
            var s = ScriptableObject.CreateInstance<WildlifeSpeciesDef>();
            try { return s.ToTuning(); } finally { Object.DestroyImmediate(s); }
        }

        // ---- determinism ---------------------------------------------------------------------------------------

        [Test]
        public void Schedule_IsAPureFunctionOfSeedTimeAndTide()
        {
            SealHauloutDef def = Whelps();
            SealTuning k = def.Species.ToTuning();
            float[] lie = def.Spots.Select(s => s.LieMetres).ToArray();
            uint[] ids = def.Spots.Select(s => SealHash.Id(s.Id)).ToArray();
            uint place = SealHash.Id(def.Id);
            var tide = new ModelTide { Profile = StPeters, Config = _config };
            var a = new SealPlace[lie.Length];
            var b = new SealPlace[lie.Length];
            for (double t = 0; t < _config.SecondsPerDay * 3; t += 37.0)
            {
                SealTideReading ra = SealSchedule.ReadTide(tide, t, Period);
                SealTideReading rb = SealSchedule.ReadTide(tide, t, Period);
                float hour = SealSchedule.HourOf(t, _config.SecondsPerDay);
                SealSchedule.Evaluate(k, Seed, place, lie, ids, lie.Length, ra, hour, 0.1f, a);
                SealSchedule.Evaluate(k, Seed, place, lie, ids, lie.Length, rb, hour, 0.1f, b);
                CollectionAssert.AreEqual(a, b, $"t={t}");
            }
        }

        [Test]
        public void Hash_IsStable_AndKeyedByEveryInput()
        {
            double u = SealHash.Unit(Seed, 11u, 22u, 33L, 44u);
            Assert.AreEqual(u, SealHash.Unit(Seed, 11u, 22u, 33L, 44u));
            Assert.That(u, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            Assert.AreNotEqual(u, SealHash.Unit(Seed + 1, 11u, 22u, 33L, 44u));
            Assert.AreNotEqual(u, SealHash.Unit(Seed, 12u, 22u, 33L, 44u));
            Assert.AreNotEqual(u, SealHash.Unit(Seed, 11u, 23u, 33L, 44u));
            Assert.AreNotEqual(u, SealHash.Unit(Seed, 11u, 22u, 34L, 44u));
            Assert.AreNotEqual(u, SealHash.Unit(Seed, 11u, 22u, 33L, 45u));
            Assert.AreEqual(SealHash.Id("haulout.st_peters.whelps"), SealHash.Id("haulout.st_peters.whelps"));
        }

        // ---- the tide rule ------------------------------------------------------------------------------------

        [Test]
        public void TideRule_NoSealLiesOnAWetSpot_AtAnyStepOverASpringNeapCycle()
        {
            SealHauloutDef def = Whelps();
            SealTuning k = def.Species.ToTuning();
            float[] lie = def.Spots.Select(s => s.LieMetres).ToArray();
            uint[] ids = def.Spots.Select(s => SealHash.Id(s.Id)).ToArray();
            uint place = SealHash.Id(def.Id);
            var tide = new ModelTide { Profile = StPeters, Config = _config };
            var places = new SealPlace[lie.Length];
            var minds = new SealState[lie.Length];
            bool[] begun = new bool[lie.Length];
            int hauledSteps = 0, wetSteps = 0;
            const double step = 30.0;
            double days = _config.LunarMonthDays * 0.5 + 1.0;
            for (double t = 0; t < _config.SecondsPerDay * days; t += step)
            {
                SealTideReading r = SealSchedule.ReadTide(tide, t, Period);
                SealSchedule.Evaluate(k, Seed, place, lie, ids, lie.Length, r, SealSchedule.HourOf(t, _config.SecondsPerDay), 0f, places);
                for (int i = 0; i < lie.Length; i++)
                {
                    if (!begun[i]) { minds[i] = SealMind.Begin(k, places[i], Seed, place, ids[i]); begun[i] = true; }
                    minds[i] = SealMind.Step(k, minds[i], places[i], SealMind.NoIntruder, (float)step, Seed, place, ids[i]);
                    bool onRock = places[i] == SealPlace.Hauled;
                    bool lying = minds[i].Phase == SealPhase.Resting || minds[i].Phase == SealPhase.Alert;
                    if (onRock) hauledSteps++;
                    if (!TidalExposure.IsExposed(r.Level, lie[i])) wetSteps++;
                    if (onRock)
                        Assert.That(r.Level, Is.LessThanOrEqualTo(lie[i] - k.FloodSlipBelowMetres + 1e-4f),
                            $"{def.Spots[i].Id} hauled at t={t} with the water {r.Level:F3} over its margin (rock {lie[i]:F2}).");
                    if (lying)
                        Assert.IsTrue(onRock && TidalExposure.IsExposed(r.Level, lie[i]),
                            $"{def.Spots[i].Id} lies on its rock at t={t} but the schedule has it {places[i]} at water {r.Level:F3}.");
                }
            }
            Assert.That(hauledSteps, Is.GreaterThan(0), "No seal ever hauled out over two weeks: the test proves nothing.");
            Assert.That(wetSteps, Is.GreaterThan(0), "No rock ever covered over two weeks: the test proves nothing.");
        }

        [Test]
        public void Flood_KeepsASealOutOnlyIfThisTidesLowWaterBroughtItOut()
        {
            SealTuning k = Defaults();
            const float lie = -1.0f;
            // The water has been 0.25 under the rock all tide (never far enough on the ebb): stays in.
            var neverOut = new SealTideReading(-1.25f, true, -1.25f, 7);
            Assert.AreNotEqual(SealPlace.Hauled, SealSchedule.PlaceOf(k, neverOut, lie, 1f, 0.0, 0.0, 0.0, 0.99));
            // The same level on a flood after a low of -2: it came out on the ebb and is still out.
            var cameOut = new SealTideReading(-1.25f, true, -2f, 7);
            Assert.AreEqual(SealPlace.Hauled, SealSchedule.PlaceOf(k, cameOut, lie, 1f, 0.0, 0.0, 0.0, 0.99));
            // At the flood margin it goes.
            var covered = new SealTideReading(lie - k.FloodSlipBelowMetres + 0.01f, true, -2f, 7);
            Assert.AreNotEqual(SealPlace.Hauled, SealSchedule.PlaceOf(k, covered, lie, 1f, 0.0, 0.0, 0.0, 0.99));
        }

        // ---- the approach puzzle ------------------------------------------------------------------------------

        static SealState StepAt(in SealTuning k, SealState s, float distance, float dt, SealPlace place = SealPlace.Hauled)
            => SealMind.Step(k, s, place, distance, dt, Seed, 1u, 2u);

        [Test]
        public void Approach_AlertsThenCreepSettlesThenTooCloseSlips()
        {
            SealTuning k = Defaults();
            SealState s = SealMind.Begin(k, SealPlace.Hauled, Seed, 1u, 2u);
            Assert.AreEqual(SealPhase.Resting, s.Phase);

            float d = k.AlertDistanceMetres + 1f;
            s = StepAt(k, s, d, 0.5f);
            Assert.AreEqual(SealPhase.Resting, s.Phase, "outside the alert ring");

            // Walk in at 1 m/s (quicker than a creep, slower than a startle): it lifts its head.
            d = k.AlertDistanceMetres - 0.5f; s = StepAt(k, s, d, 0.5f);
            d -= 0.5f; s = StepAt(k, s, d, 0.5f);
            Assert.AreEqual(SealPhase.Alert, s.Phase);

            // Hold still: it settles after AlertCalmSeconds.
            for (float t = 0; t < k.AlertCalmSeconds + 0.6f; t += 0.5f) s = StepAt(k, s, d, 0.5f);
            Assert.AreEqual(SealPhase.Resting, s.Phase);

            // Creep in: it stays down until the slip distance.
            while (d > k.SlipDistanceMetres + 0.2f) { d -= k.CreepSpeed * 0.5f * 0.9f; s = StepAt(k, s, d, 0.5f); Assert.AreEqual(SealPhase.Resting, s.Phase, $"creeping at {d:F2} m"); }
            s = StepAt(k, s, k.SlipDistanceMetres - 0.1f, 0.5f);
            Assert.AreEqual(SealPhase.SlidingIn, s.Phase);
            Assert.IsTrue(s.Flushed);
        }

        [Test]
        public void Approach_TooFastStartlesIntoTheWaterAtOnce()
        {
            SealTuning k = Defaults();
            SealState s = SealMind.Begin(k, SealPlace.Hauled, Seed, 1u, 2u);
            float d = k.AlertDistanceMetres - 1f;
            s = StepAt(k, s, d, 0.5f);
            s = StepAt(k, s, d - (k.StartleApproachSpeed + 0.5f) * 0.5f, 0.5f);
            Assert.AreEqual(SealPhase.SlidingIn, s.Phase);
            Assert.IsTrue(s.Flushed);
        }

        [Test]
        public void Flushed_StaysInTheWaterWhileWatched_ThenHaulsBackAfterCalm()
        {
            SealTuning k = Defaults();
            SealState s = SealMind.Begin(k, SealPlace.Hauled, Seed, 1u, 2u);
            s = StepAt(k, s, 1f, 0.5f);
            Assert.AreEqual(SealPhase.SlidingIn, s.Phase);
            for (float t = 0; t < k.SlideSeconds + 0.5f; t += 0.5f) s = StepAt(k, s, k.SlipDistanceMetres + 1f, 0.5f);
            Assert.That(s.Phase, Is.EqualTo(SealPhase.Swimming).Or.EqualTo(SealPhase.Diving));
            // Watched for a long while: never back up.
            for (float t = 0; t < k.CalmReturnSeconds * 2f; t += 0.5f)
            {
                s = StepAt(k, s, k.AlertDistanceMetres - 1f, 0.5f);
                Assert.AreNotEqual(SealPhase.SlidingOut, s.Phase);
                Assert.AreNotEqual(SealPhase.Resting, s.Phase);
            }
            // Left alone: back on its rock within calm + a dive + a slide.
            float limit = k.CalmReturnSeconds + k.DiveSeconds + k.SlideSeconds + k.SurfaceSecondsMax + 2f;
            float waited = 0f;
            while (s.Phase != SealPhase.Resting && waited < limit) { s = StepAt(k, s, SealMind.NoIntruder, 0.5f); waited += 0.5f; }
            Assert.AreEqual(SealPhase.Resting, s.Phase, $"not back after {waited} s");
            Assert.IsFalse(s.Flushed);
        }

        [Test]
        public void TideCover_SendsARestingSealIn_WithoutFlushingIt()
        {
            SealTuning k = Defaults();
            SealState s = SealMind.Begin(k, SealPlace.Hauled, Seed, 1u, 2u);
            s = StepAt(k, s, SealMind.NoIntruder, 0.5f, SealPlace.InWater);
            Assert.AreEqual(SealPhase.SlidingIn, s.Phase);
            Assert.IsFalse(s.Flushed);
        }

        [Test]
        public void Wind_UpwindScaresFromFarther_DownwindLetsYouCloser_CalmIsNeutral()
        {
            SealTuning k = Defaults();
            Vector2 toSeal = Vector2.right * 10f;
            Assert.AreEqual(1f, SealMind.WindScale(k, toSeal, Vector2.zero), 1e-5f);
            Assert.AreEqual(k.UpwindDistanceScale, SealMind.WindScale(k, toSeal, Vector2.right * k.FullScentWindSpeed * 2f), 1e-4f);
            Assert.AreEqual(k.DownwindDistanceScale, SealMind.WindScale(k, toSeal, Vector2.left * k.FullScentWindSpeed * 2f), 1e-4f);
            float light = SealMind.WindScale(k, toSeal, Vector2.right * k.FullScentWindSpeed * 0.5f);
            Assert.That(light, Is.GreaterThan(1f).And.LessThan(k.UpwindDistanceScale));
        }

        // ---- how many ------------------------------------------------------------------------------------------

        int HauledOverTides(in SealTuning k, float hour, float sea)
        {
            SealHauloutDef def = Whelps();
            float[] lie = def.Spots.Select(s => s.LieMetres).ToArray();
            uint[] ids = def.Spots.Select(s => SealHash.Id(s.Id)).ToArray();
            var places = new SealPlace[lie.Length];
            int n = 0;
            for (long cycle = 0; cycle < 400; cycle++)
            {
                var lowWater = new SealTideReading(-5f, false, -5f, cycle);
                SealSchedule.Evaluate(k, Seed, SealHash.Id(def.Id), lie, ids, lie.Length, lowWater, hour, sea, places);
                n += places.Count(p => p == SealPlace.Hauled);
            }
            return n;
        }

        [Test]
        public void Counts_MostOnACalmSunnyLowTide_FewerAtNight_FewerInARoughSea()
        {
            SealTuning k = Whelps().Species.ToTuning();
            int calmDay = HauledOverTides(k, 13f, 0f);
            int night = HauledOverTides(k, 2f, 0f);
            int rough = HauledOverTides(k, 13f, 1f);
            int spots = Whelps().Spots.Count * 400;
            Assert.That(calmDay, Is.EqualTo(spots * k.BaseOccupancy).Within(spots * 0.06f));
            Assert.That(night, Is.EqualTo(spots * k.BaseOccupancy * k.NightOccupancyScale).Within(spots * 0.06f));
            Assert.That(rough, Is.EqualTo(spots * k.BaseOccupancy * k.RoughOccupancyScale).Within(spots * 0.06f));
            Assert.That(calmDay, Is.GreaterThan(night));
            Assert.That(calmDay, Is.GreaterThan(rough));
        }

        [Test]
        public void DayFactor_RampsAtDawnAndDusk()
        {
            SealTuning k = Defaults();
            Assert.AreEqual(k.NightOccupancyScale, SealSchedule.DayFactor(k, 1f), 1e-5f);
            Assert.AreEqual(1f, SealSchedule.DayFactor(k, 12f), 1e-5f);
            float dawn = SealSchedule.DayFactor(k, (k.DawnFromHour + k.DawnToHour) * 0.5f);
            Assert.That(dawn, Is.GreaterThan(k.NightOccupancyScale).And.LessThan(1f));
            float dusk = SealSchedule.DayFactor(k, (k.DuskFromHour + k.DuskToHour) * 0.5f);
            Assert.That(dusk, Is.GreaterThan(k.NightOccupancyScale).And.LessThan(1f));
        }

        // ---- the camera's question ----------------------------------------------------------------------------

        [Test]
        public void CountInFrame_CountsVisibleSealsOfTheSubjectInsideTheFrame()
        {
            var list = new[]
            {
                new WildlifeSighting("wildlife.grey_seal", new Vector2(1, 1), WildlifePose.Resting, 1),
                new WildlifeSighting("wildlife.grey_seal", new Vector2(2, 1), WildlifePose.Swimming, 2),
                new WildlifeSighting("wildlife.grey_seal", new Vector2(2, 2), WildlifePose.Diving, 3),
                new WildlifeSighting("wildlife.grey_seal", new Vector2(50, 50), WildlifePose.Alert, 4),
                new WildlifeSighting("bird.herring_gull", new Vector2(1, 2), WildlifePose.Resting, 5),
            };
            var frame = new Rect(0, 0, 10, 10);
            Assert.AreEqual(2, WildlifePresences.CountInFrame(list, list.Length, frame, "wildlife.grey_seal", false));
            Assert.AreEqual(1, WildlifePresences.CountInFrame(list, list.Length, frame, "wildlife.grey_seal", true));
            Assert.AreEqual(0, WildlifePresences.CountInFrame(list, list.Length, frame, "wildlife.harbour_seal", false));
        }

        sealed class FakeProducer : IWildlifePresence
        {
            public WildlifeSighting[] Items = Array.Empty<WildlifeSighting>();
            public int Count => Items.Length;
            public int CopySightings(WildlifeSighting[] into)
            {
                int n = Math.Min(into.Length, Items.Length);
                Array.Copy(Items, into, n);
                return n;
            }
        }

        [Test]
        public void Registry_AsksEveryProducer_AndForgetsOnReset()
        {
            WildlifePresences.Reset();
            var a = new FakeProducer { Items = new[] { new WildlifeSighting("wildlife.grey_seal", Vector2.one, WildlifePose.Resting, 1) } };
            var b = new FakeProducer { Items = new[] { new WildlifeSighting("wildlife.grey_seal", Vector2.one * 2, WildlifePose.Alert, 2) } };
            Assert.IsTrue(WildlifePresences.Register(a));
            Assert.IsTrue(WildlifePresences.Register(b));
            Assert.IsTrue(WildlifePresences.Register(a));
            Assert.AreEqual(2, WildlifePresences.ProducerCount);
            var scratch = new WildlifeSighting[8];
            Assert.AreEqual(2, WildlifePresences.CountInFrame(new Rect(0, 0, 5, 5), "wildlife.grey_seal", true, scratch));
            WildlifePresences.Unregister(a);
            Assert.AreEqual(1, WildlifePresences.CountInFrame(new Rect(0, 0, 5, 5), "wildlife.grey_seal", true, scratch));
            WildlifePresences.Reset();
            Assert.AreEqual(0, WildlifePresences.ProducerCount);
        }

        [Test]
        public void Facing_FollowsTheKeySceneDir_CounterClockwiseFromNorth()
        {
            Assert.IsFalse(SealHaulout.FacesEast(2, 0u), "2 is west");
            Assert.IsFalse(SealHaulout.FacesEast(1, 0u));
            Assert.IsFalse(SealHaulout.FacesEast(3, 0u));
            Assert.IsTrue(SealHaulout.FacesEast(6, 0u), "6 is east");
            Assert.IsTrue(SealHaulout.FacesEast(5, 0u));
            Assert.IsTrue(SealHaulout.FacesEast(7, 0u));
        }

        [Test]
        public void RestPose_IsTheSpotsOwn_AndTheReefShowsMoreThanOne()
        {
            SealHauloutDef def = Whelps();
            uint place = SealHash.Id(def.Id);
            foreach (int seed in new[] { 1, 7, 12345 })
            {
                var poses = new System.Collections.Generic.HashSet<int>();
                foreach (SealSpot spot in def.Spots)
                {
                    uint h = SealHash.Id(spot.Id);
                    int pose = SealHaulout.RestPoseOf(seed, place, h);
                    Assert.AreEqual(pose, SealHaulout.RestPoseOf(seed, place, h), "the same seal lies the same way");
                    Assert.That(pose, Is.InRange(0, 2));
                    poses.Add(pose);
                }
                Assert.That(poses.Count, Is.GreaterThanOrEqualTo(2), $"seed {seed}: every seal on the reef lies the same way");
            }
            Assert.That(def.Spots.Select(s => SealHaulout.FacesEast(s.Facing, SealHash.Id(s.Id))).Distinct().Count(), Is.EqualTo(2),
                "the reef's seals all face one way");
        }

        // ---- the scene ----------------------------------------------------------------------------------------

        [Test]
        public void Scene_HoldsOneHauloutHost_AndTheStepRebuildsItUnchanged()
        {
            string text = System.IO.File.ReadAllText(StPetersLayerRefresh.ScenePath);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(text,
                "^  m_Name: " + StPetersLayerRefresh.SealsRootName + "$", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
            SealHauloutDef def = Whelps();
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string guid, out long local));
            StringAssert.Contains("_haulout: {fileID: " + local + ", guid: " + guid + ", type: 2}", text);
            StPetersLayerRefresh.ScriptRef script = new StPetersLayerRefresh.EditorVillageLightAssets().ScriptFor(typeof(SealHaulout));
            var patch = StPetersLayerRefresh.Seals(StPetersLayerRefresh.SceneYaml.Parse(text), script,
                new StPetersLayerRefresh.ObjRef(local, guid, 2));
            Assert.AreEqual(text, patch.ApplyTo(text), "the seals step rebuilds the committed scene byte for byte");
        }

        // ---- the content ---------------------------------------------------------------------------------------

        [Test]
        public void Content_TheGreySealSpecies_IsWhole()
        {
            WildlifeSpeciesDef s = Whelps().Species;
            Assert.IsNotNull(s);
            Assert.AreEqual(SealContentSetup.SpeciesId, s.Id);
            StringAssert.StartsWith(WildlifeSpeciesDef.IdPrefix, s.Id);
            Assert.That(s.FloodSlipBelowMetres, Is.GreaterThan(0f), "a zero flood margin would let a seal lie in the water");
            Assert.That(s.EbbHaulOutBelowMetres, Is.GreaterThanOrEqualTo(s.FloodSlipBelowMetres));
            Assert.That(s.SlipDistanceMetres, Is.LessThan(s.AlertDistanceMetres));
            Assert.That(s.CreepSpeed, Is.LessThan(s.StartleApproachSpeed));
            Assert.That(s.DawnFromHour, Is.LessThanOrEqualTo(s.DawnToHour));
            Assert.That(s.DawnToHour, Is.LessThanOrEqualTo(s.DuskFromHour));
            Assert.That(s.DuskFromHour, Is.LessThanOrEqualTo(s.DuskToHour));
            Assert.That(s.CalmSeaState01, Is.LessThan(s.RoughSeaState01));
            foreach (var (name, frames, count) in new[]
                     {
                         ("rest", s.RestFrames, 6), ("alert", s.AlertFrames, 4), ("slide", s.SlideFrames, 6),
                         ("swim", s.SwimFrames, 4), ("dive", s.DiveFrames, 5),
                     })
            {
                Assert.AreEqual(count, frames?.Length ?? 0, name + " frames");
                Assert.IsTrue(frames.All(f => f != null), name + " frames has a hole");
                Assert.IsTrue(frames.All(f => Mathf.Approximately(f.pixelsPerUnit, 32f)), name + " frames are not PPU 32");
                Assert.IsTrue(frames.All(f => Mathf.Approximately(f.rect.width, SealContentSetup.CellW)
                                              && Mathf.Approximately(f.rect.height, SealContentSetup.CellH)), name + " frames are not one cell");
                // the pivot is the belly's ground contact: Unity's pivot is from the cell's bottom-left
                Vector2 pivot = new Vector2(SealContentSetup.PivotFromTopLeft.x, SealContentSetup.CellH - SealContentSetup.PivotFromTopLeft.y);
                Assert.IsTrue(frames.All(f => (f.pivot - pivot).sqrMagnitude < 0.01f), name + " frames' pivot moved");
            }
            // a ~2 m seal against the 5 m dory: the cell is wide enough for one
            Assert.That(SealContentSetup.CellW / (float)SealContentSetup.PixelsPerUnit, Is.GreaterThanOrEqualTo(2f));
            Assert.That(s.Coats.Length, Is.GreaterThanOrEqualTo(SealContentSetup.CoatNames.Length));
        }

        [Test]
        public void Content_TheWhelpsHaulout_MatchesItsKeySceneRows()
        {
            SealHauloutDef def = Whelps();
            Assert.AreEqual(SealContentSetup.HauloutId, def.Id);
            StringAssert.StartsWith(SealHauloutDef.IdPrefix, def.Id);
            Assert.AreEqual("region.st_peters", def.RegionId);
            Assert.That(def.Spots.Count, Is.InRange(1, Math.Min(SealHauloutDef.MaxSpots, def.Species.MaxDrawn)));
            Assert.AreEqual(def.Spots.Count, def.Spots.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count(), "spot ids repeat");
            Assert.That(def.SwimPoints.Length, Is.GreaterThan(0));

            var scene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(SealContentSetup.KeyScenePath);
            Assert.AreEqual(scene.Id, def.KeySceneId);
            var rows = scene.Pieces.Where(SealContentSetup.IsHaulOutRow).ToList();
            Assert.AreEqual(rows.Count, def.Spots.Count, "every haul-out row is a spot, and no more");
            SealTuning k = def.Species.ToTuning();
            foreach (KeyScenePiece row in rows)
            {
                SealSpot spot = def.Spots.Single(s => s.Id == SealContentSetup.SpotIdOf(row.Id));
                Assert.AreEqual(row.At, spot.At, row.Id);
                Assert.AreEqual(row.Z, spot.LieMetres, 1e-4f, row.Id);
                Assert.IsTrue(SealContentSetup.TryReadTideRule(row.WhenTide, out float ebb, out float flood), row.Id);
                Assert.AreEqual(row.Z - k.EbbHaulOutBelowMetres, ebb, 0.006f, row.Id + " ebb margin differs from the key scene's");
                Assert.AreEqual(row.Z - k.FloodSlipBelowMetres, flood, 0.006f, row.Id + " flood margin differs from the key scene's");
            }
        }
    }
}
