#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Masks = HiddenHarbours.App.Editor.NineMileCreekGroundMasks;
using Plan = HiddenHarbours.App.Editor.NineMileCreekGroundPlan;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// NMC H2's four G3 masks (<see cref="Masks"/>): each committed file is its derivation from G3's committed
    /// codes and plan, texel for texel; the town and the campground take none of them; open ground takes its own;
    /// nothing grows on the made ground; and each imports as linear R8 data. Pure file reads: the same test runs
    /// headless.
    /// </summary>
    public class NineMileCreekGroundMasksTests
    {
        static Masks.Ground _ground;
        static byte[][] _derived;
        static Masks.Ground Ground => _ground ??= Masks.ReadGround();
        static byte[][] Derived => _derived ??= Masks.DeriveAll(Ground);

        public static Masks.Kind[] Kinds => Masks.Kinds;

        [TestCaseSource(nameof(Kinds))]
        public void EachCommittedMask_IsItsDerivationFromG3(Masks.Kind kind)
        {
            byte[] committed = Masks.ReadMask(Masks.PathOf(kind));
            byte[] derived = Derived[(int)kind];
            int differ = 0, first = -1;
            for (int i = 0; i < derived.Length; i++)
                if (committed[i] != derived[i])
                {
                    differ++;
                    if (first < 0) first = i;
                }
            Assert.AreEqual(0, differ, kind + ": " + differ + " texels differ from the derivation (first at texel " +
                first + "). Re-derive: Hidden Harbours/Nine Mile Creek Layers/Write G3 masks.");
        }

        [Test]
        public void TheTownAndTheCampTakeNoMask_AndOpenGroundTakesItsOwn()
        {
            void Expect(string what, double x, double y, int splat, int shore, int field, int wood)
            {
                int i = Masks.TexelOf(x, y);
                int[] got = Derived.Select(mask => mask[i] == Masks.On ? 1 : 0).ToArray();
                Assert.AreEqual(new[] { splat, shore, field, wood }, got,
                    what + " (" + x + ", " + y + "): splat, shore, field, wood");
            }

            Expect("the town plaza, lot.nmc_town_plaza", -227, 120.5, 0, 0, 0, 0);
            Expect("the laydown, lot.nmc_laydown", -95, 62, 0, 0, 0, 0);
            Expect("lot.nmc_south_shore_estate_1", -345, -148.5, 0, 0, 0, 0);
            Expect("lot.nmc_waterfront_5", -58, 220.75, 0, 0, 0, 0);
            Expect("the campground", -40, -50, 0, 0, 0, 0);
            Expect("woodlot.nmc_south, inside the campground", -61.1, -96, 0, 0, 0, 0);
            Expect("the west farm, south", -300, -60, 1, 0, 1, 0);
            Expect("woodlot.nmc_river_wood", -332, 236, 1, 0, 1, 1);
            Expect("woodlot.nmc_north, east side", -50, 192, 1, 0, 1, 1);
            Expect("the south beach", -250, -175, 1, 1, 0, 0);
            Expect("the bar, below the paint floor", 120, -200, 0, 1, 0, 0);
            Expect("the salt pond, still water", -230, -160, 1, 0, 0, 0);
            Expect("Route 91's carriageway", -177.45, 120, 1, 0, 0, 0);
            Expect("the basin", 128, 70, 0, 0, 0, 0);
        }

        [Test]
        public void TheMasksKeepOffStillWater_AndTheWoodsStandOnFieldGround()
        {
            byte[] shore = Derived[(int)Masks.Kind.Shore], field = Derived[(int)Masks.Kind.Field],
                   wood = Derived[(int)Masks.Kind.Wood];
            int shoreAndField = 0, woodOffField = 0, overStill = 0;
            for (int i = 0; i < shore.Length; i++)
            {
                if (shore[i] == Masks.On && field[i] == Masks.On) shoreAndField++;
                if (wood[i] == Masks.On && field[i] != Masks.On) woodOffField++;
                if (Ground.Still[i] != 0 && (shore[i] == Masks.On || field[i] == Masks.On || wood[i] == Masks.On))
                    overStill++;
            }
            Assert.AreEqual(0, shoreAndField, "texels both shore and field");
            Assert.AreEqual(0, woodOffField, "wood texels off field ground");
            Assert.AreEqual(0, overStill, "shore, field or wood texels over still water");
            Assert.Greater(Masks.Count(wood), 0, "the woods take no ground at all");
        }

        /// <summary>
        /// ⭐ <b>NOTHING GROWS ON THE MADE GROUND</b>, classified from G3: the ruled successor of
        /// <c>NineMileCreekShoreMapTests.MadeGroundIsTheFills_AndTheFieldsAreNot</c>, whose fills are the plan's
        /// analytic zones. The third issue's made ground is its own: the north apron behind
        /// <see cref="Plan.NorthCap"/>, the crane apron behind <see cref="Plan.CraneCap"/>, the wharf's turning pad and
        /// the two source arms. All of it stands above spring high and below the field floor, so the masks give it the
        /// splat and nothing else: no shore, and no field or wood for a hedge or a tree. The fields inland take their
        /// own. Derived from G3's heights and the final exclusions (the town, the camp, the roads); nothing is written
        /// back.
        /// </summary>
        [Test]
        public void TheMadeGroundTakesTheSplatAndNothingElse_TheFieldsTakeTheirOwn()
        {
            int[] madeGround = { 1, 0, 0, 0 };
            int samples = 0;
            void Expect(string what, double x, double y, int[] want)
            {
                int i = Masks.TexelOf(x, y);
                int[] got = Derived.Select(mask => mask[i] == Masks.On ? 1 : 0).ToArray();
                Assert.AreEqual(want, got, what + " (" + x + ", " + y + "): splat, shore, field, wood");
                samples++;
            }
            void Apron(string what, double west, double east, double face)
            {
                for (double x = west + 1; x <= east - 1; x += 1)
                    for (double y = face + 1; y <= face + 9; y += 1) Expect(what, x, y, madeGround);
            }
            void Arm(string what, UnityEngine.Vector2[] line)
            {
                for (int s = 1; s < line.Length; s++)
                {
                    double ax = line[s - 1].x, ay = line[s - 1].y, bx = line[s].x, by = line[s].y;
                    double length = System.Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    for (double d = 0; d <= length; d += 1)
                        Expect(what, ax + (bx - ax) * d / length, ay + (by - ay) * d / length, madeGround);
                }
            }

            Apron("the north apron", Plan.FaceStepX, Plan.NorthFaceEastX, Plan.NorthFaceY);
            Apron("the crane apron", Plan.CraneFaceWestX, Plan.FaceStepX, Plan.CraneFaceY);
            var pad = Plan.TurningPadCentre;
            double r = Plan.TurningPadRadius;
            for (double x = pad.x - r; x <= pad.x + r; x += 1)
                for (double y = pad.y - r; y <= pad.y + r; y += 1)
                    if ((x - pad.x) * (x - pad.x) + (y - pad.y) * (y - pad.y) <= r * r)
                        Expect("the turning pad", x, y, madeGround);
            Arm("the south arm", Plan.SouthArm);
            Arm("the east arm", Plan.EastArm);
            Assert.Greater(samples, 2000, "the made ground was hardly sampled");

            Expect("the west farm, south", -300, -60, new[] { 1, 0, 1, 0 });
        }

        [TestCaseSource(nameof(Kinds))]
        public void EachMask_ImportsAsLinearR8Data(Masks.Kind kind)
        {
            string meta = File.ReadAllText(Masks.PathOf(kind) + ".meta").Replace("\r\n", "\n");
            StringAssert.Contains("\n  textureType: 0\n", meta);
            StringAssert.Contains("\n    sRGBTexture: 0\n", meta);
            StringAssert.Contains("\n    enableMipMap: 0\n", meta);
            StringAssert.Contains("\n  isReadable: 1\n", meta);
            StringAssert.Contains("\n    sprites: []\n", meta);
            Assert.IsTrue(Regex.IsMatch(meta,
                    @"buildTarget: Standalone\n(?:    .*\n)*?    textureFormat: 63\n(?:    .*\n)*?    overridden: 1\n"),
                kind + ": the Standalone override must name R8.");
        }
    }
}
#endif
