using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Region / scene-load logic (VS-22): the pure pieces of the additive region path — RegionDef
    /// accessors, the RegionRegistry lookup, and the RegionTravel decisions. No Unity scene loading is
    /// exercised here (that needs play mode + Build Settings); these guard the logic the loader/passage
    /// lean on.
    /// </summary>
    public class RegionTests
    {
        private readonly List<Object> _spawned = new();

        private RegionDef MakeRegion(string id, string sceneName, string unlock = "")
        {
            var r = ScriptableObject.CreateInstance<RegionDef>();
            r.Id = id;
            r.SceneName = sceneName;
            r.UnlockFlag = unlock;
            _spawned.Add(r);
            return r;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- RegionDef ----------------------------------------------------------------------

        [Test]
        public void RegionDef_HasScene_ReflectsSceneName()
        {
            Assert.IsTrue(MakeRegion("region.port_greywick", "Greywick").HasScene);
            Assert.IsFalse(MakeRegion("region.nowhere", "").HasScene);
            Assert.IsFalse(MakeRegion("region.blank", "   ").HasScene, "whitespace is not a scene");
        }

        [Test]
        public void RegionDef_RequiresUnlock_ReflectsUnlockFlag()
        {
            Assert.IsFalse(MakeRegion("region.coddle_cove", "Greybox").RequiresUnlock);
            Assert.IsTrue(MakeRegion("region.ironbound", "Ironbound", "flag.ironbound").RequiresUnlock);
        }

        // ---- RegionRegistry -----------------------------------------------------------------

        [Test]
        public void Registry_FindsByStableId()
        {
            var greywick = MakeRegion("region.port_greywick", "Greywick");
            var cove = MakeRegion("region.coddle_cove", "Greybox");
            var reg = new RegionRegistry(new[] { greywick, cove });

            Assert.AreEqual(2, reg.Count);
            Assert.IsTrue(reg.Contains("region.port_greywick"));
            Assert.IsTrue(reg.TryGet("region.coddle_cove", out var got));
            Assert.AreSame(cove, got);
            Assert.AreSame(greywick, reg.Get("region.port_greywick"));
        }

        [Test]
        public void Registry_UnknownId_IsMiss()
        {
            var reg = new RegionRegistry(new[] { MakeRegion("region.port_greywick", "Greywick") });
            Assert.IsFalse(reg.Contains("region.banks"));
            Assert.IsFalse(reg.TryGet("region.banks", out var got));
            Assert.IsNull(got);
            Assert.IsNull(reg.Get("region.banks"));
            Assert.IsFalse(reg.TryGet(null, out _), "null id is a graceful miss");
        }

        [Test]
        public void Registry_SkipsNullsBlankIdsAndDuplicates()
        {
            var first = MakeRegion("region.dup", "SceneA");
            var dup   = MakeRegion("region.dup", "SceneB");        // same id — ignored (first wins)
            var blank = MakeRegion("", "SceneC");                  // blank id — skipped
            var reg = new RegionRegistry(new[] { first, null, dup, blank });

            Assert.AreEqual(1, reg.Count, "only the first valid, unique-id region is kept");
            Assert.AreSame(first, reg.Get("region.dup"));
        }

        [Test]
        public void Registry_NullInput_IsEmpty()
        {
            var reg = new RegionRegistry(null);
            Assert.AreEqual(0, reg.Count);
            Assert.IsFalse(reg.Contains("anything"));
        }

        // ---- RegionTravel -------------------------------------------------------------------

        [Test]
        public void Travel_CanLoad_RequiresARegionWithAScene()
        {
            Assert.IsTrue(RegionTravel.CanLoad(MakeRegion("region.port_greywick", "Greywick")));
            Assert.IsFalse(RegionTravel.CanLoad(null));
            Assert.IsFalse(RegionTravel.CanLoad(MakeRegion("region.nowhere", "")));
        }

        [Test]
        public void Travel_IsAlreadyHere_MatchesCurrentScene()
        {
            var greywick = MakeRegion("region.port_greywick", "Greywick");
            Assert.IsTrue(RegionTravel.IsAlreadyHere("Greywick", greywick));
            Assert.IsFalse(RegionTravel.IsAlreadyHere("Greybox", greywick));
            Assert.IsFalse(RegionTravel.IsAlreadyHere("", greywick), "no current scene → not 'here'");
        }

        [Test]
        public void Travel_ShouldLoad_OnlyWhenLoadableAndElsewhere()
        {
            var greywick = MakeRegion("region.port_greywick", "Greywick");
            var cove = MakeRegion("region.coddle_cove", "Greybox");

            // From the cove, Greywick is loadable.
            Assert.IsTrue(RegionTravel.ShouldLoad("Greybox", greywick));
            // Already in Greywick → don't reload it.
            Assert.IsFalse(RegionTravel.ShouldLoad("Greywick", greywick));
            // A region with no scene is never loadable.
            Assert.IsFalse(RegionTravel.ShouldLoad("Greywick", MakeRegion("region.nowhere", "")));
            // Null target → no-op.
            Assert.IsFalse(RegionTravel.ShouldLoad("Greywick", null));
            // Round-trip back to the cove from Greywick is loadable.
            Assert.IsTrue(RegionTravel.ShouldLoad("Greywick", cove));
        }
    }
}
