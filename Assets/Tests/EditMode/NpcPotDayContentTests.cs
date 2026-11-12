using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Every authored NPC pot day is workable on the real ground: she floats the whole way at the lowest tide,
    /// her pots lie in fishable water, and each rope reaches the surface at the highest.
    /// </summary>
    public class NpcPotDayContentTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const double TrackStepClockSeconds = 0.5;
        private static readonly int[] Seeds = { 0, 1, 4242, 987654321 };
        private const int DaysProbed = 6;

        private static List<NpcPotDayDef> LoadAll()
        {
            var list = new List<NpcPotDayDef>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(NpcPotDayDef)}", new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        private static BoatOwnerDef OwnerOf(NpcPotDayDef def)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(BoatOwnerDef)}", new[] { DataRoot }))
            {
                var owner = AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (owner != null && owner.Id == def.OwnerId) return owner;
            }
            return null;
        }

        /// <summary>The ground a Def's region is tested on. One region today; a new region adds its ground here.</summary>
        private static ITidalTerrain GroundOf(NpcPotDayDef def)
        {
            Assert.That(def.RegionSceneName, Is.EqualTo("NineMileCreek"),
                        $"{def.Id}: no test ground is wired for region '{def.RegionSceneName}' yet");
            return NineMileCreekLiveGround.Plan;
        }

        [Test]
        public void Days_Exist_WithStableIds_AnOwner_AndAPot()
        {
            var defs = LoadAll();
            Assert.IsNotEmpty(defs, "the working coast ships at least one NpcPotDayDef in Data/");

            var ids = new HashSet<string>();
            var owners = new HashSet<string>();
            foreach (var def in defs)
            {
                string path = AssetDatabase.GetAssetPath(def);
                StringAssert.StartsWith("potday.", def.Id, $"{path}: def ids are type.snake_case");
                Assert.That(def.Id, Does.Match("^potday\\.[a-z0-9_]+$"), path);
                Assert.IsTrue(ids.Add(def.Id), $"{path}: duplicate id '{def.Id}'");
                Assert.IsTrue(owners.Add(def.OwnerId), $"{path}: owner '{def.OwnerId}' already works another day");
                Assert.IsNotNull(OwnerOf(def), $"{path}: no BoatOwnerDef has id '{def.OwnerId}'");
                Assert.IsNotNull(def.Trap, $"{path}: no Trap");
                Assert.IsNotNull(def.Trap.BuoySprite, $"{path}: her pot has no buoy sprite");
                Assert.IsNull(NpcPotDay.Problem(def), $"{path}: {NpcPotDay.Problem(def)}");
                Assert.That(def.BuoyColour.a, Is.EqualTo(1f), $"{path}: a float is opaque");
            }
        }

        [Test]
        public void TheLibrary_ListsEveryDay_AndNothingElse()
        {
            var library = Resources.Load<NpcPotDayLibrary>(NpcPotDayLibrary.ResourcesPath);
            Assert.IsNotNull(library, "Resources/NpcPotDayLibrary.asset is what the run-time presenter reads");
            CollectionAssert.AllItemsAreNotNull(library.Days);
            CollectionAssert.AreEquivalent(LoadAll(), library.Days);
        }

        [Test]
        public void EveryDay_FitsTheClock_AtTheDefaultDayLength()
        {
            foreach (var def in LoadAll())
            {
                foreach (int seed in Seeds)
                {
                    for (int day = 0; day < DaysProbed; day++)
                    {
                        NpcPotDay d = NpcPotDay.Build(def, seed, day, GameConfig.DefaultSecondsPerDay);
                        Assert.That(d.FitsTheDay(GameConfig.DefaultSecondsPerDay), Is.True,
                                    $"{def.Id} seed {seed} day {day}: out {d.DepartSeconds - d.DayStartSeconds:0} s, " +
                                    $"home {d.ReturnSeconds - d.DayStartSeconds:0} s of {GameConfig.DefaultSecondsPerDay} s");
                    }
                }
            }
        }

        [Test]
        public void SheFloats_TheWholeDay_AtSpringLow()
        {
            foreach (var def in LoadAll())
            {
                ITidalTerrain ground = GroundOf(def);
                BoatOwnerDef owner = OwnerOf(def);
                Assert.IsNotNull(owner, def.Id);
                Assert.IsNotNull(owner.Boat, $"{def.Id}: her owner has no hull");
                Assert.That(def.DraughtMeters, Is.GreaterThanOrEqualTo(owner.Boat.DraughtMeters),
                            $"{def.Id}: the day's draught is shallower than her hull Def's");

                float least = float.MaxValue;
                float deepestKeelAllowed = NineMileCreekMainland.SpringLowWater - def.DraughtMeters - def.KeelClearanceMeters;
                foreach (int seed in Seeds)
                {
                    for (int day = 0; day < DaysProbed; day++)
                    {
                        NpcPotDay d = NpcPotDay.Build(def, seed, day, GameConfig.DefaultSecondsPerDay);
                        for (double t = d.DepartSeconds; t <= d.ReturnSeconds; t += TrackStepClockSeconds)
                        {
                            NpcPotBoatPose pose = d.PoseAt(t);
                            float bed = ground.ElevationAt(pose.Position);
                            least = Mathf.Min(least, NineMileCreekMainland.SpringLowWater - bed);
                            Assert.That(bed, Is.LessThanOrEqualTo(deepestKeelAllowed),
                                        $"{def.Id} seed {seed} day {day}: at {pose.Position} ({pose.Stage}) the bed is {bed:0.00} m; " +
                                        $"she draws {def.DraughtMeters} m and spring low is {NineMileCreekMainland.SpringLowWater:0.00} m");
                        }
                    }
                }
                TestContext.WriteLine($"{def.Id}: least water under her track at spring low {least:0.00} m");
            }
        }

        [Test]
        public void HerPots_LieInFishableWater_AndEveryRopeReachesTheSurfaceAtSpringHigh()
        {
            foreach (var def in LoadAll())
            {
                ITidalTerrain ground = GroundOf(def);
                foreach (int seed in Seeds)
                {
                    for (int day = -1; day < DaysProbed; day++)
                    {
                        for (int i = 0; i < def.PotCount; i++)
                        {
                            Vector2 pot = NpcPotDay.PotPosition(def, seed, day, i);
                            float bed = ground.ElevationAt(pot);
                            float low = NineMileCreekMainland.SpringLowWater - bed;
                            float high = NineMileCreekMainland.SpringHighWater - bed;
                            string where = $"{def.Id} seed {seed} day {day} pot {i} at {pot} (bed {bed:0.00} m)";
                            Assert.That(low, Is.GreaterThanOrEqualTo(def.Trap.MinSoakDepthMeters), $"{where}: too shoal at spring low");
                            Assert.That(high, Is.LessThanOrEqualTo(def.Trap.MaxSoakDepthMeters), $"{where}: too deep at spring high");
                            Assert.That(PotRopeMath.IsDrowned(def.RopeLengthMeters, high), Is.False,
                                        $"{where}: {high:0.00} m of water drowns a {def.RopeLengthMeters} m rope");
                        }
                    }
                }
            }
        }
    }
}
