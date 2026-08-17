using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>"Can you stop and talk?" is authored, not guessed</b> — a flag on the routine block, carried
    /// through the planner onto the plan, where the conversation layer reads it.
    ///
    /// <para>And the thing that must remain true whatever this feature does: <b>the plan is still a pure
    /// function of the clock</b> (CLAUDE.md rule 5). Interruptibility is metadata ABOUT a block; it does
    /// not move a departure, does not change a route and cannot change a pose. That is asserted here
    /// directly, because "we added an overlay and determinism is fine" is exactly the sentence that ought
    /// to be checked rather than believed.</para>
    /// </summary>
    public class RoutineInterruptibilityTests
    {
        const float SecondsPerGameHour = 1800f / 24f;   // the pinned 1800 s day

        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // green(0) ── lane(1) ── school(2)
        RoutineLanes MakeLanes()
        {
            var go = new GameObject("lanes");
            _spawned.Add(go);
            var lanes = go.AddComponent<RoutineLanes>();
            lanes.Configure(
                nodePositions: new[] { new Vector2(0f, 0f), new Vector2(0f, 20f), new Vector2(-15f, 20f) },
                nodeParents: new[] { RoutineLaneTree.NoParent, 0, 1 },
                nodeNames: new[] { "green", "lane", "school" },
                viaStart: new int[3], viaCount: new int[3], via: System.Array.Empty<Vector2>());
            Assert.That(lanes.Validate(), Is.Empty);
            return lanes;
        }

        RoutineStations MakeStations()
        {
            var go = new GameObject("stations");
            _spawned.Add(go);
            var stations = go.AddComponent<RoutineStations>();
            stations.Configure(new[]
            {
                new RoutineStationEntry
                {
                    Id = "s.green", Position = new Vector2(0f, 0f), StandHeadingDegrees = 0f,
                    LaneNodeName = "green", Why = "the green",
                },
                new RoutineStationEntry
                {
                    Id = "s.school_yard", Position = new Vector2(-15f, 20f), StandHeadingDegrees = 180f,
                    LaneNodeName = "school", Why = "the schoolhouse dooryard",
                },
            });
            return stations;
        }

        RoutineDef MakeRoutine(params bool[] uninterruptible)
        {
            var def = ScriptableObject.CreateInstance<RoutineDef>();
            _spawned.Add(def);
            def.Id = "routine.interruptibility";
            def.WalkSpeedMetresPerSecond = 1.2f;
            def.ScheduleJitterMinutes = 6f;
            def.Entries = new[]
            {
                new RoutineEntry
                {
                    StartHour = 8f, StationId = "s.green", Activity = RoutineActivity.Work,
                    Uninterruptible = uninterruptible.Length > 0 && uninterruptible[0], Why = "test",
                },
                new RoutineEntry
                {
                    StartHour = 16f, StationId = "s.school_yard", Activity = RoutineActivity.Work,
                    Uninterruptible = uninterruptible.Length > 1 && uninterruptible[1], Why = "test",
                },
            };
            return def;
        }

        RoutinePlan Plan(RoutineDef routine, int worldSeed = 4242)
        {
            RoutinePlan plan = RoutinePlanner.Build(routine, MakeStations(), MakeLanes(), worldSeed,
                                                   out _, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            return plan;
        }

        // ---- the authored default -----------------------------------------------------------

        [Test]
        public void StoppingToTalkIsTheDEFAULT_SoAnOlderRoutineAssetKeepsWorking()
        {
            // ⭐ WHY THE FIELD RECORDS THE EXCEPTION. Unity deserializes a field an asset predates as
            // its zero value, so `Uninterruptible = false` is what every routine authored before today
            // reads as — which is "she stops to talk", the behaviour they should all have.
            var entry = new RoutineEntry();

            Assert.That(entry.Uninterruptible, Is.False);
            Assert.That(entry.Interruptible, Is.True,
                        "a routine written before conversations existed must not silently become one " +
                        "of somebody who refuses to talk to you");
        }

        [Test]
        public void ThePlanCarriesTheAuthoredFlag_BlockByBlock()
        {
            RoutinePlan plan = Plan(MakeRoutine(false, true));

            Assert.That(plan.InterruptibleAt(0), Is.True, "block 0 was authored interruptible");
            Assert.That(plan.InterruptibleAt(1), Is.False,
                        "block 1 was authored uninterruptible and must stay that way through the planner");
        }

        [Test]
        public void ItIsAPropertyOfTheBLOCK_NotOfThePerson()
        {
            // The same villager stops for you on the green at noon and keeps walking through the beat
            // she must not miss. If this ever collapses to one flag per person, it has stopped being
            // the thing that was asked for.
            RoutinePlan plan = Plan(MakeRoutine(true, false));

            Assert.That(plan.InterruptibleAt(0), Is.False);
            Assert.That(plan.InterruptibleAt(1), Is.True);
        }

        [Test]
        public void AnOutOfRangeBlock_FailsOPEN()
        {
            RoutinePlan plan = Plan(MakeRoutine());

            Assert.That(plan.InterruptibleAt(-1), Is.True);
            Assert.That(plan.InterruptibleAt(99), Is.True,
                        "a villager you cannot talk to at all is a worse bug than one who stops when " +
                        "she might not have — every other unauthored field here fails open too");
        }

        [Test]
        public void TheHurryIsPerPerson_AndCannotBeSlowerThanTheWalk()
        {
            RoutineDef routine = MakeRoutine();
            routine.WalkSpeedMetresPerSecond = 1.2f;
            routine.CatchUpSpeedMultiplier = 1.6f;
            Assert.That(routine.CatchUpSpeedMetresPerSecond, Is.EqualTo(1.92f).Within(0.0001f));

            // A multiplier below 1 would make catching a target that is itself walking impossible.
            routine.CatchUpSpeedMultiplier = 0.5f;
            Assert.That(routine.CatchUpSpeedMetresPerSecond, Is.EqualTo(1.2f).Within(0.0001f),
                        "hurrying is never slower than walking, whatever the inspector says");

            Assert.That(Plan(routine).CatchUpSpeedMetresPerSecond, Is.EqualTo(1.2f).Within(0.0001f),
                        "and the plan carries the resolved number, not the raw multiplier");
        }

        // ---- determinism, unmoved -----------------------------------------------------------

        [Test]
        public void InterruptibilityChangesNothingAboutWHEREANYBODYIS_BitForBit()
        {
            // ⭐ THE RULE 5 GUARD. The two plans differ ONLY in the authored flag. If flipping it moved a
            // departure, a route or a pose by a single bit, the conversation overlay would have leaked
            // into the simulation and the save/determinism contract with it.
            RoutinePlan stops = Plan(MakeRoutine(false, false));
            RoutinePlan doesNot = Plan(MakeRoutine(true, true));

            for (float h = 0f; h < 24f; h += 0.05f)
            {
                RoutinePose a = stops.SampleAt(h, SecondsPerGameHour);
                RoutinePose b = doesNot.SampleAt(h, SecondsPerGameHour);
                Assert.That(b.Position.x, Is.EqualTo(a.Position.x), $"x at h={h:0.00}");
                Assert.That(b.Position.y, Is.EqualTo(a.Position.y), $"y at h={h:0.00}");
                Assert.That(b.BlockIndex, Is.EqualTo(a.BlockIndex), $"block at h={h:0.00}");
                Assert.That(b.Walking, Is.EqualTo(a.Walking), $"walking at h={h:0.00}");
                Assert.That(b.HeadingDegrees, Is.EqualTo(a.HeadingDegrees), $"heading at h={h:0.00}");
                Assert.That(b.Shelter, Is.EqualTo(a.Shelter), $"shelter at h={h:0.00}");
            }
        }

        [Test]
        public void TheHurrySpeedChangesNothingAboutWHEREANYBODYIS_Either()
        {
            RoutineDef slow = MakeRoutine();
            slow.CatchUpSpeedMultiplier = 1f;
            RoutineDef fast = MakeRoutine();
            fast.CatchUpSpeedMultiplier = 4f;

            RoutinePlan a = Plan(slow), b = Plan(fast);

            for (float h = 0f; h < 24f; h += 0.25f)
            {
                Assert.That(b.SampleAt(h, SecondsPerGameHour).Position,
                            Is.EqualTo(a.SampleAt(h, SecondsPerGameHour).Position),
                            $"the hurry is an OVERLAY speed and must not touch the day at h={h:0.00}");
            }
        }
    }
}
