using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who a character IS, as data</b> — the build a person wears: which baked body, and which
    /// colours over it. One asset per file under <c>Data/Characters/Builds</c>, keyed by a stable,
    /// append-only <see cref="Id"/> (<c>char_build.snake_case</c>).
    ///
    /// <para><b>The shape follows ADR 0029, and the shape IS the ruling.</b> The character's parameter
    /// space splits in two, because the rig's does:</para>
    ///
    /// <list type="bullet">
    ///   <item><b><see cref="Preset"/> is the structure</b> — sex, age, garment, hat, hair, beard,
    ///   proportions. It names one of the rig's own cast builds, and therefore one BAKED sheet set.
    ///   Nothing at runtime can draw a body the editor did not bake (ADR 0021: no JavaScript ships),
    ///   so structure is a choice among sheets, not a set of dials.</item>
    ///   <item><b>The colour keys are free</b> — a ramp swap, not a render. This is what lets a shirt
    ///   be bought, worn and changed without an art pass.</item>
    /// </list>
    ///
    /// <para><b>There are deliberately NO structural override fields</b> (a garment or hat picked
    /// separately from the preset). The layer-separation spike measured the rig and found the
    /// structural axes integrated — an overlay baked over one body does not composite onto another
    /// (ADR 0029, Outcome B). A <c>Garment</c> field here would be a Def promising art that does not
    /// exist, resolving to a silently different body at the point of use. If a later pass-7 export
    /// isolates layers, those fields are an append-only addition to this asset.</para>
    ///
    /// <para><b>An empty colour key means "the preset's own".</b> A build is an override list, not a
    /// full specification, so <c>{preset: nan}</c> with nothing else is a complete, legal build and
    /// reads exactly as the art director drew her.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Build", fileName = "CharacterBuild")]
    public class CharacterBuildDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (char_build.snake_case, e.g. char_build.skipper). " +
                 "Content-validated for uniqueness and shape.")]
        public string Id = "char_build.example";

        [Header("Structure — a BAKED body, not a set of dials (ADR 0029)")]
        [Tooltip("A key of the rig's own BUILDS/CAST table: ginny, skipper, nan, deckboss, packer, " +
                 "cutter, hand, boy, girl (fisher is the player). This names the baked sheet set.")]
        public string Preset = "";

        [Tooltip("The baked sheets for that preset. Wired by CharacterBuildsBuilder from the preset " +
                 "key, so the world layer never holds an art path (rule 2). Null = no baked body " +
                 "yet, and whoever places this character keeps whatever drew them before.")]
        public CharacterVisualDef Visual;

        [Header("Colour — a runtime ramp swap. Empty means 'the preset's own'.")]
        [Tooltip("Key into CharacterRampsDef's skin axis.")]     public string Skin = "";
        [Tooltip("Key into CharacterRampsDef's hair axis.")]     public string Hair = "";
        [Tooltip("Key into CharacterRampsDef's outfit axis.")]   public string Outfit = "";
        [Tooltip("Key into CharacterRampsDef's shirt axis.")]    public string Shirt = "";
        [Tooltip("Key into CharacterRampsDef's hatCol axis.")]   public string HatCol = "";
        [Tooltip("Key into CharacterRampsDef's apronCol axis.")] public string ApronCol = "";
        [Tooltip("Key into CharacterRampsDef's eyes axis.")]     public string Eyes = "";

        /// <summary>True when this build names a preset AND has the sheets to draw it.</summary>
        public bool HasBakedBody => !string.IsNullOrEmpty(Preset) && Visual != null && Visual.HasAnyArt();

        /// <summary>
        /// The colour overrides as (axis, key) pairs, skipping the empty ones — the exact list a ramp
        /// swap has to apply, and the exact list content validation has to check against
        /// <see cref="CharacterRampsDef"/>. One place, so the two can never disagree about what an
        /// axis is called.
        /// </summary>
        public (string axis, string key)[] ColourOverrides()
        {
            var all = new (string axis, string key)[]
            {
                ("skin", Skin), ("hair", Hair), ("outfit", Outfit), ("shirt", Shirt),
                ("hatCol", HatCol), ("apronCol", ApronCol), ("eyes", Eyes),
            };

            int n = 0;
            foreach (var p in all) if (!string.IsNullOrEmpty(p.key)) n++;
            var set = new (string, string)[n];
            int i = 0;
            foreach (var p in all) if (!string.IsNullOrEmpty(p.key)) set[i++] = p;
            return set;
        }
    }
}
