using System.Collections.Generic;
using System.Linq;
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
    /// <b>The shipped carry-anchor table against the shipped sidecar it was imported from</b> — the
    /// acceptance for "the hand-prop table has a consumer".
    ///
    /// <para><b>The claim that carries the file</b> is <see cref="TheLivePath_AtTheRestPose_ReproducesTheImportedRestPin"/>.
    /// The rig computes its absolute rest pin as <c>anchor(rest anim, rest frame, dir) + grip</c> — the
    /// same composition the runtime performs live, evaluated at one particular pose. So the two must agree
    /// exactly at that pose, for all fifty-six baked rows, and they agree only if the wrist grid, the
    /// pixel→metre conversion, the hand swap and the composition rule are ALL right. It is a single assert
    /// that cannot be satisfied by a plausible-looking mistake, which is the only kind of geometry test
    /// worth writing here (the flattened-slices lesson: a table that looks right per element can still be
    /// off by one everywhere).</para>
    ///
    /// <para>Everything else pins one fact each, and each one is a fact a re-bake could quietly change:
    /// which hand, which draw order, how many frames.</para>
    /// </summary>
    public class CarryAnchorImportTests
    {
        const string SidecarPath = "Assets/_Project/Art/Characters/Iso/FisherFightAnchors.json";
        const string IsoFisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";
        const string IdleSheet = "Assets/_Project/Art/Characters/Iso/Fisher_idle.png";
        const int Directions = 8;

        // The seven the rig bakes, in its declared order. Restated rather than read from the table so a
        // prop that silently stopped importing is a RED here rather than a shorter list agreeing with
        // itself.
        static readonly string[] Props =
            { "rodTrail", "rodSling", "fish", "clam", "knife", "gaff", "rope" };

        static CarryAnchorTableDef Table()
        {
            var t = AssetDatabase.LoadAssetAtPath<CarryAnchorTableDef>(CarryAnchorTableBuilder.TablePath);
            Assert.That(t, Is.Not.Null,
                        $"No carry-anchor table at '{CarryAnchorTableBuilder.TablePath}'. It is a " +
                        "COMMITTED asset — if it is missing from a clean checkout that is the bug; " +
                        "otherwise run Art ▸ Import (after a new drop) ▸ Build Carry Anchor Table.");
            return t;
        }

        static object Sidecar()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SidecarPath);
            Assert.That(text, Is.Not.Null, $"No sidecar at '{SidecarPath}' — re-bake the character kit.");
            return MiniJson.Parse(text.text);
        }

        static Dictionary<string, object> Row(object sidecar, string prop, int dir)
        {
            var rows = MiniJson.List(
                MiniJson.Dict(MiniJson.Dict(MiniJson.Dict(sidecar, "handProps"), "props"), prop), "rows");
            Assert.That(rows, Is.Not.Null, $"the sidecar has no rows for '{prop}'");
            return rows[dir] as Dictionary<string, object>;
        }

        /// <summary>The sheets' own import PPU — never a typed-in 32, for the reason the importer gives.</summary>
        static float Ppu()
        {
            Sprite[] frames = PersistentCoreBuilder.LoadIsoDirFrames(IdleSheet);
            Assert.That(frames.Length, Is.GreaterThan(0),
                        $"'{IdleSheet}' has not imported as a sliced sheet — every metre below would be " +
                        "measured against a guess.");
            return frames[0].pixelsPerUnit;
        }

        // ---- shape ------------------------------------------------------------------------------------

        [Test]
        public void TheTable_CarriesEverySevenPropRows_AtEightFacings()
        {
            CarryAnchorTableDef t = Table();

            Assert.That(t.FacingCount, Is.EqualTo(Directions));
            Assert.That(t.Props.Select(p => p.PropKey).ToArray(), Is.EquivalentTo(Props),
                        "the imported prop set drifted from the rig's PROP_ORDER");
            foreach (string key in Props)
                Assert.That(t.Prop(key).Rows.Length, Is.EqualTo(Directions), $"'{key}' facing rows");
        }

        [Test]
        public void TheTable_RecordsWhereItCameFrom_SoAStaleOneIsVisible()
        {
            CarryAnchorTableDef t = Table();

            Assert.That(t.SourceSidecar, Is.EqualTo(SidecarPath));
            Assert.That(t.Pass, Is.GreaterThanOrEqualTo(6.4f),
                        "the table was imported from a rig pass older than the one that introduced " +
                        "handProps — re-run the importer against the current bake");
            Assert.That(t.RestAnim, Is.EqualTo("idle"));
            Assert.That(t.RestFrame, Is.EqualTo(0));
            Assert.That(t.TorsoHalfMetres, Is.GreaterThan(0f));
        }

        // ---- ⭐ the relation that proves the whole import ----------------------------------------------

        [Test]
        public void TheLivePath_AtTheRestPose_ReproducesTheImportedRestPin()
        {
            CarryAnchorTableDef t = Table();
            int checkedRows = 0, liveRows = 0;

            foreach (string key in Props)
            {
                CarryPropAnchors prop = t.Prop(key);
                for (int d = 0; d < Directions; d++)
                {
                    CarryAnchorRow row = prop.Rows[d];
                    Vector2 pin = t.PinMeters(row, CharacterGait.Idle, d, t.RestFrame, out bool live);
                    checkedRows++;

                    if (row.Mount == CarryMountKind.Back)
                    {
                        // The one row family with no live anchor — it must NOT claim one.
                        Assert.That(live, Is.False, $"{key} d{d}: a back mount cannot track a wrist");
                        continue;
                    }

                    liveRows++;
                    Assert.That(live, Is.True,
                                $"{key} d{d}: the idle wrist grid did not resolve, so the runtime would " +
                                "silently hang this prop off its rest pin instead of the hand");

                    // A quarter of a pixel at this kit's PPU. Tight enough that a wrong wrist (the two are
                    // ~11 px apart), a wrong frame or a missing pivot subtraction cannot survive; loose
                    // enough for the sidecar's own 3-decimal rounding.
                    Assert.That((pin - row.RestOffsetMeters).magnitude, Is.LessThan(0.25f / Ppu()),
                                $"{key} d{d}: live pin {pin} ≠ the rig's own rest pin " +
                                $"{row.RestOffsetMeters}. The rig computes rest as anchor(idle,0,dir) + " +
                                "grip, so these must agree — one of the wrist grid, the metre " +
                                "conversion, the hand swap or the composition rule is wrong.");
                }
            }

            Assert.That(checkedRows, Is.EqualTo(Props.Length * Directions), "rows actually checked");
            Assert.That(liveRows, Is.EqualTo((Props.Length - 1) * Directions),
                        "exactly one prop (the sling) is back-mounted");
        }

        [Test]
        public void EveryRestPin_IsTheSidecarsPixel_ConvertedAtTheSheetsOwnPpu()
        {
            CarryAnchorTableDef t = Table();
            object sidecar = Sidecar();
            var pivot = MiniJson.Dict(sidecar, "pivotTopLeft");
            float px = MiniJson.Float(pivot, "x"), py = MiniJson.Float(pivot, "y"), ppu = Ppu();

            foreach (string key in Props)
                for (int d = 0; d < Directions; d++)
                {
                    Dictionary<string, object> r = Row(sidecar, key, d);
                    Vector2 want = RodKitAnchorMath.CellPxToPivotWorld(
                        MiniJson.Float(r, "restX"), MiniJson.Float(r, "restY"), px, py, ppu);
                    Vector2 wantGrip = RodKitAnchorMath.OffsetPxToWorld(
                        MiniJson.Float(r, "gripDx"), MiniJson.Float(r, "gripDy"), ppu);

                    CarryAnchorRow row = t.Prop(key).Rows[d];
                    Assert.That((row.RestOffsetMeters - want).magnitude, Is.LessThan(1e-4f),
                                $"{key} d{d} rest pin");
                    // ⚠️ The grip is an OFFSET and must NOT have the pivot subtracted from it. Confusing
                    // the two conversions is a ~1 m error that still looks like a plausible pose.
                    Assert.That((row.GripOffsetMeters - wantGrip).magnitude, Is.LessThan(1e-4f),
                                $"{key} d{d} grip offset");
                }
        }

        // ---- the facts a re-bake could change --------------------------------------------------------

        [Test]
        public void TheHandSwap_IsTheRigs_SmallPropsGoLeftOnTheThreeAwayFacings_AndTheRodNever()
        {
            CarryAnchorTableDef t = Table();
            // SW, W, NW — the headings where the right wrist is on the far side of the body.
            int[] away = { 5, 6, 7 };

            foreach (string small in new[] { "fish", "clam" })
            {
                foreach (int d in away)
                    Assert.That(t.Prop(small).Rows[d].Hand, Is.EqualTo(CarryHandSide.Left),
                                $"{small} must move to the near hand at d{d} or it draws through her chest");
                foreach (int d in new[] { 0, 1, 2, 3, 4 })
                    Assert.That(t.Prop(small).Rows[d].Hand, Is.EqualTo(CarryHandSide.Right), $"{small} d{d}");
            }

            // The rod is long enough to read from either side and never swaps — so a consumer that
            // "helpfully" swapped on the away headings would be wrong for it.
            for (int d = 0; d < Directions; d++)
                Assert.That(t.Prop("rodTrail").Rows[d].Hand, Is.EqualTo(CarryHandSide.Right), $"rod d{d}");
        }

        [Test]
        public void TheDrawOrder_IsTheRigs_AndItDisagreesWithTheOldHeadingRule_InBothDirections()
        {
            CarryAnchorTableDef t = Table();

            // ⭐ THE DEFECT THIS PR FIXES. At NORTH the fisher faces away, so deriving the order from the
            // heading draws a held item BEHIND her — and the rig says over, because the wrist is 0.215 m
            // out while the torso is only ~0.115 m half-wide, so the item is clear of the silhouette. That
            // is why a held clam used to disappear at N.
            Assert.That(CarryMath.AheadOrdersFor(0f, 1), Is.LessThan(0),
                        "precondition: the heading rule puts a held item behind the body at N");
            foreach (string key in new[] { "rodTrail", "fish", "clam" })
                Assert.That(t.Prop(key).Rows[0].Behind, Is.False,
                            $"{key} must draw OVER at N — this is the vanishing-item case");

            // …and the reverse, so this is not simply "never behind": at WEST the heading rule reads abeam
            // and resolves to in-front, while the rig genuinely wants the rod under the body.
            Assert.That(CarryMath.AheadOrdersFor(270f, 1), Is.GreaterThan(0),
                        "precondition: the heading rule puts an abeam item in front");
            Assert.That(t.Prop("rodTrail").Rows[6].Behind, Is.True, "the trailing rod is under her at W");
        }

        [Test]
        public void TheSlungRod_DrawsUnder_ExactlyWhereAHandPropDrawsOver()
        {
            CarryAnchorTableDef t = Table();
            CarryPropAnchors sling = t.Prop("rodSling"), trail = t.Prop("rodTrail");

            // A back mount inverts: it is aft, so it hides behind her on the headings where a hand prop is
            // in front. Pinned because it is the one row whose order a reader's intuition gets backwards.
            for (int d = 0; d < Directions; d++)
                Assert.That(sling.Rows[d].Mount, Is.EqualTo(CarryMountKind.Back), $"sling d{d} mount");

            foreach (int d in new[] { 2, 3, 4, 5 })
            {
                Assert.That(sling.Rows[d].Behind, Is.True, $"the sling is under her at d{d}");
                Assert.That(trail.Rows[d].Behind, Is.False, $"the trailing rod is over her at d{d}");
            }
            Assert.That(sling.Rows[0].Behind, Is.False, "and over her at N, where she faces away");
        }

        [Test]
        public void TheWristGrid_CoversTheThreeFreeGaits_AtTheFrameCountsTheBodyIsActuallyDrawnAt()
        {
            CarryAnchorTableDef t = Table();
            var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(IsoFisherVisual);
            Assert.That(visual, Is.Not.Null, $"no character visual def at '{IsoFisherVisual}'");

            foreach (CharacterGait gait in new[]
                     { CharacterGait.Idle, CharacterGait.Walk, CharacterGait.Run })
            {
                CarryWristFrames w = t.Wrists.FirstOrDefault(x => x.Gait == gait);
                Assert.That(w, Is.Not.Null, $"no wrist grid for the {gait} gait");

                // ⚠️ The runtime indexes this grid by the frame the VISUAL DEF is showing. If the two
                // counts drift, TryAnchorMeters wraps and the prop tracks the wrong point of the cycle
                // rather than failing — visible as a hand that leads or lags the arm, and sourceless.
                Assert.That(w.FramesPerDir, Is.EqualTo(visual.FrameCountFor(gait)),
                            $"the {gait} wrist grid has {w.FramesPerDir} frames/dir but the body is drawn " +
                            $"at {visual.FrameCountFor(gait)} — re-run BOTH importers after a re-bake");
                Assert.That(w.Left.Length, Is.EqualTo(Directions * w.FramesPerDir));
                Assert.That(w.Right.Length, Is.EqualTo(Directions * w.FramesPerDir));
            }
        }

        [Test]
        public void TheWristsGenuinelyMove_AcrossTheWalkCycle_WhichIsWhyTheLivePathExists()
        {
            CarryAnchorTableDef t = Table();
            CarryWristFrames walk = t.Wrists.First(x => x.Gait == CharacterGait.Walk);

            float widest = 0f;
            for (int d = 0; d < Directions; d++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int f = 0; f < walk.FramesPerDir; f++)
                {
                    float x = walk.Right[d * walk.FramesPerDir + f].x;
                    lo = Mathf.Min(lo, x);
                    hi = Mathf.Max(hi, x);
                }
                widest = Mathf.Max(widest, hi - lo);
            }

            // The measured swing is ~8.3 px ≈ 0.26 m. A tenth of a metre is a floor that a genuinely
            // static grid (an import that read frame 0 eight times) could not clear.
            Assert.That(widest, Is.GreaterThan(0.1f),
                        "the walk wrist grid does not move across the cycle — the import collapsed the " +
                        "frames, and every carried prop would hang off a stationary point");
        }

        // ---- the shovel, which is the trap ------------------------------------------------------------

        [Test]
        public void NoShovelRow_SoTheShovelDefNamesNoHandProp_AndTheRodNamesTheCarriedOne()
        {
            CarryAnchorTableDef t = Table();

            Assert.That(t.Prop("shovel"), Is.Null);
            Assert.That(t.Prop("shovelTrail"), Is.Null,
                        "a shovel row appeared — if the art lane has baked one, wire Shovel.asset to it " +
                        "rather than leaving this red");

            var rod = AssetDatabase.LoadAssetAtPath<ToolDef>("Assets/_Project/Data/Tools/Rod.asset");
            var shovel = AssetDatabase.LoadAssetAtPath<ToolDef>("Assets/_Project/Data/Tools/Shovel.asset");
            Assert.That(rod, Is.Not.Null);
            Assert.That(shovel, Is.Not.Null);

            Assert.That(rod.HandPropKey, Is.EqualTo(CarryPropKeys.RodTrail),
                        "the rod is CARRIED in the hand; the sling is a back mount nothing poses yet");
            Assert.That(string.IsNullOrEmpty(shovel.HandPropKey), Is.True,
                        "⚠️ the shovel must name NO row until one is baked for it. Pointing it at 'gaff' " +
                        "would put a 1.04 m spade on a 1.55 m pole's grip.");
        }

        [Test]
        public void TheDeclaredArtDebts_ImportAsRowsWithNoRig_RatherThanBeingDroppedOnTheFloor()
        {
            CarryAnchorTableDef t = Table();

            // knife and gaff carry a spec and no art. They must still IMPORT — the day their rigs bake,
            // the anchoring is already right and only the sprite is new.
            foreach (string debt in new[] { "knife", "gaff" })
            {
                Assert.That(t.Prop(debt), Is.Not.Null, $"'{debt}' must still import");
                Assert.That(string.IsNullOrEmpty(t.Prop(debt).Rig), Is.True,
                            $"'{debt}' is a declared art debt and should carry no rig");
            }
            foreach (string drawn in new[] { "rodTrail", "fish", "clam", "rope" })
                Assert.That(string.IsNullOrEmpty(t.Prop(drawn).Rig), Is.False,
                            $"'{drawn}' has art baked and should name its rig");

            Assert.That(t.Prop("rope").ItemScale, Is.EqualTo(0.5f).Within(1e-4f),
                        "the carried rope hank is a half-scale deck coil");
        }
    }
}
