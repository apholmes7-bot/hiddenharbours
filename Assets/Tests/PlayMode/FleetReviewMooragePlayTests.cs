using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The review anchorage's hulls are drawn by the MESH path, live.</b>
    ///
    /// <para><b>Why a PlayMode test and not a screenshot.</b> A picture of a boat proves nothing about
    /// WHICH renderer drew her — the sprite fallback and the mesh path both produce a boat, in the
    /// right place, at the right heading. The whole reason
    /// <c>FleetReviewMoorage</c> places rather than draws is that an edit-time builder would serialise
    /// the fallback into the committed scene; the only way to know it did not is to wake a hull and ask
    /// what is actually on her.</para>
    ///
    /// <para>One hull per rig SHAPE, because that is the axis along which this can break: a generator
    /// build (zodiac), a one-hull-per-rig hull (the Mk2), and a registry hull whose art hangs off a
    /// per-hull object (the sport fisher).</para>
    /// </summary>
    public class FleetReviewMooragePlayTests
    {
        const string VisualsFolder = "Assets/_Project/Data/Boats/Visuals";

        static BoatVisualDef Load(string assetName)
        {
#if UNITY_EDITOR
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                $"{VisualsFolder}/{assetName}.asset");
            if (visual != null) return visual;
#endif
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed hull, not a mirror.");
            return null;
        }

        /// <summary>
        /// ⚠️ <b>INACTIVE FIRST, AND THAT ORDER IS LOAD-BEARING.</b> <c>MooredBoat.OnEnable</c> calls
        /// <c>Present()</c>, so a component added to an ALREADY-ACTIVE object presents itself before
        /// anyone has told it what to draw — it warns, returns, and never tries again. The region
        /// builder gets away with `new GameObject` + `AddComponent` + configure only because it runs in
        /// EDIT mode, where <c>OnEnable</c> does not fire on a plain MonoBehaviour; the serialised
        /// field is what reaches play mode. A test does not have that luxury.
        /// <see cref="ConfiguringAnAlreadyAwakeHull_DrawsNothing"/> pins the trap itself.
        /// </summary>
        static MooredBoat AnchorForReview(BoatVisualDef visual, float heading = 90f)
        {
            var go = new GameObject($"ReviewHull_{visual.Id}");
            go.SetActive(false);
            var moored = go.AddComponent<MooredBoat>();
            moored.ConfigureForReview(visual, heading);
            go.SetActive(true);
            return moored;
        }

        static IEnumerator Present(MooredBoat moored)
        {
            yield return null;   // let OnEnable's skin settle before anything is read off it
        }

        [UnityTest]
        public IEnumerator EveryRigShape_SkinsAsAMeshHullAtRuntime()
        {
            foreach (string assetName in new[]
                     {
                         "ZodiacHurricaneIso",          // a generator build
                         "SportSkiffMk2Iso",            // one hull per rig
                         "SportFisherSkybridgeIso",     // a registry hull, 27.4 m
                     })
            {
                BoatVisualDef visual = Load(assetName);
                MooredBoat moored = AnchorForReview(visual);
                yield return Present(moored);

                Assert.IsTrue(moored.IsReviewHull,
                    $"{assetName}: configured for review but does not report as a review hull.");
                Assert.IsTrue(moored.IsPresented,
                    $"{assetName}: never skinned. She would anchor as an empty patch of water — see " +
                    "the warning MooredBoat logs, which names whether the art or the mesh is missing.");

                var facet = moored.GetComponentInChildren<IsoFacetHullRenderer>();
                Assert.IsNotNull(facet,
                    $"{assetName}: SHE IS NOT A MESH. She skinned, so there is a boat on the water and " +
                    "a screenshot would look right — but the hull was drawn by the SPRITE FALLBACK, " +
                    "which is exactly the failure FleetReviewMoorage places-rather-than-draws to " +
                    "avoid. Check that the visual's HullMesh is committed and usable.");

                Object.DestroyImmediate(moored.gameObject);
            }
        }

        /// <summary>
        /// <b>Nobody stands on a review hull.</b> She has no owner, so there is no skipper to place —
        /// and the guard for that is inside <c>StandTheSkipper</c>, where a null owner would otherwise
        /// have thrown halfway through presenting her.
        /// </summary>
        [UnityTest]
        public IEnumerator AReviewHull_CarriesNobody()
        {
            BoatVisualDef visual = Load("SportFisherConvertibleIso");
            MooredBoat moored = AnchorForReview(visual);
            yield return Present(moored);

            Assert.IsNull(moored.Owner, "a review hull has no owner by construction");
            Assert.IsTrue(moored.IsPresented, "she must still be drawn");

            var skipper = moored.GetComponentInChildren<IsoCharacterSprite>();
            Assert.IsNull(skipper,
                "A figure is standing on a review hull. The anchorage exists to look at HULLS, and a " +
                "skipper with no owner behind him is a figure nobody authored.");

            Object.DestroyImmediate(moored.gameObject);
        }

        /// <summary>
        /// ⚠️ <b>The trap itself, pinned.</b> Configuring a hull that is ALREADY awake draws nothing,
        /// because <c>OnEnable</c> has already run and presented an empty component. This is not a
        /// defect to fix — it is the ordering the builder and
        /// <see cref="AnchorForReview"/> both respect, and a test that did it the other way round would
        /// report a fleet that never appeared.
        /// </summary>
        [UnityTest]
        public IEnumerator ConfiguringAnAlreadyAwakeHull_DrawsNothing()
        {
            BoatVisualDef visual = Load("ZodiacFrcIso");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "has neither an owner Def nor a review visual"));

            var go = new GameObject("AwakeThenConfigured");   // ACTIVE — the wrong way round
            var moored = go.AddComponent<MooredBoat>();       // OnEnable fires here, with nothing set
            moored.ConfigureForReview(visual, 90f);
            yield return null;

            Assert.IsFalse(moored.IsPresented,
                "She presented despite being configured after she woke. If MooredBoat has gained a " +
                "re-present, this test should become the opposite assertion — but the region builder's " +
                "ordering was written on the assumption it has not.");

            Object.DestroyImmediate(go);
        }
    }
}
