using System;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fish art table every scene can reach</b> (content is data, rule 2): one committed asset in
    /// <c>Resources</c> that says which new fish art draws a species on the line, in the land pose and in
    /// the hands, with no scene having to carry a copy.
    ///
    /// <para><b>Why it exists.</b> The fight's size ladders and the catch art table used to reach a scene
    /// only by being serialized into it by a region builder. Two scenes were last saved before those
    /// fields existed (St Peters cannot be rebuilt), so there the fight drew no fish at all and the
    /// hands fell back to the old UI icon. This asset is the fallback that reaches them anyway, the
    /// pattern <c>FishSchoolPresenter</c> and <c>IconRegistrar</c> already use: an inline table that
    /// answers wins, and this answers what it does not.</para>
    ///
    /// <para><b>Built, not typed.</b> <c>FishArtLibraryBuilder</c> fills it from the same rig sidecar
    /// and sheets the region builders read (<c>RodKitImporter.BuildFishSpecies</c>) and points it at
    /// the committed <see cref="CatchItemLibrary"/>. Re-run it after a fish re-bake.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FishArtLibrary", menuName = "Hidden Harbours/Art/Fish Art Library")]
    public sealed class FishArtLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the shared table is loaded from.</summary>
        public const string ResourcesPath = "FishArtLibrary";

        [SerializeField, Tooltip("Every species with its own rig art: the fight's size ladder (shadow, " +
                                 "dart, thrash, roll, jump and the land-pose held sheet per rung), keyed " +
                                 "by the FishSpeciesDef id.")]
        private FishSpeciesVisual[] _species = Array.Empty<FishSpeciesVisual>();

        [SerializeField, Tooltip("The catch art table (held sheets and tote lays, sized). The hands use it " +
                                 "when their own scene field is empty.")]
        private CatchItemLibrary _catchArt;

        private static FishArtLibrary _loaded;

        /// <summary>Every species ladder in the table. For tests / tooling.</summary>
        public FishSpeciesVisual[] Species => _species;

        /// <summary>The catch art table this points at, or null.</summary>
        public CatchItemLibrary CatchArt => _catchArt;

        /// <summary>The ladder for <paramref name="fishId"/>, or null when this table has none.</summary>
        public FishSpeciesVisual Find(string fishId)
        {
            if (string.IsNullOrEmpty(fishId) || _species == null) return null;
            for (int i = 0; i < _species.Length; i++)
                if (_species[i] != null && string.Equals(_species[i].FishId, fishId, StringComparison.Ordinal))
                    return _species[i];
            return null;
        }

        /// <summary>Wire the whole table in one call (the builder, and tests).</summary>
        public void Configure(FishSpeciesVisual[] species, CatchItemLibrary catchArt)
        {
            _species = species ?? Array.Empty<FishSpeciesVisual>();
            _catchArt = catchArt;
        }

        /// <summary>
        /// The shared table from <c>Resources</c>, loaded once, or null when it is not shipped (a stripped
        /// build, a fixture with no assets). Callers degrade to whatever they had before.
        /// </summary>
        public static FishArtLibrary Shared
        {
            get
            {
                // Asked once per hooked species and once per landed catch, never per frame, so a missing
                // asset costing one Resources lookup per ask is cheaper than a stale cache.
                if (_loaded == null) _loaded = Resources.Load<FishArtLibrary>(ResourcesPath);
                return _loaded;
            }
        }

        /// <summary>
        /// Which ladder draws <paramref name="fishId"/>: the scene's own entry when it carries at least
        /// one rung, else the shared table's. Pure, so the choice is pinned without a scene.
        ///
        /// <para>An inline entry with NO rungs is the pre-ladder table some scenes still carry; it is
        /// treated as absent, never as "draw nothing".</para>
        /// </summary>
        public static FishSpeciesVisual Resolve(FishSpeciesVisual inline, FishArtLibrary library, string fishId)
        {
            if (HasRungs(inline)) return inline;
            FishSpeciesVisual shared = library != null ? library.Find(fishId) : null;
            return HasRungs(shared) ? shared : null;
        }

        /// <summary>The catch art table to use: the scene's own when wired, else the shared table's.</summary>
        public static CatchItemLibrary ResolveCatchArt(CatchItemLibrary inline, FishArtLibrary library)
            => inline != null ? inline : library != null ? library.CatchArt : null;

        static bool HasRungs(FishSpeciesVisual v)
        {
            if (v == null || v.Rungs == null) return false;
            for (int i = 0; i < v.Rungs.Length; i++)
                if (v.Rungs[i] != null) return true;
            return false;
        }
    }
}
