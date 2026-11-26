using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Basil Samson wears his own outfit (owner, 2026-11-25: outfit A, a cream knit jumper, charcoal
    /// trousers and a navy peaked cap) as a skin of his own, and Hector Bernard keeps the skipper's
    /// oilskins. Both ends of the chain are held: the data (NpcDef, build, visual, skin) and the one
    /// reference the St Peters scene draws Basil with, since a scene placed before the outfit existed
    /// would otherwise keep drawing the skipper.
    /// </summary>
    public class BasilOutfitTests
    {
        const string Npcs = "Assets/_Project/Data/NPCs";
        const string BasilVisualPath = "Assets/_Project/Data/Characters/BasilIso.asset";
        const string SkipperVisualPath = "Assets/_Project/Data/Characters/SkipperIso.asset";
        const string StPetersScene = "Assets/_Project/Scenes/StPeters.unity";

        static NpcDef Npc(string asset)
        {
            var npc = AssetDatabase.LoadAssetAtPath<NpcDef>($"{Npcs}/{asset}.asset");
            Assert.IsNotNull(npc, $"no NpcDef at {Npcs}/{asset}.asset");
            return npc;
        }

        [Test]
        public void BasilsFigure_ResolvesToHisOwnSkin()
        {
            NpcDef basil = Npc("BasilSamson");
            Assert.IsNotNull(basil.Build, "Basil has no build");
            Assert.AreEqual("char_build.basil", basil.Build.Id);
            Assert.AreEqual("basil", basil.Build.Preset);
            Assert.IsNotNull(basil.Build.Visual, "Basil's build has no visual def");
            Assert.AreEqual("visual.basil_iso", basil.Build.Visual.Id);
            Assert.IsNotNull(basil.Build.Visual.Skin, "Basil's visual def links no skin, so he would draw as sprites");
            Assert.AreEqual("charskin.basil", basil.Build.Visual.Skin.Id);
            Assert.AreEqual("basil", basil.Build.Visual.Skin.Preset);
            Assert.IsTrue(basil.Build.Visual.Skin.IsUsable(), "Basil's skin does not read as usable");
            Assert.IsTrue(basil.HasBakedBody, "Basil's build reads as no baked body");
        }

        [Test]
        public void HectorsFigure_KeepsTheSkippersOilskins()
        {
            NpcDef hector = Npc("HectorBernard");
            Assert.IsNotNull(hector.Build, "Hector has no build");
            Assert.AreEqual("char_build.skipper", hector.Build.Id);
            Assert.IsNotNull(hector.Build.Visual?.Skin, "Hector's visual def links no skin");
            Assert.AreEqual("charskin.skipper", hector.Build.Visual.Skin.Id);
            Assert.AreNotEqual("charskin.basil", hector.Build.Visual.Skin.Id);
        }

        [Test]
        public void BasilsSkin_IsNotTheSkippersAndDrawsTheSameStates()
        {
            CharacterSkinDef basil = Npc("BasilSamson").Build.Visual.Skin;
            CharacterSkinDef skipper = Npc("HectorBernard").Build.Visual.Skin;
            Assert.AreNotSame(skipper, basil);
            CollectionAssert.AreEquivalent(skipper.MeshStates, basil.MeshStates,
                "Basil should draw as a mesh in every state the skipper does, so he never drops to the " +
                "skipper's sprites mid-routine");
        }

        [Test]
        public void StPetersScene_DrawsBasilWithHisOwnVisual()
        {
            string basilGuid = AssetDatabase.AssetPathToGUID(BasilVisualPath);
            string skipperGuid = AssetDatabase.AssetPathToGUID(SkipperVisualPath);
            Assert.IsNotEmpty(basilGuid, $"{BasilVisualPath} is not in the asset database");

            string scene = File.ReadAllText(StPetersScene);
            // The BasilSamson GameObject's id, then the IsoCharacterSprite on that GameObject.
            Match go = Regex.Match(scene, @"--- !u!1 &(\d+)\r?\nGameObject:(?:(?!\n--- ).)*?m_Name: BasilSamson\r?\n",
                                   RegexOptions.Singleline);
            Assert.IsTrue(go.Success, "St Peters has no BasilSamson GameObject");
            string id = go.Groups[1].Value;
            Match sprite = Regex.Match(scene,
                @"m_GameObject: \{fileID: " + id + @"\}(?:(?!\n--- ).)*?IsoCharacterSprite\r?\n\s*_visual: \{fileID: \d+, guid: ([0-9a-f]{32})",
                RegexOptions.Singleline);
            Assert.IsTrue(sprite.Success, "Basil's GameObject in St Peters carries no IsoCharacterSprite");
            Assert.AreEqual(basilGuid, sprite.Groups[1].Value,
                sprite.Groups[1].Value == skipperGuid
                    ? "St Peters still draws Basil with the skipper's visual def (oilskins)"
                    : "St Peters draws Basil with a visual def that is not his");
        }
    }
}
