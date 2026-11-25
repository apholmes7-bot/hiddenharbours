using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>LeBlanc's store front</b> (owner 2026-11-25: a clear entry, a shop sign, and a couple of displays out
    /// while the store is open and gone when it is closed). The displays follow the store's hours to the edge:
    /// out from the opening hour, away from the closing hour. The shipped front matches the store's island
    /// shelves' hours, every piece has its own picture, nothing stands on the walk to the door, and the old
    /// yard chalk sign is gone.
    /// </summary>
    public class StoreFrontTests
    {
        private const string FrontId = "storefront.st_peters_general";
        private const string ShelvesId = "islandshelves.st_peters_general";
        private const string WalkId = "route.stpeters.general_store_walk";
        private const string ChalkSign = "prop.stp_vl_general_store_chalk_sign";
        private const string Scene = "Assets/_Project/Scenes/StPeters.unity";

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Clean()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private StoreFront BuildTestFront(out GameObject sign, out GameObject[] displays)
        {
            var def = ScriptableObject.CreateInstance<StoreFrontDef>();
            _made.Add(def);
            def.OpensHour = 7f;
            def.ClosesHour = 19f;
            def.Pieces = new List<StoreFrontPiece>
            {
                new StoreFrontPiece { Id = "front.sign", OnlyWhileOpen = false },
                new StoreFrontPiece { Id = "front.crate", OnlyWhileOpen = true, Offset = new Vector2(1f, -2f) },
                new StoreFrontPiece { Id = "front.rack", OnlyWhileOpen = true, Offset = new Vector2(-1f, -2f) },
            };
            var building = new GameObject("generalStore");
            _made.Add(building);
            StoreFront front = StoreFront.Build(def, building.transform);
            sign = front.transform.Find("front.sign").gameObject;
            displays = new[] { front.transform.Find("front.crate").gameObject, front.transform.Find("front.rack").gameObject };
            return front;
        }

        [Test]
        public void TheDisplays_GoOut_AtTheOpeningHour_AndAreAway_JustBeforeIt()
        {
            StoreFront front = BuildTestFront(out _, out GameObject[] displays);
            front.ApplyHour(6.99f);
            Assert.That(displays.All(d => !d.activeSelf), Is.True, "closed at 06:59: displays put away");
            Assert.That(front.DisplaysOut, Is.False);
            front.ApplyHour(7f);
            Assert.That(displays.All(d => d.activeSelf), Is.True, "open at 07:00: displays out");
            Assert.That(front.DisplaysOut, Is.True);
        }

        [Test]
        public void TheDisplays_AreOut_JustBeforeClosing_AndAway_AtTheClosingHour()
        {
            StoreFront front = BuildTestFront(out _, out GameObject[] displays);
            front.ApplyHour(18.99f);
            Assert.That(displays.All(d => d.activeSelf), Is.True, "open at 18:59: displays out");
            front.ApplyHour(19f);
            Assert.That(displays.All(d => !d.activeSelf), Is.True, "closed at 19:00: displays put away");
            front.ApplyHour(23.5f);
            Assert.That(displays.All(d => !d.activeSelf), Is.True, "closed at night");
            front.ApplyHour(0f);
            Assert.That(displays.All(d => !d.activeSelf), Is.True, "closed at midnight");
        }

        [Test]
        public void TheSign_StaysOut_OpenOrClosed_AndARepeatedHourChangesNothing()
        {
            StoreFront front = BuildTestFront(out GameObject sign, out _);
            Assert.That(front.ApplyHour(12f), Is.True, "the first hour decides the front");
            Assert.That(sign.activeSelf, Is.True);
            Assert.That(front.ApplyHour(12.5f), Is.False, "still open: nothing to change");
            Assert.That(front.ApplyHour(21f), Is.True);
            Assert.That(sign.activeSelf, Is.True, "the sign stays out after closing");
        }

        [Test]
        public void IsOpenAt_IsHalfOpen_FromOpeningUpToClosing()
        {
            Assert.That(StoreFrontDef.IsOpenAt(6.999f, 7f, 19f), Is.False);
            Assert.That(StoreFrontDef.IsOpenAt(7f, 7f, 19f), Is.True);
            Assert.That(StoreFrontDef.IsOpenAt(18.999f, 7f, 19f), Is.True);
            Assert.That(StoreFrontDef.IsOpenAt(19f, 7f, 19f), Is.False);
        }

        private static StoreFrontDef Shipped()
        {
            StoreFrontDef[] all = Resources.LoadAll<StoreFrontDef>(StoreFrontDef.ResourcesFolder);
            Assert.That(all.Count(d => d.Id == FrontId), Is.EqualTo(1), "exactly one " + FrontId);
            return all.First(d => d.Id == FrontId);
        }

        [Test]
        public void TheShippedFront_KeepsTheStoreShelvesHours_AndHasASignAndTwoOrThreeDisplays()
        {
            StoreFrontDef front = Shipped();
            IslandStoreShelvesDef shelves = AssetDatabase.FindAssets("t:" + nameof(IslandStoreShelvesDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<IslandStoreShelvesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(d => d != null && d.Id == ShelvesId);
            Assert.That(front.ShopId, Is.EqualTo(shelves.ShopId));
            Assert.That(front.OpensHour, Is.EqualTo(shelves.OpensHour), "the displays go out when the store opens");
            Assert.That(front.ClosesHour, Is.EqualTo(shelves.ClosesHour), "the displays come in when it closes");
            Assert.That(front.Pieces.Count(p => !p.OnlyWhileOpen), Is.EqualTo(1), "one sign, always out");
            int displays = front.Pieces.Count(p => p.OnlyWhileOpen);
            Assert.That(displays, Is.InRange(2, 3), "two or three displays");
            Assert.That(front.Pieces.Select(p => p.Id).Distinct().Count(), Is.EqualTo(front.Pieces.Count), "unique piece ids");
            Assert.That(front.Pieces.All(p => p.Id.StartsWith("front.")), Is.True, "piece ids are front.snake_case");
        }

        [Test]
        public void EveryShippedPiece_HasItsOwnPicture_StoodOnItsBottomCentre()
        {
            StoreFrontDef front = Shipped();
            var seen = new HashSet<Sprite>();
            foreach (StoreFrontPiece piece in front.Pieces)
            {
                Assert.That(piece.Sprite, Is.Not.Null, piece.Id + " has a picture");
                Assert.That(seen.Add(piece.Sprite), Is.True, piece.Id + " shares its picture");
                Vector2 pivot = piece.Sprite.pivot / piece.Sprite.rect.size;
                Assert.That(pivot.x, Is.EqualTo(0.5f).Within(0.01f), piece.Id + " pivot x");
                Assert.That(pivot.y, Is.EqualTo(0f).Within(0.01f), piece.Id + " pivot y");
                Assert.That(piece.Sprite.pixelsPerUnit, Is.EqualTo(32f), piece.Id + " at the village's 32 px a metre");
            }
        }

        [Test]
        public void NoShippedPiece_StandsOnTheWalkToTheDoor()
        {
            StoreFrontDef front = Shipped();
            RouteDef walk = AssetDatabase.FindAssets("t:" + nameof(RouteDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<RouteDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(r => r != null && r.Id == WalkId);
            Vector2 ground = BuildingGroundPoint(front.BuildingName);
            float walkX = walk.Points[0].x;
            Assert.That(walk.Points.All(p => Mathf.Approximately(p.x, walkX)), Is.True, "the walk runs straight to the door");
            float half = walk.WidthMetres * 0.5f;
            foreach (StoreFrontPiece piece in front.Pieces)
            {
                Vector2 at = ground + piece.Offset;
                float halfWide = piece.Sprite.bounds.extents.x;
                float gap = Mathf.Abs(at.x - walkX) - halfWide - half;
                Assert.That(gap, Is.GreaterThanOrEqualTo(0f), piece.Id + " at " + at + " stands on the walk");
            }
        }

        [Test]
        public void TheYardChalkSign_IsGone_FromTheYardThePlanAndTheScene()
        {
            YardDef yard = AssetDatabase.FindAssets("t:" + nameof(YardDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<YardDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(y => y != null && y.Id == "yard.st_peters.general_store");
            Assert.That(yard.Dressing.Any(d => d.PieceId == ChalkSign), Is.False, "yard dressing");
            Assert.That(File.ReadAllText(Scene).Contains("m_Name: " + ChalkSign), Is.False, "scene");
            foreach (VillagePlanDef plan in AssetDatabase.FindAssets("t:" + nameof(VillagePlanDef))
                         .Select(g => AssetDatabase.LoadAssetAtPath<VillagePlanDef>(AssetDatabase.GUIDToAssetPath(g))))
                Assert.That(plan.Pieces.Any(p => p.Id == ChalkSign), Is.False, "village plan " + plan.Id);
        }

        /// <summary>The store building's position in the committed scene (its parent sits at the origin).</summary>
        private static Vector2 BuildingGroundPoint(string buildingName)
        {
            string text = File.ReadAllText(Scene);
            Match go = Regex.Match(text, @"--- !u!1 &(\d+)\r?\nGameObject:(?:\r?\n  .*)*?\r?\n  m_Name: " +
                                         Regex.Escape(buildingName) + @"\r?\n");
            Assert.That(go.Success, Is.True, buildingName + " in the scene");
            Match tr = Regex.Match(text, @"--- !u!4 &\d+\r?\nTransform:(?:\r?\n  .*)*?\r?\n  m_GameObject: \{fileID: " +
                                         go.Groups[1].Value + @"\}(?:\r?\n  .*)*?\r?\n  m_LocalPosition: \{x: ([-\d.]+), y: ([-\d.]+)");
            Assert.That(tr.Success, Is.True, buildingName + "'s transform");
            return new Vector2(float.Parse(tr.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                               float.Parse(tr.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
