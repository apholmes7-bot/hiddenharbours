using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE SCHOOL MODEL'S LIFETIME (ADR 0025 S3) — the half of the feature that only exists once the scene
    /// lifecycle is running, and therefore cannot be pinned in EditMode: <c>FishingController</c>
    /// self-registers the region's <see cref="FishSchoolModel"/> into
    /// <see cref="GameServices.FishSchools"/> on enable, and hands the seam back to the empty sea on
    /// disable.
    ///
    /// <para><b>Why PlayMode.</b> EditMode never fires <c>OnEnable</c>, so a registration test there
    /// passes for the wrong reason (or silently tests nothing). The sim's own rules — determinism, the
    /// gates, the density map, the honesty invariant — are all EditMode, where they belong; this file is
    /// only about the wiring.</para>
    ///
    /// <para><b>Deliberately unwired otherwise.</b> No environment service, no clock, no terrain, no
    /// GameConfig — so the live world adapter takes every one of its documented fallbacks (seed 0, a flat
    /// calm, open water, the shipped defaults) and the schools still come out. That is the "a missing
    /// subsystem degrades, it never refuses" posture, proved rather than asserted.</para>
    /// </summary>
    public class FishSchoolProducerPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private FishingController NewRod()
        {
            var go = new GameObject("rod");
            _spawned.Add(go);
            return go.AddComponent<FishingController>();
        }

        [UnityTest]
        public IEnumerator TheRodRegistersTheSchoolModel_AndLeavesAnEmptySeaBehindIt()
        {
            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools,
                "before anything is wired the seam is the empty sea");

            FishingController rod = NewRod();
            yield return null;

            Assert.IsInstanceOf<FishSchoolModel>(GameServices.FishSchools,
                "the rod produces the schools — so they exist whether or not any boat has a finder fitted");

            rod.enabled = false;
            yield return null;

            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools,
                "a torn-down producer must leave an honest empty sea, never a dead model (and never null)");
        }

        [UnityTest]
        public IEnumerator DestroyingTheRod_AlsoHandsTheSeamBack()
        {
            FishingController rod = NewRod();
            yield return null;
            Assert.IsInstanceOf<FishSchoolModel>(GameServices.FishSchools);

            Object.Destroy(rod.gameObject);
            yield return null;

            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools);
            Assert.IsNotNull(GameServices.FishSchools, "and it is NEVER null — the UI reads it unchecked");
        }

        [UnityTest]
        public IEnumerator TheRegisteredModel_ActuallyFishes_WithNoServicesWiredAtAll()
        {
            FishingController rod = NewRod();
            rod.Configure(null, new[] { Cod() }, "region.st_peters", Gear.Handline, seed: 1);
            yield return null;

            IFishSchools schools = GameServices.FishSchools;
            var marks = new List<FishMark>();
            var found = new List<FishSchool>();

            // Sweep a stretch of water across a couple of in-game hours. With every service absent the
            // adapter falls back to seed 0 / flat calm / open water — and the sim still produces fish.
            int hits = 0;
            for (int t = 0; t < 24; t++)
            for (int x = 0; x < 24; x++)
            {
                if (schools.MarksAt(new Vector2(x * 47f, x * -23f), t * 91.0, marks) <= 0) continue;
                hits++;
                Assert.Greater(marks[0].Count, 0, "a mark always draws at least one fish");
                Assert.Greater(marks[0].DepthMetres, 0f, "and sits somewhere in the column, in METRES");

                // The same query through the gameplay read reports the same number of schools — the seam's
                // two views of one record (pinned exactly in FishSchoolHonestyTests).
                Assert.AreEqual(marks.Count,
                                schools.SchoolsAt(new Vector2(x * 47f, x * -23f), t * 91.0, found));
            }

            Assert.Greater(hits, 0, "an unwired rig must still find fish — the fallbacks are real behaviour");
        }

        private static FishSpeciesDef Cod()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.atlantic_cod";
            f.RegionIds = new[] { "region.st_peters" };
            f.AllowedGear = Gear.Handline | Gear.Jig;
            f.Seasons = SeasonMask.AllYear;
            f.MinWeightKg = 1f; f.MaxWeightKg = 4f;
            return f;
        }
    }
}
