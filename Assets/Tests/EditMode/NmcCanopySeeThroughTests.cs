using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The canopy's see-through, as the committed Nine Mile Creek scene carries it (the owner's 11-14 ruling: "add see
    /// through rule to canopy"). Two faults hid the player under <c>station.nmc_canopy</c>, and each is held here:
    /// <list type="bullet">
    /// <item>the rule named no occupant, so with the scene played on its own (where nothing publishes a player to Core)
    /// it never saw anyone under the roof and the roof stayed solid over him;</item>
    /// <item>the ghost that replaces the roof sat on the rule's own object, which stands at the centre of the hidden
    /// ground, so it drew 2.36 m north of the roof it replaced.</item>
    /// </list>
    /// And the forecourt step over the committed scene changes nothing, so the scene is what the builder writes.
    /// </summary>
    public class NmcCanopySeeThroughTests
    {
        const string ScenePath = "_Project/Scenes/NineMileCreek.unity";

        static SceneYaml _scene;
        static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(Path.Combine(Application.dataPath, ScenePath)).Replace("\r", ""));

        static Doc Rule() => Scene.Docs.Single(d => d.ClassId == 114 && Runs(d, "Assets/_Project/Code/World/BuildingInterior.cs")
            && d.FieldRef("_room") != 0 && Ancestors(Scene.TransformOf(Scene.Require(d.FieldRef("m_GameObject"), "rule")))
                .Any(t => Name(t) == NineMileCreekWharfLandSide.CanopyRowId));

        /// <summary>True when the component runs the script at <paramref name="path"/> (by its guid, which every scene doc carries).</summary>
        static bool Runs(Doc component, string path) =>
            (component.Field("m_Script") ?? "").Contains("guid: " + UnityEditor.AssetDatabase.AssetPathToGUID(path) + ",");

        static string Name(Doc transform) => Scene.Require(transform.FieldRef("m_GameObject"), "go").Field("m_Name");

        static System.Collections.Generic.IEnumerable<Doc> Ancestors(Doc transform)
        {
            for (Doc t = transform; t != null; t = t.FieldRef("m_Father") == 0 ? null : Scene.Get(t.FieldRef("m_Father")))
                yield return t;
        }

        static Vector2 Local(Doc transform)
        {
            string p = transform.Field("m_LocalPosition");
            float N(string k) => float.Parse(Regex.Match(p, k + @": ([-\d.eE+]+)").Groups[1].Value, CultureInfo.InvariantCulture);
            return new Vector2(N("x"), N("y"));
        }

        /// <summary>World position by summing local positions up the chain (every transform here is unrotated, unscaled).</summary>
        static Vector2 World(Doc transform) => Ancestors(transform).Aggregate(Vector2.zero, (sum, t) => sum + Local(t));

        static Doc TransformOfComponent(Doc component) => Scene.TransformOf(Scene.Require(component.FieldRef("m_GameObject"), "component"));

        [Test]
        public void TheCanopyRule_NamesTheScenesReviewPlayer()
        {
            long occupant = Rule().FieldRef("_occupant");
            Assert.That(occupant, Is.Not.EqualTo(0), "no occupant: played on its own, the scene never shows the roof thin");
            Doc player = Scene.Docs.Single(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == NineMileCreekWharfLandSide.ReviewPlayerName);
            Assert.That(occupant, Is.EqualTo(Scene.TransformOf(player).FileId), "the occupant is the review player's transform");
            Assert.That(Scene.ComponentsOf(player).Any(c => Runs(c, "Assets/_Project/Code/Player/PlayerWalkController.cs")), Is.True,
                "the object it names is the walking player");
        }

        [Test]
        public void TheGhost_StandsExactlyWhereTheRoofDoes()
        {
            Doc rule = Rule();
            Doc shell = Scene.Require(rule.FieldRef("_shell"), "shell"), ghost = Scene.Require(rule.FieldRef("_room"), "ghost");
            Vector2 roof = World(TransformOfComponent(shell)), thin = World(TransformOfComponent(ghost));
            Assert.That(Vector2.Distance(roof, thin), Is.LessThan(1e-3f), $"the ghost draws at {thin}, the roof at {roof}");
            Assert.That(ghost.Field("m_Sprite"), Is.EqualTo(shell.Field("m_Sprite")), "the ghost is the roof's own picture");
            Assert.That(ghost.Field("m_SortingOrder"), Is.EqualTo(shell.Field("m_SortingOrder")), "and sorts where the roof sorts");
            Assert.That(ghost.Field("m_Enabled"), Is.EqualTo("0"), "hidden until someone is under the roof");
        }

        [Test]
        public void TheRulesGround_IsCentredOnItsOwnTransform_AndCoversThePivot()
        {
            Doc rule = Rule();
            Rect under = NineMileCreekWharfLandSide.CanopyUnder();
            Vector2 centre = World(TransformOfComponent(rule));
            Assert.That(Vector2.Distance(centre, under.center), Is.LessThan(1e-3f), "the footprint is centred on the rule's transform");
            Doc piece = Ancestors(TransformOfComponent(rule)).First(t => Name(t) == NineMileCreekWharfLandSide.CanopyRowId);
            Assert.That(under.Contains(World(piece)), Is.True, "standing at the canopy's pivot is under the roof");
        }

        [Test]
        public void TheForecourtStep_OverTheCommittedScene_ChangesNothing()
        {
            LayerPatch patch = NineMileCreekWharfLandSide.PlaceForecourt(SceneYaml.Parse(
                File.ReadAllText(Path.Combine(Application.dataPath, ScenePath)).Replace("\r", "")));
            Assert.That(patch.IsEmpty, Is.True, "the scene is not what the fuel stop builder writes:\n" + patch.Summary());
        }
    }
}
