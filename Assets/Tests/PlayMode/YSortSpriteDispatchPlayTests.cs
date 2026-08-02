using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Pins the MECHANISM behind <see cref="YSortSprite"/>'s static-decor perf promise (rule 7): a static
    /// instance sorts once on enable and then DISABLES itself — <c>enabled = false</c> is what stops the
    /// engine dispatching its (empty) <c>Update</c>/<c>LateUpdate</c>, and with ~1300 decor instances after
    /// the St Peters dressing pass those no-op calls were the whole cost. A mover keeps re-sorting every
    /// frame, and <see cref="YSortSprite.Dynamic"/> re-arms / stands down the dispatch at runtime. If the
    /// self-disable ever regresses, the decor bill silently returns — these tests are the tripwire.
    /// </summary>
    public class YSortSpriteDispatchPlayTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
        }

        /// <summary>A GO carrying SpriteRenderer + YSortSprite; built INACTIVE so the test controls enable order.</summary>
        private YSortSprite Make(bool dynamic, float y)
        {
            _go = new GameObject("ysort-test");
            _go.SetActive(false);
            _go.transform.position = new Vector3(0f, y, 0f);
            _go.AddComponent<SpriteRenderer>();
            var sort = _go.AddComponent<YSortSprite>();
            sort.Dynamic = dynamic;
            _go.SetActive(true);
            return sort;
        }

        private static int ExpectedOrder(float y) => YSortSprite.OrderFor(y, 10f, 4f, 2, 40);

        [UnityTest]
        public IEnumerator StaticSprite_SortsOnEnable_ThenDisablesItself()
        {
            var sort = Make(dynamic: false, y: 1.5f);
            var sr = _go.GetComponent<SpriteRenderer>();

            Assert.AreEqual(ExpectedOrder(1.5f), sr.sortingOrder, "The one-shot sort on enable must land.");
            Assert.IsFalse(sort.enabled,
                "A static YSortSprite must disable itself after the one-shot sort — enabled=false is the " +
                "mechanism that stops per-frame Update/LateUpdate dispatch (rule 7).");

            // And it really is inert: moving it produces no re-sort.
            _go.transform.position = new Vector3(0f, -3f, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(ExpectedOrder(1.5f), sr.sortingOrder,
                "A static sprite must NOT re-sort when moved — it was parked at its enable-time order.");
        }

        [UnityTest]
        public IEnumerator StaticSprite_GameObjectToggle_KeepsItsParkedOrder()
        {
            // Region travel toggles whole scene roots. A parked component's OnEnable does NOT re-fire on
            // SetActive (the component itself is disabled) — and it doesn't need to: sortingOrder persists
            // on the renderer and static decor never moves in play mode, so the parked order IS the right
            // order. This pins that a toggle neither loses the sort nor resurrects the dispatch.
            var sort = Make(dynamic: false, y: 1.5f);
            var sr = _go.GetComponent<SpriteRenderer>();

            _go.SetActive(false);
            _go.SetActive(true);
            yield return null;

            Assert.AreEqual(ExpectedOrder(1.5f), sr.sortingOrder, "The parked order must survive a region toggle.");
            Assert.IsFalse(sort.enabled, "…and the sprite must stay parked (no dispatch resurrection).");
        }

        [UnityTest]
        public IEnumerator StaticSprite_ComponentReEnable_IsTheOneShotResortPoke()
        {
            // The documented escape hatch for a teleported "static" prop: enabled = true re-sorts at the
            // new spot and immediately stands down again.
            var sort = Make(dynamic: false, y: 1.5f);
            var sr = _go.GetComponent<SpriteRenderer>();

            _go.transform.position = new Vector3(0f, -2f, 0f);
            sort.enabled = true;
            yield return null;

            Assert.AreEqual(ExpectedOrder(-2f), sr.sortingOrder, "enabled=true must re-run the one-shot sort.");
            Assert.IsFalse(sort.enabled, "…and then the static sprite stands down again.");
        }

        [UnityTest]
        public IEnumerator DynamicSprite_StaysEnabled_AndResortsEveryFrame()
        {
            var sort = Make(dynamic: true, y: 0f);
            var sr = _go.GetComponent<SpriteRenderer>();

            Assert.IsTrue(sort.enabled, "A mover must keep its per-frame dispatch.");

            _go.transform.position = new Vector3(0f, -2f, 0f);
            yield return null;
            Assert.AreEqual(ExpectedOrder(-2f), sr.sortingOrder, "A mover re-sorts in LateUpdate after moving.");

            _go.transform.position = new Vector3(0f, 3f, 0f);
            yield return null;
            Assert.AreEqual(ExpectedOrder(3f), sr.sortingOrder, "…every frame, not once.");
        }

        [UnityTest]
        public IEnumerator Dynamic_SetTrueAtRuntime_ReArmsTheDispatch()
        {
            var sort = Make(dynamic: false, y: 1f);
            var sr = _go.GetComponent<SpriteRenderer>();
            Assert.IsFalse(sort.enabled, "Precondition: static sprite parked itself.");

            sort.Dynamic = true;
            Assert.IsTrue(sort.enabled, "Setting Dynamic=true must re-enable the component.");

            _go.transform.position = new Vector3(0f, -4f, 0f);
            yield return null;
            Assert.AreEqual(ExpectedOrder(-4f), sr.sortingOrder, "…and per-frame sorting must actually resume.");
        }

        [UnityTest]
        public IEnumerator Dynamic_SetFalseAtRuntime_ParksAtTheCurrentSpot()
        {
            var sort = Make(dynamic: true, y: 0f);
            var sr = _go.GetComponent<SpriteRenderer>();

            _go.transform.position = new Vector3(0f, 2f, 0f);
            sort.Dynamic = false;

            Assert.AreEqual(ExpectedOrder(2f), sr.sortingOrder,
                "Turning Dynamic off must sort once at the resting spot before standing down.");
            Assert.IsFalse(sort.enabled, "…and then stop the dispatch.");

            _go.transform.position = new Vector3(0f, -5f, 0f);
            yield return null;
            Assert.AreEqual(ExpectedOrder(2f), sr.sortingOrder, "Once parked, moves no longer re-sort.");
        }
    }
}
