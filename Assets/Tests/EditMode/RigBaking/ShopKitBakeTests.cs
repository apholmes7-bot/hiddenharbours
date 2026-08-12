using System;
using System.Globalization;
using NUnit.Framework;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the SHOP KIT's contract against the rigs themselves — the sibling of
    /// <c>InteriorRigBakeTests</c> and <c>VillageBuildingSetTests</c>.
    ///
    /// <para>Nothing here bakes a sheet. Every claim the kit makes is answerable from the rigs' own
    /// <c>dims</c>/<c>anchors</c>/<c>sheet</c> surface, and a test that renders eight facings of nine
    /// trades to assert a number it could have asked for is a slow test, not a strong one. The two that
    /// genuinely need pixels — the union crop and therefore the pack — measure the bounding box in JS and
    /// bring back four integers.</para>
    ///
    /// <para><b>What these are really defending.</b> Every failure mode this kit has is silent. The
    /// sheets bake, the sprites slice, the building stands, and the defect is a room turned a half-turn,
    /// or a shell a few centimetres wider than its own interior, or a texture that imported at half size.
    /// None of it throws and none of it is visible in a diff.</para>
    /// </summary>
    public sealed class ShopKitBakeTests
    {
        const string Shell = "Shopfront";
        const string Room = "ShopInterior";
        const string Plan = "ShopBuilding";

        static IRigScriptHost WholeKit()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            // Installs shopInterior, then shopfront, then shopBuilding — the declared Prerequisites.
            RigCatalog.InstallModule(host, RigCatalog.Get("shopBuilding"));
            return host;
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static string ShellOpts(ShopKit.Build b) => $"Object.assign({{}},{Shell}.PRESETS['{b.Preset}'])";

        static string LevelOpts(ShopKit.Build b, string level) =>
            ShopLevelBaker.OptionsLiteral(b.Trade, level, b.Size);

        // =================================================================================
        //  the kit loads, and loads in one piece
        // =================================================================================

        [Test]
        public void InstallingTheBuildingRigInstallsTheWholeKit()
        {
            using IRigScriptHost host = WholeKit();

            foreach (string g in new[] { Shell, Room, Plan })
                Assert.That(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"), Is.True,
                    $"{g} is not installed. The three rigs are declared as Prerequisites of shopBuilding " +
                    "precisely so one install brings the kit; if this fails the catalog's dependency " +
                    "list has drifted from the rigs.");
        }

        /// <summary>
        /// The interior rig owns the 0.5 m cell SNAP and the other two read it back, so a shell resolved
        /// without it is a shell its own interior cannot fit. MEASURED: restaurant is 8.90 × 11.25 m
        /// alone and 9.00 × 11.50 m with the interior rig loaded.
        ///
        /// <para>This is pinned as a test rather than trusted to the Prerequisites list because the
        /// Prerequisites list is exactly the thing that could be edited away, and the symptom — a shell
        /// a hundred millimetres out — is not one anybody spots by eye.</para>
        /// </summary>
        [Test]
        public void ShellFootprintNeedsTheInteriorRigLoadedToBeSnapped()
        {
            double alone, together;

            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                // Deliberately NOT through the catalog entry — that would drag the prerequisite in and
                // there would be nothing left to measure.
                host.Execute(RigCatalog.ReadSource(RigCatalog.Get("shopfront")));
                alone = host.EvaluateNumber($"{Shell}.dims({{type:'restaurant'}}).Ln");
            }

            using (IRigScriptHost host = WholeKit())
                together = host.EvaluateNumber($"{Shell}.dims({{type:'restaurant'}}).Ln");

            Assert.That(together, Is.EqualTo(11.5).Within(1e-9),
                "the snapped restaurant length moved — the kit's 0.5 m plan grid is the registration " +
                "contract and every room rect is derived from it.");
            Assert.That(alone, Is.Not.EqualTo(together).Within(1e-9),
                "the shopfront rig now resolves the same footprint with and without the interior rig. " +
                "That would be an improvement, but it means RigCatalog's comment (and this test's " +
                "reason to exist) is stale — re-measure and rewrite both rather than deleting this.");
        }

        /// <summary>
        /// Naming a room without the building rig loaded returns the WHOLE SHELL under that room's name.
        /// MEASURED: restaurant/kitchen is 6.09 × 5.09 m planned, 9.00 × 11.50 m unplanned — the same
        /// call, 3.5× the area, no error. <c>dims().planned</c> is the rig's own report of which happened
        /// and is the only thing that distinguishes them.
        /// </summary>
        [Test]
        public void NamingARoomWithoutThePlanRigSilentlyReturnsTheWholeShell()
        {
            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                RigCatalog.InstallModule(host, RigCatalog.Get("shopInterior"));
                Assert.That(host.EvaluateBool($"{Room}.dims({{type:'restaurant',room:'kitchen'}}).planned"),
                    Is.False, "without the plan rig there is no plan to adopt.");
                Assert.That(host.EvaluateNumber($"{Room}.dims({{type:'restaurant',room:'kitchen'}}).Wd"),
                    Is.EqualTo(9.0).Within(1e-6),
                    "unplanned, a named room resolves the whole shell — that is the silent failure.");
            }

            using (IRigScriptHost host = WholeKit())
            {
                Assert.That(host.EvaluateBool($"{Room}.dims({{type:'restaurant',room:'kitchen'}}).planned"),
                    Is.True,
                    "with the whole kit loaded a named room MUST adopt its plan. If this fails, any " +
                    "single-room bake is silently drawing the entire building.");
                Assert.That(host.EvaluateNumber($"{Room}.dims({{type:'restaurant',room:'kitchen'}}).Wd"),
                    Is.LessThan(9.0),
                    "the kitchen is a room inside the restaurant, so it must be narrower than the shell.");
            }
        }

        // =================================================================================
        //  the registration — the #418 trap, and why it does NOT apply here
        // =================================================================================

        /// <summary>
        /// All three rigs put the street door on the <b>+Y</b> gable.
        ///
        /// <para>This is the load-bearing difference from the house family. <c>interiorIsoRig</c> puts
        /// its door on −Y, which is why a cottage room stands a half-turn from its shell and why
        /// <c>InteriorKit.exteriorFacingOffset</c> is 4. Anyone reading that and carrying the 4 across to
        /// the shops would rotate all three buildings: the player walks in the street door and comes out
        /// behind the counter. If this test ever fails, the offset below is the thing to re-measure —
        /// do not patch the offset and leave this red.</para>
        /// </summary>
        [Test]
        public void EveryShopRigPutsItsDoorOnThePlusYGable()
        {
            using IRigScriptHost host = WholeKit();

            foreach (var build in ShopKit.ShopSet)
            {
                int shell = ShopRegistrationProbe.DoorGable(
                    host, $"{Shell}.anchors($DIR,{ShellOpts(build)})", out double shellTravel);
                int room = ShopRegistrationProbe.DoorGable(
                    host, $"{Room}.anchors($DIR,{{type:'{build.Trade}'}})", out _);
                int plan = ShopRegistrationProbe.DoorGable(
                    host, $"{Plan}.anchors($DIR,{LevelOpts(build, ShopKit.GroundLevel)})", out _);

                Assert.That(shell, Is.EqualTo(+1), $"{build.Key}: the shell's door left the +Y gable.");
                Assert.That(room, Is.EqualTo(+1), $"{build.Key}: the room's door left the +Y gable.");
                Assert.That(plan, Is.EqualTo(+1), $"{build.Key}: the plan's door left the +Y gable.");
                Assert.That(shellTravel, Is.GreaterThan(ShopRegistrationProbe.MinGableTravelPx),
                    $"{build.Key}: the door barely moves between dir 0 and dir 4, so its gable is not " +
                    "readable and the measurement below means nothing.");
            }
        }

        /// <summary>
        /// The measured shell↔level facing offset is <b>0</b> for every build — and it is measured, not
        /// asserted into existence. The probe's third layer only admits a constant offset if the two rigs
        /// turn the same way, so this is the handedness proof as well as the number.
        /// </summary>
        [Test]
        public void LevelRegistersUnderItsShellAtTheSameFacing()
        {
            using IRigScriptHost host = WholeKit();

            foreach (var build in ShopKit.ShopSet)
            {
                var reg = ShopRegistrationProbe.Measure(
                    host, Shell, ShellOpts(build),
                    Plan, LevelOpts(build, ShopKit.GroundLevel), ShopKit.Facings);

                Assert.That(reg.FacingOffset, Is.EqualTo(0),
                    $"{build.Key}: the level no longer stands under its shell at the same facing " +
                    $"(measured {reg.FacingOffset}).\n{reg.Report}");
                Assert.That(reg.FacingOffset, Is.Not.EqualTo(4),
                    $"{build.Key}: an offset of 4 is the HOUSE family's answer, and it means someone has " +
                    "made this kit's interior door change gables. Re-measure before trusting it.");
            }
        }

        [Test]
        public void AllThreeRigsShareOneCameraAndOneTurntable()
        {
            using IRigScriptHost host = WholeKit();

            Assert.That(ShopRegistrationProbe.ProjectionsAgree(
                    host, Shell, $"{Shell}.pivot", "",
                    Room, $"{Room}.pivot", "", out string report),
                Is.True, report);
        }

        // =================================================================================
        //  the build table against the rigs
        // =================================================================================

        /// <summary>
        /// Each build's trade and size must match the preset its shell bakes from. The shell takes the
        /// preset, every level takes <see cref="ShopKit.Build.Size"/>; if they drift, both sheets bake
        /// cleanly and the interior sits outside its own walls.
        /// </summary>
        [Test]
        public void EveryBuildAgreesWithThePresetItsShellBakesFrom()
        {
            using IRigScriptHost host = WholeKit();

            foreach (var build in ShopKit.ShopSet)
            {
                Assert.That(host.EvaluateBool($"!!{Shell}.PRESETS['{build.Preset}']"), Is.True,
                    $"{build.Key}: '{build.Preset}' is not a preset. An unresolved preset name bakes the " +
                    "rig's DEFAULT build with no error at all.");

                Assert.That(host.EvaluateString($"String({Shell}.PRESETS['{build.Preset}'].type)"),
                    Is.EqualTo(build.Trade),
                    $"{build.Key}: the preset is a different trade from the build, so the shell and the " +
                    "interior would be two different buildings.");

                Assert.That(host.EvaluateNumber($"{Shell}.PRESETS['{build.Preset}'].size"),
                    Is.EqualTo(build.Size).Within(1e-9),
                    $"{build.Key}: Build.Size and the preset's size have drifted.");
            }
        }

        /// <summary>The shell and every level of a build must resolve the SAME footprint — the kit's
        /// registration contract, checked rather than assumed, because the three rigs share the
        /// size → Wd/Ln formula only for as long as nobody re-drops one of them.</summary>
        [Test]
        public void ShellAndLevelResolveTheSameFootprint()
        {
            using IRigScriptHost host = WholeKit();

            foreach (var build in ShopKit.ShopSet)
            {
                double sWd = host.EvaluateNumber($"{Shell}.dims({ShellOpts(build)}).Wd");
                double sLn = host.EvaluateNumber($"{Shell}.dims({ShellOpts(build)}).Ln");

                foreach (string level in build.Levels)
                {
                    string o = LevelOpts(build, level);
                    Assert.That(host.EvaluateNumber($"{Plan}.dims({o}).Wd"), Is.EqualTo(sWd).Within(1e-6),
                        $"{build.Key}/{level}: width disagrees with the shell.");
                    Assert.That(host.EvaluateNumber($"{Plan}.dims({o}).Ln"), Is.EqualTo(sLn).Within(1e-6),
                        $"{build.Key}/{level}: length disagrees with the shell.");
                }
            }
        }

        /// <summary>
        /// <see cref="ShopKit.HasUpper"/> must match what the rig actually does. It does NOT report a
        /// missing storey: asking for a fish market's upper level returns the GROUND rooms under the name
        /// <c>upper</c>, which bakes a duplicate ground floor half a metre in the air.
        /// </summary>
        [Test]
        public void TradesWithoutAnUpperStoreyReturnTheGroundPlanForOne()
        {
            using IRigScriptHost host = WholeKit();

            foreach (string trade in new[] { "fishMarket", "takeoutStand" })
            {
                Assert.That(ShopKit.HasUpper(trade), Is.False,
                    $"{trade} is recorded as having no upper storey.");

                string ground = host.EvaluateString(
                    $"JSON.stringify({Plan}.dims({{type:'{trade}',level:'ground'}}).rooms.map(r=>r.id))");
                string upper = host.EvaluateString(
                    $"JSON.stringify({Plan}.dims({{type:'{trade}',level:'upper'}}).rooms.map(r=>r.id))");

                Assert.That(upper, Is.EqualTo(ground),
                    $"{trade}: the rig now distinguishes its upper level from its ground one. That is a " +
                    "better rig — but ShopKit.HasUpper and ShopBakeMenu's guard exist because it did " +
                    "not, so re-derive them rather than deleting this.");
            }

            foreach (var build in ShopKit.ShopSet)
                foreach (string level in build.Levels)
                    Assert.That(level == ShopKit.GroundLevel || ShopKit.HasUpper(build.Trade), Is.True,
                        $"{build.Key} asks to bake '{level}', which its trade does not have.");
        }

        // =================================================================================
        //  the pack and the cap
        // =================================================================================

        /// <summary>
        /// Every build packs inside <see cref="ShopKit.ImportSizeCap"/>. Over the cap Unity downscales
        /// SILENTLY and the sprite count still comes out right, so nothing but a pivot or cell-size
        /// assert would ever catch it.
        ///
        /// <para>The bounding box is measured in JS and comes back as four integers — pulling eight
        /// 1320×1180 RGBA buffers across the boundary to compute a bbox would cost 50 MB of copying to
        /// learn the same thing.</para>
        /// </summary>
        [Test]
        public void EveryBuildPacksInsideTheImportCap()
        {
            using IRigScriptHost host = WholeKit();
            InstallBboxHelper(host);

            foreach (var build in ShopKit.ShopSet)
            {
                AssertPacks(host, $"{build.Key} shell",
                    $"function(d){{return {Shell}.render(d,{ShellOpts(build)});}}",
                    (int)host.EvaluateNumber($"{Shell}.W"), (int)host.EvaluateNumber($"{Shell}.H"));

                foreach (string level in build.Levels)
                {
                    string o = LevelOpts(build, level);
                    AssertPacks(host, $"{build.Key}/{level}",
                        $"function(d){{return {Plan}.render(d,{o});}}",
                        (int)host.EvaluateNumber($"{Plan}.sheet({o}).W"),
                        (int)host.EvaluateNumber($"{Plan}.sheet({o}).H"));
                }
            }
        }

        static void InstallBboxHelper(IRigScriptHost host) => host.Execute(@"
            function __bbox(fn, W, H){
              var x0=1e9,y0=1e9,x1=-1,y1=-1;
              for(var d=0; d<8; d++){
                var r=fn(d), a=r.rgba?r.rgba:r, w=r.W||W, h=r.H||H;
                for(var y=0;y<h;y++){ var row=y*w*4;
                  for(var x=0;x<w;x++){ if(a[row+x*4+3]!==0){
                    if(x<x0)x0=x; if(x>x1)x1=x; if(y<y0)y0=y; if(y>y1)y1=y; } } }
              }
              return {w:x1-x0+1, h:y1-y0+1};
            }");

        static void AssertPacks(IRigScriptHost host, string what, string renderFn, int nativeW, int nativeH)
        {
            host.Execute($"var __b = __bbox({renderFn},{nativeW},{nativeH});");
            int cw = (int)host.EvaluateNumber("__b.w") + 2 * ShopLevelBaker.Padding;
            int ch = (int)host.EvaluateNumber("__b.h") + 2 * ShopLevelBaker.Padding;

            BuildingRigBaker.ChooseGrid(cw, ch, ShopKit.Facings, out int cols, out int rows,
                                        ShopKit.ImportSizeCap);
            int pw = cols * cw, ph = rows * ch;

            Assert.That(Math.Max(pw, ph), Is.LessThanOrEqualTo(ShopKit.ImportSizeCap),
                $"{what}: cropped cell {cw}×{ch} packs to {cols}×{rows} = {pw}×{ph}, past the " +
                $"{ShopKit.ImportSizeCap} px cap. Raise ShopKit.ImportSizeCap — it is the ONE number the " +
                "pack, the importer lock and the verify assert all read — or dial this build's size down.");
        }

        // =================================================================================
        //  the conventions this kit must not cross-apply
        // =================================================================================

        [Test]
        public void EveryRigCarriesTheRuledEightFacings()
        {
            using IRigScriptHost host = WholeKit();
            foreach (string g in new[] { Shell, Room, Plan })
                Assert.That((int)host.EvaluateNumber($"{g}.DIRS"), Is.EqualTo(ShopKit.Facings),
                    $"{g} carries a different facing count from the kit's. Eight is the owner's " +
                    "2026-08-05 ruling and the rigs' own turntable.");
        }

        [Test]
        public void EveryRigBakesAtThirtyTwoPixelsPerMetre()
        {
            using IRigScriptHost host = WholeKit();
            foreach (string g in new[] { Shell, Room, Plan })
                Assert.That((int)host.EvaluateNumber($"{g}.PX"), Is.EqualTo(32),
                    $"{g} left the project's 32 px = 1 m scale, so a shop would not stand in the same " +
                    "space as a cottage or a boat.");
        }

        /// <summary>
        /// The Unity pivot written into the contract is <c>(pivotX/cellW, (cellH − pivotY)/cellH)</c> —
        /// bottom-origin, and NOT bottom-centre. Under the ¾ camera the near half of the footprint
        /// projects below the ground centre, so a bottom-centre pivot sinks the building by that much,
        /// silently. (ADR 0026.)
        /// </summary>
        [Test]
        public void NormalizedPivotIsBottomOriginFromTheGroundCentre()
        {
            var e = new ShopKit.Entry { cellW = 400, cellH = 300, pivotX = 200f, pivotY = 210f };
            var p = ShopKit.NormalizedPivot(e);

            Assert.That(p.x, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(p.y, Is.EqualTo((300f - 210f) / 300f).Within(1e-6f),
                "the y term is (cellH − pivotY)/cellH. Getting it upside-down is silent and looks like " +
                "the art is wrong.");
            Assert.That(p.y, Is.Not.EqualTo(0f),
                "a bottom-centre pivot would be 0 here — that is the trap, not the convention.");
        }

        /// <summary>Sheet stems must be distinct per build and per level, or two bakes overwrite one
        /// file and the contract points half its entries at the survivor.</summary>
        [Test]
        public void EverySheetStemIsUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

            foreach (var build in ShopKit.ShopSet)
            {
                Assert.That(seen.Add(ShopKit.ShellStemFor(build.Key)), Is.True,
                    $"{build.Key}: duplicate shell stem.");
                foreach (string level in build.Levels)
                    Assert.That(seen.Add(ShopKit.LevelStemFor(build.Key, level)), Is.True,
                        $"{build.Key}/{level}: duplicate level stem.");
            }
        }
    }
}
