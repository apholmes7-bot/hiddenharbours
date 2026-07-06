using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Self-installing publisher of the authored <see cref="FishSpeciesLibrary"/> into
    /// <see cref="FishSpeciesRegistry"/>. Mirrors Core's <c>IconRegistrar</c> and <c>SaveService</c>
    /// bootstrap: at <c>BeforeSceneLoad</c> it loads the library from <c>Resources</c> and registers every
    /// species by id, so the trap runtime (and any future id-only consumer) can resolve a fish id → Def
    /// before any scene object awakes — <b>no scene or builder wiring needed</b>.
    ///
    /// <para>Resilient by design: if no <see cref="FishSpeciesLibrary"/> is present (EditMode, a stripped
    /// build, or before the asset is authored) the registry stays empty and every consumer degrades
    /// gracefully — <see cref="FishSpeciesRegistry.Get"/> returns null, an unresolved trap pool is empty
    /// and lands nothing rather than throwing. Idempotent: the registry is first-wins, so a second load is
    /// a no-op.</para>
    /// </summary>
    public static class FishSpeciesRegistrar
    {
        /// <summary>Resources path (no extension) the species library is loaded from at boot.</summary>
        public const string ResourcesPath = "FishSpeciesLibrary";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var lib = Resources.Load<FishSpeciesLibrary>(ResourcesPath);
            if (lib != null) lib.RegisterAll();
        }
    }
}
