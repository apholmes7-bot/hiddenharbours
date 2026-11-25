using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The St Peters cannery's trolley line, as the picture draws it. The line's sprite is baked on the ¾ camera,
    /// so a plan offset (dx, dy) from its <c>At</c> is drawn at (dx, dy · sin e) on screen. The carts were placed
    /// on the plan path instead, so each one stood about a metre south of the rails, and the line was laid a
    /// metre too far east, so its lower end ran onto the wharf's planks and stopped mid-deck. These hold the
    /// carts on the drawn rails and the rails' lower end at the deck's west edge.
    /// </summary>
    public class CanneryTrolleyLineTests
    {
        const string Scene = "keyscene.stp_cannery";
        const string Line = "structure.stp_cannery_trolley_line", LineRestored = "structure.stp_cannery_trolley_line_restored";
        const string Cart = "prop.stp_cannery_trolley", CartRestored = "prop.stp_cannery_trolley_restored";

        /// <summary>How far a cart's ground point may stand from the rails' centre line: inside the 1.2 m gauge
        /// of the drawn track.</summary>
        const float OnTheRails = 0.3f;

        /// <summary>How far short of the deck's west edge the rails' centre may stop: the ballast bed still
        /// reaches the planks' edge.</summary>
        const float ShortOfTheDeck = 0.6f;

        static KeyScenePiece Piece(string id)
        {
            KeySceneDef scene = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == Scene);
            KeyScenePiece p = scene.Pieces.SingleOrDefault(x => x.Id == id);
            Assert.IsNotNull(p, $"{Scene} has no '{id}'");
            return p;
        }

        /// <summary>The line's centre on screen: its plan path from <c>At</c>, foreshortened in y.</summary>
        static List<Vector2> DrawnRails(KeyScenePiece line)
        {
            string path = line.Options.Single(o => o.Key == "path").Value;
            float depth = Mathf.Sin(SpriteLightMath.CameraElevationDeg * Mathf.Deg2Rad);
            var drawn = new List<Vector2>();
            foreach (Match m in Regex.Matches(path, @"\[\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\]"))
            {
                float x = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                float y = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                drawn.Add(line.At + new Vector2(x, y * depth));
            }
            Assert.GreaterOrEqual(drawn.Count, 2, $"{line.Id}: its path has no run");
            return drawn;
        }

        static float DistanceTo(List<Vector2> run, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < run.Count; i++)
            {
                Vector2 a = run[i], ab = run[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
            }
            return best;
        }

        [TestCase(Line, Cart)]
        [TestCase(LineRestored, CartRestored)]
        public void TheCartStandsOnTheDrawnRails(string line, string cart)
        {
            KeyScenePiece c = Piece(cart);
            float off = DistanceTo(DrawnRails(Piece(line)), c.At);
            Assert.LessOrEqual(off, OnTheRails, $"{cart} at {c.At} stands {off:0.00} m off {line}'s drawn rails");
        }

        [TestCase(Line)]
        [TestCase(LineRestored)]
        public void TheRailsEndAtTheWharfsWestEdge(string line)
        {
            Vector2 end = DrawnRails(Piece(line)).Last();
            float deckWest = StPetersWharf.RootCellX;
            Assert.LessOrEqual(end.x, deckWest, $"{line} ends at x {end.x:0.00}, on the deck past its west edge (x {deckWest})");
            Assert.GreaterOrEqual(end.x, deckWest - ShortOfTheDeck, $"{line} ends at x {end.x:0.00}, short of the deck (x {deckWest})");
        }
    }
}
