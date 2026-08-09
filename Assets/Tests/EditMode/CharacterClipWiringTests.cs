using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The committed player def carries a COMPLETE sheet for every clip the rig declares and the
    /// bake recipe bakes. A clip can exist three ways and still never play: declared on the enum,
    /// declared on the def's fields, baked to a sheet — and wired by nobody. That is the
    /// plumbed-but-unread failure (the night panel shipped dead twice on it; rig 6.3's six
    /// deck-work clips arrived exactly this way, 2026-08-09), and only an asset-state assertion
    /// catches it, because every code path answers "false, correctly" the whole time.
    /// </summary>
    public class CharacterClipWiringTests
    {
        const string FisherDef = "Assets/_Project/Data/Characters/FisherIso.asset";

        static readonly CharacterClip[] PlayerBakedClips =
        {
            CharacterClip.Board, CharacterClip.BoardDown, CharacterClip.Haul,
            CharacterClip.LadderDown, CharacterClip.Hauler, CharacterClip.Bench,
            CharacterClip.Chop, CharacterClip.Lift, CharacterClip.Place, CharacterClip.Toss,
        };

        [Test]
        public void TheCommittedPlayerDef_WiresEveryClipThePlayerBakes()
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherDef);
            Assert.That(def, Is.Not.Null, FisherDef + " is missing — run Build Character Visual Defs.");

            foreach (var clip in PlayerBakedClips)
                Assert.That(def.HasClip(clip), Is.True,
                    $"'{clip}' is declared and baked but NOT wired on {FisherDef} — " +
                    "CharacterVisualLibraryBuilder must TakeClip it and the defs build must re-run. " +
                    "A clip in this state never plays and no runtime path reports it.");
        }
    }
}
