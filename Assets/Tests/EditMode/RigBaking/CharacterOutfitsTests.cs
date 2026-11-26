using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// An outfit is installed into a host's copy of the rig's BUILDS and nowhere else: the rig answers
    /// the outfit's fields as the table states them, keeps every other field of the base preset, and
    /// its own CAST10 is untouched, so every guard that walks the rig's presets walks what it did.
    /// </summary>
    public class CharacterOutfitsTests
    {
        static readonly string[] Structural =
        {
            "shape", "age", "height", "weight", "head", "skin", "hair", "hairStyle", "beard", "eyes",
            "eyeShape", "apronCol", "bottom", "sex", "frame",
        };

        [Test]
        public void EveryOutfit_AnswersItsFields_AndKeepsItsBasesBody()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
            string g = CharacterRigKit.Rig10.GlobalName;
            foreach (CharacterOutfits.Outfit o in CharacterOutfits.All)
            {
                if (o.Kit != CharacterRigKit.Rig10) continue;
                foreach ((string field, string value) in o.Changes)
                    Assert.AreEqual(value, host.EvaluateString($"String({g}.normBuild('{o.Key}').{field})"),
                        $"{o.Key}.{field}");
                foreach (string field in Structural)
                    Assert.AreEqual(
                        host.EvaluateString($"String({g}.normBuild('{o.BasePreset}').{field})"),
                        host.EvaluateString($"String({g}.normBuild('{o.Key}').{field})"),
                        $"{o.Key} changes {field}; an outfit changes clothes, not the person");
                Assert.DoesNotThrow(() => CharacterSkinExtractor.AssertPreset9(host, o.Key));
            }
        }

        [Test]
        public void Install_LeavesTheRigsOwnCastAsItShips()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10); // idempotent
            string[] cast = CharacterSkinExtractor.Presets9(host);
            Assert.AreEqual(10, cast.Length, string.Join(",", cast));
            foreach (CharacterOutfits.Outfit o in CharacterOutfits.All)
                CollectionAssert.DoesNotContain(cast, o.Key);
        }

        [Test]
        public void Basil_WearsOutfitA()
        {
            CharacterOutfits.Outfit basil = CharacterOutfits.Find(CharacterRigKit.Rig10, "basil");
            Assert.IsNotNull(basil);
            Assert.AreEqual("skipper", basil.BasePreset);
            Assert.AreEqual("skipper", CharacterOutfits.SidecarPresetFor(CharacterRigKit.Rig10, "basil"));
            CollectionAssert.AreEquivalent(new[]
            {
                ("garment", "sweater"), ("shirt", "cream"), ("outfit", "char"), ("hat", "captain"), ("hatCol", "navy"),
            }, basil.Changes);
        }
    }
}
