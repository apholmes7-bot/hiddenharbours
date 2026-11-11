using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>The beats of the St Peters buy-and-repair opening, in the order the note walks them.</summary>
    public enum OnboardingStep
    {
        TalkGinny,
        DigClams,
        BuyCodLicence,
        BuyRod,
        BuyDory,
        RepairDory,
        Done,
    }

    /// <summary>
    /// What the opening needs to know about the player, as plain facts. Built from the save
    /// (<see cref="OnboardingProgress.Read"/>) and, in the director, OR-ed with what this session has
    /// already heard on the bus — so the answer is the same whether the fact arrived as a signal a
    /// moment ago or was read back from disk after Continue.
    /// </summary>
    public readonly struct OnboardingFacts
    {
        public readonly bool MetGinny;
        public readonly bool LandedCatch;
        public readonly bool HasCodLicence;
        public readonly bool HasRod;
        public readonly bool BoughtDory;
        public readonly bool DoryRepaired;
        public readonly bool Onboarded;

        public OnboardingFacts(bool metGinny, bool landedCatch, bool hasCodLicence, bool hasRod,
                               bool boughtDory, bool doryRepaired, bool onboarded)
        {
            MetGinny = metGinny; LandedCatch = landedCatch; HasCodLicence = hasCodLicence;
            HasRod = hasRod; BoughtDory = boughtDory; DoryRepaired = doryRepaired; Onboarded = onboarded;
        }
    }

    /// <summary>
    /// <b>Which beat of the opening is next, decided from state</b> — so a new game, a Continue and a
    /// reload show the same line for the same save.
    ///
    /// <para>The director used to hold the answer in four private booleans set by Core signals. They
    /// were never saved, so after Continue the note fell back to an early beat; and the licence one was
    /// set by ANY <see cref="LicensePurchased"/>, so the clam licence counted as the cod licence. Here
    /// each beat is read from what the save already holds, and a licence is the one named by its Def
    /// id (<see cref="CodLicenceId"/>).</para>
    ///
    /// <para><b>What each beat reads.</b>
    /// <list type="bullet">
    ///   <item>met Ginny — the existing <c>met_ginny</c> flag;</item>
    ///   <item>cod licence — <c>SaveData.OwnedLicenses</c> holds <see cref="CodLicenceId"/>;</item>
    ///   <item>rod — <c>SaveData.OwnedGear</c> holds <see cref="RodGearId"/> (owning, not holding:
    ///   the free rod on the wharf is a tool in the world, never an owned-gear record);</item>
    ///   <item>dory repaired — <c>SaveData.RepairedBoats</c> holds <see cref="DoryBoatId"/>;</item>
    ///   <item>first catch landed and dory bought — flag keys (see <see cref="OnboardingFlags"/> on
    ///   why these two cannot be read from a holding). A catch still in the pail, the freezer or a
    ///   hold, a cod licence, a rod or a bought dory also count as "has landed a catch", which is how
    ///   a save made before the flag existed still reads true.</item>
    /// </list></para>
    ///
    /// <para>Pure: no Unity objects, no bus, no allocation — a handful of short list scans, cheap
    /// enough for the director to ask every frame (rule 7).</para>
    /// </summary>
    public static class OnboardingProgress
    {
        // Content ids (rule 2's id seam), matching the catalog assets. Public so the tests can pin
        // them to the shipped Defs and so the owner can see what each beat waits on.
        public const string CodLicenceId = "license.cod";
        public const string RodGearId    = "gear.rod";
        public const string DoryBoatId   = "boat.dory";

        /// <summary>The facts as the save and the flag store hold them. Null-safe on both: no save
        /// reads as a fresh game, which is what a rig without a save service is.</summary>
        public static OnboardingFacts Read(SaveData save, OnboardingFlags flags)
        {
            bool hasCodLicence = Holds(save?.OwnedLicenses, CodLicenceId);
            bool hasRod        = Holds(save?.OwnedGear, RodGearId);
            bool doryRepaired  = Holds(save?.RepairedBoats, DoryBoatId);
            bool boughtDory    = doryRepaired || (flags != null && flags.BoughtDory);

            bool catchHeld = save != null
                             && (Any(save.BucketCatch) || Any(save.FreezerCatch) || Any(save.BoatHoldCatch));
            bool landedCatch = (flags != null && flags.LandedCatch)
                               || catchHeld || hasCodLicence || hasRod || boughtDory;

            return new OnboardingFacts(
                metGinny: flags != null && flags.MetGinny,
                landedCatch: landedCatch,
                hasCodLicence: hasCodLicence,
                hasRod: hasRod,
                boughtDory: boughtDory,
                doryRepaired: doryRepaired,
                onboarded: flags != null && flags.Onboarded);
        }

        /// <summary>The next beat for a set of facts. Strictly in order: an owned rod does not excuse
        /// a missing cod licence, and a clam licence is not asked about at all.</summary>
        public static OnboardingStep StepFor(in OnboardingFacts facts)
        {
            if (facts.Onboarded || facts.DoryRepaired) return OnboardingStep.Done;
            if (!facts.MetGinny)      return OnboardingStep.TalkGinny;
            if (!facts.LandedCatch)   return OnboardingStep.DigClams;
            if (!facts.HasCodLicence) return OnboardingStep.BuyCodLicence;
            if (!facts.HasRod)        return OnboardingStep.BuyRod;
            if (!facts.BoughtDory)    return OnboardingStep.BuyDory;
            return OnboardingStep.RepairDory;
        }

        /// <summary>The note's line for a beat, from <see cref="WorldStrings"/>; null once the opening
        /// is done (the closing lines are the director's, on a timer, and only on the live repair).</summary>
        public static string LineFor(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.TalkGinny:     return WorldStrings.OnboardTalkGinny;
                case OnboardingStep.DigClams:      return WorldStrings.OnboardDigClams;
                case OnboardingStep.BuyCodLicence: return WorldStrings.OnboardBuyLicence;
                case OnboardingStep.BuyRod:        return WorldStrings.OnboardBuyRod;
                case OnboardingStep.BuyDory:       return WorldStrings.OnboardBuyDory;
                case OnboardingStep.RepairDory:    return WorldStrings.OnboardRepairDory;
                default:                           return null;
            }
        }

        private static bool Holds(System.Collections.Generic.List<string> list, string id)
            => list != null && list.Contains(id);

        private static bool Any<T>(System.Collections.Generic.List<T> list)
            => list != null && list.Count > 0;
    }
}
