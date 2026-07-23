using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2, Wave 1 — the shared contracts (design/rod-fishing-v2-brainstorm.md §5, §6, §8).
    /// These tests pin the CONTRACT other Wave 1/2/3 agents build against, so a later edit that would
    /// break a sibling agent fails here first:
    ///  • the FishingPhase wire format is append-only — the VS-13 ints are frozen, v2 appends at 8+;
    ///  • the FishingState struct grew additively — the legacy 7-arg constructor still compiles and the
    ///    new diegetic reads default neutral, so no existing consumer's meaning shifts;
    ///  • RodFightDef's defaults and the shipped template asset satisfy the forgiving-cove invariants
    ///    the parallel RodFightMath relies on;
    ///  • the species opt-in seam defaults to the legacy fight (no Def = no behaviour change);
    ///  • LureTag is a well-formed flags vocabulary (defined-only, not yet wired — §6.2).
    /// </summary>
    public class FishingV2ContractTests
    {
        // ---- FishingPhase: append-only wire format ------------------------------------------

        [Test]
        public void FishingPhase_LegacyValues_KeepTheirSerializedInts()
        {
            // VS-13 shipped these at 0..7. They are serialized ints — renumbering breaks every scene,
            // asset, and save that stored one. FROZEN.
            Assert.AreEqual(0, (int)FishingPhase.Idle);
            Assert.AreEqual(1, (int)FishingPhase.Waiting);
            Assert.AreEqual(2, (int)FishingPhase.Bite);
            Assert.AreEqual(3, (int)FishingPhase.Fighting);
            Assert.AreEqual(4, (int)FishingPhase.Tending);
            Assert.AreEqual(5, (int)FishingPhase.Landed);
            Assert.AreEqual(6, (int)FishingPhase.Snapped);
            Assert.AreEqual(7, (int)FishingPhase.NoBite);
        }

        [Test]
        public void FishingPhase_V2Values_AreAppendedAfterTheLegacySet()
        {
            // v2 grows the set by APPENDING (8+) — never by renumbering (CLAUDE.md rule 2's append-only
            // discipline applied to the enum wire format).
            Assert.AreEqual(8, (int)FishingPhase.WindBack);
            Assert.AreEqual(9, (int)FishingPhase.Cast);
            Assert.AreEqual(10, (int)FishingPhase.Sinking);
            Assert.AreEqual(11, (int)FishingPhase.FightDeep);
            Assert.AreEqual(12, (int)FishingPhase.FightSurface);
        }

        [Test]
        public void FishingPhase_AllValues_RoundTripThroughInt()
        {
            foreach (FishingPhase p in Enum.GetValues(typeof(FishingPhase)))
                Assert.AreEqual(p, (FishingPhase)(int)p, $"{p} must survive an int round-trip");
        }

        [Test]
        public void FishingPhase_HasNoDuplicateInts()
        {
            var values = (FishingPhase[])Enum.GetValues(typeof(FishingPhase));
            var seen = new HashSet<int>();
            foreach (var p in values)
                Assert.IsTrue(seen.Add((int)p), $"duplicate underlying int for {p} — an alias would " +
                    "make serialized data ambiguous; the set must stay one-value-one-int");
        }

        // ---- FishingState: additive struct growth -------------------------------------------

        [Test]
        public void FishingState_LegacyConstructor_DefaultsV2ReadsNeutral()
        {
            // The 7-arg VS-13 constructor is the compatibility contract: every existing caller keeps
            // compiling AND the new reads default neutral, so legacy publishers can't accidentally
            // signal a depth game or a slack window.
            var s = new FishingState(FishingPhase.Fighting, 0.4f, 0.6f,
                                     "fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 3f);
            Assert.AreEqual(0f, s.Depth01, "legacy fight has no depth game");
            Assert.IsFalse(s.SlackWindowOpen, "legacy fight has no slack-window tell");
            Assert.AreEqual(0f, s.RodBend01, "legacy fight has no rod-bend read");
            // …and the seven original fields keep their meaning untouched.
            Assert.AreEqual(FishingPhase.Fighting, s.Phase);
            Assert.AreEqual(0.4f, s.Tension01);
            Assert.AreEqual(0.6f, s.Landing01);
            Assert.AreEqual("fish.atlantic_cod", s.FishId);
            Assert.AreEqual("Atlantic Cod", s.DisplayName);
            Assert.AreEqual(3f, s.WeightKg);
        }

        [Test]
        public void FishingState_FullConstructor_CarriesTheV2Reads()
        {
            var s = new FishingState(FishingPhase.FightDeep, 0.2f, 0.1f,
                                     "fish.halibut", "Halibut", FishCategory.Deepwater, 40f,
                                     depth01: 0.85f, slackWindowOpen: true, rodBend01: 0.7f);
            Assert.AreEqual(0.85f, s.Depth01);
            Assert.IsTrue(s.SlackWindowOpen);
            Assert.AreEqual(0.7f, s.RodBend01);
        }

        [Test]
        public void FishingState_Wave2Constructor_DefaultsTheFishOffsetNeutral()
        {
            // Wave 3 grew the struct again (the line's far end — fish offset). The Wave-2 10-arg
            // constructor is preserved and defaults the pair to (0,0), so no Wave-1/2 publisher can
            // accidentally signal a moving line.
            var s = new FishingState(FishingPhase.Fighting, 0.4f, 0.6f,
                                     "fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 3f,
                                     depth01: 0.2f, slackWindowOpen: false, rodBend01: 0.1f);
            Assert.AreEqual(0f, s.FishOffsetX, "legacy publishers carry no fish offset");
            Assert.AreEqual(0f, s.FishOffsetY, "legacy publishers carry no fish offset");
        }

        [Test]
        public void FishingState_Wave3Constructor_CarriesTheFishOffset()
        {
            var s = new FishingState(FishingPhase.FightSurface, 0.2f, 0.7f,
                                     "fish.mackerel", "Mackerel", FishCategory.Pelagic, 0.8f,
                                     depth01: 0f, slackWindowOpen: true, rodBend01: 0.4f,
                                     fishOffsetX: 1.5f, fishOffsetY: -2.25f);
            Assert.AreEqual(1.5f, s.FishOffsetX);
            Assert.AreEqual(-2.25f, s.FishOffsetY);
        }

        [Test]
        public void FishingState_Idle_IsInactive_AndNeutral()
        {
            var idle = FishingState.Idle;
            Assert.IsFalse(idle.IsActive);
            Assert.IsFalse(idle.IsFightPhase);
            Assert.AreEqual(0f, idle.Depth01);
            Assert.IsFalse(idle.SlackWindowOpen);
            Assert.AreEqual(0f, idle.RodBend01);
        }

        [Test]
        public void FishingState_IsFightPhase_GroupsExactlyTheFightPhases()
        {
            // The grouping helper consumers should use instead of re-listing phases (so audio/UI keep
            // working as the phase set grows). Legacy Fighting, both v2 fight phases, and Tending are
            // "a fight"; every other phase — including the new cast/sink beats — is not.
            var fight = new[] { FishingPhase.Fighting, FishingPhase.FightDeep,
                                FishingPhase.FightSurface, FishingPhase.Tending };
            foreach (FishingPhase p in Enum.GetValues(typeof(FishingPhase)))
            {
                var s = new FishingState(p, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f);
                bool expected = Array.IndexOf(fight, p) >= 0;
                Assert.AreEqual(expected, s.IsFightPhase, $"IsFightPhase({p})");
            }
        }

        // ---- RodFightDef: defaults + the forgiving-cove invariants --------------------------

        private static void AssertForgivingInvariants(RodFightDef def, string label)
        {
            // The two invariants RodFightMath relies on (the FishFightTuning pair carried into v2):
            // you can never just pin to win, and easing always nets tension down even mid-run.
            Assert.Greater(def.tensionRisePerSec, def.landingFillPerSec,
                $"{label}: tensionRisePerSec must exceed landingFillPerSec — a sustained reel must snap " +
                "before it lands (pulse, don't pin)");
            Assert.Less(def.runTensionPressure, def.tensionFallPerSec,
                $"{label}: runTensionPressure must stay below tensionFallPerSec — easing must ALWAYS " +
                "recover tension, even mid-run (a run is a tell, never an unavoidable snap)");
            // Wave 3: the fight runs the STRENGTH-SCALED pressure (RodFightStrength — the Def's
            // "Strength scales the run pressure" tooltip made true), so the maintain-recovers invariant
            // must hold at the EFFECTIVE value, not just the raw field.
            Assert.Less(RodFightStrength.EffectiveRunPressure(def.runTensionPressure, def.Strength),
                def.tensionFallPerSec,
                $"{label}: the Strength-scaled run pressure must STILL stay below tensionFallPerSec — " +
                "author strong fish with headroom (eff = runTensionPressure × 2·Strength)");
            Assert.GreaterOrEqual(def.surfaceThreshold01, 0f, $"{label}: surfaceThreshold01 below 0");
            Assert.LessOrEqual(def.surfaceThreshold01, 1f, $"{label}: surfaceThreshold01 above 1");
            Assert.GreaterOrEqual(def.Strength, 0f, $"{label}: Strength below 0");
            Assert.LessOrEqual(def.Strength, 1f, $"{label}: Strength above 1");
            Assert.Greater(def.StaminaCadence.RunSeconds, 0f, $"{label}: RunSeconds must be positive");
            Assert.Greater(def.StaminaCadence.SlackSeconds, 0f, $"{label}: SlackSeconds must be positive");
        }

        [Test]
        public void RodFightDef_CodeDefaults_SatisfyTheForgivingInvariants()
        {
            // A freshly created Def (Create ▸ Hidden Harbours ▸ Rod Fight) must be born valid, so a
            // content agent starting from defaults can only break the invariants deliberately.
            var def = ScriptableObject.CreateInstance<RodFightDef>();
            try { AssertForgivingInvariants(def, "code defaults"); }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void RodFightDef_TemplateAsset_Loads_AndIsWellFormed()
        {
            // The ONE shipped template (the per-species roster is Wave 3). Guards the id convention
            // (type.snake_case) and that the serialized values still satisfy the invariants.
            var def = AssetDatabase.LoadAssetAtPath<RodFightDef>(
                "Assets/_Project/Data/RodFights/RodFightTemplate.asset");
            Assert.IsNotNull(def, "the Wave-1 template RodFightDef asset must exist for tests/wiring");
            Assert.AreEqual("rodfight.template", def.Id, "stable, append-only id (rule 2)");
            StringAssert.StartsWith("rodfight.", def.Id, "RodFightDef ids are namespaced 'rodfight.*'");
            Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName));
            AssertForgivingInvariants(def, "template asset");
        }

        [Test]
        public void RodFightDefs_InData_HaveUniqueWellFormedIds()
        {
            // Content-validation shape for the Wave 3 roster: as species Defs land, every one must have
            // a non-empty, unique, namespaced id and satisfy the fight invariants.
            var seen = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:RodFightDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<RodFightDef>(path);
                if (def == null) continue;
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Id), $"{path}: empty id");
                StringAssert.StartsWith("rodfight.", def.Id, $"{path}: id must be namespaced 'rodfight.*'");
                Assert.IsFalse(seen.ContainsKey(def.Id),
                    $"duplicate RodFightDef id '{def.Id}' in '{path}' and " +
                    $"'{(seen.TryGetValue(def.Id, out var other) ? other : "?")}'");
                seen[def.Id] = path;
                AssertForgivingInvariants(def, path);
            }
            Assert.IsNotEmpty(seen, "at least the Wave-1 template must ship");
        }

        // ---- the species opt-in seam ---------------------------------------------------------

        [Test]
        public void FishSpecies_RodFight_DefaultsToNull_TheLegacyFight()
        {
            // The opt-in contract (TrapDef→DeckWorkDef pattern): a fresh species has NO RodFightDef, so
            // every existing species asset (which serialized before the field existed) deserializes to
            // null and keeps the simple/legacy fight — zero behaviour change from this PR.
            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            try { Assert.IsNull(fish.RodFight, "no Def = the simple/legacy fight"); }
            finally { UnityEngine.Object.DestroyImmediate(fish); }
        }

        [Test]
        public void ShippedFishSpecies_RodFightReferences_AreValidAuthoredDefs()
        {
            // Wave 3: the roster is live. (This replaces Wave 1's "no species may opt in yet"
            // sequencing guard — the fight logic it waited for has landed.) A species is free to stay
            // legacy (null), but every reference it DOES carry must be a well-formed, invariant-
            // satisfying authored Def — a broken reference would silently drop the fish to the legacy
            // fight in play while promising a personality in data.
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                if (fish == null || fish.RodFight == null) continue;
                StringAssert.StartsWith("rodfight.", fish.RodFight.Id,
                    $"{path}: references a Def with a malformed id");
                AssertForgivingInvariants(fish.RodFight, $"{path} → {fish.RodFight.Id}");
            }
        }

        [Test]
        public void StarterRoster_WearsTheAuthoredPersonalities()
        {
            // The owner's Wave-3 roster (design §5 made data): cod bulldogs, haddock circles,
            // mackerel darts. Pollock's personality (rodfight.pollock, Thrasher) is authored and
            // data-ready but its SPECIES asset is economy-sim's to add — so it is asserted as a Def
            // only, unreferenced until that lane ships the fish.
            var wired = new (string path, string fightId, RodFightMovement move)[]
            {
                ("Assets/_Project/Data/Fish/AtlanticCod.asset", "rodfight.atlantic_cod", RodFightMovement.Bulldog),
                ("Assets/_Project/Data/Fish/Haddock.asset", "rodfight.haddock", RodFightMovement.Circler),
                ("Assets/_Project/Data/Fish/Mackerel.asset", "rodfight.mackerel", RodFightMovement.Darter),
            };
            foreach ((string path, string fightId, RodFightMovement move) in wired)
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
                Assert.IsNotNull(fish, $"{path}: the starter species must exist");
                Assert.IsNotNull(fish.RodFight, $"{path}: the starter species must opt into its personality");
                Assert.AreEqual(fightId, fish.RodFight.Id, $"{path}: wired to the wrong personality");
                Assert.AreEqual(move, fish.RodFight.MovementPattern, $"{fightId}: the authored pattern");
            }

            var pollock = AssetDatabase.LoadAssetAtPath<RodFightDef>(
                "Assets/_Project/Data/RodFights/PollockFight.asset");
            Assert.IsNotNull(pollock, "rodfight.pollock ships data-ready for economy-sim's Pollock species");
            Assert.AreEqual("rodfight.pollock", pollock.Id);
            Assert.AreEqual(RodFightMovement.Thrasher, pollock.MovementPattern);
        }

        // ---- LureTag: a well-formed flags vocabulary (defined, not yet wired — §6.2) --------

        [Test]
        public void LureTag_IsFlags_WithDistinctSingleBits_AndNoneZero()
        {
            Assert.IsNotNull(Attribute.GetCustomAttribute(typeof(LureTag), typeof(FlagsAttribute)),
                "LureTag must be [Flags] — a species will carry a favored-lure MASK (the Gear pattern)");
            Assert.AreEqual(0, (int)LureTag.None, "None must be 0 (the 'nothing tied on' default)");

            int seenMask = 0;
            foreach (LureTag t in Enum.GetValues(typeof(LureTag)))
            {
                if (t == LureTag.None) continue;
                int bits = (int)t;
                Assert.AreEqual(1, CountBits(bits), $"{t} must be a single bit (a primitive tag, not a combo)");
                Assert.AreEqual(0, seenMask & bits, $"{t} overlaps another tag's bit");
                seenMask |= bits;
            }
        }

        [Test]
        public void LureTag_WireFormat_IsPinned()
        {
            // Append-only: these bits are the Wave-1 vocabulary; later tags append at 1<<5 and up.
            Assert.AreEqual(1 << 0, (int)LureTag.Spoon);
            Assert.AreEqual(1 << 1, (int)LureTag.Plug);
            Assert.AreEqual(1 << 2, (int)LureTag.SoftBait);
            Assert.AreEqual(1 << 3, (int)LureTag.Feather);
            Assert.AreEqual(1 << 4, (int)LureTag.Spinner);
        }

        private static int CountBits(int v)
        {
            int n = 0;
            while (v != 0) { v &= v - 1; n++; }
            return n;
        }
    }
}
