using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE SWIMMING FISH'S ART TABLE (content is data — CLAUDE.md rule 2): which baked cell draws each
    /// species, anim, heading and frame, which visual kind a species Def id maps to, and how long each
    /// species is drawn. One asset the owner can rewire without touching code.
    ///
    /// <para><b>Where the cells come from.</b> The catch pass 2 bake
    /// (<c>Art/Fishing/Iso/Fish_&lt;kind&gt;_&lt;anim&gt;.png</c>, sliced
    /// <c>Fish_&lt;kind&gt;_&lt;anim&gt;_d&lt;dir&gt;_f&lt;frame&gt;</c> on the rig's 64x64 grid at
    /// pivot 32,38 and 32 px = 1 m). Sprite references, not a texture — unlike the wake plumes these
    /// sheets ARE sliced into named sub-sprites, so the slice is the thing to reference and there is no
    /// full-image sprite to build. ⚠ Sprite refs resolve by the slice's <c>internalID</c>, so a re-slice
    /// that renumbers the cells must be followed by a rebuild of this asset.</para>
    ///
    /// <para><b>Lengths live here, not on the Def.</b> <see cref="LengthMetresFor"/> is the rig's own
    /// <c>SPECIES.len</c> — the length the art was DRAWN at, which is what the shoal maths needs to space
    /// fish apart. It is an art constant, not a balance number: <c>FishSpeciesDef</c> owns the weights the
    /// market pays for, and neither should be derived from the other.</para>
    ///
    /// <para>Pure content metadata — never serialized into a save, no determinism concern. Lives in
    /// <c>Resources</c> so the presenter can load it with no scene wiring, the
    /// <c>WakeSpriteLibrary</c> pattern.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FishSwimSpriteLibrary",
                     menuName = "Hidden Harbours/Fishing/Fish Swim Sprite Library")]
    public sealed class FishSwimSpriteLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the presenter loads the library from.</summary>
        public const string ResourcesPath = "FishSwimSpriteLibrary";

        /// <summary>The rig's eight-direction turntable — the row count of every baked sheet.</summary>
        public const int Dirs = 8;

        /// <summary>Anim keys, exactly the rig's <c>AORDER</c>.</summary>
        public const string AnimSwim = "swim";
        public const string AnimDart = "dart";
        public const string AnimThrash = "thrash";
        public const string AnimShadow = "shadow";
        public const string AnimRoll = "roll";
        public const string AnimJump = "jump";

        /// <summary>One baked sheet: every cell of one species' one anim.</summary>
        [Serializable]
        public sealed class AnimEntry
        {
            [Tooltip("Rig species key: cod / haddock / pollock / mackerel / bass / flounder / herring")]
            public string Kind;

            [Tooltip("Rig anim key: swim / dart / thrash / shadow / roll / jump")]
            public string Anim;

            [Tooltip("Frames per direction (the rig's ANIMS.<anim>.n)")]
            public int Frames;

            [Tooltip("The sheet's cells, indexed [dir * Frames + frame] — 8 dirs of Frames each")]
            public Sprite[] Cells;
        }

        /// <summary>One species Def id and the visual kind it draws as.</summary>
        [Serializable]
        public sealed class SpeciesEntry
        {
            [Tooltip("Stable species Def id, e.g. fish.atlantic_cod")]
            public string SpeciesId;

            [Tooltip("The rig species key it draws as (a Kind above)")]
            public string Kind;
        }

        /// <summary>One kind's drawn length — the rig's SPECIES.len, in metres at scale 1.</summary>
        [Serializable]
        public sealed class KindLength
        {
            public string Kind;

            [Tooltip("Metres at scale 1 — the rig's SPECIES.len")]
            public float LengthMetres = 0.5f;
        }

        [SerializeField, Tooltip("Every baked (kind, anim) sheet")]
        private AnimEntry[] _anims;

        [SerializeField, Tooltip("Species Def id → rig species key")]
        private SpeciesEntry[] _species;

        [SerializeField, Tooltip("The rig's drawn length per kind, metres at scale 1")]
        private KindLength[] _lengths;

        [SerializeField, Tooltip("Kind for a species with no mapping — an unmapped fish should still " +
                                 "SHOW as something rather than leave a hole in the sea")]
        private string _fallbackKind = "cod";

        [NonSerialized] private Dictionary<string, AnimEntry> _animLookup;
        [NonSerialized] private Dictionary<string, string> _speciesLookup;
        [NonSerialized] private Dictionary<string, float> _lengthLookup;

        private void OnEnable()
        {
            _animLookup = null;
            _speciesLookup = null;
            _lengthLookup = null;
        }

        private static string Key(string kind, string anim) => kind + "/" + anim;

        private void BuildLookups()
        {
            _animLookup = new Dictionary<string, AnimEntry>(StringComparer.Ordinal);
            if (_anims != null)
                foreach (AnimEntry e in _anims)
                    if (e != null && !string.IsNullOrEmpty(e.Kind) && !string.IsNullOrEmpty(e.Anim))
                        _animLookup[Key(e.Kind, e.Anim)] = e;

            _speciesLookup = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_species != null)
                foreach (SpeciesEntry e in _species)
                    if (e != null && !string.IsNullOrEmpty(e.SpeciesId) && !string.IsNullOrEmpty(e.Kind))
                        _speciesLookup[e.SpeciesId] = e.Kind;

            _lengthLookup = new Dictionary<string, float>(StringComparer.Ordinal);
            if (_lengths != null)
                foreach (KindLength e in _lengths)
                    if (e != null && !string.IsNullOrEmpty(e.Kind))
                        _lengthLookup[e.Kind] = e.LengthMetres;
        }

        /// <summary>
        /// One cell, or null when that sheet is not wired yet (the presenter then draws nothing rather
        /// than a blank quad). <paramref name="dir"/> and <paramref name="frame"/> are wrapped, so a
        /// caller can hand over a raw heading row or an anim frame without pre-clamping.
        /// </summary>
        public Sprite Cell(string kind, string anim, int dir, int frame)
        {
            if (_animLookup == null) BuildLookups();
            if (kind == null || anim == null) return null;
            if (!_animLookup.TryGetValue(Key(kind, anim), out AnimEntry e)) return null;
            if (e.Cells == null || e.Cells.Length == 0 || e.Frames <= 0) return null;

            int d = dir % Dirs; if (d < 0) d += Dirs;
            int f = frame % e.Frames; if (f < 0) f += e.Frames;

            int i = d * e.Frames + f;
            return i >= 0 && i < e.Cells.Length ? e.Cells[i] : null;
        }

        /// <summary>How many frames one (kind, anim) sheet holds, or 0 when it is not wired.</summary>
        public int FramesFor(string kind, string anim)
        {
            if (_animLookup == null) BuildLookups();
            if (kind == null || anim == null) return 0;
            return _animLookup.TryGetValue(Key(kind, anim), out AnimEntry e) ? Mathf.Max(0, e.Frames) : 0;
        }

        /// <summary>Is there art for this (kind, anim) at all?</summary>
        public bool Has(string kind, string anim) => FramesFor(kind, anim) > 0;

        /// <summary>The rig species key a species Def id draws as (mapped, else the fallback).</summary>
        public string KindFor(string speciesId)
        {
            if (_speciesLookup == null) BuildLookups();
            return speciesId != null && _speciesLookup.TryGetValue(speciesId, out string kind)
                ? kind
                : _fallbackKind;
        }

        /// <summary>
        /// The kind this species draws as, or FALSE when the table does not map it — <b>the door a
        /// shellfish is turned away at</b>.
        ///
        /// <para>⚠ Why this exists beside <see cref="KindFor"/>. That method falls back to a default kind,
        /// which is right for a catch ITEM (an unmapped fish should still make the tote look fuller rather
        /// than vanish) and badly wrong for the water: a region pool holds clams, lobster and crab as well
        /// as finfish, and a school whose species is the soft-shell clam would otherwise be drawn as a
        /// shoal of swimming COD. A caller that is drawing something ALIVE IN THE WATER COLUMN asks this
        /// one and draws nothing when the answer is no.</para>
        /// </summary>
        public bool TrySwimKindFor(string speciesId, out string kind)
        {
            if (_speciesLookup == null) BuildLookups();
            kind = null;
            if (string.IsNullOrEmpty(speciesId)) return false;
            return _speciesLookup.TryGetValue(speciesId, out kind) && Has(kind, AnimSwim);
        }

        /// <summary>The kind's drawn length in metres at scale 1 (the rig's <c>SPECIES.len</c>).</summary>
        public float LengthMetresFor(string kind)
        {
            if (_lengthLookup == null) BuildLookups();
            return kind != null && _lengthLookup.TryGetValue(kind, out float m) && m > 0f ? m : 0.5f;
        }

        /// <summary>
        /// Wire the whole table in one call — the door the editor builder and the tests come in by. The
        /// arrays are private serialized fields because they are the OWNER's to edit in an inspector,
        /// not an API; this invalidates the caches so a re-wired table is read fresh.
        /// </summary>
        public void Configure(AnimEntry[] anims, SpeciesEntry[] species, KindLength[] lengths,
                              string fallbackKind = null)
        {
            if (anims != null) _anims = anims;
            if (species != null) _species = species;
            if (lengths != null) _lengths = lengths;
            if (!string.IsNullOrEmpty(fallbackKind)) _fallbackKind = fallbackKind;
            _animLookup = null;
            _speciesLookup = null;
            _lengthLookup = null;
        }
    }
}
