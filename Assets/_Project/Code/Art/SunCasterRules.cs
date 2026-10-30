using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    public enum SunCasterBand { Decor, Wharf }

    [Serializable]
    public sealed class SunCasterClassRule
    {
        public string Id;
        [Tooltip("Wildcards matched against the renderer object's name and its sprite name.")]
        public string[] Names = Array.Empty<string>();
        public bool CastsGroundContact;
        public float FootOffset;
        [Tooltip("For named legacy standing art whose sorting order predates SortingBands.")]
        public bool AllowOutsideBands;
        [Tooltip("Use Core's live walking-deck stance for an existing hidden figure caster; the helm clears it.")]
        public bool CastOnWalkingDeck;
    }

    /// <summary>Eligibility and contact settings for standing sprite art. No scene or gameplay types.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Sun Caster Rules", fileName = "SunCasterRules")]
    public sealed class SunCasterRules : ScriptableObject
    {
        public string Id;
        public SunCasterBand[] Bands = Array.Empty<SunCasterBand>();
        [Min(0f)] public float MinimumHeightMetres;
        public string[] ExcludedNames = Array.Empty<string>();
        public string[] ExcludedScenes = Array.Empty<string>();
        public SunCasterClassRule[] Classes = Array.Empty<SunCasterClassRule>();
        public bool DefaultGroundContact;
        public float DefaultFootOffset;
        [Min(0f)] public float CameraDistanceMetres;
        [Min(0f)] public float ArrivalScanSeconds;

        public bool IncludesOrder(int order)
        {
            foreach (var band in Bands)
            {
                switch (band)
                {
                    case SunCasterBand.Decor:
                        if (order >= SortingBands.DecorFloor && order <= SortingBands.DecorCeiling) return true;
                        break;
                    case SunCasterBand.Wharf:
                        if (order >= SortingBands.WharfDeckMin && order <= SortingBands.WharfDeckMax) return true;
                        break;
                }
            }
            return false;
        }

        public bool ExcludesScene(string sceneName) => MatchesAny(sceneName, ExcludedScenes);

        public bool Qualifies(SpriteRenderer renderer, out SunCasterClassRule settings)
        {
            settings = null;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null)
                return false;
            if (renderer.TryGetComponent<SpriteShadow>(out _) || IsExcludedArt(renderer)) return false;
            if (ExcludesScene(renderer.gameObject.scene.name)) return false;
            settings = ClassFor(renderer);
            if (!IncludesOrder(renderer.sortingOrder) && (settings == null || !settings.AllowOutsideBands)) return false;
            return renderer.sprite.bounds.size.y * Mathf.Abs(renderer.transform.lossyScale.y) >= MinimumHeightMetres;
        }

        public bool IsExcludedArt(SpriteRenderer renderer)
            => renderer == null || renderer.GetComponentInParent<Canvas>() != null ||
                MatchesAny(renderer.name, ExcludedNames) ||
                (renderer.sprite != null && MatchesAny(renderer.sprite.name, ExcludedNames));

        public SunCasterClassRule ClassFor(SpriteRenderer renderer)
        {
            if (renderer == null) return null;
            string objectName = renderer.name;
            string spriteName = renderer.sprite != null ? renderer.sprite.name : null;
            foreach (var rule in Classes)
                if (MatchesAny(objectName, rule.Names) || MatchesAny(spriteName, rule.Names)) return rule;
            return null;
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(Id)) error = "A stable id is required.";
            else if (Bands == null || Bands.Length == 0) error = "At least one symbolic sorting band is required.";
            else if (!Finite(MinimumHeightMetres) || MinimumHeightMetres <= 0f) error = "Minimum height must be positive.";
            else if (!Finite(ArrivalScanSeconds) || ArrivalScanSeconds <= 0f) error = "Arrival scan interval must be positive.";
            else if (!Finite(CameraDistanceMetres) || CameraDistanceMetres < 0f) error = "Camera distance must be non-negative.";
            else if (!Finite(DefaultFootOffset)) error = "Default foot offset must be finite.";
            else if (ExcludedNames == null || ExcludedScenes == null || Classes == null) error = "Rule lists must exist.";
            else
            {
                foreach (var band in Bands)
                    if (!Enum.IsDefined(typeof(SunCasterBand), band)) { error = "Unknown sorting band."; break; }
                foreach (var rule in Classes)
                {
                    if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || rule.Names == null || rule.Names.Length == 0 || !Finite(rule.FootOffset))
                    { error = "Each class needs an id, name patterns and a finite foot offset."; break; }
                    foreach (var pattern in rule.Names)
                        if (string.IsNullOrWhiteSpace(pattern)) { error = "Empty class pattern."; break; }
                }
            }
            return error == null;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static bool MatchesAny(string name, string[] patterns)
        {
            if (patterns == null) return false;
            foreach (var pattern in patterns)
                if (Matches(name, pattern)) return true;
            return false;
        }

        // '*' is the only wildcard. Character comparisons avoid regex and temporary strings.
        public static bool Matches(string value, string pattern)
        {
            if (value == null || string.IsNullOrEmpty(pattern)) return false;
            int v = 0, p = 0, star = -1, retry = 0;
            while (v < value.Length)
            {
                if (p < pattern.Length && char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(value[v])) { v++; p++; }
                else if (p < pattern.Length && pattern[p] == '*') { star = p++; retry = v; }
                else if (star >= 0) { p = star + 1; v = ++retry; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }
}
