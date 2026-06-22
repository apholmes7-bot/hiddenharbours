using System.Text;
using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Pure helpers for the region-transition fade (VS-22): cover the hard scene-cut + reposition snap
    /// so the crossing reads as a short voyage. Engine-light &amp; stateless → EditMode-testable; the
    /// MonoBehaviour glue (the self-installing overlay, the activeSceneChanged hook) lives in
    /// <see cref="RegionFadeOverlay"/>. Mirrors the WindReadout/HudFormat split: the no-stuck-black,
    /// no-double-fade discipline is the overlay's; the curve maths are here.
    /// </summary>
    public static class RegionFade
    {
        /// <summary>Default cover duration (real seconds) — a brief voyage flash, not a slow wipe.</summary>
        public const float DefaultFadeSeconds = 0.6f;

        /// <summary>
        /// Overlay alpha while FADING IN the world: it flashes black (1) on arrival and clears to
        /// transparent (0) over <paramref name="fadeSeconds"/>. Linear, clamped to [0,1]; a non-positive
        /// duration is instantly clear (so the overlay never gets stuck black). Monotonic non-increasing
        /// in <paramref name="elapsed"/>.
        /// </summary>
        public static float AlphaAfter(float elapsed, float fadeSeconds)
        {
            if (fadeSeconds <= 0f) return 0f;
            float t = Mathf.Clamp01(Mathf.Max(0f, elapsed) / fadeSeconds);
            return 1f - t;
        }

        /// <summary>
        /// True when an active-scene change is a real ARRIVAL the overlay should cover: the next scene
        /// is named and different from the previous. False for a no-op (same scene) or an unnamed/boot
        /// scene — so a region hop in either direction fades, but spurious changes don't.
        /// </summary>
        public static bool ShouldCover(string previousScene, string nextScene)
            => !string.IsNullOrEmpty(nextScene) && nextScene != previousScene;

        /// <summary>
        /// A readable arrival title from a scene name: split camelCase and underscores/hyphens into words
        /// (e.g. "CoddleCove" → "Coddle Cove", "Port_Greywick" → "Port Greywick"; "Greywick" → "Greywick").
        /// Deliberately decoupled from World <c>RegionDef</c> data — proper player-facing display names
        /// (Coddle Cove / Port Greywick) are a follow-up once region display names are exposed via Core.
        /// </summary>
        public static string ArrivalTitle(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return string.Empty;
            var sb = new StringBuilder(sceneName.Length + 4);
            for (int i = 0; i < sceneName.Length; i++)
            {
                char c = sceneName[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    continue;
                }
                // A camelCase boundary: an upper-case letter right after a lower-case letter or a digit.
                if (i > 0 && char.IsUpper(c) && (char.IsLower(sceneName[i - 1]) || char.IsDigit(sceneName[i - 1])))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
