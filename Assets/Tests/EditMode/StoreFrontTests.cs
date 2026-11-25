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
                new StoreFrontPiece { Id = "front.sign", OnlyWhileOpen = false, HideWhileInside = true },
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
        public void TheSignOnTheWall_HidesWhileHeIsInside_AndTheDisplaysDoNot()
        {
            StoreFront front = BuildTestFront(out GameObject sign, out GameObject[] displays);
            front.ApplyHour(12f);
            Assert.That(front.ApplyInside(false), Is.True, "the first look decides the wall");
            Assert.That(sign.activeSelf, Is.True, "outside: the sign shows");
            Assert.That(front.ApplyInside(true), Is.True);
            Assert.That(sign.activeSelf, Is.False, "inside: the wall is away, and the sign with it");
            Assert.That(front.WallPiecesShown, Is.False);
            Assert.That(displays.All(d => d.activeSelf), Is.True, "the displays stay out while the store is open");
            Assert.That(front.ApplyInside(true), Is.False, "still inside: nothing to change");
            front.ApplyInside(false);
            Assert.That(sign.activeSelf, Is.True, "back outside: the sign shows again");
            front.ApplyHour(21f);
            Assert.That(sign.activeSelf, Is.True, "closing hours never take the sign in");
        }

        private sealed class RecordingPresentation : HiddenHarbours.Core.IGroundPropPresentationService
        {
            public readonly List<(GameObject host, Sprite sprite, HiddenHarbours.Core.GroundGlow glow)> Dressed =
                new List<(GameObject, Sprite, HiddenHarbours.Core.GroundGlow)>();

            public void Dress(GameObject host, Sprite sprite, in HiddenHarbours.Core.GroundGlow glow) =>
                Dressed.Add((host, sprite, glow));
        }

        /// <summary>A lamp asks the village lamp look for its light once, at its bulb, and the light goes in with
        /// the wall while he is inside; the board and the displays ask for none.</summary>
        [Test]
        public void ALamp_AsksTheVillageLampLookForItsLight_AtItsBulb_AndGoesInWithTheWall()
        {
            var was = HiddenHarbours.Core.GroundPropPresentation.Service;
            var seen = new RecordingPresentation();
            HiddenHarbours.Core.GroundPropPresentation.Service = seen;
            try
            {
                var def = ScriptableObject.CreateInstance<StoreFrontDef>();
                _made.Add(def);
                def.Pieces = new List<StoreFrontPiece>
                {
                    new StoreFrontPiece { Id = "front.sign", HideWhileInside = true },
                    new StoreFrontPiece { Id = "front.lamp", HideWhileInside = true, Offset = new Vector2(-0.9f, 0.5f),
                                          Glows = true, GlowOffset = new Vector2(0.1f, 0f), GlowRangeMetres = 1.5f,
                                          GlowIntensity = 0.8f, GlowColor = Color.yellow },
                    new StoreFrontPiece { Id = "front.crate", OnlyWhileOpen = true },
                };
                var building = new GameObject("generalStore");
                _made.Add(building);
                StoreFront front = StoreFront.Build(def, building.transform);
                Assert.That(seen.Dressed.Count, Is.EqualTo(1), "one light, for the one lamp");
                var (host, sprite, glow) = seen.Dressed[0];
                Assert.That(sprite, Is.Null, "the lamp draws its own picture; only the light is asked for");
                Assert.That(glow.Lit, Is.True);
                Assert.That(glow.RangeMetres, Is.EqualTo(1.5f));
                Assert.That(glow.Intensity, Is.EqualTo(0.8f));
                Assert.That(glow.Color, Is.EqualTo(Color.yellow));
                Assert.That(glow.HeightMetres, Is.Zero, "the light hangs at the bulb");
                Assert.That((Vector2)host.transform.position, Is.EqualTo(new Vector2(-0.8f, 0.5f)).Using(Vector2EqualityComparer.Instance));
                Assert.That(front.LampLights.Single(), Is.SameAs(host));
                front.ApplyInside(true);
                Assert.That(host.activeInHierarchy, Is.False, "inside: the lamp and its light are away with the wall");
                front.ApplyInside(false);
                Assert.That(host.activeInHierarchy, Is.True);
            }
            finally
            {
                HiddenHarbours.Core.GroundPropPresentation.Service = was;
            }
        }

        /// <summary>A piece with a night picture gets it as a hidden child drawn at the front's night order; it
        /// shows from sunset and goes again at sunrise, and goes in with the wall while he is inside.</summary>
        [Test]
        public void TheBoardsNightPicture_ShowsFromSunsetToSunrise_OverTheDark_AndGoesInWithTheWall()
        {
            var def = ScriptableObject.CreateInstance<StoreFrontDef>();
            _made.Add(def);
            var tex = new Texture2D(4, 2);
            _made.Add(tex);
            Sprite lit = Sprite.Create(tex, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0f), 32f);
            _made.Add(lit);
            def.Pieces = new List<StoreFrontPiece>
            {
                new StoreFrontPiece { Id = "front.sign", HideWhileInside = true, NightSprite = lit },
                new StoreFrontPiece { Id = "front.crate", OnlyWhileOpen = true },
            };
            var building = new GameObject("generalStore");
            _made.Add(building);
            StoreFront front = StoreFront.Build(def, building.transform);
            Transform night = front.transform.Find("front.sign/front.sign.night");
            Assert.That(night, Is.Not.Null, "the board's night picture hangs on the board");
            Assert.That(front.transform.Find("front.crate/front.crate.night"), Is.Null, "a display has none");
            var sr = night.GetComponent<SpriteRenderer>();
            Assert.That(sr.sprite, Is.SameAs(lit));
            Assert.That(sr.sortingOrder, Is.EqualTo(def.NightSortingOrder));
            Assert.That(night.gameObject.activeSelf, Is.False, "hidden until the front has looked at the hour");

            Assert.That(front.ApplyNight(HiddenHarbours.Core.WatchFaceState.NightAt(12f)), Is.True);
            Assert.That(night.gameObject.activeSelf, Is.False, "noon: the board shows by day");
            Assert.That(front.ApplyNight(HiddenHarbours.Core.WatchFaceState.NightAt(22f)), Is.True);
            Assert.That(night.gameObject.activeSelf, Is.True, "22:00: the lamps light the board");
            Assert.That(front.NightPicturesShown, Is.True);
            Assert.That(front.ApplyNight(HiddenHarbours.Core.WatchFaceState.NightAt(23f)), Is.False, "still night: nothing to change");
            front.ApplyInside(true);
            Assert.That(night.gameObject.activeInHierarchy, Is.False, "inside: away with the wall");
            front.ApplyInside(false);
            Assert.That(night.gameObject.activeInHierarchy, Is.True);
            front.ApplyNight(HiddenHarbours.Core.WatchFaceState.NightAt(6f));
            Assert.That(night.gameObject.activeSelf, Is.False, "06:00: day again");
        }

        /// <summary>The night picture draws over the day/night darkness (32760) and under the lamps' own glow
        /// (SceneLight's top order, 32767), and the shipped board's night picture is its day picture lit: the same
        /// size, pivot and density, so it lies exactly over it.</summary>
        [Test]
        public void TheShippedBoardsNightPicture_LiesExactlyOverIt_BetweenTheDarkAndTheGlow()
        {
            StoreFrontDef front = Shipped();
            Assert.That(front.NightSortingOrder, Is.InRange(32761, 32766), "over the darkness, under the lamps' glow");
            StoreFrontPiece sign = front.Pieces.Single(p => p.Id == "front.sign");
            Assert.That(sign.NightSprite, Is.Not.Null, "the board is lit at night");
            Assert.That(sign.NightSprite.rect.size, Is.EqualTo(sign.Sprite.rect.size), "same size as by day");
            Assert.That(sign.NightSprite.pivot, Is.EqualTo(sign.Sprite.pivot), "same pivot as by day");
            Assert.That(sign.NightSprite.pixelsPerUnit, Is.EqualTo(sign.Sprite.pixelsPerUnit));
            Assert.That(front.Pieces.Where(p => p.Id != "front.sign").All(p => p.NightSprite == null), Is.True,
                        "only the board has a night picture");
        }

        private sealed class Vector2EqualityComparer : IEqualityComparer<Vector2>
        {
            public static readonly Vector2EqualityComparer Instance = new Vector2EqualityComparer();
            public bool Equals(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-8f;
            public int GetHashCode(Vector2 v) => 0;
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
            Assert.That(front.Pieces.Count(p => !p.OnlyWhileOpen && !p.Glows), Is.EqualTo(1), "one name board, always out");
            Assert.That(front.Pieces.Where(p => !p.OnlyWhileOpen).All(p => p.HideWhileInside), Is.True,
                        "the board and its lamps are fixed to the store's face");
            Assert.That(front.Pieces.Count(p => p.Glows), Is.InRange(2, 3), "two or three lamps over the board");
            Assert.That(front.Pieces.Where(p => p.Glows).All(p => !p.OnlyWhileOpen), Is.True, "the lamps stay up when it closes");
            Assert.That(front.Pieces.Where(p => p.OnlyWhileOpen).All(p => !p.HideWhileInside), Is.True, "the displays stand on the ground");
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
            float door = walk.Points.Max(p => p.y);
            foreach (StoreFrontPiece piece in front.Pieces)
            {
                Vector2 at = ground + piece.Offset;
                if (piece.HideWhileInside)
                {
                    // Hung on the wall: it may be over the walk's line, but only above the door, never on it.
                    Assert.That(at.y, Is.GreaterThanOrEqualTo(door), piece.Id + " at " + at + " hangs below the door");
                    continue;
                }
                float halfWide = piece.Sprite.bounds.extents.x;
                float gap = Mathf.Abs(at.x - walkX) - halfWide - half;
                Assert.That(gap, Is.GreaterThanOrEqualTo(0f), piece.Id + " at " + at + " stands on the walk");
            }
        }

        /// <summary>The name board is on the store itself, above the awning (owner 2026-11-25: the post sign was
        /// "a little big and too commercial looking for a small island"): centred on the store, well above the
        /// door, modest in size, drawn just over the building's own picture (1060).</summary>
        [Test]
        public void TheShippedBoard_IsOnTheStoresFace_AboveTheAwning_AndModest()
        {
            StoreFrontDef front = Shipped();
            StoreFrontPiece sign = front.Pieces.Single(p => p.Id == "front.sign");
            RouteDef walk = AssetDatabase.FindAssets("t:" + nameof(RouteDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<RouteDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(r => r != null && r.Id == WalkId);
            Vector2 at = BuildingGroundPoint(front.BuildingName) + sign.Offset;
            Vector2 door = walk.Points.OrderByDescending(p => p.y).First();
            Assert.That(Mathf.Abs(sign.Offset.x), Is.LessThan(0.1f), "centred on the store's face");
            Assert.That(at.y, Is.GreaterThan(door.y + 2f), "above the awning, not over the door");
            Assert.That(sign.Sprite.bounds.size.x, Is.LessThanOrEqualTo(2f), "a modest board, two metres at most");
            Assert.That(sign.Sprite.bounds.size.y, Is.LessThanOrEqualTo(0.5f), "it fits under the upper windows");
            Assert.That(sign.SortingOrder, Is.EqualTo(1061), "just over the building's picture (1060), under its lamps");
        }

        /// <summary>The lamps hang over the board, one over each end, and light it from just above.</summary>
        [Test]
        public void TheShippedLamps_HangOverTheBoardsEnds_AndLightIt()
        {
            StoreFrontDef front = Shipped();
            StoreFrontPiece sign = front.Pieces.Single(p => p.Id == "front.sign");
            float half = sign.Sprite.bounds.extents.x;
            float top = sign.Offset.y + sign.Sprite.bounds.size.y;
            var lamps = front.Pieces.Where(p => p.Glows).ToList();
            Assert.That(lamps.Any(l => l.Offset.x < sign.Offset.x) && lamps.Any(l => l.Offset.x > sign.Offset.x), Is.True,
                        "a lamp over each end");
            foreach (StoreFrontPiece lamp in lamps)
            {
                Vector2 bulb = lamp.Offset + lamp.GlowOffset;
                Assert.That(Mathf.Abs(bulb.x - sign.Offset.x), Is.LessThan(half), lamp.Id + "'s bulb is over the board");
                Assert.That(bulb.y, Is.InRange(top - 0.05f, top + 0.25f), lamp.Id + "'s bulb hangs just over the board");
                Assert.That(lamp.SortingOrder, Is.GreaterThan(sign.SortingOrder), lamp.Id + " in front of the board");
                Assert.That(lamp.GlowRangeMetres, Is.GreaterThan(half * 0.5f), lamp.Id + " reaches across its half of the board");
                Assert.That(lamp.GlowIntensity, Is.GreaterThan(0f), lamp.Id);
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
