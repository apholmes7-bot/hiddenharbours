using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE TONE RULE AND ITS CAP, ON THE DEF</b> (<see cref="CharacterSkinDef.ToneRule"/>). Rig 7 keeps
    /// its sixteen materials and its validity exactly as they were. V9 carries up to thirty-two, each with
    /// a four-tone window that must be the right way round. A def that never set the rule is rig 7, so
    /// every asset baked before the field existed is drawn as it was.
    ///
    /// <para>Synthetic defs only. No shipped asset is read here: the character intake builds the v9 skins
    /// later, as data.</para>
    /// </summary>
    public sealed class CharacterSkinDefToneRuleTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        static CharacterSkinDef.Material[] Materials(int count)
        {
            var mats = new CharacterSkinDef.Material[count];
            for (int m = 0; m < count; m++)
                mats[m] = new CharacterSkinDef.Material
                {
                    Name = "m" + m,
                    Colors = new[]
                    {
                        new Color32(20, 30, 40, 255), new Color32(60, 70, 80, 255), new Color32(100, 110, 120, 255),
                    },
                    Gain = 1f,
                    Bias = float.NaN,
                    FixedIndex = -1,
                    ToneLo = 1,
                    ToneHi = 2,
                };
            return mats;
        }

        /// <summary>The smallest def <see cref="CharacterSkinDef.IsUsable"/> accepts: one bone, one
        /// one-frame clip, and <paramref name="materials"/> three-colour materials with the window 1..2.</summary>
        CharacterSkinDef UsableDef(ToneRule rule, int materials)
        {
            CharacterSkinDef def = Track(ScriptableObject.CreateInstance<CharacterSkinDef>());
            def.Id = "charskin.test_tone_rule";
            def.BindMesh = Track(new Mesh { name = "HHToneRuleTestBind", hideFlags = HideFlags.HideAndDontSave });
            def.Bones = new[]
            {
                new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone, OwnsVertex = true },
            };
            def.MaxInfluences = 1;
            def.Bayer16 = new float[16];
            def.Clips = new[]
            {
                new CharacterSkinDef.SkinClip
                {
                    Anim = "idle", FramesPerSecond = 12f, FrameCount = 1,
                    Keys = new[] { new CharacterSkinDef.BoneKey { Rotation = Quaternion.identity } },
                },
            };
            def.Materials = Materials(materials);
            def.ToneRule = rule;
            return def;
        }

        [Test]
        public void ADefThatNeverSetTheRule_IsRig7()
        {
            // Absence is data. A new instance runs the field initialiser; a def serialised before the
            // field existed carries no value for it, and loading such a payload leaves the initialiser's.
            CharacterSkinDef fresh = Track(ScriptableObject.CreateInstance<CharacterSkinDef>());
            Assert.AreEqual(ToneRule.Rig7, fresh.ToneRule, "a new def");

            CharacterSkinDef old = Track(ScriptableObject.CreateInstance<CharacterSkinDef>());
            JsonUtility.FromJsonOverwrite("{\"Id\":\"charskin.before_the_field\"}", old);
            Assert.AreEqual("charskin.before_the_field", old.Id,
                "the payload did not load, so the next assertion would prove nothing");
            Assert.AreEqual(ToneRule.Rig7, old.ToneRule, "a def loaded from a payload with no ToneRule in it");

            // The rule is serialised as its number, so the numbers are append-only.
            Assert.AreEqual(ToneRule.Rig7, default(ToneRule), "Rig7 must stay the zero a missing value reads as");
            Assert.AreEqual(1, (int)ToneRule.V9, "V9 is serialised as 1; never renumber it");
        }

        [Test]
        public void MaxMaterials_IsSixteenForRig7_ThirtyTwoForV9_AndNoneForAnUnknownRule()
        {
            Assert.AreEqual(16, CharacterSkinDef.RampSlots,
                "RampSlots is rig 7's cap and the fleet's _RampMeta width; this PR does not move it");
            Assert.AreEqual(32, CharacterSkinDef.V9RampSlots);
            Assert.AreEqual(CharacterSkinDef.RampSlots, CharacterSkinDef.MaxMaterials(ToneRule.Rig7));
            Assert.AreEqual(CharacterSkinDef.V9RampSlots, CharacterSkinDef.MaxMaterials(ToneRule.V9));
            Assert.AreEqual(0, CharacterSkinDef.MaxMaterials((ToneRule)2),
                "a rule this build does not know gets no table, rather than a guessed width");
        }

        [Test]
        public void ARig7Def_CarriesSixteenMaterials_NotSeventeen()
        {
            CharacterSkinDef def = UsableDef(ToneRule.Rig7, 16);
            Assert.IsTrue(def.IsUsable(), "the control: rig 7 at its cap");

            def.Materials = Materials(17);
            Assert.IsFalse(def.IsUsable(), "rig 7 with seventeen materials: its _RampMeta is float4[16]");
        }

        [Test]
        public void AV9Def_CarriesThirtyTwoMaterials_NotThirtyThree()
        {
            CharacterSkinDef def = UsableDef(ToneRule.V9, 32);
            Assert.IsTrue(def.IsUsable(), "v9 at its cap: the kit's cast paints thirty-two");

            def.Materials = Materials(33);
            Assert.IsFalse(def.IsUsable(), "v9 with thirty-three: the HH_FIGURE tables are float4[32]");
        }

        [Test]
        public void AV9Window_MustBeTheRightWayRound_AndNotBelowZero()
        {
            CharacterSkinDef def = UsableDef(ToneRule.V9, 3);
            Assert.IsTrue(def.IsUsable(), "the control: every window 1..2");

            def.Materials[1].ToneLo = 3;
            Assert.IsFalse(def.IsUsable(), "ToneLo 3 > ToneHi 2: the window is inside out");

            def.Materials[1].ToneLo = 2;
            Assert.IsTrue(def.IsUsable(), "ToneLo == ToneHi is a window one tone wide");

            def.Materials[1].ToneLo = -1;
            Assert.IsFalse(def.IsUsable(), "ToneLo −1: a tone below the ramp's darkest");

            // A v9 fixed material (ink, brow, lid): one colour, window 0..0, offset 0, and never FixedIndex.
            def.Materials[1] = new CharacterSkinDef.Material
            {
                Name = "ink", Colors = new[] { new Color32(16, 26, 25, 255) },
                Gain = 1f, Bias = float.NaN, FixedIndex = -1, ToneLo = 0, ToneHi = 0, Offset = 0,
            };
            Assert.IsTrue(def.IsUsable(), "a v9 fixed material is a one-colour ramp with the window 0..0");
        }

        [Test]
        public void ARig7Def_IgnoresTheWindow()
        {
            CharacterSkinDef def = UsableDef(ToneRule.Rig7, 3);
            def.Materials[0].ToneLo = 5;
            def.Materials[0].ToneHi = -5;
            Assert.IsTrue(def.IsUsable(), "rig 7 carries no window, and its validity is exactly what it was");
        }

        [Test]
        public void ADefWithARuleThisBuildDoesNotKnow_IsRefused()
        {
            CharacterSkinDef def = UsableDef((ToneRule)2, 1);
            Assert.IsFalse(def.IsUsable(),
                "an unknown tone rule must be refused, not drawn with some other rule's table");
        }
    }
}
