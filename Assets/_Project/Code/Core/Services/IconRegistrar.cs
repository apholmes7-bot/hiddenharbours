using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Self-installing publisher of the authored <see cref="IconLibrary"/> into <see cref="IconRegistry"/>.
    /// Mirrors <see cref="SaveService"/>'s bootstrap: at <c>BeforeSceneLoad</c> it loads the library from
    /// <c>Resources</c> and registers every id → icon mapping, so the sell screen / catch card / HUD can
    /// resolve content icons by id before any scene object awakes — <b>no scene or builder wiring needed</b>
    /// (keeps it clear of the contested builders).
    ///
    /// <para>Resilient by design: if no <c>IconLibrary</c> is present (EditMode, a stripped build) the UI
    /// simply falls back to its text-only presentation — <see cref="IconRegistry.Get"/> returns null and
    /// every consumer null-checks. Idempotent: the registry is first-wins, so a second load is a no-op.</para>
    /// </summary>
    public static class IconRegistrar
    {
        /// <summary>Resources path (no extension) the icon library is loaded from at boot.</summary>
        public const string ResourcesPath = "IconLibrary";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var lib = Resources.Load<IconLibrary>(ResourcesPath);
            if (lib != null) lib.RegisterAll();
        }
    }
}
