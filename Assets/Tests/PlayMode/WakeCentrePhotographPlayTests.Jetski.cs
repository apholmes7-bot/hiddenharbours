using System.Collections;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WakeCentrePhotographPlayTests
    {
        const string JetskiHullPath = "Assets/_Project/Data/Boats/JetSki.asset";
        const string JetskiVisualPath = "Assets/_Project/Data/Boats/Visuals/JetSkiIso.asset";

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
    }
}
