using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE OWNER'S PER-SPECIES SCHOOL DATA, judged as authored (ruling 2026-09-06: "fish school density
    /// should be more accurate for species, give them species defs"). A content agent who adds a finfish
    /// without saying how many of them there are, how big a school is or how loosely it swims goes red
    /// HERE, by asset path, rather than shipping a fish that silently inherits one global number.
    ///
    /// <para><b>Shellfish are exempt on purpose.</b> A clam does not shoal, and the school sim excludes
    /// <see cref="FishCategory.Shellfish"/> from the swimming pool outright
    /// (<c>FishSchoolModel.Eligible</c>) — so demanding a shoal spread of a clam would be demanding a
    /// number that can never be read.</para>
    /// </summary>
    public class FishSchoolDensityContentValidationTests
    {
        private static (string path, FishSpeciesDef def)[] AuthoredDefs()
        {
            var list = new List<(string, FishSpeciesDef)>();
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                if (def != null) list.Add((path, def));
            }
            return list.ToArray();
        }

        private static bool Swims(FishSpeciesDef d) => !d.IsShellfish;

        [Test]
        public void EveryFinfish_StatesItsOwnSchool()
        {
            var defs = AuthoredDefs();
            Assert.IsNotEmpty(defs, "the fish defs ship");

            int finfish = 0;
            foreach ((string path, FishSpeciesDef def) in defs)
            {
                if (!Swims(def)) continue;
                finfish++;

                Assert.IsTrue(def.StatesSchoolSize,
                    $"{path}: a finfish must state MinSchoolMarks/MaxSchoolMarks (owner 2026-09-06)");
                Assert.LessOrEqual(def.MinSchoolMarks, def.MaxSchoolMarks,
                    $"{path}: school size range is inverted");

                Assert.IsTrue(def.StatesShoalSpread,
                    $"{path}: a finfish must state ShoalSpreadMetres — how loosely it shoals is a fact " +
                    "about the species, and the fallback exists for legacy data, not for new fish");
                Assert.Greater(def.ShoalSpreadMetres, 0f, $"{path}: spread must be positive");

                Assert.IsTrue(def.StatesSchoolDensity,
                    $"{path}: a finfish must state SchoolsPerSquareKilometre");
                Assert.Greater(def.SchoolsPerSquareKilometre, 0f, $"{path}: density must be positive");
            }

            Assert.Greater(finfish, 0, "at least one finfish is authored");
        }

        /// <summary>
        /// The authored spreads must be LENGTHS a shoal can actually be seen at — small next to the
        /// school's own radius, which is a boat-nearness number and not a fish one. A spread authored
        /// anywhere near the 22–55 m radius range is the exact mistake this PR removed, so the bar is
        /// set from the CAMERA the game shows rather than from anything the fix touched.
        /// </summary>
        [Test]
        public void AuthoredSpreads_FitInTheCameraHeShipsWith()
        {
            // CameraFollow.DefaultWorldHeightMeters (boat framing), restated.
            const float cameraHeightM = 14f;

            foreach ((string path, FishSpeciesDef def) in AuthoredDefs())
            {
                if (!Swims(def) || !def.StatesShoalSpread) continue;

                Assert.Less(def.ShoalSpreadMetres, cameraHeightM * 0.5f,
                    $"{path}: a shoal loop of {def.ShoalSpreadMetres} m does not fit the {cameraHeightM} m " +
                    "camera — the fish would swim out of the picture, which is the defect this field fixed");
                Assert.GreaterOrEqual(def.ShoalSpreadMetres, 0.1f,
                    $"{path}: a spread this small stacks the whole school on one pixel");
            }
        }

        /// <summary>
        /// The authored densities must be reachable: a cell holds at most ONE school, so a density above
        /// one school per cell is a number the sim can never honour and would read to the owner as a
        /// tuning knob that stopped responding.
        /// </summary>
        [Test]
        public void AuthoredDensities_AreReachableAtTheShippedCell()
        {
            float cell = FishSchoolSettings.Default.CellSizeMetres;
            float cellKm2 = (cell / 1000f) * (cell / 1000f);
            float ceiling = 1f / cellKm2;                       // one school per cell, in schools/km^2

            foreach ((string path, FishSpeciesDef def) in AuthoredDefs())
            {
                if (!Swims(def) || !def.StatesSchoolDensity) continue;

                Assert.LessOrEqual(def.SchoolsPerSquareKilometre, ceiling,
                    $"{path}: {def.SchoolsPerSquareKilometre}/km^2 is past the one-school-per-cell " +
                    $"ceiling of {ceiling:F0}/km^2 at the shipped {cell} m cell — the extra cannot appear");
            }
        }

        /// <summary>
        /// THE OWNER'S JUMPER RULING, pinned to the data: exactly bass, mackerel and herring clear the
        /// water; cod, haddock, pollock and flounder never do (2026-09-06). Both directions, so neither
        /// adding the flag to a groundfish nor dropping it from a jumper can pass.
        /// </summary>
        [Test]
        public void ExactlyTheRuledSpecies_Jump()
        {
            var jumpers = new HashSet<string> { "fish.striped_bass", "fish.mackerel", "fish.atlantic_herring" };
            var neverJump = new HashSet<string>
            {
                "fish.atlantic_cod", "fish.haddock", "fish.pollock", "fish.winter_flounder"
            };

            var seen = new HashSet<string>();
            foreach ((string path, FishSpeciesDef def) in AuthoredDefs())
            {
                bool jumps = (def.BehaviorFlags & FishFlags.Jumps) != 0;
                if (jumpers.Contains(def.Id))
                {
                    seen.Add(def.Id);
                    Assert.IsTrue(jumps, $"{path}: the owner ruled {def.Id} a jumper");
                }
                else if (neverJump.Contains(def.Id))
                {
                    seen.Add(def.Id);
                    Assert.IsFalse(jumps, $"{path}: the owner ruled {def.Id} does NOT clear the water");
                }
            }

            var expected = new List<string>(jumpers);
            expected.AddRange(neverJump);
            CollectionAssert.AreEquivalent(expected, seen,
                "every species the ruling names must still be authored under that id");
        }
    }
}
