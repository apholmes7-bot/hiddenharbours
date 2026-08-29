using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The sorting-order ceiling an additive light is clamped to</b> (ADR 0016) — the pure half.
    ///
    /// <para><b>What went wrong.</b> <c>Renderer.sortingOrder</c> is typed <c>int</c> on the property
    /// and stored in a <b>signed 16-bit</b> field. <see cref="SceneLight"/> asked for <b>32770</b> and
    /// the renderer kept <b>−32766</b> — silently, with no warning — putting every additive light in
    /// the game beneath the sea rather than above the day/night overlay.</para>
    ///
    /// <para><b>The other half of this fixture is in PLAY mode, and has to be.</b> The bug lives in the
    /// round trip through Unity's own field, so the assertion that actually catches it reads the order
    /// back off a real quad's renderer — and the quad is minted in <c>Awake</c>, which edit mode does
    /// not run. See <c>SceneLightSortingPlayTests</c>. What is left here is what can be proved without
    /// a frame: the ceiling is the field's, and going over it clamps rather than wraps.</para>
    /// </summary>
    public class SceneLightSortingTests
    {
        // DayNightController.cs:214 — the overlay a light must sort above to add brightness at all.
        const int DayNightOverlayOrder = 32760;

        [Test]
        public void TheCeilingIsWhatTheFieldCanHold()
        {
            Assert.AreEqual(short.MaxValue, SceneLight.MaxSortingOrder,
                "the ceiling is not a taste — it is the largest value Renderer.sortingOrder keeps");
            Assert.Greater(SceneLight.MaxSortingOrder, DayNightOverlayOrder,
                "and there is room above the day/night overlay inside it, so the clamp costs the " +
                "light nothing it actually needed");
        }

        [Test]
        public void AnOverlargeOrderIsClampedRatherThanWrapped()
        {
            var go = new GameObject("clamp-test");
            try
            {
                var light = go.AddComponent<SceneLight>();
                var so = new UnityEditor.SerializedObject(light);
                so.FindProperty("_sortingOrder").intValue = 40000;   // past the ceiling, as the ADR was
                so.ApplyModifiedProperties();

                Assert.AreEqual(SceneLight.MaxSortingOrder, light.SafeSortingOrder,
                    "an order past the ceiling is pinned TO the ceiling. Wrapping would send it to " +
                    "−25536 — the bottom of the world — which is the opposite of what was asked for, " +
                    "and is the failure this whole pair of fixtures exists for.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheDefaultIsAlreadyInsideTheCeiling()
        {
            var go = new GameObject("default-test");
            try
            {
                var light = go.AddComponent<SceneLight>();
                Assert.AreEqual(SceneLight.MaxSortingOrder, light.SafeSortingOrder,
                    "a light nobody has tuned asks for the ceiling and gets it — the shipped default " +
                    "must not be a value that needs clamping to be correct");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
