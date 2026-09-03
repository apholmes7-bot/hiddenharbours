#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Ships the owner's day/night lever</b> — creates <c>Assets/_Project/Resources/DayNightProfile.asset</c>
    /// when it is missing, carrying <see cref="DayNightProfile.ApplyDefaults"/>.
    ///
    /// <para><b>Why this exists at all.</b> <c>DayNightController</c> has always read
    /// <c>Resources/DayNightProfile</c> and fallen back to a code-built default when it found none — and it
    /// found none, because the asset was never authored. The night was therefore a constant in
    /// <see cref="DayNightProfile.ApplyDefaults"/> and the owner had no way to touch it without a code
    /// change: the <c>GameConfig</c>-is-behind-the-code family, one layer over. Shipping the asset (water
    /// fidelity PR 4, the 2026-09-02 night ruling) puts the gradient, the intensity curve and the moonlight
    /// in an inspector where they belong (rule 6).</para>
    ///
    /// <para>⚠️ <b>CREATE-ONLY. This never refreshes an existing asset, and that is the whole design.</b>
    /// The profile is the owner's TUNING surface, not derived data: a builder that re-stamped
    /// <c>ApplyDefaults()</c> over it would silently discard an evening of his art direction the next time
    /// anybody ran the menu — the opposite of the refresh-in-place contract the DERIVED builders beside
    /// this one keep (their source is a committed sidecar; this one's source is his eye). To take new code
    /// defaults deliberately, delete the asset and run this again.</para>
    /// </summary>
    public static class DayNightProfileBuilder
    {
        const string MenuPath = "Hidden Harbours/Lighting/Create the Day-Night Profile (only if missing)";

        /// <summary>Where the profile lives — the path <c>DayNightController</c> loads by name.</summary>
        public const string ProfilePath = "Assets/_Project/Resources/DayNightProfile.asset";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DayNightProfile>(ProfilePath);
            if (existing != null)
            {
                Debug.Log($"[day-night] {ProfilePath} already exists — left untouched (this builder never " +
                          "overwrites the owner's tuning). Delete it and re-run to take the code defaults.");
                return;
            }

            var profile = ScriptableObject.CreateInstance<DayNightProfile>();
            profile.ApplyDefaults();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[day-night] created {ProfilePath} at the shipped defaults " +
                      $"(moonlight lift max {profile.MoonlightLiftMax:F2}).");
        }
    }
}
#endif
