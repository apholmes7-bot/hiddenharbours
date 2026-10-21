using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>The helm band's frame</b> (H2, the owner's 2026-09-24 ruling), pinned where it is pure: the
    /// band is the full width of the bottom edge and 144·k tall, k = max(1, H div 1080); the end caps
    /// sit at the screen's edges, the wheel is centred, and every other piece packs outward from it
    /// on its own side inside the central 1280·k, with the panel in every gap. Every housing is always
    /// drawn, so the layout never moves with the fit; only what the plates say does. The band's code
    /// names no hull (rule 2): which hull has which helm is the helm asset's data. Nothing here paints,
    /// so the class runs without a GPU or a texture.
    /// </summary>
    public class HelmBandLayoutTests
    {
        /// <summary>The ruling's screens: 720p, 800p, 1080p, 1440p, ultrawide and 4K.</summary>
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        /// <summary>k on each of <see cref="Screens"/>.</summary>
        private static readonly int[] ScreenK = { 1, 1, 1, 1, 1, 2 };

        /// <summary>The band's share of each screen's height, as the ruling's table gives it (%).</summary>
        private static readonly float[] SharePct = { 20f, 18f, 13.3f, 10f, 10f, 13.3f };

        private static readonly ConsoleRigKind[] Rigs =
        {
            ConsoleRigKind.Console, ConsoleRigKind.Sport, ConsoleRigKind.Novi, ConsoleRigKind.Cape,
        };

        private static bool IsPilot(ConsoleRigKind rig) => HelmDashGeometry.IsPilothouse(rig);

        // ---- the frame -------------------------------------------------------------------------------

        [Test]
        public void TheBand_IsFullWidth_144kTall_OnTheBottomEdge_AtEveryListedScreen()
        {
            for (int s = 0; s < Screens.Length; s++)
            {
                Vector2Int sc = Screens[s];
                int k = ScreenK[s], top = 144 * k;
                Assert.AreEqual(k, HelmBandLayout.K(sc.y), $"{sc}: k is the screen's whole 1080s, never below 1");
                Assert.AreEqual(top, HelmBandLayout.BandHeightPx(sc.y), $"{sc}: the band is 144k tall");
                Assert.AreEqual(new Rect(0f, 0f, sc.x, top), HelmBandLayout.BandRect(sc.x, sc.y),
                    $"{sc}: the band is the full width of the bottom edge");

                foreach (ConsoleRigKind rig in Rigs)
                {
                    var ctl = new HelmBandController();
                    ctl.Layout(rig, sc.x, sc.y);
                    Assert.AreEqual(k, ctl.K, $"{rig} at {sc}: the controller lays out at the band's k");
                    Assert.IsTrue(ctl.Contains(new Vector2(0f, 0f)), $"{rig} at {sc}: the band starts at the corner");
                    Assert.IsTrue(ctl.Contains(new Vector2(sc.x - 0.5f, top - 0.5f)),
                        $"{rig} at {sc}: …and runs to the far edge, 144k up");
                    Assert.IsFalse(ctl.Contains(new Vector2(sc.x / 2f, top)), $"{rig} at {sc}: …and not a pixel above");
                    Assert.IsFalse(ctl.Contains(new Vector2(sc.x, 10f)), $"{rig} at {sc}: …nor past the screen's edge");
                    for (int i = 0; i < ctl.PieceCount; i++)
                    {
                        Rect r = ctl.PieceRect(i);
                        Assert.AreEqual(0f, r.yMin, $"{rig} at {sc}, piece {i}: on the bottom edge");
                        Assert.AreEqual(top, r.height, $"{rig} at {sc}, piece {i}: the band's full height");
                    }
                }
            }
            Assert.AreEqual(1, HelmBandLayout.K(600), "a screen shorter than 1080 still draws the band at x1");

            var none = new HelmBandController();
            none.Layout(ConsoleRigKind.None, 1920, 1080);
            Assert.AreEqual(0, none.PieceCount, "a hull with no console has no band");
            Assert.IsFalse(none.Contains(new Vector2(10f, 10f)), "…and covers nothing");
        }

        [Test]
        public void TheBandsShare_IsTheRulingsTable_AtEveryListedScreen()
        {
            for (int s = 0; s < Screens.Length; s++)
            {
                float share = 100f * HelmBandLayout.BandHeightPx(Screens[s].y) / Screens[s].y;
                Assert.AreEqual(SharePct[s], share, 0.05f, $"{Screens[s]}: the band's share of the screen's height");
            }
        }

        // ---- the pieces ------------------------------------------------------------------------------

        [Test]
        public void EveryPiece_SitsInsideTheCentral1280_AndThePanelFillsEveryGap()
        {
            foreach (ConsoleRigKind rig in Rigs)
            {
                for (int s = 0; s < Screens.Length; s++)
                {
                    Vector2Int sc = Screens[s];
                    int k = ScreenK[s];
                    int n = HelmBandGeometry.PieceCount(rig);
                    float spanL = sc.x / 2 - HelmBandGeometry.SpanW / 2 * k;
                    float spanR = spanL + HelmBandGeometry.SpanW * k;
                    // The panel is one quad under the whole band, so every gap between pieces shows it:
                    // what has to hold is that every piece lies on it and none lies on another.
                    Rect panel = HelmBandLayout.BandRect(sc.x, sc.y);
                    float prevRight = float.NegativeInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        HelmBandPiece p = HelmBandGeometry.Piece(rig, i);
                        Rect r = HelmBandLayout.PieceRect(rig, i, sc.x, sc.y);
                        string at = $"{rig} at {sc.x}x{sc.y}, piece {i} ({p.Kind})";
                        Assert.AreEqual(p.W * k, r.width, at + ": drawn at xk");
                        Assert.IsTrue(panel.xMin <= r.xMin && r.xMax <= panel.xMax
                                      && panel.yMin <= r.yMin && r.yMax <= panel.yMax, at + ": on the panel");
                        Assert.GreaterOrEqual(r.xMin, prevRight, at + ": overlaps the piece before it");
                        prevRight = r.xMax;
                        if (p.Kind == HelmBandPieceKind.Cap)
                        {
                            Assert.AreEqual(i == 0 ? 0f : sc.x - HelmBandGeometry.CapW * k, r.xMin,
                                at + ": the end caps sit at the screen's edges");
                            continue;
                        }
                        Assert.GreaterOrEqual(r.xMin, spanL, at + ": inside the central 1280k");
                        Assert.LessOrEqual(r.xMax, spanR, at + ": inside the central 1280k");
                        if (HelmBandGeometry.Piece(rig, i - 1).Kind != HelmBandPieceKind.Cap)
                            Assert.AreEqual(HelmBandLayout.PieceRect(rig, i - 1, sc.x, sc.y).xMax, r.xMin,
                                at + ": packs against its neighbour, so the panel shows only outboard of the pieces");
                    }
                }
            }
        }

        [Test]
        public void TheFullestPilotBand_FitsEachSideOfTheWheel_At1280()
        {
            int side = (HelmBandGeometry.SpanW - HelmBandGeometry.WheelW) / 2;
            Assert.AreEqual(512, side, "each side of the wheel has 512 of the 1280, its end cap included");
            Assert.AreEqual(494, HelmBandGeometry.SideW(ConsoleRigKind.Novi, HelmBandSide.Left));
            Assert.AreEqual(468, HelmBandGeometry.SideW(ConsoleRigKind.Novi, HelmBandSide.Right));
            Assert.AreEqual(382, HelmBandGeometry.SideW(ConsoleRigKind.Console, HelmBandSide.Left));
            Assert.AreEqual(376, HelmBandGeometry.SideW(ConsoleRigKind.Console, HelmBandSide.Right));
            Assert.Greater(HelmBandGeometry.SideW(ConsoleRigKind.Novi, HelmBandSide.Right) + HelmBandGeometry.PlateW, side,
                "why the radar's housing goes left: on the right, where the dash's brow order puts it, that side needs 572");

            foreach (ConsoleRigKind rig in Rigs)
            {
                Assert.AreEqual(HelmBandGeometry.WheelW, HelmBandGeometry.SideW(rig, HelmBandSide.Centre), $"{rig}: the wheel alone is centred");
                Assert.LessOrEqual(HelmBandGeometry.SideW(rig, HelmBandSide.Left), side, $"{rig}: the left side fits its 512");
                Assert.LessOrEqual(HelmBandGeometry.SideW(rig, HelmBandSide.Right), side, $"{rig}: the right side fits its 512");

                // On the narrowest listed screen the pieces clear the end caps at the edges.
                int n = HelmBandGeometry.PieceCount(rig);
                Rect leftCap = HelmBandLayout.PieceRect(rig, 0, 1280, 720);
                Rect rightCap = HelmBandLayout.PieceRect(rig, n - 1, 1280, 720);
                Assert.LessOrEqual(leftCap.xMax, HelmBandLayout.PieceRect(rig, 1, 1280, 720).xMin, $"{rig}: clear of the left cap at 1280");
                Assert.LessOrEqual(HelmBandLayout.PieceRect(rig, n - 2, 1280, 720).xMax, rightCap.xMin, $"{rig}: clear of the right cap at 1280");
            }
        }

        [Test]
        public void ThePieces_KeepTheirOrderAndSide_AtEveryWidth()
        {
            HelmBandPieceKind[] skiff =
            {
                HelmBandPieceKind.Cap, HelmBandPieceKind.Switches, HelmBandPieceKind.Tach, HelmBandPieceKind.SounderPlate,
                HelmBandPieceKind.Wheel, HelmBandPieceKind.Compass, HelmBandPieceKind.Fuel, HelmBandPieceKind.Drive,
                HelmBandPieceKind.Cap,
            };
            HelmBandPieceKind[] pilot =
            {
                HelmBandPieceKind.Cap, HelmBandPieceKind.Switches, HelmBandPieceKind.Tach, HelmBandPieceKind.SounderPlate,
                HelmBandPieceKind.RadarPlate, HelmBandPieceKind.Wheel, HelmBandPieceKind.Compass, HelmBandPieceKind.GpsPlate,
                HelmBandPieceKind.Fuel, HelmBandPieceKind.Drive, HelmBandPieceKind.Cap,
            };
            var widths = new[]
            {
                new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440),
            };
            foreach (ConsoleRigKind rig in Rigs)
            {
                HelmBandPieceKind[] order = IsPilot(rig) ? pilot : skiff;
                int n = HelmBandGeometry.PieceCount(rig);
                int wheel = HelmBandGeometry.WheelIndex(rig);
                Assert.AreEqual(order.Length, n, $"{rig}: the piece count");
                for (int i = 0; i < n; i++)
                {
                    HelmBandPiece p = HelmBandGeometry.Piece(rig, i);
                    Assert.AreEqual(order[i], p.Kind, $"{rig}, piece {i}: the listed order");
                    HelmBandSide want = i < wheel ? HelmBandSide.Left : (i > wheel ? HelmBandSide.Right : HelmBandSide.Centre);
                    Assert.AreEqual(want, p.Side, $"{rig}, piece {i}: its side of the wheel");
                }

                foreach (Vector2Int sc in widths)
                {
                    Rect w = HelmBandLayout.PieceRect(rig, wheel, sc.x, sc.y);
                    Assert.AreEqual(sc.x / 2, w.center.x, 1e-3f, $"{rig} at {sc}: the wheel is centred");
                    for (int i = 1; i < n; i++)
                        Assert.Greater(HelmBandLayout.PieceRect(rig, i, sc.x, sc.y).xMin,
                                       HelmBandLayout.PieceRect(rig, i - 1, sc.x, sc.y).xMin,
                                       $"{rig} at {sc}, piece {i}: left to right in the listed order");
                    for (int i = 0; i < n; i++)
                    {
                        Rect r = HelmBandLayout.PieceRect(rig, i, sc.x, sc.y);
                        if (i < wheel) Assert.LessOrEqual(r.xMax, w.xMin, $"{rig} at {sc}, piece {i}: left of the wheel");
                        if (i > wheel) Assert.GreaterOrEqual(r.xMin, w.xMax, $"{rig} at {sc}, piece {i}: right of the wheel");
                    }
                }
            }
        }

        [Test]
        public void EveryHitShape_IsAtLeast24Square_AndLiesInsideItsPiece()
        {
            const int floor = HelmBandGeometry.HitFloorPx;
            Assert.AreEqual(24, floor, "the brief's floor");

            foreach (ConsoleRigKind rig in Rigs)
            {
                // The anchor switch: padded to the floor, inside the switches piece.
                Assert.IsTrue(HelmBandGeometry.TryAnchorHitBox(rig, out RectInt b), $"{rig}: an anchor hit box");
                int sw = HelmBandGeometry.Piece(rig, HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.Switches)).W;
                Assert.GreaterOrEqual(b.width, floor, $"{rig}: the switch is at least 24 wide");
                Assert.GreaterOrEqual(b.height, floor, $"{rig}: …and 24 tall");
                Assert.IsTrue(b.xMin >= 0 && b.xMax <= sw && b.yMin >= 0 && b.yMax <= HelmBandGeometry.H,
                    $"{rig}: the switch's hit lies inside the switches piece ({b})");

                // Every housing is its whole piece: a press anywhere on the plate is the plate.
                for (int i = 0; i < HelmBandGeometry.PieceCount(rig); i++)
                {
                    HelmBandPiece p = HelmBandGeometry.Piece(rig, i);
                    if (!p.IsPlate) continue;
                    Assert.GreaterOrEqual(p.W, floor, $"{rig}, piece {i}: a housing is at least 24 wide");
                }
            }

            // The wheel's grab: a 24-square on the rim is all grab, and nothing outside the piece is,
            // though the padded ring reaches past the piece's sides.
            float pad = HelmWheelSettings.Default.RimGrabPadPx;
            Assert.Greater(WheelRigGeometry.OUTER + pad, HelmBandGeometry.WheelW / 2f,
                "premise: the padded ring is wider than the piece, so the clip is what keeps it off the housings");
            var rimTop = new Vector2(HelmBandGeometry.HubX, HelmBandGeometry.HubY - WheelRigGeometry.R);
            for (int dx = -floor / 2; dx <= floor / 2; dx += 4)
                for (int dy = -floor / 2; dy <= floor / 2; dy += 4)
                    Assert.IsTrue(HelmBandGeometry.IsOnWheel(rimTop + new Vector2(dx, dy), pad),
                        $"the rim's top is grabbable {dx},{dy} from it");
            Assert.IsFalse(HelmBandGeometry.IsOnWheel(new Vector2(-0.5f, HelmBandGeometry.HubY - 1f), pad),
                "left of the piece is the housing beside it, not the wheel");
            Assert.IsFalse(HelmBandGeometry.IsOnWheel(new Vector2(HelmBandGeometry.WheelW, HelmBandGeometry.HubY - 1f), pad),
                "…and so is right of it");
            Assert.IsFalse(HelmBandGeometry.IsOnWheel(new Vector2(HelmBandGeometry.HubX, HelmBandGeometry.HubY + 1f), pad),
                "the wheel's lower half is below the screen");

            // The drive: at every drive the window holds a 24-square of the grip, all of it the grip.
            float grab = HelmOverlaySettings.Default.GrabRadiusPx;
            foreach (float drive in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                LeverRigGeometry.HandleOffset(drive, out double gx, out double gy);
                var grip = new Vector2(LeverRigGeometry.PX + (float)gx - HelmBandGeometry.DriveWindowX,
                                       LeverRigGeometry.PY + (float)gy - HelmBandGeometry.DriveWindowY);
                for (int dx = -floor / 2; dx <= floor / 2; dx += 4)
                    for (int dy = -floor / 2; dy <= floor / 2; dy += 4)
                    {
                        Vector2 at = grip + new Vector2(dx, dy);
                        Assert.IsTrue(HelmBandGeometry.IsInDriveWindow(at), $"drive {drive}: {at} inside the lever's window");
                        Assert.IsTrue(HelmOverlayLayout.IsOnGrip(HelmBandGeometry.DriveToLeverCell(at), drive, grab),
                            $"drive {drive}: {at} on the grip");
                    }
            }
            Assert.IsFalse(HelmBandGeometry.IsInDriveWindow(new Vector2(HelmBandGeometry.GearX, HelmBandGeometry.H / 2f)),
                "the gear letter's column is inert");
            Assert.LessOrEqual(HelmBandGeometry.DriveWindowW + HelmBandGeometry.GearW, HelmBandGeometry.DriveW,
                "the window and the letter fit the drive piece");
        }

        [Test]
        public void TheLayoutNeverChangesWithTheFit_OnlyThePlatesDo()
        {
            foreach (ConsoleRigKind rig in Rigs)
            {
                Assert.GreaterOrEqual(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.SounderPlate), 0, $"{rig}: a sounder housing");
                Assert.AreEqual(IsPilot(rig), HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.RadarPlate) >= 0, $"{rig}: a radar housing");
                Assert.AreEqual(IsPilot(rig), HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.GpsPlate) >= 0, $"{rig}: a plotter housing");

                var ctl = new HelmBandController();
                ctl.Layout(rig, 1920, 1080);
                var rects = new Rect[ctl.PieceCount];
                for (int i = 0; i < rects.Length; i++) rects[i] = ctl.PieceRect(i);

                foreach (SounderKind sounder in new[] { SounderKind.None, SounderKind.Depth, SounderKind.Fish })
                foreach (CompassMount compass in new[] { CompassMount.None, CompassMount.Dome, CompassMount.Flush })
                foreach (bool radar in new[] { false, true })
                foreach (bool gps in new[] { false, true })
                {
                    var fit = new HelmFit(rig, sounder, compass, radar, gps);
                    ctl.Layout(fit.Rig, 1920, 1080);   // what the host does every frame, whatever is fitted
                    Assert.AreEqual(rects.Length, ctl.PieceCount, $"{rig}: the piece count holds");
                    for (int i = 0; i < rects.Length; i++)
                    {
                        string at = $"{rig} {sounder}/{compass}/radar {radar}/gps {gps}, piece {i}";
                        Assert.AreEqual(rects[i], ctl.PieceRect(i), at + ": the layout holds");
                        HelmBandPiece p = ctl.Piece(i);
                        if (!p.IsPlate) continue;
                        string label = HelmBandController.PlateLabel(p.Kind, in fit);
                        HelmInstrumentSlot slot = HelmBandController.SlotOf(p.Kind, in fit);
                        Assert.AreEqual(Expected(p.Kind, in fit), label, at + ": the plate names what is fitted");
                        Assert.AreEqual(label == null, slot == HelmInstrumentSlot.None,
                            at + ": a blank housing raises nothing, a named one its instrument");
                        Assert.AreEqual(slot, ctl.PlateAt(in fit, rects[i].center), at + ": a press on it is its instrument");
                    }
                }
            }
        }

        private static string Expected(HelmBandPieceKind kind, in HelmFit fit)
        {
            switch (kind)
            {
                case HelmBandPieceKind.SounderPlate:
                    return fit.Sounder == SounderKind.Depth ? "SOUNDER" : (fit.Sounder == SounderKind.Fish ? "FINDER" : null);
                case HelmBandPieceKind.RadarPlate: return fit.Radar ? "RADAR" : null;
                case HelmBandPieceKind.GpsPlate: return fit.Gps ? "PLOTTER" : null;
            }
            return null;
        }

        [Test]
        public void EveryInstrumentAHelmCanCarry_HasItsHousingInThatHelmsBand()
        {
            // The band's tables are keyed on the rig; the fit a hull may buy is the helm asset's data.
            // A helm that could carry an instrument its band has no housing for could never raise it.
            string[] helms = Directory.GetFiles("Assets/_Project/Data/Boats/Helms", "*.asset");
            Assert.That(helms.Length, Is.GreaterThanOrEqualTo(4), "premise: the fleet's helm assets are where they were");
            foreach (string path in helms)
            {
                string yaml = File.ReadAllText(path);
                var rig = (ConsoleRigKind)Field(yaml, "Rig");
                string name = Path.GetFileNameWithoutExtension(path);
                if (rig == ConsoleRigKind.None) continue;
                Assert.IsTrue(HelmBandGeometry.HasBand(rig), $"{name}: a console rig draws a band");
                Assert.GreaterOrEqual(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.SounderPlate), 0, $"{name}: a sounder housing");
                Assert.GreaterOrEqual(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.Compass), 0, $"{name}: a compass piece");
                if (Field(yaml, "SupportsRadar") != 0 || Field(yaml, "DefaultRadar") != 0)
                    Assert.GreaterOrEqual(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.RadarPlate), 0,
                        $"{name}: carries a radar, so its band needs the radar's housing");
                if (Field(yaml, "SupportsGps") != 0 || Field(yaml, "DefaultGps") != 0)
                    Assert.GreaterOrEqual(HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.GpsPlate), 0,
                        $"{name}: carries a plotter, so its band needs the plotter's housing");
            }
        }

        private static int Field(string yaml, string key)
        {
            Match m = Regex.Match(yaml, @"^\s*" + key + @":\s*(-?\d+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"the helm asset has no {key} field");
            return int.Parse(m.Groups[1].Value);
        }

        // ---- rule 2 ----------------------------------------------------------------------------------

        [Test]
        public void TheBandCode_NamesNoHull()
        {
            string[] files =
            {
                "Assets/_Project/Code/UI/Draw/HelmBandGeometry.cs",
                "Assets/_Project/Code/UI/Draw/HelmBandPlateRender.cs",
                "Assets/_Project/Code/UI/HelmBandLayout.cs",
                "Assets/_Project/Code/UI/HelmBandController.cs",
            };
            var hulls = new List<string>();
            foreach (string dir in new[] { "Assets/_Project/Data/Boats", "Assets/_Project/Data/Boats/Helms" })
                foreach (string f in Directory.GetFiles(dir, "*.asset", SearchOption.TopDirectoryOnly))
                    hulls.Add(Path.GetFileNameWithoutExtension(f));
            Assert.That(hulls, Has.Member("CapeIslander").And.Member("CapeIslanderHelm").And.Member("DoryOutboard"),
                "premise: the scan reads the fleet's real asset names");

            var boatId = new Regex(@"\bboat\.[a-z][a-z0-9_]*");
            foreach (string file in files)
            {
                Assert.IsTrue(File.Exists(file), $"premise: the band lives in {file}");
                string src = File.ReadAllText(file);
                Assert.IsFalse(boatId.IsMatch(src), $"{file} names a boat id: which hull has which helm is data");
                foreach (string hull in hulls)
                    Assert.IsFalse(Regex.IsMatch(src, @"\b" + Regex.Escape(hull) + @"\b"),
                        $"{file} names {hull}: the band is keyed on the rig its helm asset names, never on a hull");
            }
        }

        // ---- the compass -----------------------------------------------------------------------------

        [Test]
        public void EveryBandCarriesTheFittedCompass()
        {
            // The premise of the nav cluster's duplication rule: wherever a compass is fitted the HUD's
            // hides, so every band must draw it, the pilot bands included (they ship with none, and can
            // buy either mount).
            Assert.IsTrue(HelmBandGeometry.FlushX >= 0 && HelmBandGeometry.FlushX + HelmBandGeometry.FlushW <= HelmBandGeometry.CompassW
                          && HelmBandGeometry.FlushY >= 0 && HelmBandGeometry.FlushY + HelmBandGeometry.FlushH <= HelmBandGeometry.H,
                "the flush unit's own box fits the compass piece");
            Assert.AreEqual(HelmBandGeometry.CompassW / 2, HelmBandGeometry.FlushX + HelmBandGeometry.FlushW / 2, "…centred across");
            Assert.AreEqual(HelmBandGeometry.H / 2, HelmBandGeometry.FlushY + HelmBandGeometry.FlushH / 2, "…and down");

            foreach (ConsoleRigKind rig in Rigs)
            {
                int ci = HelmBandGeometry.IndexOf(rig, HelmBandPieceKind.Compass);
                Assert.GreaterOrEqual(ci, 0, $"{rig}: a compass piece");
                Assert.AreEqual(HelmBandGeometry.WheelIndex(rig) + 1, ci, $"{rig}: it flanks the wheel");
                Assert.AreEqual(HelmBandGeometry.CompassW, HelmBandGeometry.Piece(rig, ci).W, $"{rig}: its width");

                foreach (CompassMount mount in new[] { CompassMount.Dome, CompassMount.Flush })
                {
                    var fit = new HelmFit(rig, SounderKind.None, mount, false, false);
                    Assert.IsTrue(HelmHudSuppression.DashIsUp(HelmControlStyle.Lever, in fit), $"{rig} {mount}: the band is up");
                    Assert.AreEqual(NavClusterPlacement.Hidden, HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever, in fit),
                        $"{rig} {mount}: the band draws this compass, so the HUD's hides");
                }
                var bare = new HelmFit(rig, SounderKind.None, CompassMount.None, false, false);
                Assert.AreEqual(NavClusterPlacement.ClearOfTheDash, HelmHudSuppression.NavCluster(true, HelmControlStyle.Lever, in bare),
                    $"{rig}: no compass fitted, so the cluster stays: it is the only heading aboard");
            }

            // The band and the HUD agree on where a band is up: two predicates, never apart.
            foreach (ConsoleRigKind rig in System.Enum.GetValues(typeof(ConsoleRigKind)))
            {
                var fit = new HelmFit(rig, SounderKind.None, CompassMount.None, false, false);
                Assert.AreEqual(HelmBandGeometry.HasBand(rig), HelmHudSuppression.DashIsUp(HelmControlStyle.Lever, in fit),
                    $"{rig}: the host draws a band exactly where the HUD yields to one");
            }
        }
    }
}
