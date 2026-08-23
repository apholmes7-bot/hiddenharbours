using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ground tackle's pure rules (P1 "the sea has moods" / P5 "cozy, but with teeth") — the whole
    /// decision half of anchoring, without a scene, a physics loop or a boat:
    ///
    ///   • THE DROP GATE — she anchors iff she is afloat AND the rode reaches the bottom. Too deep finds
    ///     no bottom; the flats are aground, not an anchorage; and "open water" (no height map, infinite
    ///     depth) refuses rather than falsely claiming to hold.
    ///   • THE SWING CIRCLE — √(rode² − depth²), the one formula: spare rode is swing, and a rising tide
    ///     shrinks the circle to nothing at the exact moment the hook is about to lose the bottom.
    ///   • THE RODE IS DATA — a hull's own RodeMeters, else the shared dinghy-class default, resolved in
    ///     ONE place so gate, swing and drag can never disagree about how much line she has.
    ///   • DRAGGING — the brake only checks speed ABOVE the creep, never pushes, and is direction-blind.
    ///   • DETERMINISM — every helper is bit-stable for identical inputs (no hidden RNG, rule 5).
    ///   • THE CONFIG TRAP (#420) — GameConfig.asset must actually list every Anchor key, or Unity
    ///     deserializes the block to C# zeros and no hull can ever anchor anywhere.
    /// </summary>
    public class AnchorMathTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private BoatHullDef Hull(float rode, float draught = 0.3f)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.anchor_test";
            hull.RodeMeters = rode;
            hull.DraughtMeters = draught;
            _spawned.Add(hull);
            return hull;
        }

        // ============ Part 1: the rode is DATA (rule 2), resolved in one place ============

        [Test]
        public void RodeFor_TakesTheHullsOwnRode_WhenSheHasOne()
        {
            var settings = AnchorSettings.Default;
            Assert.AreEqual(30f, AnchorMath.RodeFor(Hull(30f), in settings),
                "a hull that authors her own rode carries exactly that — bigger vessel, deeper anchorage (P2)");
        }

        [Test]
        public void RodeFor_FallsBackToTheDinghyDefault_WhenTheHullAuthorsNone()
        {
            var settings = AnchorSettings.Default;
            // 0 is what EVERY hull asset written before RodeMeters existed deserializes to. It must mean
            // "short rode", never "no rode" — a zero here would silently make anchoring impossible.
            Assert.AreEqual(settings.DefaultRodeMeters, AnchorMath.RodeFor(Hull(0f), in settings),
                "an un-authored hull takes the shared dinghy-class rode, not a zero one");
            Assert.Greater(settings.DefaultRodeMeters, 0f,
                "the shipped fallback must be a real length or no untouched hull could ever anchor");
        }

        [Test]
        public void RodeFor_NullHull_CarriesNothing()
            => Assert.AreEqual(0f, AnchorMath.RodeFor(null, AnchorSettings.Default));

        // ============ Part 2: THE DROP GATE (depth vs rode vs draught) ============

        [Test]
        public void Drop_Sets_WhenTheRodeReachesTheBottom()
        {
            Assert.AreEqual(AnchorDrop.Set, AnchorMath.EvaluateDrop(depth: 4f, rodeMeters: 6f, draughtMeters: 0.3f));
        }

        [Test]
        public void Drop_Sets_AtExactlyRodeDepth_TheBoundaryIsInclusive()
        {
            // depth == rode is the last spot she holds: the rode is bar-taut, straight up and down.
            Assert.AreEqual(AnchorDrop.Set, AnchorMath.EvaluateDrop(6f, 6f, 0.3f));
        }

        [Test]
        public void Drop_FindsNoBottom_WhenDeeperThanTheRode()
        {
            Assert.AreEqual(AnchorDrop.NoBottom, AnchorMath.EvaluateDrop(6.01f, 6f, 0.3f),
                "one centimetre past the rode and the hook never reaches — a refusal, not a hold");
        }

        [Test]
        public void Drop_FindsNoBottom_InOpenWaterWithNoHeightMap()
        {
            // BoatCrossing.DepthAt returns +Infinity where no seabed is authored. The honest answer is
            // "no bottom to reach", the mirror of the crossing gate's "a missing map never blocks a boat".
            Assert.AreEqual(AnchorDrop.NoBottom,
                AnchorMath.EvaluateDrop(float.PositiveInfinity, 180f, 6.5f),
                "even the tanker's 180 m rode finds no bottom where the region paints no seabed");
        }

        [Test]
        public void Drop_IsAground_OnDryGround()
        {
            Assert.AreEqual(AnchorDrop.Aground, AnchorMath.EvaluateDrop(0f, 6f, 0.3f), "dead low, high and dry");
            Assert.AreEqual(AnchorDrop.Aground, AnchorMath.EvaluateDrop(-1.2f, 6f, 0.3f), "well above the water");
        }

        [Test]
        public void Drop_IsAground_WhenTheKeelIsAlreadyOnTheBottom()
        {
            // Wet, but not floating: the existing grounding sim owns her. You do not anchor aground.
            Assert.AreEqual(AnchorDrop.Aground, AnchorMath.EvaluateDrop(0.2f, 6f, 0.3f),
                "0.2 m of water under a 0.3 m draught is aground, not a shallow anchorage");
            Assert.AreEqual(AnchorDrop.Set, AnchorMath.EvaluateDrop(0.3f, 6f, 0.3f),
                "exactly her draught is the shoalest water she still floats in — the crossing gate's own boundary");
        }

        [Test]
        public void Drop_ADeepDraughtHullInShoalWater_IsAgroundNotNoBottom()
        {
            // The trawler in 2 m: the rode reaches easily, but she is on the bottom. Aground wins — the
            // order of the two tests is the behaviour, not an implementation detail.
            Assert.AreEqual(AnchorDrop.Aground, AnchorMath.EvaluateDrop(2f, 90f, 4.2f));
        }

        // ============ Part 3: THE SWING CIRCLE (one formula, √(rode² − depth²)) ============

        [Test]
        public void Swing_IsThePythagoreanScopeCircle()
        {
            // 5 m rode in 3 m of water: the vertical leg takes 3, so 4 m of horizontal swing. The floor
            // is well below that, so this measures the formula itself.
            Assert.AreEqual(4f, AnchorMath.SwingRadius(5f, 3f, minSwingMeters: 0.5f), 1e-4f);
        }

        [Test]
        public void Swing_MoreSpareRode_MeansMoreSwing()
        {
            float shallow = AnchorMath.SwingRadius(20f, 4f, 0.5f);
            float deeper = AnchorMath.SwingRadius(20f, 15f, 0.5f);
            Assert.Greater(shallow, deeper,
                "the same rode ranges further in shallow water — spare rode IS swing (the mechanic's rule)");
        }

        [Test]
        public void Swing_ShrinksAsTheTideRises_ToNothingAtTheRodesLimit()
        {
            const float rode = 6f;
            float atLowWater = AnchorMath.SwingRadius(rode, 2f, 0f);
            float atHalfTide = AnchorMath.SwingRadius(rode, 4f, 0f);
            float upAndDown = AnchorMath.SwingRadius(rode, rode, 0f);

            Assert.Greater(atLowWater, atHalfTide, "a making tide takes her swing away first…");
            Assert.AreEqual(0f, upAndDown, 1e-4f, "…and at depth == rode she is straight up and down");
            // …which is exactly the tick before the hook loses the bottom. The two rules meet cleanly.
            Assert.IsFalse(AnchorMath.HasLostBottom(rode, rode));
            Assert.IsTrue(AnchorMath.HasLostBottom(rode + 0.01f, rode));
        }

        [Test]
        public void Swing_NeverCollapsesBelowTheFloor_ABoatOnHerHookAlwaysFidgets()
        {
            const float floor = 0.75f;
            Assert.AreEqual(floor, AnchorMath.SwingRadius(6f, 6f, floor), 1e-4f, "a bar-taut rode still fidgets");
            Assert.AreEqual(floor, AnchorMath.SwingRadius(6f, 9f, floor), 1e-4f, "and so does one past its reach");
        }

        [Test]
        public void Swing_IsNeverNaN_ForAnyDepthPastTheRode()
        {
            // √ of a negative is the obvious way to write this wrong, and it would poison the rigidbody.
            for (float depth = 0f; depth <= 20f; depth += 0.37f)
                Assert.IsFalse(float.IsNaN(AnchorMath.SwingRadius(6f, depth, 0.75f)), $"depth {depth}");
        }

        // ============ Part 4: LOSING THE BOTTOM (the rising-tide failure) ============

        [Test]
        public void HasLostBottom_OnlyOnceTheWaterIsPastTheRode()
        {
            Assert.IsFalse(AnchorMath.HasLostBottom(5.9f, 6f), "still biting");
            Assert.IsFalse(AnchorMath.HasLostBottom(6f, 6f), "bar-taut, still biting");
            Assert.IsTrue(AnchorMath.HasLostBottom(6.1f, 6f), "the tide's over her rode — she drags");
        }

        [Test]
        public void HasLostBottom_OpenWaterCountsAsLost()
            => Assert.IsTrue(AnchorMath.HasLostBottom(float.PositiveInfinity, 180f));

        // ============ Part 5: THE DRAGGING BRAKE ============

        [Test]
        public void DragBrake_IsZeroBelowTheCreep_SheIsAlreadyGoingSlowly()
        {
            Assert.AreEqual(Vector2.zero,
                AnchorMath.DragBrakeForce(new Vector2(0.2f, 0f), creepSpeed: 0.35f, brakeStrength: 900f));
            Assert.AreEqual(Vector2.zero, AnchorMath.DragBrakeForce(Vector2.zero, 0.35f, 900f));
        }

        [Test]
        public void DragBrake_OpposesOnlyTheExcessSpeed()
        {
            Vector2 f = AnchorMath.DragBrakeForce(new Vector2(1.35f, 0f), creepSpeed: 0.35f, brakeStrength: 900f);
            // 1 m/s of EXCESS × 900. If the whole speed were braked this would read −1215 instead, so the
            // tolerance only has to survive float round-trips through magnitude(), not distinguish models.
            Assert.AreEqual(-900f, f.x, 0.1f, "the excess only, not the whole speed");
            Assert.AreEqual(0f, f.y, 1e-4f);
        }

        [Test]
        public void DragBrake_AlwaysOpposesMotion_AndNeverPushes()
        {
            foreach (var v in new[] { new Vector2(3f, 0f), new Vector2(-2f, 1f), new Vector2(0f, -4f) })
            {
                Vector2 f = AnchorMath.DragBrakeForce(v, 0.35f, 900f);
                Assert.Less(Vector2.Dot(f, v), 0f, $"the tackle must brake {v}, never shove it");
            }
        }

        [Test]
        public void DragBrake_IsDirectionBlind_ADraggingHookDoesNotCareWhichWayYouGo()
        {
            Vector2 east = AnchorMath.DragBrakeForce(new Vector2(2f, 0f), 0.35f, 900f);
            Vector2 north = AnchorMath.DragBrakeForce(new Vector2(0f, 2f), 0.35f, 900f);
            Assert.AreEqual(east.magnitude, north.magnitude, 1e-3f);
        }

        [Test]
        public void DragBrake_ZeroStrength_LetsHerGoEntirely()
            => Assert.AreEqual(Vector2.zero, AnchorMath.DragBrakeForce(new Vector2(5f, 0f), 0.35f, 0f));

        // ============ Part 6: determinism (rule 5 — no hidden RNG anywhere in the tackle) ============

        [Test]
        public void EveryHelper_IsBitStable_ForIdenticalInputs()
        {
            var settings = AnchorSettings.Default;
            var hull = Hull(0f);
            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(AnchorMath.RodeFor(hull, in settings), AnchorMath.RodeFor(hull, in settings));
                Assert.AreEqual(AnchorMath.EvaluateDrop(3.5f, 6f, 0.3f), AnchorMath.EvaluateDrop(3.5f, 6f, 0.3f));
                Assert.AreEqual(AnchorMath.SwingRadius(6f, 3.5f, 0.75f), AnchorMath.SwingRadius(6f, 3.5f, 0.75f));
                Assert.AreEqual(AnchorMath.DragBrakeForce(new Vector2(1.1f, -0.4f), 0.35f, 900f),
                                AnchorMath.DragBrakeForce(new Vector2(1.1f, -0.4f), 0.35f, 900f));
            }
        }

        // ============ Part 7: the shipped tuning + the GameConfig YAML trap (#420) ============

        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private static string ReadRepoFile(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        [Test]
        public void TheConfigAsset_ActuallySerializesEveryAnchorKey_TheSilentZeroTrap()
        {
            // ⚠️ #420 in its most dangerous shape. AnchorSettings is a STRUCT that GameConfig.asset
            // serializes field by field; a field the YAML omits deserializes to its C# default (0f), NOT
            // to AnchorSettings.Default. A zero DefaultRodeMeters means every un-authored hull carries no
            // rode and can never anchor — a feature that reads as simply not working.
            string yaml = ReadRepoFile(ConfigAssetPath);
            var block = Regex.Match(yaml, @"^  Anchor:\r?\n((?:    .*\r?\n)+)", RegexOptions.Multiline);
            Assert.IsTrue(block.Success, "GameConfig.asset does not serialize an Anchor block at all");

            string[] keys =
            {
                nameof(AnchorSettings.ReferenceAnchorMassKg),
                nameof(AnchorSettings.DefaultRodeMeters), nameof(AnchorSettings.MinSwingMeters),
                nameof(AnchorSettings.RodeGiveMeters), nameof(AnchorSettings.LimitStiffness),
                nameof(AnchorSettings.LimitDamping), nameof(AnchorSettings.DragCreepMetersPerSec),
                nameof(AnchorSettings.DragBrakeStrength),
            };
            foreach (string key in keys)
                Assert.IsTrue(Regex.IsMatch(block.Groups[1].Value, $@"^\s*{key}:\s*\S+\s*$", RegexOptions.Multiline),
                    $"GameConfig.asset's Anchor block does not list {key}. Unity deserializes it as 0 " +
                    $"while the code reads AnchorSettings.Default — add the key to the asset.");

            var rode = Regex.Match(block.Groups[1].Value,
                                   @"^\s*DefaultRodeMeters:\s*(\S+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(float.TryParse(rode.Groups[1].Value,
                                         System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture,
                                         out float shippedRode),
                $"DefaultRodeMeters is not a number in the asset: '{rode.Groups[1].Value}'");
            Assert.Greater(shippedRode, 0f,
                "a zero shipped rode makes anchoring impossible on every hull that authors none");

            var hook = Regex.Match(block.Groups[1].Value,
                                   @"^\s*ReferenceAnchorMassKg:\s*(\S+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(float.TryParse(hook.Groups[1].Value,
                                         System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture,
                                         out float shippedHook),
                $"ReferenceAnchorMassKg is not a number in the asset: '{hook.Groups[1].Value}'");
            Assert.Greater(shippedHook, 0f,
                "a zero reference hook takes the per-hull nuance out of the drag brake for the whole " +
                "fleet — every anchor would check identically, which is the field not working");
        }

        [Test]
        public void TheShippedDefaults_AreAWorkingAnchor()
        {
            var d = AnchorSettings.Default;
            Assert.Greater(d.ReferenceAnchorMassKg, 0f, "the reference hook is small, never absent");
            Assert.Greater(d.DefaultRodeMeters, 0f, "a dinghy-class rode is short, never absent");
            Assert.GreaterOrEqual(d.MinSwingMeters, 0f);
            Assert.Greater(d.LimitStiffness, 0f, "a rode with no stiffness would not check her at all");
            Assert.Greater(d.DragBrakeStrength, 0f, "a dragging anchor still checks her; it just doesn't hold");
            Assert.Greater(d.DragCreepMetersPerSec, 0f,
                "⚠ _confirm (owner): the speed a dragging anchor lets her make. Zero would make a lost " +
                "hook a full stop, which is the opposite of the failure this models.");
        }

        [Test]
        public void GameServices_FallsBackToTheDefaults_WithNoConfigWired()
        {
            GameConfig previous = GameServices.Config;
            try
            {
                GameServices.Config = null;
                Assert.AreEqual(AnchorSettings.Default.DefaultRodeMeters, GameServices.Anchor.DefaultRodeMeters,
                    "an unwired rig must still anchor on a dinghy-class rode, not on a zero one");
            }
            finally { GameServices.Config = previous; }
        }

        // ============ Part 7b: the HOOK — does she carry one, what does it weigh, what does it check ====

        [Test]
        public void CarriesAnchor_IsTheHullsOwnData_AndANullHullCarriesNothing()
        {
            Assert.IsFalse(AnchorMath.CarriesAnchor(null), "there is no hook on a hull that is not there");

            BoatHullDef hull = Hull(6f);
            Assert.IsTrue(hull.HasAnchor,
                "a hull built in code carries a hook by default — the owner's ruling is an anchor on " +
                "every hull, so 'inert until authored' would be the wrong way round here");
            Assert.IsTrue(AnchorMath.CarriesAnchor(hull));

            hull.HasAnchor = false;
            Assert.IsFalse(AnchorMath.CarriesAnchor(hull), "…and false is the deliberate exception");
        }

        [Test]
        public void AnchorMassFor_FallsBackToTheSharedGrapnel_ExactlyAsTheRodeDoes()
        {
            var settings = AnchorSettings.Default;

            Assert.AreEqual(30f, AnchorMath.AnchorMassFor(HullWithHook(30f), in settings), 1e-4f,
                "her own authored hook wins");

            // 0 is what a hull asset written before AnchorMassKg existed deserializes to. It must mean
            // "the shared dinghy grapnel", never "no iron at all" — the RodeMeters convention.
            Assert.AreEqual(settings.ReferenceAnchorMassKg,
                            AnchorMath.AnchorMassFor(HullWithHook(0f), in settings), 1e-4f);
            Assert.AreEqual(0f, AnchorMath.AnchorMassFor(null, in settings), 1e-4f);
        }

        [Test]
        public void DragBrake_ScalesWithTheIronSheDrags_AndLeavesAnUnauthoredHullExactlyWhereSheWas()
        {
            var settings = AnchorSettings.Default;
            float reference = settings.ReferenceAnchorMassKg;

            // The whole point of quoting the shared strength AT the reference hook: a hull that has
            // never been given her own drags byte-identically to the way she did before hulls had
            // anchor weights at all.
            Assert.AreEqual(settings.DragBrakeStrength,
                            AnchorMath.DragBrakeStrengthFor(reference, in settings), 1e-3f,
                "the reference hook must reproduce the shipped brake exactly, or this field is a " +
                "silent re-tune of the whole fleet rather than a new axis on it");

            // Twice the iron, twice the check — linear, because friction goes with the weight on it.
            Assert.AreEqual(2f * settings.DragBrakeStrength,
                            AnchorMath.DragBrakeStrengthFor(2f * reference, in settings), 1e-3f);
            Assert.Less(AnchorMath.DragBrakeStrengthFor(0.5f * reference, in settings),
                        settings.DragBrakeStrength, "a lighter grapnel skips more freely");

            // A config that omits the key (the YAML trap) costs the nuance, never the brake.
            var trapped = settings;
            trapped.ReferenceAnchorMassKg = 0f;
            Assert.AreEqual(settings.DragBrakeStrength,
                            AnchorMath.DragBrakeStrengthFor(90f, in trapped), 1e-3f,
                "no reference, no scaling — a mis-authored config must not divide by nothing");

            // …and it feeds the SAME brake maths, not a second copy of it.
            float strength = AnchorMath.DragBrakeStrengthFor(2f * reference, in settings);
            Vector2 brake = AnchorMath.DragBrakeForce(new Vector2(3f, 0f),
                                                      settings.DragCreepMetersPerSec, strength);
            Assert.Less(brake.x, 0f, "the brake still opposes her motion, whatever the hook weighs");
            Assert.AreEqual(0f, brake.y, 1e-4f);
        }

        [Test]
        public void EveryShippedHull_CarriesAHook_AndAHeavierOneUpTheLadder()
        {
            var dory = LoadHull("Dory");
            var punt = LoadHull("Punt");
            var lobster = LoadHull("LobsterBoat");
            var trawler = LoadHull("SternTrawler");

            foreach (var h in new[] { dory, punt, lobster, trawler })
            {
                Assert.IsTrue(h.HasAnchor, $"{h.Id} carries no anchor — the owner's ruling is every hull");
                Assert.Greater(h.AnchorMassKg, 0f, $"{h.Id} states no hook weight");
            }

            Assert.Less(dory.AnchorMassKg, punt.AnchorMassKg, "the punt out-anchors the dory");
            Assert.Less(punt.AnchorMassKg, lobster.AnchorMassKg, "the lobster boat out-anchors the punt");
            Assert.Less(lobster.AnchorMassKg, trawler.AnchorMassKg, "the trawler swings the most iron");
        }

        private BoatHullDef HullWithHook(float massKg)
        {
            BoatHullDef hull = Hull(6f);
            hull.AnchorMassKg = massKg;
            return hull;
        }

        // ============ Part 8: the rode is authored per hull, and grows up the ladder (P2) ============

        [Test]
        public void EveryShippedHull_CarriesARode_AndBiggerHullsCarryMore()
        {
            var dory = LoadHull("Dory");
            var punt = LoadHull("Punt");
            var lobster = LoadHull("LobsterBoat");
            var trawler = LoadHull("SternTrawler");
            var settings = AnchorSettings.Default;

            foreach (var h in new[] { dory, punt, lobster, trawler })
                Assert.Greater(AnchorMath.RodeFor(h, in settings), 0f, $"{h.Id} carries no rode at all");

            Assert.Less(dory.RodeMeters, punt.RodeMeters, "the punt out-anchors the dory");
            Assert.Less(punt.RodeMeters, lobster.RodeMeters, "the lobster boat out-anchors the punt");
            Assert.Less(lobster.RodeMeters, trawler.RodeMeters, "the trawler anchors deepest of the four");
        }

        private BoatHullDef LoadHull(string name)
        {
            var hull = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                $"Assets/_Project/Data/Boats/{name}.asset");
            Assert.IsNotNull(hull, $"missing hull asset: {name}");
            return hull;
        }
    }
}
