using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The STANCE axis of the character sheets — the second dimension beside gait, and the resolution rule
    /// that decides which of the two a character actually draws.
    ///
    /// <para>Two claims matter most. First, the <b>A/B</b>: a def with no stance art must answer exactly
    /// what it answered before stances existed, or every NPC and the ashore player quietly change. Second,
    /// the <b>order of the ladders</b>: the stance is tried at the WANTED gait and the fallback is to the
    /// free body, not to a slower gait — otherwise a fisher who starts walking across a rolling deck keeps
    /// showing the braced idle while they slide.</para>
    ///
    /// <para>Also pins the per-hull pilot stance (<see cref="BoatVisualDef.PilotStanceFor"/>), which is the
    /// one place "wheel or oars?" is decided — from data, never in code (rule 2).</para>
    /// </summary>
    public class CharacterStanceTests
    {
        const int Directions = 8;

        static Sprite[] Sheet(int frames)
        {
            var set = new Sprite[Directions * frames];
            var tex = new Texture2D(4, 4);
            for (int i = 0; i < set.Length; i++)
                set[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.1f), 32f);
            return set;
        }

        static CharacterVisualDef NewDef(bool withStances)
        {
            var d = ScriptableObject.CreateInstance<CharacterVisualDef>();
            d.Id = "visual.test";
            d.FacingCount = Directions;
            d.IdleFrameCount = 6; d.IdleSheet = Sheet(6);
            d.WalkFrameCount = 8; d.WalkSheet = Sheet(8);
            d.RunFrameCount = 6; d.RunSheet = Sheet(6);
            if (withStances)
            {
                // Exactly what the shipped Fisher kit bakes: balance is idle-only, the carries do both.
                d.BalanceStance = new CharacterStanceSheets
                { IdleFrameCount = 8, IdleSheet = Sheet(8), IdleFramesPerSecond = 1000f / 150f };
                d.HelmStance = new CharacterStanceSheets
                { IdleFrameCount = 6, IdleSheet = Sheet(6), WalkFrameCount = 8, WalkSheet = Sheet(8) };
                d.OarsStance = new CharacterStanceSheets
                { IdleFrameCount = 6, IdleSheet = Sheet(6), WalkFrameCount = 8, WalkSheet = Sheet(8) };
            }
            return d;
        }

        // ---- the A/B: no stance art changes nothing --------------------------------------------------

        [Test]
        public void ADefWithNoStanceArt_AnswersExactlyAsItDidBeforeStancesExisted()
        {
            var d = NewDef(withStances: false);
            foreach (CharacterGait gait in System.Enum.GetValues(typeof(CharacterGait)))
            {
                CharacterPose pose = d.Playable(CharacterStance.Balance, gait);
                Assert.AreEqual(CharacterStance.Free, pose.Stance, $"{gait}: nothing to brace with");
                Assert.AreEqual(d.PlayableGait(gait), pose.Gait, $"{gait}: the old ladder, untouched");

                // …and the lookups agree with the flat ones they used to be.
                Assert.AreSame(d.SheetFor(gait), d.SheetFor(CharacterStance.Free, gait));
                Assert.AreEqual(d.FrameCountFor(gait), d.FrameCountFor(CharacterStance.Free, gait));
                Assert.AreEqual(d.FramesPerSecondFor(gait), d.FramesPerSecondFor(CharacterStance.Free, gait));
            }
        }

        [Test]
        public void TheFreeStance_IsTheFlatSheets_NotACopyOfThem()
        {
            var d = NewDef(withStances: true);
            Assert.AreSame(d.IdleSheet, d.SheetFor(CharacterStance.Free, CharacterGait.Idle));
            Assert.AreSame(d.WalkSheet, d.SheetFor(CharacterStance.Free, CharacterGait.Walk));
            Assert.AreSame(d.RunSheet, d.SheetFor(CharacterStance.Free, CharacterGait.Run));
        }

        // ---- the resolution rule ---------------------------------------------------------------------

        [Test]
        public void StandingOnADeck_Braces()
        {
            var d = NewDef(withStances: true);
            var pose = d.Playable(CharacterStance.Balance, CharacterGait.Idle);
            Assert.AreEqual(CharacterStance.Balance, pose.Stance);
            Assert.AreEqual(CharacterGait.Idle, pose.Gait);
            Assert.AreEqual(8, d.FrameCountFor(CharacterStance.Balance, CharacterGait.Idle),
                            "the rig bakes the deck brace at 8 frames, not the free idle's 6");
        }

        [Test]
        public void CROSSINGADeck_WalksNormally_RatherThanSlidingInTheBracedIdle()
        {
            // THE ORDER-OF-LADDERS TEST. The deck brace bakes no walk. Laddering the GAIT first would find
            // "balance has no walk → drop to idle" and the fisher would skate across the deck standing
            // still. Trying the STANCE first, at the wanted gait, sends it to the free walk instead.
            var d = NewDef(withStances: true);
            var pose = d.Playable(CharacterStance.Balance, CharacterGait.Walk);
            Assert.AreEqual(CharacterStance.Free, pose.Stance);
            Assert.AreEqual(CharacterGait.Walk, pose.Gait, "walking, not braced-idling");
        }

        [Test]
        public void APilotingStance_CoversIdleAndWalk_AndFallsBackForARun()
        {
            var d = NewDef(withStances: true);
            foreach (var stance in new[] { CharacterStance.Helm, CharacterStance.Oars })
            {
                Assert.AreEqual(stance, d.Playable(stance, CharacterGait.Idle).Stance);
                Assert.AreEqual(stance, d.Playable(stance, CharacterGait.Walk).Stance);

                var running = d.Playable(stance, CharacterGait.Run);
                Assert.AreEqual(CharacterStance.Free, running.Stance, "no stance bakes a run");
                Assert.AreEqual(CharacterGait.Run, running.Gait, "so the free run plays");
            }
        }

        [Test]
        public void AHALFWIREDStance_IsDroppedWhole_NeverHalfPlayed()
        {
            // One missing slice would index a stale cell mid-cycle — the same all-or-nothing rule the free
            // sheets have always had.
            var d = NewDef(withStances: true);
            d.HelmStance.IdleSheet[17] = null;
            Assert.IsFalse(d.HasStanceGait(CharacterStance.Helm, CharacterGait.Idle));
            Assert.AreEqual(CharacterStance.Free, d.Playable(CharacterStance.Helm, CharacterGait.Idle).Stance);

            d.OarsStance.WalkFrameCount = 9;   // a count that no longer matches the sheet
            Assert.IsFalse(d.HasStanceGait(CharacterStance.Oars, CharacterGait.Walk));
        }

        [Test]
        public void SpriteLookup_WrapsBothWays_SoAMidCycleStanceSwapCannotIndexOutOfRange()
        {
            var d = NewDef(withStances: true);
            // Taking the helm mid-stride swaps a 6-frame sheet for an 8-frame one on the same elapsed time.
            Assert.IsNotNull(d.SpriteFor(CharacterStance.Balance, CharacterGait.Idle, 7, 7));
            Assert.IsNotNull(d.SpriteFor(CharacterStance.Helm, CharacterGait.Idle, 0, 7),
                             "frame 7 of a 6-frame sheet wraps rather than returning null");
            Assert.IsNotNull(d.SpriteFor(CharacterStance.Helm, CharacterGait.Idle, -3, -5),
                             "negative row/frame are safe too");
            Assert.IsNull(d.SpriteFor(CharacterStance.Balance, CharacterGait.Walk, 0, 0),
                          "an unwired stance sheet has no cell to give");
        }

        // ---- the per-hull pilot stance is DATA -------------------------------------------------------

        static BoatVisualDef NewHull(bool withOars)
        {
            var v = ScriptableObject.CreateInstance<BoatVisualDef>();
            v.Id = "visual.test_hull";
            var tex = new Texture2D(4, 4);
            Sprite Cell() => Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);

            v.Facings = new Sprite[Directions];
            for (int i = 0; i < Directions; i++) v.Facings[i] = Cell();

            if (withOars)
            {
                v.OarColumnCount = 10;
                v.OarPort = new Sprite[Directions * 10];
                v.OarStar = new Sprite[Directions * 10];
                for (int i = 0; i < v.OarPort.Length; i++) { v.OarPort[i] = Cell(); v.OarStar[i] = Cell(); }
            }
            return v;
        }

        [Test]
        public void APulledHull_PutsItsPilotOnTheOars_AndASteeredOneAtTheHelm()
        {
            // Auto is the shipped default on all fourteen visual assets, and it reads the answer off the
            // OARS those assets already carry — which is why adding a pilot needed no asset edited.
            Assert.AreEqual(CharacterStance.Oars, NewHull(withOars: true).PilotStanceFor(),
                            "she is rowed, so the fisher is at the looms");
            Assert.AreEqual(CharacterStance.Helm, NewHull(withOars: false).PilotStanceFor(),
                            "no oars wired, so she is steered");
        }

        [Test]
        public void AnExplicitChoice_OverridesTheOarsRead()
        {
            var rowed = NewHull(withOars: true);
            rowed.PilotStance = PilotStanceChoice.Helm;
            Assert.AreEqual(CharacterStance.Helm, rowed.PilotStanceFor(), "oars as ship's gear, not propulsion");

            var steered = NewHull(withOars: false);
            steered.PilotStance = PilotStanceChoice.Oars;
            Assert.AreEqual(CharacterStance.Oars, steered.PilotStanceFor(), "sculled, with no oar overlays");

            steered.PilotStance = PilotStanceChoice.None;
            Assert.AreEqual(CharacterStance.Free, steered.PilotStanceFor(), "no pose worth pinning");
        }

        [Test]
        public void EveryChoiceAnswers_AndAutoIsTheDefault()
        {
            Assert.AreEqual(PilotStanceChoice.Auto, ScriptableObject.CreateInstance<BoatVisualDef>().PilotStance,
                            "a freshly authored visual must need no edit to gain a pilot");
            foreach (PilotStanceChoice choice in System.Enum.GetValues(typeof(PilotStanceChoice)))
            {
                var v = NewHull(withOars: false);
                v.PilotStance = choice;
                Assert.DoesNotThrow(() => v.PilotStanceFor(), $"{choice}");
            }
        }
    }
}
