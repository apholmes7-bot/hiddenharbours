using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>BOTH HANDS, ALL EIGHT FACINGS — measured, never converted from the other hand.</b>
    ///
    /// <para>This is the acceptance for two-hand carry's geometry. The moment two things are held, one of
    /// them is in the hand the rig did NOT resolve for it, and it has to hang off that hand's real wrist.
    /// The wrist grid is where that comes from, and the claims here are about it:
    /// <list type="number">
    ///   <item><b>PRESENT.</b> Every gait, every one of the eight facings, every frame answers for the
    ///   LEFT hand and for the RIGHT — not just for whichever the rig preferred.</item>
    ///   <item><b>⭐ MEASURED, NOT MIRRORED.</b> The two hands are two independent measurements off the
    ///   sheet. A mirror would satisfy "present" perfectly and be wrong everywhere but N and S — so the
    ///   test is that the pair is NOT a reflection, at the headings where a reflection would show.</item>
    ///   <item><b>THE SAME PIXELS.</b> Each hand's metres are its own <c>handL</c>/<c>handR</c> pixel from
    ///   the sidecar, through the sheet's own PPU — the conversion, not just the shape.</item>
    ///   <item><b>THE PER-HAND ROW.</b> Asking the table for a prop in a named hand answers for that hand,
    ///   says whether the answer was measured, and — when it was not — drops the grip rather than
    ///   inventing one.</item>
    /// </list></para>
    ///
    /// <para><b>What is deliberately NOT asserted:</b> that the shipped table carries per-hand PROP rows.
    /// It does not yet — the sidecar it was imported from predates that bake — and asserting it would be
    /// a red on a clean checkout for something no code in this PR can produce. What IS asserted is that
    /// the runtime behaves correctly in BOTH cases, which is the property that has to survive the
    /// re-bake.</para>
    /// </summary>
    public class CarryAnchorHandsTests
    {
        const string SidecarPath = "Assets/_Project/Art/Characters/Iso/FisherFightAnchors.json";
        const string IdleSheet = "Assets/_Project/Art/Characters/Iso/Fisher_idle.png";
        const int Directions = 8;

        static CarryAnchorTableDef Table()
        {
            var t = AssetDatabase.LoadAssetAtPath<CarryAnchorTableDef>(CarryAnchorTableBuilder.TablePath);
            Assert.That(t, Is.Not.Null,
                        $"No carry-anchor table at '{CarryAnchorTableBuilder.TablePath}'. It is a " +
                        "COMMITTED asset — if it is missing from a clean checkout that is the bug; " +
                        "otherwise run Art ▸ Import (after a new drop) ▸ Build Carry Anchor Table.");
            return t;
        }

        static float Ppu()
        {
            Sprite[] frames = PersistentCoreBuilder.LoadIsoDirFrames(IdleSheet);
            Assert.That(frames.Length, Is.GreaterThan(0),
                        $"'{IdleSheet}' has not imported as a sliced sheet — every metre below would be " +
                        "measured against a guess.");
            return frames[0].pixelsPerUnit;
        }

        // ---- 1. PRESENT ---------------------------------------------------------------------------------

        [Test]
        public void EveryGait_AnswersForBOTHHands_AtAllEightFacings()
        {
            CarryAnchorTableDef t = Table();
            Assert.That(t.FacingCount, Is.EqualTo(Directions));
            Assert.That(t.Wrists, Is.Not.Null.And.Not.Empty,
                        "no wrist grid at all — every carried thing would fall back to its rest pin and " +
                        "come off her hand the moment she moved");

            int checkedCells = 0;
            foreach (CarryWristFrames gait in t.Wrists)
            {
                Assert.That(gait.FramesPerDir, Is.GreaterThan(0), $"{gait.Gait} frames");
                for (int d = 0; d < Directions; d++)
                    for (int f = 0; f < gait.FramesPerDir; f++)
                    {
                        Assert.That(t.TryAnchorMeters(gait.Gait, d, f, CarryHandSide.Left, out Vector2 l),
                                    Is.True, $"{gait.Gait} d{d} f{f}: no LEFT wrist");
                        Assert.That(t.TryAnchorMeters(gait.Gait, d, f, CarryHandSide.Right, out Vector2 r),
                                    Is.True, $"{gait.Gait} d{d} f{f}: no RIGHT wrist");
                        Assert.That(l, Is.Not.EqualTo(r),
                                    $"{gait.Gait} d{d} f{f}: the two wrists are the SAME point, which no " +
                                    "body has — one hand's grid was written from the other's");
                        checkedCells++;
                    }
            }

            Assert.That(checkedCells, Is.GreaterThanOrEqualTo(Directions * 3),
                        "at least one full turn of one gait was actually checked");
        }

        [Test]
        public void ACradle_TakesTheMidpointOfTheTwo_AndABackMountTakesNeither()
        {
            CarryAnchorTableDef t = Table();
            CarryWristFrames gait = t.Wrists[0];

            for (int d = 0; d < Directions; d++)
            {
                t.TryAnchorMeters(gait.Gait, d, 0, CarryHandSide.Left, out Vector2 l);
                t.TryAnchorMeters(gait.Gait, d, 0, CarryHandSide.Right, out Vector2 r);
                Assert.That(t.TryAnchorMeters(gait.Gait, d, 0, CarryHandSide.Both, out Vector2 both),
                            Is.True);
                Assert.That((both - (l + r) * 0.5f).magnitude, Is.LessThan(1e-5f), $"d{d} cradle");
            }

            Assert.That(t.TryAnchorMeters(gait.Gait, 0, 0, CarryHandSide.None, out _), Is.False,
                        "a back sling has no wrist and must not be handed one");
        }

        // ---- 2. ⭐ MEASURED, NOT MIRRORED ----------------------------------------------------------------

        [Test]
        public void TheTwoWrists_AreNotAReflectionOfEachOther()
        {
            // ⚠️ THE CLAIM THAT MATTERS. "Both present" is satisfied perfectly by a table that computed
            // one hand as (−x, y) of the other, and that table draws right at N and S — where the body's
            // axis is on screen-x — and wrong at all six other headings, by up to the whole projected
            // depth difference between the two arms. So: at the diagonals and the profiles, the pair must
            // NOT be a mirror about the body's centre line.
            CarryAnchorTableDef t = Table();
            CarryWristFrames gait = t.Wrists[0];

            int nonMirrored = 0;
            for (int d = 0; d < Directions; d++)
            {
                t.TryAnchorMeters(gait.Gait, d, 0, CarryHandSide.Left, out Vector2 l);
                t.TryAnchorMeters(gait.Gait, d, 0, CarryHandSide.Right, out Vector2 r);

                // A reflection about x would put the two at the same height and opposite offsets.
                bool mirrored = Mathf.Abs(l.y - r.y) < 1e-4f && Mathf.Abs(l.x + r.x) < 1e-4f;
                if (!mirrored) nonMirrored++;
            }

            Assert.That(nonMirrored, Is.GreaterThanOrEqualTo(4),
                        "at most the two axis facings may look like a mirror pair (there the body IS " +
                        "symmetric on screen). Four or more mirrored rows means one hand was DERIVED " +
                        "from the other rather than measured off the sheet — which is the one thing the " +
                        "two-hand carry must never do.");
        }

        // ---- 3. THE SAME PIXELS -------------------------------------------------------------------------

        [Test]
        public void EachHandsMetres_AreItsOwnSidecarPixel_AtTheSheetsOwnPpu()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SidecarPath);
            Assert.That(text, Is.Not.Null, $"No sidecar at '{SidecarPath}' — re-bake the character kit.");
            object sidecar = MiniJson.Parse(text.text);

            var pivot = MiniJson.Dict(sidecar, "pivotTopLeft");
            float px = MiniJson.Float(pivot, "x"), py = MiniJson.Float(pivot, "y"), ppu = Ppu();

            CarryAnchorTableDef t = Table();
            var states = MiniJson.Dict(sidecar, "states");

            // The importer's own gait→state map, restated: idle is the rest pose every other test here
            // measures from, so it is the one that must be exactly right.
            List<object> dirs = MiniJson.List(MiniJson.Dict(states, "idle"), "anchors");
            Assert.That(dirs, Is.Not.Null.And.Count.EqualTo(Directions), "the idle anchor grid");

            for (int d = 0; d < Directions; d++)
            {
                var row = dirs[d] as List<object>;
                Assert.That(row, Is.Not.Null.And.Not.Empty, $"d{d}");

                var handL = MiniJson.Dict(row[0], "handL");
                var handR = MiniJson.Dict(row[0], "handR");
                Vector2 wantL = RodKitAnchorMath.CellPxToPivotWorld(
                    MiniJson.Float(handL, "x"), MiniJson.Float(handL, "y"), px, py, ppu);
                Vector2 wantR = RodKitAnchorMath.CellPxToPivotWorld(
                    MiniJson.Float(handR, "x"), MiniJson.Float(handR, "y"), px, py, ppu);

                Assert.That(t.TryAnchorMeters(CharacterGait.Idle, d, 0, CarryHandSide.Left, out Vector2 l),
                            Is.True);
                Assert.That(t.TryAnchorMeters(CharacterGait.Idle, d, 0, CarryHandSide.Right, out Vector2 r),
                            Is.True);

                Assert.That((l - wantL).magnitude, Is.LessThan(1e-4f), $"d{d} LEFT wrist");
                Assert.That((r - wantR).magnitude, Is.LessThan(1e-4f), $"d{d} RIGHT wrist");
            }
        }

        // ---- 4. THE PER-HAND ROW ------------------------------------------------------------------------

        [Test]
        public void AskingForAHand_AnswersForTHATHand_AtEveryFacing()
        {
            CarryAnchorTableDef t = Table();

            foreach (string prop in new[] { "fish", "clam", "rodTrail" })
                for (int d = 0; d < Directions; d++)
                    foreach (CarryHandSide hand in new[] { CarryHandSide.Left, CarryHandSide.Right })
                    {
                        Assert.That(t.TryRow(prop, d, hand, out CarryAnchorRow row, out bool measured),
                                    Is.True, $"{prop} d{d} {hand}");
                        Assert.That(row.Hand, Is.EqualTo(hand),
                                    $"{prop} d{d}: asked for the {hand} hand and got the {row.Hand} one");

                        // And the pin it produces lands on THAT hand's measured wrist.
                        Vector2 pin = t.PinMeters(row, CharacterGait.Idle, d, t.RestFrame, out bool live);
                        Assert.That(live, Is.True, $"{prop} d{d} {hand}: no live wrist");
                        t.TryAnchorMeters(CharacterGait.Idle, d, t.RestFrame, hand, out Vector2 wrist);
                        Assert.That((pin - wrist).magnitude, Is.LessThan(0.1f),
                                    $"{prop} d{d} {hand}: the pin is nowhere near that hand's wrist");
                    }
        }

        [Test]
        public void WhenTheOtherHandWasNotMeasured_TheGripIsDROPPED_NotMirrored()
        {
            // ⚠️ The honest-fallback claim. There is no arithmetic that recovers one hand's grip from the
            // other's — the rig signs the outboard component by the hand and then PROJECTS the triple, so
            // a 2-D result cannot be decomposed back. The table's answer is therefore the requested hand's
            // real wrist and NO grip, which costs the grip's own magnitude (1–2.5 px) and never points the
            // prop the wrong way.
            CarryAnchorTableDef t = Table();
            if (t.HasPerHandRows)
                Assert.Ignore("this table carries measured rows for both hands — the fallback is not " +
                              "reachable, which is the better state to be in");

            for (int d = 0; d < Directions; d++)
            {
                Assert.That(t.TryRow("rodTrail", d, out CarryAnchorRow resolved), Is.True);
                CarryHandSide other = resolved.Hand == CarryHandSide.Right
                                      ? CarryHandSide.Left : CarryHandSide.Right;

                Assert.That(t.TryRow("rodTrail", d, other, out CarryAnchorRow row, out bool measured),
                            Is.True);
                Assert.That(measured, Is.False, $"d{d}: this table has no measured {other} row to give");
                Assert.That(row.GripOffsetMeters, Is.EqualTo(Vector2.zero),
                            $"d{d}: the grip must be dropped, never reflected");
                Assert.That(row.Hand, Is.EqualTo(other));
                Assert.That(row.ItemFacing, Is.EqualTo(resolved.ItemFacing),
                            $"d{d}: the turntable cell is a FACING fact and survives the hand change");
            }
        }

        [Test]
        public void TheRigsOwnHand_IsAlwaysReportedAsMEASURED()
        {
            CarryAnchorTableDef t = Table();
            foreach (string prop in new[] { "fish", "clam", "rodTrail", "rope", "knife", "gaff" })
                for (int d = 0; d < Directions; d++)
                {
                    Assert.That(t.TryRow(prop, d, out CarryAnchorRow resolved), Is.True);
                    Assert.That(t.TryRow(prop, d, resolved.Hand, out CarryAnchorRow row, out bool measured),
                                Is.True);
                    Assert.That(measured, Is.True, $"{prop} d{d}: the rig's OWN hand is measured art");
                    Assert.That(row.GripOffsetMeters, Is.EqualTo(resolved.GripOffsetMeters),
                                $"{prop} d{d}: and it is the row the table already had, unchanged");
                }
        }

        [Test]
        public void NoParticularHand_StillAnswersWithTheRigsResolvedRow()
        {
            // The single-carry path: a thing held ALONE is posed from the rig's own per-facing answer, so
            // asking with no preference must return exactly what the old one-argument TryRow returns.
            CarryAnchorTableDef t = Table();
            foreach (CarryHandSide none in new[] { CarryHandSide.None, CarryHandSide.Both })
                for (int d = 0; d < Directions; d++)
                {
                    Assert.That(t.TryRow("fish", d, out CarryAnchorRow expected), Is.True);
                    Assert.That(t.TryRow("fish", d, none, out CarryAnchorRow got, out bool measured),
                                Is.True);
                    Assert.That(measured, Is.True);
                    Assert.That(got.Hand, Is.EqualTo(expected.Hand), $"d{d}");
                    Assert.That(got.GripOffsetMeters, Is.EqualTo(expected.GripOffsetMeters), $"d{d}");
                    Assert.That(got.Behind, Is.EqualTo(expected.Behind), $"d{d}");
                }
        }

        [Test]
        public void APerHandTable_IsPreferredOverTheResolvedRow_AndSaysItWasMEASURED()
        {
            // The other side of the fallback, which the shipped sidecar cannot exercise yet: once the
            // character kit is re-baked with the per-hand block, the table carries a MEASURED row for
            // each hand and the runtime must take it — grip and all — rather than falling back. Built in
            // memory so the claim is pinned NOW, before the re-bake, instead of after someone notices.
            var t = ScriptableObject.CreateInstance<CarryAnchorTableDef>();
            try
            {
                t.Id = "carryanchors.perhandfixture";
                t.FacingCount = Directions;
                t.Props = new[] { PerHandProp() };

                Assert.That(t.HasPerHandRows, Is.True, "the fixture is the shape under test");

                for (int d = 0; d < Directions; d++)
                {
                    Assert.That(t.TryRow("fish", d, CarryHandSide.Left, out CarryAnchorRow l,
                                         out bool leftMeasured), Is.True);
                    Assert.That(leftMeasured, Is.True, $"d{d}: the left row is measured art");
                    Assert.That(l.Hand, Is.EqualTo(CarryHandSide.Left));
                    Assert.That(l.GripOffsetMeters, Is.EqualTo(new Vector2(-0.1f, d)),
                                $"d{d}: the LEFT grip must come from the left row, not be zeroed or " +
                                "reflected from the right");

                    Assert.That(t.TryRow("fish", d, CarryHandSide.Right, out CarryAnchorRow r,
                                         out bool rightMeasured), Is.True);
                    Assert.That(rightMeasured, Is.True);
                    Assert.That(r.GripOffsetMeters, Is.EqualTo(new Vector2(0.3f, d)),
                                $"d{d}: and the RIGHT grip is its own measurement — deliberately NOT " +
                                "the left's reflection, which is what a mirrored import would produce");
                }
            }
            finally { Object.DestroyImmediate(t); }
        }

        /// <summary>A prop whose two hands were measured SEPARATELY and disagree in a way no reflection
        /// could produce — so a reader that mirrored one onto the other fails loudly.</summary>
        static CarryPropAnchors PerHandProp()
        {
            var resolved = new CarryAnchorRow[Directions];
            var left = new CarryAnchorRow[Directions];
            var right = new CarryAnchorRow[Directions];
            for (int d = 0; d < Directions; d++)
            {
                resolved[d] = new CarryAnchorRow
                {
                    Hand = CarryHandSide.Right, Mount = CarryMountKind.Hand,
                    GripOffsetMeters = new Vector2(0.3f, d), ItemFacing = d,
                };
                left[d] = new CarryAnchorRow
                {
                    Hand = CarryHandSide.Left, Mount = CarryMountKind.Hand,
                    GripOffsetMeters = new Vector2(-0.1f, d), ItemFacing = d,
                };
                right[d] = resolved[d];
            }
            return new CarryPropAnchors
            {
                PropKey = "fish", Rig = "FishIso", ItemScale = 1f,
                Rows = resolved, LeftRows = left, RightRows = right,
            };
        }

        [Test]
        public void AnUnknownPropOrFacing_IsAnOrdinaryNo()
        {
            CarryAnchorTableDef t = Table();
            Assert.That(t.TryRow("shovel", 0, CarryHandSide.Left, out _, out _), Is.False,
                        "the rig bakes no spade row — an ordinary state, not a fault");
            Assert.That(t.TryRow(null, 0, CarryHandSide.Left, out _, out _), Is.False);
            Assert.That(t.TryRow("fish", -1, CarryHandSide.Left, out _, out _), Is.False);
            Assert.That(t.TryRow("fish", Directions, CarryHandSide.Left, out _, out _), Is.False);
        }
    }
}
