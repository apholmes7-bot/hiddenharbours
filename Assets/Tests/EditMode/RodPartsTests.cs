using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>A rod in the hand for the parts tests: a whole rod, or one made from parts.</summary>
    internal sealed class RodPartsFakeRod : ICarriable, IRodFishingEquipment, IRodPartsEquipment
    {
        public string DefId { get; set; }
        public bool IsCarriable => true;
        public Transform Transform => null;
        public int BakedFacings => 0;
        public void ShowFacing(int facingIndex) { }
        public void RideSortingBand(int sortingLayerId, int sortingOrder) { }
        public void OnLifted(ICarrier carrier) { }
        public void OnPlaced() { }

        public bool IsFishingRod => true;
        public RodFishingProfile RodFishing { get; set; } = RodFishingProfile.Default;

        public string RodKey => DefId;
        public RodBlankDef RodBlank { get; set; }
        public FishingLineDef DefaultLine { get; set; }
        public FishingReelDef DefaultReel { get; set; }

        /// <summary>The shipped tool as it is held: its whole profile and its pre-fitted parts.</summary>
        public static RodPartsFakeRod From(ToolDef tool) => new RodPartsFakeRod
        {
            DefId = tool.Id, RodFishing = tool.RodFishing,
            RodBlank = tool.RodBlank, DefaultLine = tool.DefaultLine, DefaultReel = tool.DefaultReel,
        };
    }

    internal sealed class RodPartsFakeHands : ICarrier
    {
        public ICarriable Carried { get; set; }
        public bool IsCarrying => Carried != null;
    }

    /// <summary>
    /// Rods from parts (owner, 2026-11-25): the rod sets the reach, the line the strength and which small
    /// fish it puts off, the reel the reel speed; today's rods are pre-fitted sets that fish exactly as
    /// shipped; each player's fit is his own.
    /// </summary>
    public class RodPartsTests
    {
        private const string SkillPath = "Assets/_Project/Data/Resources/RodSkill/RodSkill.asset";
        private static readonly string[] WholeRods = { "Rod", "RodCoast", "RodDeep" };

        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
            GameServices.RodFittings = null;
            GameServices.RodSkills = null;
        }

        private static ToolDef Tool(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<ToolDef>($"Assets/_Project/Data/Tools/{name}.asset");
            Assert.NotNull(t, name);
            return t;
        }

        private static List<T> All<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (string g in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static T Part<T>(string id) where T : ScriptableObject
        {
            foreach (T p in All<T>())
            {
                string pid = p is RodBlankDef r ? r.Id : p is FishingLineDef l ? l.Id : p is FishingReelDef e ? e.Id : null;
                if (pid == id) return p;
            }
            Assert.Fail($"no shipped part {id}");
            return null;
        }

        internal static void AssertBitForBit(RodFishingProfile expected, RodFishingProfile actual, string what)
        {
            foreach (FieldInfo f in typeof(RodFishingProfile).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                float e = (float)f.GetValue(expected);
                float a = (float)f.GetValue(actual);
                Assert.AreEqual(BitConverter.SingleToInt32Bits(e), BitConverter.SingleToInt32Bits(a),
                    $"{what}: {f.Name} {e} vs {a}");
            }
        }

        private FishSpeciesDef Fish(string id, float maxKg, float spawnWeight = 1f)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _made.Add(f);
            f.Id = id;
            f.AllowedGear = Gear.Handline;
            f.MinTide = -10f; f.MaxTide = 10f;
            f.StartHour = 0f; f.EndHour = 24f;
            f.Seasons = SeasonMask.AllYear;
            f.SpawnWeight = spawnWeight;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.MinWeightKg = maxKg * 0.25f; f.MaxWeightKg = maxKg;
            return f;
        }

        private static CatchContext Ctx()
            => new CatchContext("region.coddle_cove", 0f, 12f, Season.HighSummer, Gear.Handline);

        private static RodFishingProfile Fitted(ToolDef tool, FishingLineDef line = null, FishingReelDef reel = null)
            => RodFit.Compose(tool.RodBlank, line ?? tool.DefaultLine, reel ?? tool.DefaultReel);

        // ---- the parts ------------------------------------------------------------------------------

        [Test]
        public void ShippedParts_AreSound_WithUniqueIds_UnderTheirPrefixes()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            List<RodBlankDef> rods = All<RodBlankDef>();
            List<FishingLineDef> lines = All<FishingLineDef>();
            List<FishingReelDef> reels = All<FishingReelDef>();
            Assert.GreaterOrEqual(rods.Count, 3);
            Assert.GreaterOrEqual(lines.Count, 4);
            Assert.GreaterOrEqual(reels.Count, 3);

            foreach (RodBlankDef r in rods) { Assert.IsNull(r.Problem(), r.name); Assert.IsTrue(ids.Add(r.Id), r.Id); }
            foreach (FishingLineDef l in lines) { Assert.IsNull(l.Problem(), l.name); Assert.IsTrue(ids.Add(l.Id), l.Id); }
            foreach (FishingReelDef e in reels) { Assert.IsNull(e.Problem(), e.name); Assert.IsTrue(ids.Add(e.Id), e.Id); }
        }

        [Test]
        public void EveryWholeRod_IsAPreFittedSet_ThatGivesTheShippedProfileBitForBit()
        {
            foreach (string name in WholeRods)
            {
                ToolDef tool = Tool(name);
                Assert.NotNull(tool.RodBlank, $"{name} rod part");
                Assert.NotNull(tool.DefaultLine, $"{name} line");
                Assert.NotNull(tool.DefaultReel, $"{name} reel");

                RodFishingProfile composed = Fitted(tool);
                Assert.IsTrue(composed.IsValid, name);
                AssertBitForBit(tool.RodFishing, composed, name);
                AssertBitForBit(tool.RodFishing, RodSkillMath.Apply(in composed, RodSkillBonus.None), name + " at skill zero");
                Assert.IsFalse(composed.PutsSmallFishOff, $"{name}'s own line puts no fish off");
            }
        }

        [Test]
        public void HeldRod_WithNoFit_FishesExactlyAsTheWholeRod_AndAWholeRodWithoutPartsIsUnchanged()
        {
            var session = new object();
            var book = new RodFittingBook(() => session);
            IRodFitting none = book.For("player.one");
            foreach (string name in WholeRods)
            {
                ToolDef tool = Tool(name);
                var hands = new RodPartsFakeHands { Carried = RodPartsFakeRod.From(tool) };
                Assert.IsTrue(RodFit.TryReadHeld(hands, none, out RodFishingProfile held), name);
                AssertBitForBit(tool.RodFishing, held, name);

                var whole = RodPartsFakeRod.From(tool);
                whole.RodBlank = null;
                hands.Carried = whole;
                Assert.IsTrue(RodFit.TryReadHeld(hands, none, out RodFishingProfile alone), name);
                AssertBitForBit(tool.RodFishing, alone, name + " without parts");
            }

            Assert.IsFalse(RodFit.TryReadHeld(new RodPartsFakeHands(), none, out _), "empty hands hold no rod");
        }

        [Test]
        public void EffectiveNumbers_AreTheFittedPartsPlusTheSkill()
        {
            var skillDef = AssetDatabase.LoadAssetAtPath<RodSkillDef>(SkillPath);
            Assert.NotNull(skillDef);
            RodSkillBonus top = RodSkillMath.BonusAt(skillDef.MaxLevel, skillDef);

            RodBlankDef rod = Part<RodBlankDef>("rod.hand");
            FishingLineDef braid = Part<FishingLineDef>("fishing_line.heavy_braid");
            FishingReelDef reel = Part<FishingReelDef>("reel.deep");
            RodFishingProfile fitted = RodFit.Compose(rod, braid, reel);
            RodFishingProfile effective = RodSkillMath.Apply(in fitted, in top);

            Assert.AreEqual(rod.CastMetres + top.CastMetres, effective.CastMetres, 1e-5f, "reach = rod + skill");
            Assert.AreEqual(braid.Strength + top.LineStrength, effective.LineStrengthMultiplier, 1e-5f, "strength = line + skill");
            Assert.AreEqual(reel.ReelUpMetresPerSecond + top.ReelUpMetresPerSecond, effective.ReelUpMetresPerSecond, 1e-5f,
                "reel speed = reel + skill");
            Assert.AreEqual(rod.LandingMultiplier + braid.FightEase + reel.FightEase + top.FightHelp,
                effective.LandingMultiplier, 1e-5f, "fight help = rod's lift + gear ease + skill");
            Assert.AreEqual(braid.LineMetres, effective.LineMetres, "length is the line's");
            Assert.AreEqual(braid.ShyBelowKg, effective.SmallFishShyBelowKg, "the line's shyness rides the profile");
        }

        [Test]
        public void GearFightEase_StaysWellUnderTheSkillsFightHelp()
        {
            var skillDef = AssetDatabase.LoadAssetAtPath<RodSkillDef>(SkillPath);
            float skillTop = RodSkillMath.BonusAt(skillDef.MaxLevel, skillDef).FightHelp;
            float bestLine = 0f, bestReel = 0f;
            foreach (FishingLineDef l in All<FishingLineDef>()) bestLine = Mathf.Max(bestLine, l.FightEase);
            foreach (FishingReelDef r in All<FishingReelDef>()) bestReel = Mathf.Max(bestReel, r.FightEase);
            Assert.Less(bestLine + bestReel, skillTop * 0.5f, "the gear's ease is clearly smaller than the skill's");
        }

        // ---- the line's small-fish rule -------------------------------------------------------------

        [Test]
        public void ShyFactor_FollowsTheRule()
        {
            Assert.AreEqual(1f, RodFit.ShyFactor(0f, 0.35f, 0.1f), "no mark: nothing put off");
            Assert.AreEqual(1f, RodFit.ShyFactor(2f, 1f, 0.1f), "damp 1: nothing put off");
            Assert.AreEqual(1f, RodFit.ShyFactor(2f, 0.35f, 2f), "at the mark: not put off");
            Assert.AreEqual(1f, RodFit.ShyFactor(2f, 0.35f, 12f), "over the mark: not put off");
            Assert.AreEqual(0.35f + 0.65f * 0.05f, RodFit.ShyFactor(2f, 0.35f, 0.1f), 1e-6f, "smelt on braid");
            Assert.AreEqual(0.35f, RodFit.ShyFactor(2f, 0.35f, 0f), 1e-6f, "the smallest at the damp");

            float last = 0f;
            for (float kg = 0f; kg <= 3f; kg += 0.05f)
            {
                float s = RodFit.ShyFactor(2f, 0.35f, kg);
                Assert.GreaterOrEqual(s, last, $"bigger fish never more put off ({kg} kg)");
                last = s;
            }
        }

        [Test]
        public void ALineThatPutsNothingOff_IsTheResolversOwnRoll_SeedForSeed()
        {
            var pool = new List<FishSpeciesDef>
            {
                Fish("fish.p_smelt", 0.1f, 2f), Fish("fish.p_cunner", 0.6f, 1f),
                Fish("fish.p_cod", 12f, 3f), Fish("fish.p_bass", 25f, 0.5f),
            };
            CatchContext ctx = Ctx();
            RodFishingProfile plain = Fitted(Tool("RodDeep"));
            RodFishingProfile overMark = plain;
            overMark.SmallFishShyBelowKg = 0.05f;   // every fish here is over it
            overMark.SmallFishShyDamp01 = 0.2f;

            for (int seed = 0; seed < 2000; seed++)
            {
                FishSpeciesDef expected = CatchResolver.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, new System.Random(seed));
                Assert.AreSame(expected, LineShyCatch.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, in plain, new System.Random(seed)), $"seed {seed}");
                Assert.AreSame(expected, LineShyCatch.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, in overMark, new System.Random(seed)), $"seed {seed}, over the mark");
            }
        }

        [Test]
        public void AHeavyLine_PutsSmallFishOff_AndNeverGivesABigFishMore()
        {
            FishSpeciesDef smelt = Fish("fish.p_smelt", 0.1f);
            FishSpeciesDef cod = Fish("fish.p_cod", 12f);
            var pool = new List<FishSpeciesDef> { smelt, cod };
            CatchContext ctx = Ctx();
            RodFishingProfile plain = Fitted(Tool("RodDeep"));
            RodFishingProfile braid = Fitted(Tool("RodDeep"), Part<FishingLineDef>("fishing_line.heavy_braid"));
            Assert.IsTrue(braid.PutsSmallFishOff);

            int smeltPlain = 0, smeltBraid = 0, codPlain = 0, codBraid = 0;
            for (int seed = 0; seed < 4000; seed++)
            {
                FishSpeciesDef a = LineShyCatch.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, in plain, new System.Random(seed));
                FishSpeciesDef b = LineShyCatch.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, in braid, new System.Random(seed));
                FishSpeciesDef again = LineShyCatch.Resolve(pool, in ctx, DepthDropSettings.Default,
                    BaitTackleSettings.Default, in braid, new System.Random(seed));
                Assert.AreSame(b, again, $"seed {seed} replays");

                if (a == cod) Assert.AreSame(cod, b, $"seed {seed}: the cod bites on either line");
                if (a == smelt) Assert.IsTrue(b == smelt || b == null, $"seed {seed}: the smelt bites or is put off");
                if (a == smelt) smeltPlain++;
                if (b == smelt) smeltBraid++;
                if (a == cod) codPlain++;
                if (b == cod) codBraid++;
            }

            Assert.AreEqual(codPlain, codBraid, "no big fish gained or lost");
            float expected = RodFit.ShyFactor(in braid, smelt.MaxWeightKg);
            Assert.AreEqual(expected, smeltBraid / (float)smeltPlain, 0.05f, "the smelt bites at its shy rate");
        }

        // ---- the fit is per player ------------------------------------------------------------------

        [Test]
        public void OnePlayersFit_NeverChangesAnothers_NorHisOtherRods()
        {
            var session = new object();
            GameServices.RodFittings = new RodFittingBook(() => session);
            ToolDef deep = Tool("RodDeep");
            ToolDef coast = Tool("RodCoast");
            FishingLineDef braid = Part<FishingLineDef>("fishing_line.heavy_braid");

            IRodFitting one = GameServices.RodFittings.For("player.one");
            IRodFitting two = GameServices.RodFittings.For("player.two");
            Assert.IsTrue(one.Fit(deep.Id, braid, null));

            var deepHeld = new RodPartsFakeHands { Carried = RodPartsFakeRod.From(deep) };
            var coastHeld = new RodPartsFakeHands { Carried = RodPartsFakeRod.From(coast) };

            Assert.IsTrue(RodFit.TryReadHeld(deepHeld, one, out RodFishingProfile oneDeep));
            Assert.AreEqual(braid.Strength, oneDeep.LineStrengthMultiplier, "player one's deep rod has his braid");
            Assert.AreEqual(deep.DefaultReel.ReelUpMetresPerSecond, oneDeep.ReelUpMetresPerSecond, "his reel is the rod's own");

            Assert.IsFalse(two.TryGetFit(deep.Id, out _, out _), "player two has fitted nothing");
            Assert.IsTrue(RodFit.TryReadHeld(deepHeld, two, out RodFishingProfile twoDeep));
            AssertBitForBit(deep.RodFishing, twoDeep, "player two's deep rod");

            Assert.IsTrue(RodFit.TryReadHeld(coastHeld, one, out RodFishingProfile oneCoast));
            AssertBitForBit(coast.RodFishing, oneCoast, "player one's coast rod");

            Assert.IsTrue(two.Fit(deep.Id, null, Part<FishingReelDef>("reel.hand")));
            Assert.IsTrue(RodFit.TryReadHeld(deepHeld, one, out RodFishingProfile oneAgain));
            AssertBitForBit(oneDeep, oneAgain, "player two's fit left player one's alone");
        }

        [Test]
        public void ClearingAFit_PutsTheRodBackOnItsOwnParts_AndAnnouncesEachChange()
        {
            var session = new object();
            var book = new RodFittingBook(() => session);
            IRodFitting one = book.For("player.one");
            ToolDef deep = Tool("RodDeep");
            var changes = new List<RodFitChanged>();
            Action<RodFitChanged> heard = e => changes.Add(e);
            EventBus.Subscribe(heard);
            try
            {
                Assert.IsTrue(one.Fit(deep.Id, Part<FishingLineDef>("fishing_line.light"), null));
                Assert.IsTrue(one.ClearFit(deep.Id));
                Assert.IsFalse(one.ClearFit(deep.Id), "nothing left to clear");
                Assert.IsTrue(one.Fit(deep.Id, Part<FishingLineDef>("fishing_line.light"), null));
                Assert.IsTrue(one.Fit(deep.Id, null, null), "both parts back to default clears the fit");
                Assert.IsFalse(one.TryGetFit(deep.Id, out _, out _));
            }
            finally { EventBus.Unsubscribe(heard); }

            Assert.AreEqual(4, changes.Count);
            foreach (RodFitChanged c in changes) { Assert.AreEqual("player.one", c.PlayerKey); Assert.AreEqual(deep.Id, c.RodKey); }
            var held = new RodPartsFakeHands { Carried = RodPartsFakeRod.From(deep) };
            Assert.IsTrue(RodFit.TryReadHeld(held, one, out RodFishingProfile back));
            AssertBitForBit(deep.RodFishing, back, "cleared");
        }

        [Test]
        public void AFit_RefusesAnUnsoundPart_AndNothingFitsWithNoGame()
        {
            var session = new object();
            var book = new RodFittingBook(() => session);
            IRodFitting one = book.For("player.one");
            var bad = ScriptableObject.CreateInstance<FishingLineDef>();
            _made.Add(bad);
            bad.Id = "line.bread";   // the store's stock-line id space, not a fishing line
            Assert.IsFalse(one.Fit("tool.rod", bad, null));
            Assert.IsFalse(one.TryGetFit("tool.rod", out _, out _));

            var noGame = new RodFittingBook(() => null);
            Assert.IsFalse(noGame.For("player.one").Fit("tool.rod", Part<FishingLineDef>("fishing_line.heavy"), null));
        }

        [Test]
        public void EachGame_StartsWithEveryRodOnItsOwnParts_AndTheSaveFormatIsUntouched()
        {
            var game = new SaveData();
            object session = game;
            var book = new RodFittingBook(() => session);
            Assert.IsTrue(book.For("player.one").Fit("tool.rod_deep", Part<FishingLineDef>("fishing_line.heavy_braid"), null));
            session = new SaveData();
            Assert.IsFalse(book.For("player.one").TryGetFit("tool.rod_deep", out _, out _), "a new game starts unfitted");

            // Save v27 has rows for fitted parts, but nothing writes them until a later change wires the
            // book to them: fitting a rod leaves the save as it was.
            Assert.IsEmpty(game.RodFittings, "nothing about the fit is saved yet");
        }

        [Test]
        public void GameServices_RodFittings_IsNeverNull_AndResetsToTheDefaultParts()
        {
            GameServices.RodFittings = null;
            Assert.NotNull(GameServices.RodFittings);
            var mine = new RodFittingBook(() => new object());
            GameServices.RodFittings = mine;
            Assert.AreSame(mine, GameServices.RodFittings);
            GameServices.Reset();
            Assert.AreNotSame(mine, GameServices.RodFittings);
        }
    }
}
