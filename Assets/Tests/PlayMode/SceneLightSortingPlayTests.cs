using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>An additive light draws ABOVE the world, not underneath it</b> (ADR 0016).
    ///
    /// <para><b>The defect these pin, because it was silent from the day ADR 0016 shipped.</b>
    /// <c>Renderer.sortingOrder</c> is typed <c>int</c> on the property and stored in a <b>signed
    /// 16-bit</b> field. <see cref="SceneLight"/> asked for <b>32770</b>; the renderer kept
    /// <b>−32766</b>. No warning, no error, no exception — just a light sorting below the seabed.
    /// Measured in the running game on 2026-08-28, where the quad's own renderer reported
    /// <c>order = −32766</c> while every other diagnostic (night gate open, intensity full, material
    /// bound, quad enabled and in frustum) insisted the light was working perfectly.</para>
    ///
    /// <para><b>Why the assertion is on the RENDERER, and why this fixture is in PLAY mode.</b> The bug
    /// lives entirely in the round trip: the number asked for and the number Unity keeps are different.
    /// A test reading back the serialized wish would have passed throughout. So these build a real
    /// light, let it mint its real quad, and ask that quad's own renderer what order it ended up with —
    /// and the quad is minted in <c>Awake</c>, which EDIT mode never runs for a component added to a
    /// live GameObject. An EditMode version of this fixture fails on a null quad and proves nothing,
    /// which is exactly what the first draft of it did.</para>
    /// </summary>
    public class SceneLightSortingPlayTests
    {
        // DayNightController.cs:214 — the full-screen MULTIPLY overlay a light must beat to add any
        // brightness back at all. Pinned as a literal on purpose: it is the number this light's whole
        // reason for existing is measured against, so if it moves, one of the two files is wrong and
        // somebody should be made to look at both.
        const int DayNightOverlayOrder = 32760;

        GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            _host = null;
        }

        IEnumerator NewLight()
        {
            _host = new GameObject("sorting-test");
            _host.AddComponent<SceneLight>();
            yield return null;      // let Awake/OnEnable run and mint the quad
        }

        MeshRenderer Quad()
        {
            var quad = _host.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(quad, "the light mints its additive quad on wake");
            return quad;
        }

        [UnityTest]
        public IEnumerator TheQuadsOrderSurvivesTheRoundTripIntoUnitysField()
        {
            yield return NewLight();
            MeshRenderer quad = Quad();

            // THE ASSERTION THAT WOULD HAVE CAUGHT IT. A wrapped order comes back NEGATIVE.
            Assert.Greater(quad.sortingOrder, 0,
                "the light quad's order came back negative, which means the value asked for did not " +
                "fit in the signed 16-bit field Unity stores it in, and wrapped. A wrapped order does " +
                "not sort the light 'a bit lower' — it sorts it beneath the sea, the seabed and every " +
                "sprite in the world, and nothing anywhere reports it.");

            Assert.LessOrEqual(quad.sortingOrder, short.MaxValue,
                "and it can never exceed what the field holds");
        }

        [UnityTest]
        public IEnumerator TheQuadSortsAboveTheDayNightOverlay()
        {
            yield return NewLight();

            // The whole point of the primitive: the overlay MULTIPLIES the frame dark and the light
            // ADDS brightness back on top of it. Below the overlay, an additive light is darkened by
            // the very thing it exists to punch a hole in.
            Assert.Greater(Quad().sortingOrder, DayNightOverlayOrder,
                "an additive light drawn UNDER the day/night multiply overlay is darkened by it, " +
                "which is the one thing it must never be");
        }

        [UnityTest]
        public IEnumerator TheSortingGroupAgreesWithTheRenderer()
        {
            yield return NewLight();
            MeshRenderer quad = Quad();

            var group = quad.GetComponent<UnityEngine.Rendering.SortingGroup>();
            Assert.IsNotNull(group, "the quad sorts as 2D through a SortingGroup");
            Assert.AreEqual(quad.sortingOrder, group.sortingOrder,
                "the group and the renderer must name the same order — they are two halves of one " +
                "answer, and they were both written from the same unclamped value that wrapped");
        }
    }
}
