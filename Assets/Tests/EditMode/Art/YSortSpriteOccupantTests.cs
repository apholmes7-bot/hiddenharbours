using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The occupant's own sort writer takes his dwelling's order as an input (ADR 0060 section 5, PR 4):
    /// while <see cref="OccupantSorting"/> names him, <see cref="YSortSprite"/> writes that order instead of
    /// his Y-sort; anyone else, and he once he is out, keeps the Y-sort.
    /// </summary>
    public class YSortSpriteOccupantTests
    {
        GameObject _him, _villager;

        [SetUp]
        public void SetUp()
        {
            OccupantSorting.Reset();
            _him = new GameObject("him", typeof(SpriteRenderer));
            _him.AddComponent<YSortSprite>();
            _villager = new GameObject("villager", typeof(SpriteRenderer));
            _villager.AddComponent<YSortSprite>();
            _him.transform.position = _villager.transform.position = new Vector3(3f, 20f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_him);
            Object.DestroyImmediate(_villager);
            OccupantSorting.Reset();
        }

        static int Resort(GameObject go)
        {
            var sort = go.GetComponent<YSortSprite>();
            sort.SortPivotYOffset = sort.SortPivotYOffset;   // the public setter re-sorts at once
            return go.GetComponent<SpriteRenderer>().sortingOrder;
        }

        [Test]
        public void Inside_HeDrawsAtTheDwellingInput_AndTheVillagerKeepsHisYSort()
        {
            int ySort = YSortSprite.OrderFor(20f, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                             SortingBands.DecorFloor, SortingBands.DecorCeiling);
            Assert.That(Resort(_him), Is.EqualTo(ySort));

            int dwelling = DwellingSorting.OrderAt(new Vector2(0f, 21.5f));
            OccupantSorting.Enter(new object(), _him.transform, dwelling);

            Assert.That(Resort(_him), Is.EqualTo(dwelling + DwellingSorting.OccupantRungsInFront));
            Assert.That(Resort(_villager), Is.EqualTo(ySort), "only the body inside takes the input");

            OccupantSorting.Reset();
            Assert.That(Resort(_him), Is.EqualTo(ySort), "out of the dwelling, his own Y-sort again");
        }
    }
}
