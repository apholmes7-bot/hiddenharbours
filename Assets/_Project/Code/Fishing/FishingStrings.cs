using System.Collections.Generic;
using HiddenHarbours.Economy;   // LicenseDef + CatchLicensePolicy: the licence line names its licence from data

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The home for the player-facing sentences the catch loop says when it has nowhere to put a catch,
    /// and when a fish gets away.
    /// Same convention as <c>ControlStrings</c>, <c>HudStrings</c> and <c>WorldStrings</c>: there is no
    /// runtime localization system wired yet, so centralising the copy here is the seam. When loc tables
    /// land these become lookups and no call site changes.
    ///
    /// <para><b>A sentence may only name a buyer that buys.</b> The pail sentence used to send the player
    /// to a place with no working buyer. It now names the one counter that takes a pail today. The hold
    /// sentences name no buyer at all, because nothing buys a boat's hold yet: when a hold buyer exists,
    /// this is the one place to say where it is.</para>
    ///
    /// <para><b>None of these names a key</b>, and every constant is an interned literal: no composed
    /// string, no garbage. The one composed sentence is <see cref="LicenceNeeded(string, string)"/>,
    /// built once at the moment a won fish is let go, never per frame.</para>
    /// </summary>
    public static class FishingStrings
    {
        /// <summary>The dig is refused because the pail has no room. Points at the store counter that
        /// buys a pail. <c>BucketStrings.Full</c> in the Player module says the same sentence at the pail
        /// itself; a test holds the two equal, because the modules cannot see each other.</summary>
        public const string PailFull = "The bucket's full — Marguerite at LeBlanc's store will buy them.";

        /// <summary>The cast is refused because the hold has no room. Said BEFORE a fish is hooked, so
        /// nothing is landed and lost. Names no buyer (see the class note).</summary>
        public const string HoldFull = "The hold's full — there's no room to stow another fish.";

        /// <summary>The rare case the cast gate cannot see: the hold filled between the cast and the
        /// landing, so the fish had nowhere to go. Said so the loss is never silent.</summary>
        public const string LandedNoRoom = "The hold's full — nowhere to stow it, so it went back over the side.";

        // ---- why she got away ---------------------------------------------------------------------
        // One short line per failure, so the line never comes back empty without a reason. Each names
        // what the PLAYER did (or what the water did), which is the thing they can change next cast.

        /// <summary>The strike came outside the take: at nothing, or on a teasing nibble.</summary>
        public const string StruckTooSoon = "Struck too soon.";

        /// <summary>The true take opened and closed with no strike.</summary>
        public const string SpatIt = "She spat it.";

        /// <summary>The fight was lost: the reel was held against her until the strain peaked and the
        /// hook pulled. Says the cause, because the cure (ease off on her runs) is in it.</summary>
        public const string ThrewTheHook = "Held her too hard — she threw the hook.";

        /// <summary>The wait ran out over water that holds nothing for this rig, here, now.</summary>
        public const string NothingBiting = "Nothing biting here.";

        /// <summary>The fight was won on a species this licence data gates, and the licence is not held.
        /// Used when the data cannot name the licence.</summary>
        public const string LicenceNeededUnnamed = "You need a licence for that — she went back.";

        /// <summary>The line for a mistimed strike, or null when there was no miss to explain.</summary>
        public static string MissedStrike(BiteMiss miss)
        {
            switch (miss)
            {
                case BiteMiss.Early: return StruckTooSoon;
                case BiteMiss.Late:  return SpatIt;
                default:             return null;
            }
        }

        /// <summary>"You need the Cod Fishing License to keep Atlantic Cod — she went back." Both names
        /// come from the Defs' display names, exactly as authored. Falls back to
        /// <see cref="LicenceNeededUnnamed"/> when either name is missing.</summary>
        public static string LicenceNeeded(string speciesName, string licenceName)
        {
            if (string.IsNullOrWhiteSpace(speciesName) || string.IsNullOrWhiteSpace(licenceName))
                return LicenceNeededUnnamed;
            return "You need the " + licenceName + " to keep " + speciesName + " — she went back.";
        }

        /// <summary>The licence line for <paramref name="fish"/>, with the licence looked up in the
        /// authored licence data by the same rule the landing gate uses
        /// (<see cref="CatchLicensePolicy.RequiredLicenseFor"/>).</summary>
        public static string LicenceNeeded(FishSpeciesDef fish, IReadOnlyList<LicenseDef> licences)
        {
            if (fish == null) return LicenceNeededUnnamed;

            string requiredId = CatchLicensePolicy.RequiredLicenseFor(fish.Id, licences);
            string licenceName = null;
            if (!string.IsNullOrEmpty(requiredId) && licences != null)
            {
                for (int i = 0; i < licences.Count; i++)
                {
                    LicenseDef lic = licences[i];
                    if (lic != null && lic.Id == requiredId) { licenceName = lic.DisplayName; break; }
                }
            }
            return LicenceNeeded(fish.DisplayName, licenceName);
        }
    }
}
