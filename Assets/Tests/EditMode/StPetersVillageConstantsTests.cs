using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageConstantsTests
    {
        [Test]
        public void ActiveBuildingConstants_EqualTheirLotPositions()
        {
            CheckLot("school", StPetersBuilder.SchoolPos);
            CheckLot("general_store", StPetersBuilder.GeneralStorePos);
            CheckLot("post_office", StPetersShops.PostOfficePos);
            CheckLot("sage_cottage", StPetersBuilder.SageCottagePos);
            CheckLot("harbour_gable", StPetersBuilder.HarbourGablePos);
            CheckLot("cream_saltbox", StPetersBuilder.CreamSaltboxPos);
            CheckLot("white_gable", StPetersBuilder.WhiteGablePos);
        }

        [Test]
        public void HeldWestHomesAndHearth_KeepTheirMainPositions()
        {
            CheckPosition(StPetersBuilder.RedSaltboxPos, 25f, 8f);
            CheckPosition(StPetersBuilder.WhiteFarmhousePos, 21f, 26f);
            CheckPosition(StPetersBuilder.VillageHearthPos, 0f, 14f);
            Assert.That(StPetersWoods.VillageClearingRadius, Is.EqualTo(44f));
        }

        [Test]
        public void NewHomes_AreSitesAndTerrainGatherMembers()
        {
            foreach (var row in new[]
            {
                ("harbourGable", "b_harbour_gable", "HarbourGablePos"),
                ("creamSaltbox", "b_cream_saltbox", "CreamSaltboxPos"),
                ("whiteGable", "b_white_gable", "WhiteGablePos"),
            })
            {
                Assert.That(StPetersVillage.Sites.Count(s => s.Key == row.Item1), Is.EqualTo(1));
                var spec = StPetersPlaces.Buildings.Single(s => s.Id == row.Item2);
                Assert.That(spec.Member, Is.EqualTo("StPetersBuilder." + row.Item3));
                Assert.That(spec.FromScene, Is.False);
            }
            Assert.That(StPetersVillage.Sites.Count, Is.EqualTo(7));
            Assert.That(StPetersShops.Sites.Count, Is.EqualTo(2));
        }

        static void CheckLot(string key, Vector3 position)
        {
            string text = File.ReadAllText("Assets/_Project/Data/Regions/StPetersVillage/lot.st_peters." + key + ".asset");
            var match = Regex.Match(text, @"(?m)^  Position: \{x: ([^,]+), y: ([^}]+)\}");
            Assert.That(match.Success, Is.True, key);
            CheckPosition(position, float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        static void CheckPosition(Vector3 position, float x, float y)
        {
            Assert.That(position.x, Is.EqualTo(x));
            Assert.That(position.y, Is.EqualTo(y));
            Assert.That(position.z, Is.Zero);
        }
    }
}
