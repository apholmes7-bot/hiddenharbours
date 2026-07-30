using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The bait-spend flag, priced.</b> <c>GameConfig.BaitSpentOnCatchOnly</c> (#341) decides WHEN one
    /// bait leaves the box: OFF (shipped) at the BITE — something ate it, landed or not; ON at the LANDED
    /// catch — the owner's tentative §10.2 reversal. That one branch is a pacing dial, because it sets
    /// <b>bait's real cost per landed fish</b>, and this is the check `economy-sim` was asked to run
    /// before the owner flips it (`docs/design/m1-progression-pacing.md` §7 records the recommendation;
    /// every number quoted there comes out of THIS test).
    ///
    /// <para><b>What this is.</b> A seeded Monte-Carlo over the REAL <see cref="BiteSequenceSim"/> and the
    /// REAL shipped <see cref="BiteDef"/> assets, driven by two modelled hands. Nothing is re-implemented:
    /// the pass structure, the teases, the take window and the keeps-nibbling returns are the shipping
    /// sim's, so a re-tuned personality moves these numbers and the pins below go red — which is the point
    /// (the pacing doc's table must not quietly go stale). Only the HANDS are invented, and they are
    /// declared as named constants right here so the owner can see exactly what "misses sometimes" meant.</para>
    ///
    /// <para><b>What it is NOT.</b> Not a test of the controller's branch — <c>BiteSequenceControllerTests</c>
    /// already pins that OFF spends at the bite and ON spends at the landing. This prices the consequence.
    /// The two cost curves are stated once, as <see cref="BaitPerLandedFish_SpendAtBite"/> and
    /// <see cref="BaitPerLandedFish_SpendOnCatch"/>, and everything economic is derived from them.</para>
    /// </summary>
    public class BaitEconomyPacingTests
    {
        // ---- the model's only invented numbers: two hands -----------------------------------------
        //
        // Reaction is a CHOICE reaction, not a simple one: the player must decide "tease or take?" before
        // moving, which is why these sit above the 0.15 s floor BiteContentValidationTests uses for its
        // best-case hand. Tease/quiet chances are the false-nibble game's actual failure mode — striking
        // at a dip, or at nothing. FightWin01 is the v2 fight (owner-ruled "does feel good", #281),
        // parameterised because it is a feel system, not a number anyone has measured.
        private readonly struct Hand
        {
            public readonly string Name;
            public readonly float ReactionMeanSeconds, ReactionSigmaSeconds, ReactionFloorSeconds;
            public readonly float TeaseStrike01, QuietStrike01, FightWin01;

            public Hand(string name, float mean, float sigma, float floor,
                        float tease, float quiet, float fightWin)
            {
                Name = name; ReactionMeanSeconds = mean; ReactionSigmaSeconds = sigma;
                ReactionFloorSeconds = floor; TeaseStrike01 = tease; QuietStrike01 = quiet;
                FightWin01 = fightWin;
            }
        }

        /// <summary>A hand that has learned the tells: strikes ~0.30 s into the take, rarely fooled by a
        /// tease, wins most fights.</summary>
        private static readonly Hand Competent = new Hand("competent", 0.30f, 0.09f, 0.14f, 0.08f, 0.015f, 0.90f);

        /// <summary>Hour one: slower to commit, fooled by a third of the teases, loses fights. Striking
        /// into the QUIET stays rarer than striking a tease even here — with no tell showing there is
        /// nothing to misread, only impatience.</summary>
        private static readonly Hand NewPlayer = new Hand("new", 0.55f, 0.20f, 0.20f, 0.35f, 0.06f, 0.65f);

        private const int Trials = 4000;          // ±~0.8 pp at p≈0.5 — tight enough for two-decimal bands
        private const int Seed = 20260730;        // the branch's date; any fixed seed, never time-seeded
        private const float Dt = 1f / 60f;        // the frame the controller actually ticks the sim at
        private const float GuardSeconds = 300f;  // a sequence that runs this long is a bug, not a fish

        /// <summary>The proposed economy guard-rail (`economy-sim`, not an owner ruling): bait is a TAX on
        /// a catch, never a toll booth in front of it. Past this share of the fish's own base value the
        /// consumable is eating the rung it is supposed to fund.</summary>
        private const float BaitShareCeiling01 = 0.35f;

        // ---- the two cost curves — the whole question, as arithmetic --------------------------------

        /// <summary>Bait per LANDED fish with the flag OFF (shipped): one bait leaves the box at every
        /// bite, so a landed fish carries the cost of every tease, missed strike and lost fight before
        /// it. The reciprocal of the land rate — unbounded as skill falls.</summary>
        private static float BaitPerLandedFish_SpendAtBite(float landRate01)
            => landRate01 <= 0f ? float.PositiveInfinity : 1f / landRate01;

        /// <summary>Bait per LANDED fish with the flag ON: exactly one, for every fish, for every player,
        /// forever. A miss costs time only. <paramref name="landRate01"/> is taken and deliberately
        /// IGNORED — that it cannot enter this function is the whole mode.</summary>
        private static float BaitPerLandedFish_SpendOnCatch(float landRate01) => 1f;

        /// <summary>Bait per LANDED fish under the MIDDLE PATH §7.6 of the pacing doc names but does NOT
        /// build (it would move one call in <c>FishingController</c> — gameplay-systems' file): the spend
        /// lands on the HOOK-UP. Teases and missed strikes are free (the owner's "time only"), but a fish
        /// that was hooked and then broke off keeps the bait — which is what actually happens when one
        /// does. Bounded by the FIGHT alone, so the bite funnel can never multiply it.</summary>
        private static float BaitPerLandedFish_SpendAtHookup(float fightWin01)
            => fightWin01 <= 0f ? float.PositiveInfinity : 1f / fightWin01;

        // ---- driving the real sim -------------------------------------------------------------------

        private readonly struct Outcome
        {
            public readonly float HookRate01, MeanSeconds, MeanSecondsWhenLost;
            public Outcome(float hook, float sec, float lostSec)
            { HookRate01 = hook; MeanSeconds = sec; MeanSecondsWhenLost = lostSec; }
        }

        /// <summary>Play <paramref name="trials"/> whole bite sequences of one personality with one hand.
        /// The sea's rolls and the hand's mistakes draw from SEPARATE streams, so changing the hand never
        /// re-shuffles the fish (and a re-tuned Def moves the sea's stream only).</summary>
        private static Outcome Play(in BiteProfile profile, in Hand hand, int trials)
        {
            int hooked = 0, lost = 0;
            float totalSeconds = 0f, lostSeconds = 0f;

            for (int trial = 0; trial < trials; trial++)
            {
                var sim = new BiteSequenceSim(profile, new System.Random(Seed + trial * 2 + 1));
                var hands = new System.Random(Seed + trial * 2 + 2);

                var lastPhase = (BitePhase)(-1);
                float reaction = 0f;        // sampled once, when THIS take opens
                bool mistakeQueued = false; // a decided misread, fired on the phase's first tick
                float t = 0f;

                while (!sim.IsOver && t < GuardSeconds)
                {
                    if (sim.Phase != lastPhase)      // every transition alternates the phase, so this
                    {                                // catches each new event exactly once
                        lastPhase = sim.Phase;
                        mistakeQueued = false;
                        switch (sim.Phase)
                        {
                            case BitePhase.TakeOpen:  reaction = ReactionSeconds(hands, in hand); break;
                            case BitePhase.NibbleDip: mistakeQueued = hands.NextDouble() < hand.TeaseStrike01; break;
                            case BitePhase.Approach:  mistakeQueued = hands.NextDouble() < hand.QuietStrike01; break;
                        }
                    }

                    // A misread fires at the head of its phase (striking at a dip, or into the quiet). The
                    // exact instant inside the phase is immaterial — both are EARLY and end the pass.
                    bool strike = sim.Phase == BitePhase.TakeOpen
                        ? sim.PhaseSeconds >= reaction
                        : mistakeQueued;

                    sim.Tick(Dt, strike);
                    t += Dt;
                }

                totalSeconds += t;
                if (sim.Phase == BitePhase.Hooked) hooked++;
                else { lost++; lostSeconds += t; }
            }

            return new Outcome(hooked / (float)trials,
                               totalSeconds / trials,
                               lost > 0 ? lostSeconds / lost : 0f);
        }

        /// <summary>A normal reaction time, floored. Box–Muller off the hand's own stream.</summary>
        private static float ReactionSeconds(System.Random rng, in Hand hand)
        {
            double u1 = 1.0 - rng.NextDouble();   // (0,1] — never log(0)
            double u2 = rng.NextDouble();
            double z = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return Mathf.Max(hand.ReactionFloorSeconds,
                             hand.ReactionMeanSeconds + hand.ReactionSigmaSeconds * (float)z);
        }

        // ---- the shipped content --------------------------------------------------------------------

        /// <summary>Every species the ROD can catch that has a bite personality wired — the pool the
        /// St Peters shore actually fishes (the clam is ClamFork, hand-dug, deliberately not in it).
        /// Ordered by id so the logged table is stable run to run.</summary>
        private static List<FishSpeciesDef> RodSpeciesWithBites()
        {
            var list = new List<FishSpeciesDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { "Assets/_Project/Data" }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null && fish.Bite != null && fish.GearAllowed(Gear.Handline)) list.Add(fish);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        private static List<BaitDef> AuthoredBaits()
        {
            var list = new List<BaitDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BaitDef", new[] { "Assets/_Project/Data" }))
            {
                var bait = AssetDatabase.LoadAssetAtPath<BaitDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (bait != null) list.Add(bait);
            }
            return list;
        }

        /// <summary>What a rational player ties on for this species: the CHEAPEST bait that favours it
        /// (the favour boost is the same multiplier whichever favouring bait you pick, so price decides).
        /// 0 when nothing authored favours it — then bait is not a cost of taking that fish at all.</summary>
        private static int CheapestFavouringBaitPrice(string speciesId, List<BaitDef> baits)
        {
            int best = 0;
            foreach (BaitDef bait in baits)
            {
                if (bait.FavorsSpeciesIds == null) continue;
                bool favours = false;
                foreach (string id in bait.FavorsSpeciesIds) if (id == speciesId) { favours = true; break; }
                if (!favours) continue;
                if (best == 0 || bait.Price < best) best = bait.Price;
            }
            return best;
        }

        /// <summary>The St Peters ROD pool's species mix (id → share of bites) with one bait tied on —
        /// null for a bare hook. Taken straight off the SHIPPED resolver (<c>Matches</c> +
        /// <c>EffectiveWeight</c>), never a second derivation of its weighting: a change to the bait
        /// boost or a species' SpawnWeight moves this table the same day it moves the game.</summary>
        private static Dictionary<string, float> RodMixAtStPeters(BaitDef bait, List<FishSpeciesDef> pool)
        {
            var ctx = new CatchContext("region.st_peters", 0f, 12f, Season.HighSummer, Gear.Handline,
                                       CatchContext.NoDepth, 0f, LureTag.None, bait?.FavorsSpeciesIds);
            var mix = new Dictionary<string, float>();
            float total = 0f;
            foreach (FishSpeciesDef fish in pool)
            {
                if (!CatchResolver.Matches(fish, in ctx)) continue;
                float w = CatchResolver.EffectiveWeight(fish, in ctx, DepthDropSettings.Default,
                                                        BaitTackleSettings.Default);
                mix[fish.Id] = w;
                total += w;
            }
            foreach (string id in new List<string>(mix.Keys)) mix[id] /= Mathf.Max(1e-6f, total);
            return mix;
        }

        /// <summary>Baits per landed fish across a whole session — the number a pacing projection needs,
        /// because a cast doesn't choose its fish. Bites are spent on the mix; only the landable share
        /// comes back. <paramref name="gatedOut"/> is the species the player may not yet LAND (an
        /// unlicensed cod eats a bite and returns nothing).</summary>
        private static float BlendedBaitPerLandedFish_SpendAtBite(
            Dictionary<string, float> mix, Dictionary<string, float> landRate, HashSet<string> gatedOut)
        {
            float landedPerBite = 0f;
            foreach (KeyValuePair<string, float> kv in mix)
            {
                if (gatedOut != null && gatedOut.Contains(kv.Key)) continue;
                if (landRate.TryGetValue(kv.Key, out float rate)) landedPerBite += kv.Value * rate;
            }
            return BaitPerLandedFish_SpendAtBite(landedPerBite);
        }

        private static List<LicenseDef> AuthoredLicenses()
        {
            var list = new List<LicenseDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:LicenseDef", new[] { "Assets/_Project/Data" }))
            {
                var lic = AssetDatabase.LoadAssetAtPath<LicenseDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (lic != null) list.Add(lic);
            }
            return list;
        }

        // ---- the table the pacing doc quotes ---------------------------------------------------------

        /// <summary>Not an assertion — the run that produces §7's table. Logged so the doc's numbers can be
        /// regenerated by re-running this one test rather than trusted.</summary>
        [Test]
        public void Report_BaitCostPerLandedFish_UnderBothModes()
        {
            List<BaitDef> baits = AuthoredBaits();
            var sb = new StringBuilder("[bait-economy] the flag, priced — trials=")
                .Append(Trials).Append(", seed=").Append(Seed).AppendLine()
                .AppendLine("species | hand | P(hook|bite) | P(land|bite) | bait/fish OFF | bait/fish ON | "
                            + "bait/fish HOOKUP | bait ₲/fish OFF | share of value OFF | "
                            + "mean s/sequence | mean s when lost");

            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
            {
                int baitPrice = CheapestFavouringBaitPrice(fish.Id, baits);
                foreach (Hand hand in new[] { Competent, NewPlayer })
                {
                    Outcome o = Play(fish.Bite.ToProfile(), in hand, Trials);
                    float land = o.HookRate01 * hand.FightWin01;
                    float off = BaitPerLandedFish_SpendAtBite(land);
                    sb.Append(fish.Id).Append(" | ").Append(hand.Name)
                      .Append(" | ").Append(o.HookRate01.ToString("0.000"))
                      .Append(" | ").Append(land.ToString("0.000"))
                      .Append(" | ").Append(off.ToString("0.00"))
                      .Append(" | ").Append(BaitPerLandedFish_SpendOnCatch(land).ToString("0.00"))
                      .Append(" | ").Append(BaitPerLandedFish_SpendAtHookup(hand.FightWin01).ToString("0.00"))
                      .Append(" | ").Append((off * baitPrice).ToString("0.0"))
                      .Append(" | ").Append((off * baitPrice / Mathf.Max(1, fish.BaseValue)).ToString("0.00"))
                      .Append(" | ").Append(o.MeanSeconds.ToString("0.0"))
                      .Append(" | ").Append(o.MeanSecondsWhenLost.ToString("0.0"))
                      .AppendLine();
                }
            }
            Debug.Log(sb.ToString());
            Assert.IsNotEmpty(RodSpeciesWithBites(),
                "no rod species carries a bite personality — there is nothing to price");
        }

        // ---- the invariants the recommendation rests on ----------------------------------------------

        [Test]
        public void SpendOnCatch_IsExactlyOneBaitPerFish_ForEveryPersonalityAndHand()
        {
            // The flag's whole economic content: ON, the cost is flat and knowable — one bait, one fish,
            // whoever you are. Nothing about the bite personality can move it, which is why it is the
            // only mode a pacing projection can price without also pricing the player.
            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
                foreach (Hand hand in new[] { Competent, NewPlayer })
                {
                    Outcome o = Play(fish.Bite.ToProfile(), in hand, 200);
                    Assert.Greater(o.HookRate01, 0f, $"{fish.Id}: {hand.Name} never hooks — the model is broken");
                    Assert.AreEqual(1f, BaitPerLandedFish_SpendOnCatch(o.HookRate01 * hand.FightWin01), 1e-6f,
                        $"{fish.Id}/{hand.Name}: spend-on-catch is one bait per fish by construction");
                }
        }

        [Test]
        public void SpendAtBite_MakesBaitCostRegressiveInSkill_SpendOnCatchDoesNot()
        {
            // The structural finding, and the one that survives any plausible re-guess of the two hands:
            // OFF multiplies a consumable cost by INEXPERIENCE, so the player earning least pays most per
            // fish. ON charges both hands the same. §4 of the pacing doc requires the casual player to
            // stay "late but not stuck" — a cost that scales with missing is the wrong shape for that.
            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
            {
                float landCompetent = Play(fish.Bite.ToProfile(), in Competent, Trials).HookRate01 * Competent.FightWin01;
                float landNew = Play(fish.Bite.ToProfile(), in NewPlayer, Trials).HookRate01 * NewPlayer.FightWin01;

                Assert.Greater(BaitPerLandedFish_SpendAtBite(landNew),
                               BaitPerLandedFish_SpendAtBite(landCompetent) * 1.2f,
                    $"{fish.Id}: spend-at-bite must be visibly dearer for the new hand — that IS the finding");
                Assert.GreaterOrEqual(BaitPerLandedFish_SpendAtBite(landCompetent), 1f,
                    $"{fish.Id}: spend-at-bite can never be cheaper than one bait per fish");
                Assert.AreEqual(BaitPerLandedFish_SpendOnCatch(landCompetent),
                                BaitPerLandedFish_SpendOnCatch(landNew), 1e-6f,
                    $"{fish.Id}: spend-on-catch charges the two hands the same — the land rate cannot reach it");
            }
        }

        [Test]
        public void Haddock_IsTheHardestShippedPersonality_TheTableLeansOnThis()
        {
            // §7's worst case is the haddock: the tightest window (0.45 s) AND the most teases (2–4) AND
            // the fewest passes (2). If a re-tune moves the worst case elsewhere, the doc's worked example
            // is about the wrong fish and this goes red.
            string worstId = null;
            float worstHook = float.MaxValue;
            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
            {
                float hook = Play(fish.Bite.ToProfile(), in NewPlayer, Trials).HookRate01;
                if (hook < worstHook) { worstHook = hook; worstId = fish.Id; }
            }
            Assert.AreEqual("fish.haddock", worstId,
                "the pacing doc's worked example is the haddock — a re-tuned bite moved the worst case, " +
                "so §7's table needs regenerating (re-run Report_BaitCostPerLandedFish_UnderBothModes)");
        }

        [Test]
        public void ShippedPersonalities_HookRatesStayInTheBandTheDocQuotes()
        {
            // A pin, not a judgement: these are the measured rates §7's table was written from. Re-tuning
            // a BiteDef is allowed and expected — but it must not silently leave the doc quoting numbers
            // the assets no longer produce.
            var expected = new Dictionary<string, (float competent, float newHand)>
            {
                { "fish.atlantic_cod", (0.915f, 0.586f) },
                { "fish.haddock",      (0.756f, 0.092f) },
                { "fish.mackerel",     (0.986f, 0.934f) },
                { "fish.pollock",      (0.952f, 0.792f) },
            };
            // The sweep is fully seeded, so these are exact, not sampled — the tolerance only absorbs
            // float wobble, and anything bigger than it is a real re-tune of a Def or a hand.
            const float Tolerance = 0.02f;

            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
            {
                if (!expected.TryGetValue(fish.Id, out var band)) continue;
                Assert.AreEqual(band.competent, Play(fish.Bite.ToProfile(), in Competent, Trials).HookRate01,
                    Tolerance, $"{fish.Id}: competent hook rate moved — regenerate §7's table");
                Assert.AreEqual(band.newHand, Play(fish.Bite.ToProfile(), in NewPlayer, Trials).HookRate01,
                    Tolerance, $"{fish.Id}: new-hand hook rate moved — regenerate §7's table");
            }
        }

        [Test]
        public void WhoBreachesTheBaitShareCeiling_IsPinnedUnderBothModes()
        {
            // Two separate defects, told apart. Under ON the share is purely a PRICE question (one bait,
            // one fish). It USED to breach for mackerel and pollock — capelin at 5₲ against a 10₲ mackerel
            // was half the fish — which is the defect §7.4 found and recommended a re-price for. That
            // re-price has now LANDED (bait.capelin 5₲ → 2₲, plan-to-m1 §7.5's store pass), so the ON list
            // is empty: 2/10 = 20% and 2/11 = 18% both sit under the 35% ceiling, and capelin is now also
            // cod's cheapest favouring bait at 2/14 = 14%. Under OFF EVERY species still breaches for a new
            // hand, because the multiplier is the reciprocal of a land rate — that one IS the flag, and it
            // is still the open recommendation.
            // Either list changing means the doc's §7 table is stale; regenerate it, don't patch this.
            List<BaitDef> baits = AuthoredBaits();
            var breachOn = new List<string>();
            var breachOffNewHand = new List<string>();
            var priced = new List<string>();

            foreach (FishSpeciesDef fish in RodSpeciesWithBites())
            {
                int baitPrice = CheapestFavouringBaitPrice(fish.Id, baits);
                if (baitPrice <= 0) continue;                   // nothing favours it — bait isn't its cost
                priced.Add(fish.Id);
                float value = Mathf.Max(1, fish.BaseValue);

                if (BaitPerLandedFish_SpendOnCatch(1f) * baitPrice / value > BaitShareCeiling01)
                    breachOn.Add(fish.Id);

                float landNew = Play(fish.Bite.ToProfile(), in NewPlayer, Trials).HookRate01 * NewPlayer.FightWin01;
                if (BaitPerLandedFish_SpendAtBite(landNew) * baitPrice / value > BaitShareCeiling01)
                    breachOffNewHand.Add(fish.Id);
            }

            CollectionAssert.IsEmpty(breachOn,
                $"after the capelin re-price NO rod species breaches the {BaitShareCeiling01:P0} ceiling " +
                "under spend-on-catch — the bait PRICE defect §7.4 found is closed. A species reappearing " +
                "here means a bait or a base value was re-tuned back into the red");
            CollectionAssert.AreEquivalent(priced, breachOffNewHand,
                $"spend-at-bite breaches the {BaitShareCeiling01:P0} ceiling for EVERY rod species a new " +
                "hand fishes — the finding m1-progression-pacing §7 rests on");
        }

        [Test]
        public void ASessionsBlendedBaitCost_IsWorstExactlyWhereTheNewPlayerStarts()
        {
            // A cast does not choose its fish, so the number that actually sets the pacing is the
            // SESSION blend. It is worst in precisely the wrong place: a new hand, on the St Peters
            // shore, before the cod licence, with the cheapest sensible bait tied on. Every authored bait
            // that touches the rod pool favours cod (four of the seven; the rest are pot bait), so tying
            // one on steers bites TOWARD the one species that cannot be landed yet. Under spend-at-bite
            // that compounds; under spend-on-catch it cannot happen at all.
            //
            // Since the capelin re-price the cheapest such bait is capelin, which favours mackerel and
            // pollock as well as cod — so the steer is no longer purely into the gated fish, and the blend
            // is better than it was. Better, not fixed: the OFF-mode reciprocal is still unbounded in a new
            // hand's hands, which is why §7's recommendation stands on its own without the price defect.
            List<FishSpeciesDef> pool = RodSpeciesWithBites();
            List<BaitDef> baits = AuthoredBaits();
            var gatedByLicence = new HashSet<string>();
            foreach (FishSpeciesDef fish in pool)
                if (!CatchLicensePolicy.MayLand(fish.Id, AuthoredLicenses(), null)) gatedByLicence.Add(fish.Id);
            CollectionAssert.AreEquivalent(new[] { "fish.atlantic_cod" }, gatedByLicence,
                "cod is the only licence-gated fish in the rod pool");

            var landNew = new Dictionary<string, float>();
            var landCompetent = new Dictionary<string, float>();
            foreach (FishSpeciesDef fish in pool)
            {
                landNew[fish.Id] = Play(fish.Bite.ToProfile(), in NewPlayer, Trials).HookRate01 * NewPlayer.FightWin01;
                landCompetent[fish.Id] = Play(fish.Bite.ToProfile(), in Competent, Trials).HookRate01 * Competent.FightWin01;
            }

            // The cheapest bait that favours anything in the pool — what a player short of coin ties on.
            BaitDef cheapest = null;
            foreach (BaitDef bait in baits)
            {
                if (bait.FavorsSpeciesIds == null) continue;
                bool touchesPool = false;
                foreach (FishSpeciesDef fish in pool)
                    if (CheapestFavouringBaitPrice(fish.Id, new List<BaitDef> { bait }) > 0) { touchesPool = true; break; }
                if (touchesPool && (cheapest == null || bait.Price < cheapest.Price)) cheapest = bait;
            }
            Assert.IsNotNull(cheapest, "some authored bait favours the rod pool");

            Dictionary<string, float> mixBaited = RodMixAtStPeters(cheapest, pool);
            float worst = BlendedBaitPerLandedFish_SpendAtBite(mixBaited, landNew, gatedByLicence);
            float best = BlendedBaitPerLandedFish_SpendAtBite(RodMixAtStPeters(null, pool), landCompetent, null);

            Debug.Log($"[bait-economy] blended baits per landed fish, spend-at-bite — " +
                      $"new hand + {cheapest.Id} ({cheapest.Price}₲) + no cod licence: {worst:0.0}; " +
                      $"competent, bare hook, licensed: {best:0.00}; spend-on-catch: 1.00 for both.");

            // ⚠ THE ABSOLUTE PIN IS DELIBERATELY GONE. This used to assert `worst > 10` baits per landed
            // fish. That figure was measured when the cheapest bait touching the rod pool was shucked clam
            // (3₲), which favours cod and haddock — a licence-gated fish and one a new hand almost never
            // lands, so the boost went where nothing came back. The capelin re-price (5₲ → 2₲, §7.4's own
            // recommendation, landed with the §7.5 store pass) makes CAPELIN the cheapest instead, and
            // capelin favours mackerel and pollock, which a new hand does land. The blend therefore improved
            // for a real and intended reason, and any pinned magnitude is stale by construction.
            //
            // The finding §7 rests on is a SHAPE, not that one number, so the shape is what is asserted
            // here. The measured value is logged just above — regenerate m1-progression-pacing §7's table
            // from a CI run rather than re-pinning a number that the next legitimate re-price moves again.
            Assert.Greater(worst, best,
                $"the opening-day blend is {worst:0.0} baits per landed fish against {best:0.00} for a " +
                "competent licensed hand — a new hand on the home shore must still blend worse, which is " +
                "the regressive shape the recommendation is about");
            Assert.Greater(worst, 1f,
                "and must still cost more than one bait per landed fish — 1.00 is exactly the ceiling " +
                "spend-on-catch imposes, so anything at or below it would mean the modes had converged");
            Assert.AreEqual(1f, BaitPerLandedFish_SpendOnCatch(0f), 1e-6f,
                "spend-on-catch is 1.00 in every one of those situations — it has no blend");
        }

        [Test]
        public void SpendAtHookup_SitsBetweenTheTwoModes_AndTheBiteFunnelCannotReachIt()
        {
            // Why §7.3 offers it as the compromise: it keeps a miss costing something (a broken-off fish
            // took your bait) while capping that cost at the FIGHT's reciprocal — 1.11 baits/fish for a
            // competent hand, 1.54 for a new one, the SAME for every personality. The bite minigame's
            // unbounded funnel, which is what makes spend-at-bite regressive, cannot enter it at all.
            foreach (Hand hand in new[] { Competent, NewPlayer })
            {
                float hookup = BaitPerLandedFish_SpendAtHookup(hand.FightWin01);
                foreach (FishSpeciesDef fish in RodSpeciesWithBites())
                {
                    float land = Play(fish.Bite.ToProfile(), in hand, Trials).HookRate01 * hand.FightWin01;
                    Assert.LessOrEqual(BaitPerLandedFish_SpendOnCatch(land), hookup,
                        $"{fish.Id}/{hand.Name}: spend-on-catch is the floor");
                    Assert.LessOrEqual(hookup, BaitPerLandedFish_SpendAtBite(land) + 1e-4f,
                        $"{fish.Id}/{hand.Name}: spend-at-hookup can never cost more than spend-at-bite");
                    Assert.AreEqual(hookup, BaitPerLandedFish_SpendAtHookup(hand.FightWin01), 1e-6f,
                        $"{fish.Id}/{hand.Name}: the personality cannot move the hook-up cost — that is " +
                        "the property the compromise is chosen for");
                }
            }
        }

        [Test]
        public void UnlicensedCod_IsPureBaitLossUnderSpendAtBite_AndFreeUnderSpendOnCatch()
        {
            // The sharpest early-game leak, and it needs no player model at all: cod is in the St Peters
            // ROD pool from day one, and cod is licence-gated (license.cod, bought at Nine Mile Creek).
            // Before that purchase the land rate for cod is exactly ZERO — every cod bite is a released
            // fish. OFF charges a bait for each of them; ON charges nothing.
            List<LicenseDef> licenses = AuthoredLicenses();
            Assert.AreEqual("license.cod", CatchLicensePolicy.RequiredLicenseFor("fish.atlantic_cod", licenses),
                "cod is the gated species this leak is about");
            Assert.IsFalse(CatchLicensePolicy.MayLand("fish.atlantic_cod", licenses, null),
                "no wallet yet = cod cannot be landed (the gate fails closed) — so its land rate is 0");

            Assert.IsTrue(float.IsPositiveInfinity(BaitPerLandedFish_SpendAtBite(0f)),
                "spend-at-bite: bait per landed cod is unbounded before the licence — every bite is a loss");
            Assert.AreEqual(1f, BaitPerLandedFish_SpendOnCatch(0f), 1e-6f,
                "spend-on-catch: an unlicensed release costs nothing, so the leak cannot exist");
        }
    }
}
