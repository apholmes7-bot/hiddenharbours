using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the character BUILDS — who the island's people are, as data — plus the
    /// one thing a content test normally cannot reach: <b>that the builder actually produces the
    /// wiring</b>.
    ///
    /// <para><b>"Scene-wired is not builder-wired" is the standing lesson here.</b> An NPC could be
    /// dressed by hand in the saved scene and look perfect until the next builder run silently
    /// undressed them. So this file does not check the scene at all. It checks the two things that
    /// survive a rebuild: the DATA (a build per baked preset, a build on every mapped NpcDef) and the
    /// PLACEMENT CODE (<see cref="NpcBodyDresser"/> given that data really does produce an animated
    /// body). Between them, a rebuild cannot quietly lose a face.</para>
    /// </summary>
    public class CharacterBuildContentTests
    {
        const string BuildsFolder = "Assets/_Project/Data/Characters/Builds";
        const string DataNpcs = "Assets/_Project/Data/NPCs";
        const string RampsPath = "Assets/_Project/Data/Characters/CharacterRamps.asset";

        static CharacterBuildDef[] Builds() =>
            AssetDatabase.FindAssets("t:CharacterBuildDef", new[] { BuildsFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<CharacterBuildDef>)
                         .Where(b => b != null)
                         .OrderBy(b => b.Id, System.StringComparer.Ordinal)
                         .ToArray();

        static CharacterRampsDef Ramps() =>
            AssetDatabase.LoadAssetAtPath<CharacterRampsDef>(RampsPath);

        static NpcDef Npc(string assetName) =>
            AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{assetName}.asset");

        // ---- the builds are well-formed data ---------------------------------------------------

        [Test]
        public void EveryBakedPreset_HasABuildAsset()
        {
            var byPreset = Builds().ToDictionary(b => b.Preset, b => b, System.StringComparer.Ordinal);
            foreach (var (preset, _) in CharacterBuildsBuilder.Presets)
                Assert.IsTrue(byPreset.ContainsKey(preset),
                    $"No CharacterBuildDef for baked preset '{preset}' under {BuildsFolder}. Run " +
                    "Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Character Builds + " +
                    "Cast Mapping and commit the assets.");
        }

        [Test]
        public void EveryBuildId_IsUnique_AndCorrectlyShaped()
        {
            var seen = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var shape = new Regex(@"^char_build\.[a-z0-9]+(_[a-z0-9]+)*$");

            foreach (var b in Builds())
            {
                Assert.IsTrue(shape.IsMatch(b.Id ?? ""),
                    $"'{AssetDatabase.GetAssetPath(b)}' has id '{b.Id}' — build ids are " +
                    "char_build.snake_case and append-only (CLAUDE.md §5).");
                Assert.IsFalse(seen.ContainsKey(b.Id),
                    $"Duplicate build id '{b.Id}': {AssetDatabase.GetAssetPath(b)} collides with an " +
                    "asset already seen. Ids resolve saves — two assets cannot share one.");
                seen[b.Id] = AssetDatabase.GetAssetPath(b);
            }
        }

        [Test]
        public void EveryBuild_NamesAPresetTheRigActuallyBakes()
        {
            var baked = CharacterBuildsBuilder.Presets.Select(p => p.preset).ToArray();
            foreach (var b in Builds())
                Assert.Contains(b.Preset, baked,
                    $"'{b.Id}' names preset '{b.Preset}', which is not one the bake produces " +
                    $"({string.Join(", ", baked)}). A build with no body behind it places an " +
                    "invisible person.");
        }

        [Test]
        public void EveryBuild_HasItsBakedSheetsWired()
        {
            foreach (var b in Builds())
            {
                Assert.IsNotNull(b.Visual,
                    $"'{b.Id}' has no CharacterVisualDef. Re-run Build Character Visual Defs, then " +
                    "Build Character Builds + Cast Mapping — a re-slice changes the sprite sub-asset " +
                    "ids and the def's refs go stale.");
                Assert.IsTrue(b.Visual.HasAnyArt(),
                    $"'{b.Id}' points at '{b.Visual.Id}', whose sheets are EMPTY. The preset's PNGs " +
                    "are missing or unsliced.");
                Assert.IsTrue(b.HasBakedBody, $"'{b.Id}' does not read as a complete build.");
            }
        }

        [Test]
        public void EveryColourOverride_ResolvesAgainstTheRamps()
        {
            // Every generated build ships with EMPTY colour keys (meaning "the preset's own"), so this
            // passes vacuously today — and that is exactly when to install it. The first hand-authored
            // build, or the first wardrobe purchase writing a key, is when a typo would otherwise
            // reach the point of use as a null ramp.
            var ramps = Ramps();
            Assert.IsNotNull(ramps, $"No CharacterRampsDef at {RampsPath} — run Build Character " +
                                    "Colour Ramps and commit it.");

            foreach (var b in Builds())
            foreach (var (axis, key) in b.ColourOverrides())
            {
                Assert.IsNotNull(ramps.Axis(axis),
                    $"'{b.Id}' sets colour axis '{axis}', which the ramps do not carry.");
                Assert.IsNotNull(ramps.Ramp(axis, key),
                    $"'{b.Id}' sets {axis} = '{key}', which is not a ramp the rig ships. Legal " +
                    $"values: {string.Join(", ", ramps.Axis(axis).Ramps.Select(r => r.Key))}.");
            }
        }

        // ---- the cast mapping is real -----------------------------------------------------------

        [Test]
        public void EveryMappedNpc_ExistsAndWearsItsMappedBuild()
        {
            foreach (var (npcAsset, preset, why) in CharacterBuildsBuilder.CastMapping)
            {
                var npc = Npc(npcAsset);
                Assert.IsNotNull(npc, $"The cast mapping dresses '{npcAsset}' ({why}) but there is no " +
                                      $"NpcDef at {DataNpcs}/{npcAsset}.asset.");
                Assert.IsNotNull(npc.Build,
                    $"{npc.DisplayName} has no Build. The mapping says '{preset}' — re-run Build " +
                    "Character Builds + Cast Mapping and COMMIT the NpcDef assets it wrote onto. " +
                    "(This is the 'builder-wired' half: dressing them in the scene would not survive " +
                    "the next rebuild.)");
                Assert.AreEqual(preset, npc.Build.Preset,
                    $"{npc.DisplayName} wears '{npc.Build.Preset}' but the mapping says '{preset}'. " +
                    "The table in CharacterBuildsBuilder is the single source — re-run it.");
                Assert.IsTrue(npc.HasBakedBody,
                    $"{npc.DisplayName}'s build has no baked sheets behind it, so the region builder " +
                    "will fall back to a greybox standee.");
            }
        }

        [Test]
        public void NpcsWithNoBuild_AreStillLegal()
        {
            // The append-only promise, asserted rather than assumed: a def with no Build must remain a
            // valid NPC. Ned's Letter is not a person and must never grow a body.
            var letter = Npc("NedLetter");
            if (letter == null) Assert.Ignore("NedLetter.asset is not in this branch.");

            Assert.IsNull(letter.Build,
                "Ned's Letter is a thing you READ, not someone who stands there. Giving it a " +
                "character build would put a person where the letter is.");
            Assert.IsFalse(letter.HasBakedBody);
        }

        // ---- the PLACEMENT code produces a body, not just the data -------------------------------

        [Test]
        public void Dressing_AnNpcWithABuild_ProducesAnAnimatedBody_NotAGreybox()
        {
            var npc = Npc("BasilSamson");
            if (npc == null || !npc.HasBakedBody)
                Assert.Ignore("BasilSamson has no baked build in this branch — the data tests above " +
                              "own that failure.");

            var go = new GameObject("dress-test");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var body = NpcBodyDresser.Dress(go, sr, npc, artStem: "Basil", greyboxSquare: null,
                                                greyboxTint: Color.magenta, headingDegrees: 90f);

                Assert.AreEqual(NpcBodyDresser.Body.BakedBuild, body,
                    "a person with a baked build must not fall through to a static sprite or the " +
                    "greybox — the ladder picked the wrong rung");

                var iso = go.GetComponent<IsoCharacterSprite>();
                Assert.IsNotNull(iso, "no IsoCharacterSprite — the body would never animate");
                Assert.AreSame(npc.Build.Visual, iso.Visual, "the wrong skin got wired");
                Assert.IsTrue(iso.HasArt, "the wired skin has no art");

                Assert.IsNotNull(sr.sprite,
                    "the renderer was not seeded with a first cell — an editor-placed component never " +
                    "runs Awake, so the island would look empty in the scene view until Play");
                Assert.AreEqual(Color.white, sr.color, "the greybox tint was left on real art");
                Assert.AreEqual(Vector3.one, go.transform.localScale,
                    "the greybox's 1x2 stretch was left on a PPU-32 sheet — a nine-foot islander");

                // The heading really landed on the serialized field (a plain field write would not
                // survive the scene save, which is the whole reason it goes in via SerializedObject).
                var so = new SerializedObject(iso);
                Assert.AreEqual(90f, so.FindProperty("_initialHeadingDegrees").floatValue, 1e-4f,
                    "the placement's heading did not reach the component");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Dressing_AnNpcWithoutABuild_StillFallsBackCleanly()
        {
            // The append-only promise at the placement layer: null Build changes nothing.
            var npc = ScriptableObject.CreateInstance<NpcDef>();
            var go = new GameObject("dress-test-fallback");
            var square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var body = NpcBodyDresser.Dress(go, sr, npc, artStem: "NoSuchPerson",
                                                greyboxSquare: square, greyboxTint: Color.red,
                                                headingDegrees: 180f);

                Assert.AreEqual(NpcBodyDresser.Body.Greybox, body);
                Assert.IsNull(go.GetComponent<IsoCharacterSprite>(),
                              "a buildless NPC must not gain an animator");
                Assert.AreSame(square, sr.sprite);
                Assert.AreEqual(Color.red, sr.color);
                Assert.AreEqual(new Vector3(1f, 2f, 1f), go.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(square);
                Object.DestroyImmediate(npc);
            }
        }
    }
}
