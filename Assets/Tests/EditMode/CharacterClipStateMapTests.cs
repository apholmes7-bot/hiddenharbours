using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class CharacterClipStateMapTests
    {
        // Literal expectations: neither the map's constants nor the resolver supplies the bar.
        [TestCase(CharacterClip.Board, null, "board")]
        [TestCase(CharacterClip.Board, "", "board")]
        [TestCase(CharacterClip.Board, "buckets", "board_buckets")]
        [TestCase(CharacterClip.Board, "tray", "board_tray")]
        [TestCase(CharacterClip.Board, "pot", "board_pot")]
        [TestCase(CharacterClip.Board, "unoffered_carry", "board")]
        [TestCase(CharacterClip.BoardDown, null, "boardDown")]
        [TestCase(CharacterClip.BoardDown, "", "boardDown")]
        [TestCase(CharacterClip.BoardDown, "buckets", "boardDown_buckets")]
        [TestCase(CharacterClip.BoardDown, "tray", "boardDown_tray")]
        [TestCase(CharacterClip.BoardDown, "pot", "boardDown_pot")]
        [TestCase(CharacterClip.BoardDown, "unoffered_carry", "boardDown")]
        public void ClipStateMap_TheVault_TakesHerCarry(CharacterClip clip, string carry, string expected)
        {
            Assert.AreEqual(expected, CharacterClipStateMap.StateKeyFor(clip, carry));
        }

        [TestCase(CharacterClip.LadderDown, "ladderDown")]
        [TestCase(CharacterClip.Haul, "haul")]
        [TestCase(CharacterClip.Hauler, "hauler")]
        [TestCase(CharacterClip.Bench, "bench")]
        [TestCase(CharacterClip.Chop, "chop")]
        [TestCase(CharacterClip.Lift, "lift")]
        [TestCase(CharacterClip.Place, "place")]
        [TestCase(CharacterClip.Toss, "toss")]
        public void ClipStateMap_TheOtherMoves_KeepTheirOwnPose(CharacterClip clip, string expected)
        {
            foreach (string carry in new[] { null, "", "buckets", "tray", "pot", "unoffered_carry" })
                Assert.AreEqual(expected, CharacterClipStateMap.StateKeyFor(clip, carry), carry);
        }

        [TestCase(CharacterClip.None)]
        [TestCase(CharacterClip.Swim)]
        [TestCase(CharacterClip.Tread)]
        [TestCase(CharacterClip.Sleep)]
        [TestCase(CharacterClip.Drive)]
        [TestCase(CharacterClip.ReachGround)]
        [TestCase(CharacterClip.ReachStowV)]
        [TestCase(CharacterClip.ReachStowH)]
        [TestCase((CharacterClip)(-1))]
        [TestCase((CharacterClip)int.MaxValue)]
        public void ClipStateMap_LeavesWaterSleepAndDrive_Unmapped(CharacterClip clip)
        {
            foreach (string carry in new[] { null, "", "buckets", "tray", "pot", "unoffered_carry" })
                Assert.IsNull(CharacterClipStateMap.StateKeyFor(clip, carry), carry);
        }
    }
}
