using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // LicenseDef + CatchLicensePolicy + CatalogSource (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>THE ONE LICENCE RULE AT THE LANDING</b>, shared by the rod, the clam dig and the trap haul (owner
    /// ruling 2026-11-23): <i>nothing is blocked from the player first digging, catching or trapping, but
    /// he automatically releases what he holds no licence for, saying "I don't have a licence for this
    /// &lt;species&gt; yet"</i>, and no Notebook entry is made.
    ///
    /// <para><b>Which species need a licence is the licence data, nothing else.</b> A species is gated iff
    /// some <see cref="LicenseDef"/> lists it in <see cref="LicenseDef.PermittedSpeciesIds"/>
    /// (<see cref="CatchLicensePolicy"/>). Two lists are asked, and either one gating the species is
    /// enough: the licences a component was handed (the rod's scene-wired list, a test's own), and the
    /// shipped licence catalog (<see cref="CatalogSource.Licenses"/>). So a licence authored tomorrow gates
    /// every path at once, including scenes that cannot be rebuilt.</para>
    ///
    /// <para><b>The shipped catalog is asked only in a live game</b>, meaning a licence wallet
    /// (<see cref="GameServices.Licenses"/>) is installed. With no wallet (an EditMode rig, a bare art
    /// scene) only the handed list gates, exactly as before this rule existed. The catalog is loaded once
    /// and kept: licences are content, they do not change while the game runs.</para>
    ///
    /// <para><b>Saying it.</b> The words are an inner-voice line asset keyed on
    /// <see cref="InnerVoiceCues.UnlicensedCatch"/>, with the species' display name put in its
    /// <see cref="InnerVoiceCues.NameToken"/>. This class publishes the Core cue and the Core
    /// <see cref="UnlicensedCatchReleased"/> signal; it never names a World or Player class (rule 4).</para>
    /// </summary>
    public static class UnlicensedCatch
    {
        private static IReadOnlyList<LicenseDef> _shipped;
        private static bool _shippedLoaded;
        private static IReadOnlyList<LicenseDef> _override;
        private static bool _hasOverride;

        /// <summary>
        /// The licence catalog this game gates on beyond any handed list: a test's override when one is
        /// set, else the shipped catalog while a licence wallet is installed, else null (nothing extra is
        /// gated).
        /// </summary>
        public static IReadOnlyList<LicenseDef> LiveCatalog()
            => LiveCatalog(GameServices.Licenses);

        /// <summary>The shared content catalog for an explicitly supplied player's wallet.</summary>
        public static IReadOnlyList<LicenseDef> LiveCatalog(ILicenseService wallet)
        {
            if (_hasOverride) return _override;
            if (wallet == null) return null;
            if (!_shippedLoaded)
            {
                _shipped = CatalogSource.Licenses();
                _shippedLoaded = true;
            }
            return _shipped;
        }

        /// <summary>Gate on <paramref name="catalog"/> instead of the shipped one (tests). Null means
        /// "no extra catalog", not "the shipped one"; <see cref="ResetCatalog"/> restores the default.</summary>
        public static void UseCatalog(IReadOnlyList<LicenseDef> catalog)
        {
            _override = catalog;
            _hasOverride = true;
        }

        /// <summary>Back to the default catalog source (tests' teardown).</summary>
        public static void ResetCatalog()
        {
            _override = null;
            _hasOverride = false;
        }

        /// <summary>
        /// True when a catch of <paramref name="speciesId"/> must go back because the player holds no
        /// licence for it: some licence in <paramref name="handed"/> or in <paramref name="catalog"/> lists
        /// the species and <paramref name="wallet"/> does not hold it. A species no licence lists is never
        /// released by this rule; a null wallet holds nothing (fail closed, the policy's own rule). Pure.
        /// </summary>
        public static bool MustRelease(string speciesId, IReadOnlyList<LicenseDef> handed,
                                       IReadOnlyList<LicenseDef> catalog, ILicenseService wallet)
            => !string.IsNullOrEmpty(speciesId)
               && (!CatchLicensePolicy.MayLand(speciesId, handed, wallet)
                   || !CatchLicensePolicy.MayLand(speciesId, catalog, wallet));

        /// <summary><see cref="MustRelease(string, IReadOnlyList{LicenseDef}, IReadOnlyList{LicenseDef},
        /// ILicenseService)"/> against the live catalog and the installed wallet.</summary>
        public static bool MustRelease(string speciesId, IReadOnlyList<LicenseDef> handed = null)
            => MustRelease(speciesId, handed, LiveCatalog(), GameServices.Licenses);

        /// <summary>The licence that would let him keep <paramref name="speciesId"/>: the handed list's
        /// first, else the catalog's, else null.</summary>
        public static string RequiredLicenceFor(string speciesId, IReadOnlyList<LicenseDef> handed,
                                                IReadOnlyList<LicenseDef> catalog)
            => CatchLicensePolicy.RequiredLicenseFor(speciesId, handed)
               ?? CatchLicensePolicy.RequiredLicenseFor(speciesId, catalog);

        /// <summary>
        /// Let one unlicensed catch go: publishes <see cref="UnlicensedCatchReleased"/> (which nothing that
        /// keeps a record counts). Does NOT speak, so a haul of several can say its line once; call
        /// <see cref="Say"/> for that.
        /// </summary>
        public static void Release(in CatchItem item, IReadOnlyList<LicenseDef> handed = null)
            => Release(in item, handed, LiveCatalog());

        /// <summary>Release against an explicit content catalog without reading a global wallet.</summary>
        public static void Release(in CatchItem item, IReadOnlyList<LicenseDef> handed,
                                   IReadOnlyList<LicenseDef> catalog)
            => EventBus.Publish(new UnlicensedCatchReleased(item,
                   RequiredLicenceFor(item.SpeciesId, handed, catalog)));

        /// <summary>He says it, in his own voice: "I don't have a licence for this &lt;name&gt; yet." The
        /// words live in the inner-voice line asset; this only names the species.</summary>
        public static void Say(string speciesName, string speciesId)
            => EventBus.Publish(new InnerVoiceCue(InnerVoiceCues.UnlicensedCatch,
                   string.IsNullOrWhiteSpace(speciesName) ? speciesId : speciesName));

        /// <summary>Release one catch and say the line for it (the rod and the dig: one catch, one line).</summary>
        public static void ReleaseAndSay(in CatchItem item, IReadOnlyList<LicenseDef> handed = null)
            => ReleaseAndSay(in item, handed, LiveCatalog());

        /// <summary>Release using the same explicit catalog that decided this player's attempt.</summary>
        public static void ReleaseAndSay(in CatchItem item, IReadOnlyList<LicenseDef> handed,
                                         IReadOnlyList<LicenseDef> catalog)
        {
            Release(in item, handed, catalog);
            Say(item.DisplayName, item.SpeciesId);
        }
    }
}
