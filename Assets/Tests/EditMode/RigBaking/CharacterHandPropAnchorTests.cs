using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The rev-6.4 HAND-PROP anchor layer (<c>characterIsoRig6.hands.js</c> → <c>CharacterHands6</c>),
    /// drop of 2026-08-14.
    ///
    /// <para><b>What this layer is for, and therefore what these tests actually guard.</b> Before it,
    /// every carried object in the game hung off ONE serialized offset at all eight facings
    /// (<c>CarryHands._hipOffsetMeters</c>, whose own tooltip flagged it as art-lane work). The table
    /// says, per prop and per heading, which hand holds it, how far off that wrist it sits, which cell
    /// of its own turntable to draw and whether it draws over or under the sprite. So the tests that
    /// matter are not "does it load" — they are the ones that go red if the table ever collapses back
    /// toward one number, one hand, or one draw order.</para>
    ///
    /// <para><b>Measured, not transcribed.</b> Every expectation below was read out of the rig through
    /// the V8 host and cross-checked against the pack README before being written down. Where a number
    /// appears it is a MEASUREMENT with its provenance in the comment, per ADR 0021 §4 — the rig is
    /// asked, a README is never trusted.</para>
    /// </summary>
    public class CharacterHandPropAnchorTests
    {
        const string Hands = "CharacterHands6";
        const string Body = "CharacterIso6";

        /// <summary>The declared props, in the rig's own <c>PROP_ORDER</c>.</summary>
        static readonly string[] PropOrder =
            { "rodTrail", "rodSling", "fish", "clam", "knife", "gaff", "rope" };

        /// <summary>
        /// The three headings on which a small hand prop moves to the near hand, measured off the rig.
        ///
        /// <para><b>⚠️ SW/W/NW is why a cardinal-only test cannot guard this layer.</b> At N/E/S/W the
        /// swap is invisible for two of the four; the rows that actually prove the rule are the
        /// diagonals. This lane has shipped a facing bug behind cardinal-only coverage before
        /// (ADR 0034), so the non-cardinal rows are asserted explicitly.</para>
        /// </summary>
        static readonly int[] SwapDirs = { 5, 6, 7 };

        static IRigScriptHost NewHostWithHands()
        {
            var host = RigScriptHostFactory.Create();
            // Prerequisites are depth-first and in order, so this ALONE installs eye -> head -> body
            // -> hands. That is the contract being exercised, not a convenience.
            RigCatalog.InstallModule(host, RigCatalog.Get("characterHands"));
            return host;
        }

        static double Num(IRigScriptHost h, string expr) => h.EvaluateNumber(expr);

        static string Str(IRigScriptHost h, string expr) => h.EvaluateString(expr);

        static bool Bool(IRigScriptHost h, string expr) => h.EvaluateBool(expr);

        /// <summary>One <c>pin()</c> field at the rest pose the sidecar bakes at.</summary>
        static string Pin(IRigScriptHost h, string prop, int dir, string field) =>
            Str(h, $"String({Hands}.pin('{prop}',{dir},{{anim:'{CharacterRigBaker.HandPropRestAnim}'," +
                   $"frame:{CharacterRigBaker.HandPropRestFrame},elev:40,build:{{preset:'fisher'}}}}).{field})");

        static double PinNum(IRigScriptHost h, string prop, int dir, string field) =>
            double.Parse(Pin(h, prop, dir, field), CultureInfo.InvariantCulture);

        // ---- the load order IS the contract --------------------------------------------------------

        [Test]
        public void HandPropLayer_InstallsItsWholePrerequisiteChain_InOrder()
        {
            using var host = NewHostWithHands();

            Assert.IsTrue(Bool(host, $"typeof {Hands} === 'object' && {Hands} !== null"),
                          "the hand-prop layer did not install its global.");
            foreach (string g in new[] { "EyeIso", "HeadIso3", Body })
                Assert.IsTrue(Bool(host, $"typeof {g} === 'object' && {g} !== null"),
                              $"'{g}' is missing — the prerequisite chain did not run in order. " +
                              "The hands layer reads the BODY's camera basis and anchors(), so a " +
                              "reversed order does not throw: C() resolves null and every pin() " +
                              "silently returns null. That is the failure this chain exists to stop.");

            Assert.AreEqual(6.4, Num(host, $"{Hands}.pass"), 1e-9,
                            "the layer reports a pass other than 6.4 — the kit moved under the catalog.");
        }

        /// <summary>
        /// <b>The sabotage proof for the prerequisite chain.</b> Run the hands file into a host with no
        /// body and every pin comes back null — silently. This test asserts the silence, so that if
        /// anyone ever "simplifies" the catalog by dropping the prerequisite, the resulting null pins
        /// are a RED here rather than a carried object drawn at the origin in a build.
        /// </summary>
        [Test]
        public void HandPropLayer_WithoutTheBody_PinsNull_WhichIsWhyThePrerequisiteExists()
        {
            using var host = RigScriptHostFactory.Create();
            host.Execute(RigCatalog.ReadSource(RigCatalog.Get("characterHands")));   // NO body first

            Assert.IsTrue(Bool(host, $"typeof {Hands} === 'object' && {Hands} !== null"),
                          "the file still installs its global without the body — it is the PINS that " +
                          "fail, not the load, which is exactly what makes the ordering dangerous.");
            Assert.IsTrue(Bool(host, $"{Hands}.pin('clam',0,{{}}) === null"),
                          "a bodyless pin() returned something. If this ever passes a non-null the " +
                          "silent-null failure mode has changed and the prerequisite comment is stale.");
        }

        // ---- the table's shape ---------------------------------------------------------------------

        [Test]
        public void SevenProps_InTheDeclaredOrder()
        {
            using var host = NewHostWithHands();
            Assert.AreEqual(string.Join(",", PropOrder), Str(host, $"{Hands}.PROP_ORDER.join(',')"));
        }

        /// <summary>
        /// Every prop resolves a row at all eight facings. A missing row is not an exception in this
        /// rig — <c>rowOf</c> returns <c>{}</c> and the prop falls back to its base grip — so an
        /// absent facing is INVISIBLE without asking each one.
        /// </summary>
        [Test]
        public void EveryProp_ResolvesAllEightFacings()
        {
            using var host = NewHostWithHands();
            foreach (string prop in PropOrder)
                for (int d = 0; d < 8; d++)
                    Assert.IsTrue(Bool(host, $"{Hands}.pin('{prop}',{d},{{anim:'idle',frame:0}}) !== null"),
                                  $"'{prop}' has no pin at dir {d}.");
        }

        // ---- THE POINT OF THE WHOLE LAYER ----------------------------------------------------------

        /// <summary>
        /// <b>The regression guard for the bug this drop exists to fix.</b> A carried object used to
        /// hang at one offset for every heading. If this table ever collapses back to one offset — a
        /// "simplification", a botched merge, a facing block deleted — the eight rows become equal and
        /// this goes red.
        ///
        /// <para>The threshold is stated in CELL PX at 32 px/m: the measured spread of the clam's own
        /// pin across the eight facings is ~13.3 px (x from 26.05 at S to 39.39 at N), i.e. ~0.42 m.
        /// Requiring only 4 px of spread leaves enormous headroom for an art revision while still
        /// failing instantly on a collapse to a constant.</para>
        /// </summary>
        [Test]
        public void CarriedOffsets_GenuinelyDifferPerFacing_NotOneOffsetForAllEight()
        {
            using var host = NewHostWithHands();

            foreach (string prop in new[] { "clam", "fish", "rodTrail", "rodSling" })
            {
                var xs = new List<double>();
                var ys = new List<double>();
                for (int d = 0; d < 8; d++)
                {
                    xs.Add(PinNum(host, prop, d, "x"));
                    ys.Add(PinNum(host, prop, d, "y"));
                }

                double spreadX = xs.Max() - xs.Min(), spreadY = ys.Max() - ys.Min();
                Assert.Greater(spreadX + spreadY, 4.0,
                    $"'{prop}' pins to within {spreadX + spreadY:0.00} px across all eight facings — " +
                    "that is one offset wearing eight hats, which is the exact defect this layer was " +
                    "imported to fix. Do not relax this number; find what flattened the table.");
            }
        }

        /// <summary>
        /// A small held prop moves to the NEAR hand on the three headings where the right wrist is on
        /// the far side of the body. The rod never swaps (it is long enough to read from the far hand,
        /// and a rod changing hands mid-turn reads as two rods).
        /// </summary>
        [Test]
        public void SmallProps_SwapHands_OnTheThreeAwayFacings_AndTheRodNever_Does()
        {
            using var host = NewHostWithHands();

            foreach (string prop in new[] { "fish", "clam" })
                for (int d = 0; d < 8; d++)
                {
                    bool expectSwap = System.Array.IndexOf(SwapDirs, d) >= 0;
                    Assert.AreEqual(expectSwap ? "L" : "R", Pin(host, prop, d, "hand"),
                        $"'{prop}' at dir {d} ({Pin(host, prop, d, "facing")}) is in the wrong hand. " +
                        "The swap set is SW/W/NW — the DIAGONALS are what prove it, so this must not " +
                        "be reduced to a cardinal-only check (ADR 0034).");
                }

            for (int d = 0; d < 8; d++)
                Assert.AreEqual("R", Pin(host, "rodTrail", d, "hand"),
                                $"the trailed rod swapped hands at dir {d}; it must never swap.");
        }

        /// <summary>
        /// The turntable contract: a prop baked on a turntable has no yaw, only eight cells, so its
        /// heading is whichever cell you draw. <c>turn</c> is that heading relative to the body, and it
        /// is what stops a cradled fish being end-on at N and S. The clam is a cupped handful with no
        /// meaningful heading and correctly turns nowhere.
        /// </summary>
        [Test]
        public void Fish_TurnsAcrossTheView_AndTheClamDoesNot()
        {
            using var host = NewHostWithHands();

            // Measured: N and S take a 2-step (90°) turn, which is where a body-aligned fish would
            // otherwise foreshorten to a blob.
            Assert.AreEqual(2, (int)PinNum(host, "fish", 0, "turn"), "fish at N should turn 90°.");
            Assert.AreEqual(2, (int)PinNum(host, "fish", 4, "turn"), "fish at S should turn 90°.");
            Assert.AreEqual(0, (int)PinNum(host, "fish", 2, "turn"),
                            "fish at E already reads at full length and should not turn.");

            // itemDir is the resolved cell: (dir + turn) mod 8.
            for (int d = 0; d < 8; d++)
            {
                int turn = (int)PinNum(host, "fish", d, "turn");
                int itemDir = (int)PinNum(host, "fish", d, "itemDir");
                Assert.AreEqual(((d + turn) % 8 + 8) % 8, itemDir,
                                $"fish itemDir at dir {d} does not equal (dir + turn) mod 8.");
            }

            for (int d = 0; d < 8; d++)
                Assert.AreEqual(d, (int)PinNum(host, "clam", d, "itemDir"),
                                "a cupped handful has no heading and should draw its own dir cell.");
        }

        /// <summary>
        /// <b>Draw order is not a depth test</b>, and this is the assert that says so. A back mount
        /// (the slung rod) is aft by construction, so it inverts against the hand props: it goes UNDER
        /// the sprite on the facings where a hand prop goes over. Measured: the sling draws under at
        /// E/SE/S/SW and over at N/NE/W/NW.
        /// </summary>
        [Test]
        public void SlungRod_DrawOrderInverts_AgainstTheHandProps()
        {
            using var host = NewHostWithHands();

            var underAt = new List<int>();
            for (int d = 0; d < 8; d++)
                if (Pin(host, "rodSling", d, "behind") == "true") underAt.Add(d);

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, underAt,
                "the slung rod must draw under the sprite exactly on E/SE/S/SW. A back mount whose " +
                "order matches the hand props' is a sign the mount kind was lost.");

            // And the clam — a hand prop — is over the sprite everywhere: the wrist sits 0.215 m out
            // against a ~0.115 m torso half-width, so a small held item is never covered. That
            // asymmetry IS the rule ('deeper than the body axis AND laterally inside the torso'), and
            // getting it wrong is what used to make held items vanish at N.
            for (int d = 0; d < 8; d++)
                Assert.AreEqual("false", Pin(host, "clam", d, "behind"),
                                $"the clam went behind the sprite at dir {d}; a hand prop clears the " +
                                "silhouette at every heading.");
        }

        /// <summary>
        /// The rod's carried states are the two the rig declares, and the SLING is the one that rides
        /// everything: it is a back mount, so it survives dig / haul / ladder, where both hands are
        /// committed. That is the whole reason to sling a rod, and it is what #529's catch path needs
        /// when a landed clam takes the hands.
        /// </summary>
        [Test]
        public void Rod_HasABackMountedSling_AndAHandMountedTrail()
        {
            using var host = NewHostWithHands();

            Assert.AreEqual("back", Str(host, $"{Hands}.PROPS.rodSling.mount"));
            Assert.AreEqual("hand", Str(host, $"{Hands}.PROPS.rodTrail.mount"));
            Assert.AreEqual("0", Str(host, $"String({Hands}.PROPS.rodSling.hands)"),
                            "a slung rod occupies no hands — that is what lets it ride a dig.");
            Assert.AreEqual("null", Pin(host, "rodSling", 0, "hand"),
                            "a back mount is held by no hand.");

            foreach (string prop in new[] { "rodTrail", "rodSling" })
                Assert.AreEqual("RodIso", Str(host, $"{Hands}.PROPS.{prop}.rig"));
        }

        /// <summary>
        /// The art debts the drop declares, asserted so they cannot be forgotten OR quietly invented.
        /// <c>knife</c> and <c>gaff</c> have no rig at all and carry a stub spec instead; the carried
        /// rope is a half-scaled deck prop. Rod, fish and clam need no new art — each already bakes to
        /// its own grip, which is why each pins with a ZERO pivot fix.
        /// </summary>
        [Test]
        public void TheDeclaredArtDebts_AreExactlyKnifeGaffAndTheRopeCoil()
        {
            using var host = NewHostWithHands();

            foreach (string stubbed in new[] { "knife", "gaff" })
            {
                Assert.AreEqual("null", Str(host, $"String({Hands}.PROPS.{stubbed}.rig)"),
                                $"'{stubbed}' reports a rig — if one was baked, this test and the " +
                                "kit's 'Art still owed' list both need updating.");
                Assert.IsTrue(Bool(host, $"{Hands}.PROPS.{stubbed}.stub != null"),
                              $"'{stubbed}' has neither a rig nor a stub spec — it is unanchorable.");
            }

            Assert.AreEqual(0.5, Num(host, $"{Hands}.PROPS.rope.itemScale"), 1e-9,
                "the carried rope is the deck coil at half scale. itemScale != 1 is an ART DEBT, not " +
                "a setting — a carried hank wants its own bake.");

            foreach (string baked in new[] { "rodTrail", "fish", "clam" })
            {
                Assert.AreEqual(0.0, Num(host, $"{Hands}.item('{baked}').pivotFix.dx"), 1e-9,
                                $"'{baked}' should pin with no pivot fix — its rig bakes to its grip.");
                Assert.AreEqual(0.0, Num(host, $"{Hands}.item('{baked}').pivotFix.dy"), 1e-9);
                Assert.AreEqual(1.0, Num(host, $"{Hands}.PROPS.{baked}.itemScale || 1"), 1e-9,
                                $"'{baked}' should draw at 1:1 — it was baked for the hand.");
            }
        }

        /// <summary>
        /// <b>There is no shovel row, and that is a finding rather than a bug.</b> The table anchors a
        /// carried ROD (trail + sling) and a carried CATCH (fish, clam), but a carried SHOVEL — the
        /// thing the fisher walks the flats with between digs — has no authored anchor.
        ///
        /// <para>The dig itself is unaffected: <c>dig</c> mounts the spade through the body rig's own
        /// <c>tool()</c> pin, which is authored and already baked. What is missing is only the
        /// walking-with-it pose. This test PINS the absence so the day a <c>shovelTrail</c> row lands
        /// it goes red and is consumed deliberately — and so nobody silently borrows the gaff's row for
        /// a shovel, which would be a tuned constant rescaled onto a different lever.</para>
        /// </summary>
        [Test]
        public void NoShovelRow_Exists_TheCarriedShovelIsAnOpenArtAsk()
        {
            using var host = NewHostWithHands();

            Assert.IsFalse(Bool(host, $"'shovel' in {Hands}.PROPS || 'shovelTrail' in {Hands}.PROPS"),
                "a shovel row appeared in the hand-prop table. That is GOOD NEWS and this test is the " +
                "reminder to wire it: give CarriableTool the shovel's rows rather than leaving the " +
                "spade on the single hip offset.");

            // The dig's own mount is a different, authored path — asserted here so the finding above
            // cannot be misread as 'the shovel has no anchors at all'.
            Assert.AreEqual("shovel", Str(host, $"{Body}.ANIM_MOUNT['dig']"),
                            "the dig clip still mounts the spade through the body rig's tool() pin.");
        }
    }
}
