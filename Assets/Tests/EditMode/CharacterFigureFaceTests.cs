using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WHICH THREE FACE GROUPS A FIGURE SHOWS</b> (<see cref="CharacterFigureFace"/>): the clip
    /// frame's own face, then a gaze over the OPEN eyes only, then a blink over any eyes but the ones the
    /// def skips — judged on the frame's own eyes, never on the gaze's.
    ///
    /// <para>A synthetic def with a face of its own (nine groups, not the rig's thirteen), so these hold
    /// the ORDER of the three rules. That the shipped defs carry the rig's groups, rest face and face
    /// tracks is <c>CharacterSkinBakeGuardTests</c>'s, against the rig in V8.</para>
    /// </summary>
    public sealed class CharacterFigureFaceTests
    {
        const int Open = 1, Half = 2, Shut = 3, Left = 4, Right = 5;
        const int BrowsFlat = 6, BrowsUp = 7, MouthFlat = 8, MouthSmile = 9;
        const int None = CharacterSkinDef.NoFaceGroup;

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        CharacterSkinDef Def()
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            _made.Add(def);
            def.Id = "skin.face_test";
            def.FaceGroups = new[]
            {
                "eyes.open", "eyes.half", "eyes.shut", "eyes.left", "eyes.right",
                "brows.flat", "brows.up", "mouth.flat", "mouth.smile",
            };
            def.RestFace = new[] { Open, BrowsFlat, MouthFlat };
            def.LookEyes = new Vector3Int(Open, Left, Right);
            def.BlinkSkipGroups = new[] { Shut };
            return def;
        }

        /// <summary>Three frames: open and smiling, half, shut.</summary>
        static CharacterSkinDef.SkinClip Clip() => new CharacterSkinDef.SkinClip
        {
            Anim = "idle",
            FrameCount = 3,
            Face = new byte[]
            {
                Open, BrowsUp, MouthSmile,
                Half, BrowsFlat, MouthFlat,
                Shut, BrowsFlat, MouthFlat,
            },
        };

        static (int eyes, int brows, int mouth) Show(CharacterSkinDef def, CharacterSkinDef.SkinClip clip, int frame,
                                                    int gaze = CharacterFigureLook.GazeOpen, int blink = None)
        {
            CharacterFigureFace.Compose(def, clip, frame, gaze, blink, out int eyes, out int brows, out int mouth);
            return (eyes, brows, mouth);
        }

        [Test]
        public void EachFrameShowsItsOwnFace()
        {
            CharacterSkinDef def = Def();
            CharacterSkinDef.SkinClip clip = Clip();
            Assert.AreEqual((Open, BrowsUp, MouthSmile), Show(def, clip, 0));
            Assert.AreEqual((Half, BrowsFlat, MouthFlat), Show(def, clip, 1));
            Assert.AreEqual((Shut, BrowsFlat, MouthFlat), Show(def, clip, 2));
        }

        [Test]
        public void AClipWithNoWellFormedTrackShowsTheRestFace()
        {
            CharacterSkinDef def = Def();
            CharacterSkinDef.SkinClip bare = Clip();
            bare.Face = null;
            Assert.AreEqual((Open, BrowsFlat, MouthFlat), Show(def, bare, 1), "A clip with no face track.");

            CharacterSkinDef.SkinClip ragged = Clip();
            ragged.Face = new byte[] { Half, BrowsUp, MouthSmile, Half };
            Assert.AreEqual((Open, BrowsFlat, MouthFlat), Show(def, ragged, 0), "A face track of the wrong length.");

            Assert.AreEqual((Open, BrowsFlat, MouthFlat), Show(def, Clip(), 3), "A frame past the clip's end.");
        }

        [Test]
        public void AGazeTurnsTheOpenEyesAndNoOthers()
        {
            CharacterSkinDef def = Def();
            CharacterSkinDef.SkinClip clip = Clip();
            Assert.AreEqual((Left, BrowsUp, MouthSmile), Show(def, clip, 0, CharacterFigureLook.GazeLeft));
            Assert.AreEqual((Right, BrowsUp, MouthSmile), Show(def, clip, 0, CharacterFigureLook.GazeRight));
            Assert.AreEqual(Half, Show(def, clip, 1, CharacterFigureLook.GazeLeft).eyes, "A gaze turned half-shut eyes.");
            Assert.AreEqual(Shut, Show(def, clip, 2, CharacterFigureLook.GazeRight).eyes, "A gaze turned shut eyes.");

            def.LookEyes = new Vector3Int(Open, None, Right);
            Assert.AreEqual(Open, Show(def, clip, 0, CharacterFigureLook.GazeLeft).eyes,
                "A gaze with no group of its own replaced the eyes.");
        }

        [Test]
        public void ABlinkShowsOverTheEyesButNotOverTheOnesTheDefSkips()
        {
            CharacterSkinDef def = Def();
            CharacterSkinDef.SkinClip clip = Clip();
            Assert.AreEqual((Half, BrowsUp, MouthSmile), Show(def, clip, 0, blink: Half));
            Assert.AreEqual(Shut, Show(def, clip, 1, blink: Shut).eyes);
            Assert.AreEqual(Shut, Show(def, clip, 2, blink: Half).eyes, "A blink opened eyes the frame keeps shut.");
            Assert.AreEqual(Shut, Show(def, clip, 0, CharacterFigureLook.GazeRight, Shut).eyes,
                "A blink did not show over a gaze.");
        }

        [Test]
        public void TheSkipIsJudgedOnTheFramesOwnEyesNotTheGazes()
        {
            CharacterSkinDef def = Def();
            CharacterSkinDef.SkinClip clip = Clip();
            def.BlinkSkipGroups = new[] { Right };
            Assert.AreEqual(Half, Show(def, clip, 0, CharacterFigureLook.GazeRight, Half).eyes,
                "The skip read the gaze's eyes, not the frame's.");

            Assert.IsTrue(CharacterFigureFace.Skips(def, Right));
            Assert.IsFalse(CharacterFigureFace.Skips(def, Open));
            Assert.IsFalse(CharacterFigureFace.Skips(null, Right));
        }

        [Test]
        public void ADefWithNoFaceShowsNoGroup()
        {
            CharacterSkinDef def = Def();
            def.FaceGroups = new string[0];
            Assert.AreEqual((None, None, None), Show(def, Clip(), 0, CharacterFigureLook.GazeRight, Half));
            Assert.AreEqual((None, None, None), Show(null, Clip(), 0, CharacterFigureLook.GazeRight, Half));
        }
    }
}
