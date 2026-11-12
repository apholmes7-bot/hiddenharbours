using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WakeCentrePhotographPlayTests
    {
        const string JetskiHullPath = "Assets/_Project/Data/Boats/JetSki.asset";
        const string JetskiVisualPath = "Assets/_Project/Data/Boats/Visuals/JetSkiIso.asset";
        const string JetskiPaintFolder = "Assets/_Project/Data/Boats/PaintSchemes/";

        /// <summary>Half the height, in metres, of the close plate she is shot at rest in.</summary>
        const float JetskiRestHalfHeightMeters = 4f;

        static readonly string[] JetskiPaints =
        {
            "JetSkiIso_SignalYellow", "JetSkiIso_Cranberry", "JetSkiIso_Seafoam", "JetSkiIso_TarBlack",
        };

        /// <summary>
        /// <b>The jetski through the same three headings as the Cape.</b> She is the one hull whose
        /// <c>BoatHullDef.WakeStyle</c> is Jet, so her plates beside the Cape's are the picture of the
        /// difference: a narrow stream thrown astern where the Cape lays a wide churn strip. The fixture
        /// drives every subject at its own fixed speed, a little over her 5.5 m/s.
        /// </summary>
        [UnityTest]
        public IEnumerator TheJetski_UnderWay_PhotographsWhereEachFoamFamilyIsDrawn()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            yield return Photograph("jetski", JetskiHullPath, JetskiVisualPath, forceSprite: false);
        }

        /// <summary>
        /// <b>And at rest, close, once in each of her paints.</b> A plate per scheme through the game's own
        /// camera: the pictures somebody looks at to say she reads as a jetski and that each paint is its
        /// own colour. Nothing is measured beyond "the skinner took her".
        /// </summary>
        [UnityTest]
        public IEnumerator TheJetski_AtRest_IsPhotographedInEachOfHerPaints()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            BoatHullDef hull = LoadAsset<BoatHullDef>(JetskiHullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(JetskiVisualPath);
            if (hull == null || visual == null) yield break;

            yield return LoadRegion();
            yield return SetHour(ShotHour);
            if (!TryLegAnchors(1, out List<Vector2> anchors, out float _))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED: no open water inside " + SceneName + "'s camera bounds.");
                yield break;
            }
            Vector2 at = anchors[0];

            foreach (string paint in JetskiPaints)
            {
                HullPaintSchemeDef scheme = LoadAsset<HullPaintSchemeDef>(JetskiPaintFolder + paint + ".asset");
                if (scheme == null) yield break;

                var go = new GameObject($"Photo[jetski-rest-{paint}]");
                _spawned.Add(go);
                go.transform.position = new Vector3(at.x, at.y, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, -45f);   // bow to the north-east: a 3/4 view

                var boat = go.AddComponent<BoatController>();
                boat.SetHull(hull);
                boat.SetLocalSeabedDepth(40f);
                var rb = go.GetComponent<Rigidbody2D>();
                rb.gravityScale = 0f;

                var options = default(BoatHullSkinner.Options);
                options.PaintScheme = scheme;
                BoatHullSkinner.Rig rig = BoatHullSkinner.Apply(go, visual, boat, options);
                Assert.IsTrue(rig.Skinned, $"{paint}: the skinner refused '{visual.Id}'.");

                float side = 2f * JetskiRestHalfHeightMeters;
                yield return FrameOn(go.transform, new Bounds(new Vector3(at.x, at.y, 0f), new Vector3(side, side, 0f)));
                for (int i = 0; i < 30; i++) yield return null;   // let her settle on the water

                Time.timeScale = 0f;
                for (int i = 0; i < 2; i++) yield return null;
                SavePlate($"jetski-rest-{paint}.png", Capture());
                Time.timeScale = 1f;

                _spawned.Remove(go);
                Object.Destroy(go);
                yield return null;
            }
        }
    }
}
