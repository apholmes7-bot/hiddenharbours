using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the AUTHORED personalities, judged as they will actually play (the
    /// content-validation twin of the invariant sweep): every <see cref="RodFightDef"/> asset under
    /// <c>Data/</c> is fought closed-loop through <see cref="RodFightSim"/> across several seeds —
    /// the competent pulse-and-steer hand must LAND it, and a blind pin must SNAP it (P5: daily fish
    /// stay forgiving; skill is a pulse, not a pin). A content agent who authors a personality that
    /// breaks either promise goes red HERE, by asset path, before any playtest.
    /// </summary>
    public class RodFightDataTests
    {
        private static readonly int[] Seeds = { 11, 222, 3333 };

        private static (string path, RodFightDef def)[] AuthoredDefs()
        {
            var list = new System.Collections.Generic.List<(string, RodFightDef)>();
            foreach (string guid in AssetDatabase.FindAssets("t:RodFightDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<RodFightDef>(path);
                if (def != null) list.Add((path, def));
            }
            return list.ToArray();
        }

        [Test]
        public void EveryAuthoredPersonality_LandsToACompetentHand()
        {
            var defs = AuthoredDefs();
            Assert.IsNotEmpty(defs, "at least the template personality ships");
            foreach ((string path, RodFightDef def) in defs)
                foreach (int seed in Seeds)
                {
                    var sim = new RodFightSim(def, new System.Random(seed));
                    Assert.AreEqual(FishFightResult.Landed, RodFightPolicies.PlayCompetent(sim),
                        $"{path} (seed {seed}): the daily rod loop must stay forgiving — a competent " +
                        "pulse-and-steer must land every authored personality");
                }
        }

        [Test]
        public void EveryAuthoredPersonality_WinsGroundFasterThanSheCanTakeItBack()
        {
            // Invariant 3 (the guard the give-back needs): one clean slack-window reel must win more than
            // she can ever drag back, or the fight ratchets nowhere and the fish is unlandable. Judged at
            // the WORST jitter roll — a species that only holds at its nominal slack length is a species
            // that occasionally can't be landed.
            foreach ((string path, RodFightDef def) in AuthoredDefs())
            {
                float worstSlack = def.StaminaCadence.SlackSeconds
                                 * (1f - Mathf.Clamp01(def.StaminaCadence.Jitter01));
                Assert.IsTrue(
                    RodFightMath.ProgressOutrunsTheGiveBack(def.landingFillPerSec, worstSlack, def.maxGiveBack01),
                    $"{path}: a shortest-case slack reel wins {def.landingFillPerSec * worstSlack:0.000} but " +
                    $"she can take {def.maxGiveBack01:0.000} back — this fish can never be landed. Raise " +
                    "landingFillPerSec/SlackSeconds, or lower maxGiveBack01.");
            }
        }

        [Test]
        public void EveryAuthoredPersonality_HasAReadableCharacter()
        {
            // The species are meant to feel like different ANIMALS (owner ask 2026-07-24). That only
            // happens if their footprints actually differ — this catches a content edit that quietly
            // flattens the roster back into four identical fish.
            var defs = AuthoredDefs();
            var footprints = new System.Collections.Generic.HashSet<string>();
            foreach ((string path, RodFightDef def) in defs)
            {
                Assert.That(def.RoamRadiusScale, Is.InRange(0.15f, 2f), $"{path}: roam scale in range");
                Assert.That(def.HoldsDeep01, Is.InRange(0f, 1f), $"{path}: stubbornness in range");
                footprints.Add($"{def.MovementPattern}|{def.RoamRadiusScale:0.00}|{def.HoldsDeep01:0.00}");
            }
            Assert.GreaterOrEqual(footprints.Count, defs.Length - 1,
                "the authored personalities must not collapse onto one footprint — at most the template " +
                "may duplicate a species (pattern × roam × stubbornness should be distinct per fish)");
        }

        [Test]
        public void EveryAuthoredPersonality_SnapsABlindPin()
        {
            foreach ((string path, RodFightDef def) in AuthoredDefs())
                foreach (int seed in Seeds)
                {
                    var sim = new RodFightSim(def, new System.Random(seed));
                    Assert.AreEqual(FishFightResult.Snapped, RodFightPolicies.PlayBlindPin(sim),
                        $"{path} (seed {seed}): holding the reel to the wall must part the line — " +
                        "no authored tuning may make the pin a winning strategy");
                }
        }
    }
}
