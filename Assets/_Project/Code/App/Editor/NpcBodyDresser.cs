#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The one place a placed NPC gets a body.</b> Both region builders
    /// (<see cref="StPetersInhabitants"/>, <see cref="NineMileCreekPeople"/>) place people the same
    /// way and used to fall back to a tinted rectangle the same way; now that there is a third,
    /// better option they share it rather than each growing their own copy — the drift between two
    /// copies of a placement rule is exactly what #345 cost.
    ///
    /// <para><b>Best available, in order:</b> the NpcDef's baked <see cref="CharacterBuildDef"/> →
    /// a static sprite at the conventional <c>Art/Characters/&lt;stem&gt;.png</c> → the greybox
    /// rectangle. Every step down is legal and visible; none of them is silent.</para>
    /// </summary>
    public static class NpcBodyDresser
    {
        const string ArtChars = "Assets/_Project/Art/Characters";

        /// <summary>What a person ended up wearing — for the builder's own log line.</summary>
        public enum Body { Greybox, StaticSprite, BakedBuild }

        /// <summary>
        /// Dress a placed person's <see cref="SpriteRenderer"/>.
        ///
        /// <para><b>Scale stays 1 for real art.</b> The greybox stretches to 1 × 2 m because a square
        /// marker has to stand for a person; the baked sheets are PPU 32 with the pivot on ground
        /// contact, so their metre size is already right and inheriting that stretch would make a
        /// nine-foot islander.</para>
        /// </summary>
        /// <param name="go">The person's GameObject (already positioned).</param>
        /// <param name="sr">Its renderer (sorting order already set by the caller).</param>
        /// <param name="npc">The def — null, or one without a build, falls through.</param>
        /// <param name="artStem">Stem for the conventional static-sprite path.</param>
        /// <param name="greyboxSquare">The last-resort marker.</param>
        /// <param name="greyboxTint">Its tint.</param>
        /// <param name="headingDegrees">Which way a baked body is turned (0 = N, CW).</param>
        public static Body Dress(GameObject go, SpriteRenderer sr, NpcDef npc, string artStem,
                                 Sprite greyboxSquare, Color greyboxTint, float headingDegrees)
        {
            if (npc != null && npc.HasBakedBody)
            {
                WearBakedBody(go, sr, npc.Build, headingDegrees);
                return Body.BakedBuild;
            }

            var art = string.IsNullOrEmpty(artStem)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtChars}/{artStem}.png");
            if (art != null)
            {
                sr.sprite = art;
                go.transform.localScale = Vector3.one;
                return Body.StaticSprite;
            }

            sr.sprite = greyboxSquare;
            sr.color = greyboxTint;
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            return Body.Greybox;
        }

        /// <summary>
        /// The shared <see cref="IsoCharacterSprite"/>, wired to the build's sheets and turned the way
        /// the placement asked for. It is the SAME component the player uses — an anchored NPC never
        /// moves, so it measures no speed and simply breathes through the idle cycle, and the day a
        /// routine walks them the walk sheet plays with no code change.
        ///
        /// <para>⚠️ The heading goes in through <see cref="SerializedObject"/>, not a field write. A
        /// plain write to a private serialized field on an editor-created component does not survive
        /// into the saved scene, and a builder whose wiring evaporates on save is precisely the
        /// "scene-wired is not builder-wired" defect. <c>Configure</c> needs no such ceremony because
        /// Unity serializes what it sets.</para>
        ///
        /// <para>The renderer is also SEEDED with the first cell, because an editor-placed component
        /// never runs <c>Awake</c>: without it the island looks empty in the scene view until Play.</para>
        /// </summary>
        static void WearBakedBody(GameObject go, SpriteRenderer sr, CharacterBuildDef build,
                                  float headingDegrees)
        {
            go.transform.localScale = Vector3.one;
            sr.color = Color.white;   // no greybox tint over real art

            var iso = go.AddComponent<IsoCharacterSprite>();
            iso.Configure(build.Visual);

            var so = new SerializedObject(iso);
            var heading = so.FindProperty("_initialHeadingDegrees");
            if (heading != null) heading.floatValue = headingDegrees;
            so.ApplyModifiedPropertiesWithoutUndo();

            int row = build.Visual.FacingRowFor(headingDegrees);
            var first = build.Visual.SpriteFor(CharacterGait.Idle, row, 0);
            if (first != null) sr.sprite = first;
        }
    }
}
#endif
