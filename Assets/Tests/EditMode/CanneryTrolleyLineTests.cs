using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The St Peters cannery's trolley line, as the picture draws it. The line's sprite is baked on the ¾ camera,
    /// so a plan offset (dx, dy) from its <c>At</c> is drawn at (dx, dy · sin e) on screen.
    ///
    /// <para>The line no longer runs onto the wharf's planks. It comes down from the cannery door and ends at a
    /// low loading stage on the ground just north of the wharf, beside the net shed: its last run is level and
    /// meets the stage's west edge on the stage's centre line, the stage's rails carry on to a buffer stop, and
    /// the cart stands on them against the buffer. A hand winch stands on the stage.</para>
    ///
    /// <para>The line and the stage are flat on the ground, so neither casts a sun shadow: a shadow under a
    /// floor piece is what made the rails read as raised off the ground.</para>
    /// </summary>
    public class CanneryTrolleyLineTests
    {
        const string Scene = "keyscene.stp_cannery", EastEnd = "keyscene.stp_east_end";
        const string Line = "structure.stp_cannery_trolley_line", LineRestored = "structure.stp_cannery_trolley_line_restored";
        const string Cart = "prop.stp_cannery_trolley", CartRestored = "prop.stp_cannery_trolley_restored";
        const string Stage = "structure.stp_cannery_trolley_stage", Winch = "prop.stp_cannery_hand_winch";
        const string NetShed = "structure.stp_net_shed";
        const string StageDefPath = "Assets/_Project/Data/SetPieces/SetPiece_trolleyStage.asset";
        const string CartDefPath = "Assets/_Project/Data/SetPieces/SetPiece_trolley.asset";
        const string SunCasterRulesPath = "Assets/_Project/Resources/SunCasterRules.asset";

        /// <summary>How far a cart's ground point may stand from the rails' centre line: half the 0.6 m gauge.</summary>
        const float OnTheRails = 0.3f;

        /// <summary>How far the line's end may miss the stage's west edge: under a pixel and a half at 32 px/m.</summary>
        const float Joined = 0.05f;

        /// <summary>How much the line's last run may rise or fall in drawn metres: it is level where it meets the
        /// stage, so the rails run straight on.</summary>
        const float Level = 0.01f;

        /// <summary>The gap a cart standing against the buffer may leave, in metres.</summary>
        const float AtTheBuffer = 0.1f;

        /// <summary>How far the stage's south edge may sit from the wharf's north face, in drawn metres.</summary>
        const float AgainstTheWharf = 0.1f;

        /// <summary>How near the net shed the stage stands: beside it, not across the green.</summary>
        const float BesideTheShed = 5f;

        static float Depth => Mathf.Sin(SpriteLightMath.CameraElevationDeg * Mathf.Deg2Rad);

        static KeyScenePiece Piece(string id, string scene = Scene)
        {
            KeySceneDef ks = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == scene);
            KeyScenePiece p = ks.Pieces.SingleOrDefault(x => x.Id == id);
            Assert.IsNotNull(p, $"{scene} has no '{id}'");
            return p;
        }

        static SetPieceDef Def(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(path);
            Assert.IsNotNull(def, path + " does not load");
            return def;
        }

        /// <summary>A piece-local point, turned to the piece's facing and drawn from its <c>At</c>.</summary>
        static Vector2 Drawn(KeyScenePiece p, Vector3 local)
        {
            Vector2 plan = SetPieceDef.ToWorld(new Vector2(local.x, local.y), p.Dir);
            return p.At + new Vector2(plan.x, plan.y * Depth);
        }

        static Vector2 StageAnchor(string name)
        {
            Assert.IsTrue(Def(StageDefPath).TryGetAnchor(name, out Vector3 local), $"the stage has no '{name}' anchor");
            return Drawn(Piece(Stage), local);
        }

        /// <summary>The line's centre on screen: its plan path from <c>At</c>, foreshortened in y.</summary>
        static List<Vector2> DrawnRails(KeyScenePiece line)
        {
            string path = line.Options.Single(o => o.Key == "path").Value;
            var drawn = new List<Vector2>();
            foreach (Match m in Regex.Matches(path, @"\[\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\]"))
            {
                float x = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                float y = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                drawn.Add(line.At + new Vector2(x, y * Depth));
            }
            Assert.GreaterOrEqual(drawn.Count, 2, $"{line.Id}: its path has no run");
            return drawn;
        }

        [TestCase(Line)]
        [TestCase(LineRestored)]
        public void TheLineEnds_LevelAtTheStagesWestEdge_OnItsCentreLine(string line)
        {
            List<Vector2> rails = DrawnRails(Piece(line));
            Vector2 end = rails[rails.Count - 1], before = rails[rails.Count - 2];
            Vector2 stageEnd = StageAnchor("lineEnd");
            Assert.LessOrEqual(Vector2.Distance(end, stageEnd), Joined, $"{line} ends at {end}, not at the stage's west edge {stageEnd}");
            Assert.Less(before.x, end.x, $"{line}'s last run does not run east onto the stage");
            Assert.LessOrEqual(Mathf.Abs(end.y - before.y), Level, $"{line}'s last run is not level where it meets the stage");
        }

        [TestCase(Cart)]
        [TestCase(CartRestored)]
        public void TheCart_StandsOnTheStagesRails_AgainstTheBuffer(string cart)
        {
            KeyScenePiece c = Piece(cart);
            SetPieceDef def = Def(CartDefPath);
            float half = Mathf.Max(-def.BoundsMin.x, def.BoundsMax.x);
            Vector2 west = StageAnchor("lineEnd"), buffer = StageAnchor("buffer");
            Assert.LessOrEqual(Mathf.Abs(c.At.y - west.y), OnTheRails, $"{cart} at {c.At} stands off the stage's rails (y {west.y:0.00})");
            Assert.GreaterOrEqual(c.At.x - half, west.x, $"{cart} hangs off the stage's west edge");
            Assert.LessOrEqual(c.At.x + half, buffer.x, $"{cart} runs through the buffer stop");
            Assert.LessOrEqual(buffer.x - (c.At.x + half), AtTheBuffer, $"{cart} stands short of the buffer");
        }

        [Test]
        public void TheStage_StandsJustNorthOfTheWharf_BesideTheNetShed()
        {
            KeyScenePiece stage = Piece(Stage);
            Rect planks = StPetersWharf.DeckFootprint();
            Assert.IsFalse(planks.Contains(stage.At), "the stage stands on the wharf's planks");
            Assert.AreEqual(StPetersLayerRefresh.FloorLayer, Def(StageDefPath).Layer, "the stage is not a floor piece");

            float halfDepth = Regex.Match(Def(StageDefPath).GameplayJson, "\"depth\"\\s*:\\s*([\\d.]+)") is Match m && m.Success
                ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 0.5f
                : float.NaN;
            Assert.IsFalse(float.IsNaN(halfDepth), "the stage's Def does not say how deep its deck is");
            float south = stage.At.y - halfDepth * Depth;
            Assert.AreEqual(StPetersWharf.NorthFaceY, south, AgainstTheWharf, "the stage's south edge is not against the wharf's north face");

            Vector2 west = StageAnchor("lineEnd");
            Assert.GreaterOrEqual(west.x, StPetersWharf.RootCellX - AgainstTheWharf, "the stage starts west of the wharf's root");
            float shed = Vector2.Distance(stage.At, Piece(NetShed, EastEnd).At);
            Assert.LessOrEqual(shed, BesideTheShed, $"the stage stands {shed:0.00} m from the net shed");

            KeyScenePiece winch = Piece(Winch);
            Assert.Greater(winch.At.x, StageAnchor("buffer").x, "the winch does not stand on the stage beyond the buffer");
            Assert.LessOrEqual(Mathf.Abs(winch.At.y - stage.At.y), halfDepth * Depth, "the winch does not stand on the stage");
        }

        [TestCase(Line, false)]
        [TestCase(LineRestored, false)]
        [TestCase(Stage, false)]
        [TestCase(Winch, true)]
        [TestCase(Cart, true)]
        public void OnlyWhatStandsUp_CastsASunShadow(string id, bool casts)
        {
            var rules = AssetDatabase.LoadAssetAtPath<SunCasterRules>(SunCasterRulesPath);
            Assert.IsNotNull(rules, SunCasterRulesPath + " does not load");
            bool excluded = rules.ExcludedNames.Any(n => SunCasterRules.Matches(id, n));
            Assert.AreEqual(!casts, excluded, casts ? $"{id} stands up but is kept out of the sun's shadows"
                                                    : $"{id} lies on the ground but still casts a sun shadow");
        }
    }
}
