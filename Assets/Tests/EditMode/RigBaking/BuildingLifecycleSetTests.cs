using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the LIFECYCLE half of the village building kit — <c>VillageBuildingKit.LifecycleSet</c>,
    /// the three derelict outbuildings on Ginny's plot and the shut cannery — and the bake request they
    /// turn into.
    ///
    /// <para><b>The sibling of <see cref="VillageBuildingSetTests"/>, and separate for the same reason
    /// the tables are.</b> That fixture asserts the M1 village's identity: five clapboard buildings, all
    /// house-rig, all with a door on the gable so a room can be baked inside. None of that is true here
    /// and none of it should be. What IS true here is a different list, and this is it.</para>
    ///
    /// <para><b>⚠️ Everything below runs without a JS engine and without a bake.</b> That is deliberate:
    /// the failures these catch are in the TABLE — a state id that the pass would silently ignore, a key
    /// that collides with an M1 sheet — and a table bug found at bake time has already cost twenty
    /// minutes. <c>BuildingLifecyclePassTests</c> is where the pixels are checked.</para>
    /// </summary>
    public class BuildingLifecycleSetTests
    {
        // ---- the table -------------------------------------------------------------------------

        [Test]
        public void EveryLifecycleBuild_ActuallyDeclaresAState()
        {
            CollectionAssert.IsNotEmpty(VillageBuildingKit.LifecycleSet,
                "an empty lifecycle set makes every assertion in this fixture vacuous");

            foreach (var build in VillageBuildingKit.LifecycleSet)
                Assert.IsTrue(build.HasLifecycle,
                    $"'{build.Key}' is in the LIFECYCLE set but resolves to finished/sound, so it would " +
                    "bake a building in perfect repair under a derelict's name and nothing would say so.");
        }

        [Test]
        public void EveryStateId_IsOneThePassUnderstands()
        {
            // The measured trap this exists for: an unknown id makes active() true and then normalises
            // back to the default, so the pass runs and does nothing. See BuildingLifecycleStates.
            foreach (var build in VillageBuildingKit.LifecycleSet)
            {
                Assert.DoesNotThrow(() => BuildingLifecycleStates.AssertKnown(build.Phase, build.Decay),
                    $"'{build.Key}' names a lifecycle state the pass does not know.");

                if (!string.IsNullOrEmpty(build.Phase))
                    CollectionAssert.Contains(BuildingLifecycleStates.Phases, build.Phase);
                if (!string.IsNullOrEmpty(build.Decay))
                    CollectionAssert.Contains(BuildingLifecycleStates.Decays, build.Decay);
            }
        }

        [Test]
        public void NoTwoBuildsInTheWholeKit_ShareAKeyOrASheet()
        {
            // The kit writes ONE folder and ONE contract, so a key collision between the two tables is
            // two builds writing the same PNG — the second silently wins, and the contract carries one
            // entry describing the other's pixels.
            var keys = VillageBuildingKit.AllBuilds.Select(b => b.Key).ToArray();
            CollectionAssert.AllItemsAreUnique(keys,
                "two builds share a key, so they share a sheet path and one overwrites the other");

            var stems = keys.Select(VillageBuildingKit.StemFor).ToArray();
            CollectionAssert.AllItemsAreUnique(stems, "two builds resolve to the same sheet stem");

            Assert.AreEqual(VillageBuildingKit.M1Set.Length + VillageBuildingKit.LifecycleSet.Length,
                            VillageBuildingKit.AllBuilds.Length,
                            "AllBuilds must be exactly the two tables, once each");
        }

        [Test]
        public void TheM1VillageIsUntouchedByTheLifecycleSet()
        {
            // The point of the split. If a derelict ever appears in M1Set, a dozen village invariants
            // (house rig, gable door, a room baked inside) start being asserted about a wharf-rig ruin.
            foreach (var build in VillageBuildingKit.M1Set)
                Assert.IsFalse(build.HasLifecycle,
                    $"'{build.Key}' is in the M1 village set but declares a lifecycle state. The village " +
                    "is the buildings people live and trade in; put a derelict in LifecycleSet.");

            CollectionAssert.AreEqual(new[] { "school", "generalStore", "whiteFarmhouse",
                                              "redSaltbox", "sageCottage" },
                                      VillageBuildingKit.M1Set.Select(b => b.Key).ToArray(),
                "the M1 village is still exactly the five buildings the canon asks for, in kit order");
        }

        [Test]
        public void FindBuild_ReachesBothTables()
        {
            Assert.IsNotNull(VillageBuildingKit.FindBuild("sageCottage"), "an M1 build");
            Assert.IsNotNull(VillageBuildingKit.FindBuild("stPetersCannery"), "a lifecycle build");
            Assert.IsNull(VillageBuildingKit.FindBuild("noSuchBuild"));
        }

        // ---- the bake request ------------------------------------------------------------------

        /// <summary>
        /// 🔴 <b>THE OPTIONS EXPRESSION LAYERS THE STATE ONTO THE RIG'S OWN PRESET — it does not
        /// replace it, and it does not retype it.</b>
        ///
        /// <para>Both failure modes are silent and both produce a valid sheet. Dropping the preset bakes
        /// a ruined DEFAULT shed under the net store's name; dropping the state bakes a pristine one.
        /// The expression is the contract's audit trail, so it is checked as text here and as pixels by
        /// the bake's own <c>UnderlyingOptsJs</c> guard.</para>
        /// </summary>
        [Test]
        public void TheBakeRequest_SpreadsThePreset_AndThenLaysTheStateOverIt()
        {
            foreach (var build in VillageBuildingKit.LifecycleSet)
            {
                string g = RigCatalog.Get(build.RigKey).GlobalName;
                var req = VillageBuildingBakeMenu.RequestFor(build, "Assets/tmp", "stem");

                StringAssert.Contains($"{g}.PRESETS['{build.Preset}']", req.OptsJs,
                    $"'{build.Key}' must spread the rig's OWN preset table — retyping a preset into C# " +
                    "forks it, and the fork goes stale in silence.");

                if (!string.IsNullOrEmpty(build.Decay) && build.Decay != BuildingLifecycleStates.SoundDecay)
                    StringAssert.Contains($"decay:'{build.Decay}'", req.OptsJs);
                if (!string.IsNullOrEmpty(build.Phase) &&
                    build.Phase != BuildingLifecycleStates.FinishedPhase)
                    StringAssert.Contains($"phase:'{build.Phase}'", req.OptsJs);
                if (build.Burnt) StringAssert.Contains("burnt:true", req.OptsJs);

                // …and the build UNDERNEATH is declared: the same build with the state taken off. It
                // arms the did-the-state-apply tripwire AND it is what the azimuth probe measures —
                // 🔴 without it the bake REFUSES every derelict, because the probe cross-checks the
                // drawn silhouette against the rig's Wd and a ruin's planks lie outside its footprint
                // (measured 2.2-2.4x: netShed@ruin 281 px against 124 expected). Three of the four
                // lifecycle builds failed exactly that way on the first bake.
                Assert.IsNotEmpty(req.UnderlyingOptsJs ?? "",
                    $"'{build.Key}' declares no underlying build. The bake would then measure the " +
                    "convention off the RUIN and refuse it, and nothing would check that the state " +
                    "applied at all.");
                Assert.AreNotEqual(req.OptsJs, req.UnderlyingOptsJs,
                    "the guard is comparing the build against itself");
                StringAssert.DoesNotContain("decay:", req.UnderlyingOptsJs,
                    "the 'without the state' half must not carry the state");
                StringAssert.DoesNotContain("phase:", req.UnderlyingOptsJs);
                StringAssert.DoesNotContain("burnt:", req.UnderlyingOptsJs);
            }
        }

        [Test]
        public void TheBakeRequest_SolvesTheGridForTheKitsImportCap_NotTheBakersHardLimit()
        {
            // The cannery at `collapsing` is the widest cell this kit has ever packed — the pass adds a
            // fallen wall and a spill of debris OUTSIDE the building's own silhouette, so the crop is
            // wider than the finished building's by a third. Between 2048 and 4096 a sheet bakes fine
            // and IMPORTS silently downscaled, with the sprite count still correct.
            foreach (var build in VillageBuildingKit.LifecycleSet)
            {
                var req = VillageBuildingBakeMenu.RequestFor(build, "Assets/tmp", "stem");
                Assert.AreEqual(VillageBuildingKit.ImportCapFor(build), req.MaxSheetDimension,
                    $"'{build.Key}' must pack against the cap it will be IMPORTED at");
                Assert.AreEqual(VillageBuildingKit.Facings, req.Facings);
            }
        }

        /// <summary>
        /// The 2048 discipline still holds for everything except the one build that provably cannot meet
        /// it — and that exception is NAMED, so it cannot spread by copy-paste.
        /// </summary>
        [Test]
        public void OnlyTheCanneryLiftsTheImportCap()
        {
            foreach (var build in VillageBuildingKit.AllBuilds)
            {
                int cap = VillageBuildingKit.ImportCapFor(build);
                if (build.Key == "stPetersCannery")
                {
                    Assert.AreEqual(4096, cap,
                        "the cannery does not pack under 2048 in ANY state — measured, see its row");
                    continue;
                }

                Assert.AreEqual(VillageBuildingKit.ImportSizeCap, cap,
                    $"'{build.Key}' lifts the kit's import cap. Lifting it is a documented one-building " +
                    "exception with a measurement behind it, not a way round a sheet that does not fit — " +
                    "a 4096 texture is the first thing a later mobile port has to undo (CLAUDE.md rule 7).");
            }
        }

        [Test]
        public void AStateTypo_IsRefusedAtConstruction_NotAtBakeTime()
        {
            Assert.Throws<ArgumentException>(() => VillageBuildingKit.Build.FromPresetInState(
                "typo", "Typo", "wharfBuilding", "netShed",
                phase: null, decay: "collapsed", burnt: false, why: "a plausible misspelling"));

            Assert.Throws<ArgumentException>(() => VillageBuildingKit.Build.FromPresetInState(
                "inert", "Inert", "wharfBuilding", "netShed",
                phase: "finished", decay: "sound", burnt: false, why: "no state at all"));
        }

        // ---- the tag ---------------------------------------------------------------------------

        [Test]
        public void TheStateTag_ReadsAsTheStateItNames()
        {
            Assert.AreEqual("neglected", BuildingLifecycleStates.Tag(null, "neglected", false));
            Assert.AreEqual("frame", BuildingLifecycleStates.Tag("frame", null, false));
            Assert.AreEqual("frame+abandoned", BuildingLifecycleStates.Tag("frame", "abandoned", false));
            Assert.AreEqual("ruin+burnt", BuildingLifecycleStates.Tag(null, "ruin", true));
            Assert.AreEqual("finished", BuildingLifecycleStates.Tag(null, null, false));
            Assert.AreEqual("finished", BuildingLifecycleStates.Tag("finished", "sound", false));

            foreach (var build in VillageBuildingKit.LifecycleSet)
                Assert.AreNotEqual("finished", build.StateTag,
                    $"'{build.Key}' tags as finished, so its sheet name says nothing about its state");
        }
    }
}
