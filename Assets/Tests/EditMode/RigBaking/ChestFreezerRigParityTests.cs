using HiddenHarbours.Art;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The chest freezer's rig and its runtime data agree: every freezer sheet is a rig piece, its near layer
    /// splits at the well's front rim, and the Def's contents opening is the rig's own mouth (between the front
    /// and back rims, as projected front-on at the rig's 40 degrees). A rig edit that moves a wall without the
    /// Def would lay fish over the chest's outline.
    /// </summary>
    public class ChestFreezerRigParityTests
    {
        private const string GinnyDefPath = "Assets/_Project/Data/Stowage/GinnyCatchFreezer.asset";
        private const float ElevationDegrees = 40f;   // the rig's DEFAULT_ELEV

        static IRigScriptHost _host;
        static string _g;

        [OneTimeSetUp]
        public void SetUp()
        {
            _host = RigScriptHostFactory.Create();
            RigEntry entry = RigCatalog.Get(ApplianceSheetBaker.RigKey);
            RigCatalog.Install(_host, entry);
            _g = entry.GlobalName;
        }

        [OneTimeTearDown]
        public void TearDown() { _host?.Dispose(); _host = null; }

        static float N(string js) => (float)_host.EvaluateNumber(js);

        [Test]
        public void EveryFreezerSheet_IsARigPiece_SplitAtItsFrontRim()
        {
            foreach ((string rig, string _) in ChestFreezerSheet.Pieces)
            {
                Assert.IsTrue(_host.EvaluateBool($"{_g}.PROPS['{rig}'] !== undefined"), rig);
                Assert.IsTrue(_host.EvaluateBool($"{_g}.freezerWell('{rig}',0) !== null"), rig + " well");
                Assert.AreEqual(N($"{_g}.freezerWell('{rig}',0).y0"), N($"{_g}.PROPS['{rig}'].splitAt[1]"), 1e-4f,
                                $"{rig}: the near layer does not split at the front rim");
            }
        }

        [Test]
        public void GinnysDef_ContentsOpening_IsTheRigsMouth()
        {
            var def = AssetDatabase.LoadAssetAtPath<FreezerDef>(GinnyDefPath);
            Assert.IsNotNull(def, GinnyDefPath);
            string rig = ChestFreezerSheet.RigName;
            Assert.AreEqual(ChestFreezerSheet.Stem, def.SheetStem);
            float s = Mathf.Sin(ElevationDegrees * Mathf.Deg2Rad), c = Mathf.Cos(ElevationDegrees * Mathf.Deg2Rad);
            string w = $"{_g}.freezerWell('{rig}',0)";
            float x0 = N(w + ".x0"), x1 = N(w + ".x1"), y0 = N(w + ".y0"), y1 = N(w + ".y1"), rim = N(w + ".rimZ");
            Rect o = def.ContentsOpeningMetres;
            Assert.AreEqual(x0, o.xMin, 1e-3f, "left wall");
            Assert.AreEqual(x1, o.xMax, 1e-3f, "right wall");
            Assert.AreEqual(y0 * s + rim * c, o.yMin, 1e-3f, "front rim as drawn");
            Assert.AreEqual(y1 * s + rim * c, o.yMax, 1e-3f, "back rim as drawn");
        }

        [Test]
        public void TheOpenNearLayer_IsLessThanTheOpenChest()
        {
            string rig = ChestFreezerSheet.RigName;
            int whole = Opaque(_host.EvaluateBytes($"{_g}.render('{rig}',0,{ChestFreezerSheet.OptsFor(ChestFreezerSheet.OpenEmpty)})"));
            int near = Opaque(_host.EvaluateBytes($"{_g}.render('{rig}',0,{ChestFreezerSheet.OptsFor(ChestFreezerSheet.NearOpen)})"));
            Assert.Greater(near, 0, "the near layer draws something");
            Assert.Less(near, whole, "and less than the whole open chest");
        }

        static int Opaque(byte[] rgba)
        {
            int n = 0;
            for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 0) n++;
            return n;
        }
    }
}
