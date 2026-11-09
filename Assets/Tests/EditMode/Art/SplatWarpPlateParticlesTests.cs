using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE SPLAT WARP PLATES' AMBIENT PARTICLES, HELD OFF</b> (terrain-fidelity 3b, seat ruling t19). With shader
    /// time pinned, two launches of the same state still differed by about a million pixels (t18): the sea mist and
    /// the dust motes place and age themselves from <c>Time.deltaTime</c>, <c>Time.time</c> and a spawn counter, and
    /// <c>Time.timeScale</c> 0 froze them at whatever age each launch left them. The plate case now holds the hosts of
    /// those emitters off from before its first pose, the teardown turns them back on, and the capture and the cost
    /// case do not touch them (the cost prices the frame as it ships).
    ///
    /// <para>Read from the sources: CI has no GPU, and whether two launches now agree is the second re-plate's first
    /// figure. <b>On the base</b> (3848e484) the harness holds nothing off, and this test fails at its first assert.</para>
    /// </summary>
    public sealed class SplatWarpPlateParticlesTests
    {
        private const string PlatePath = "Assets/Tests/PlayMode/StPetersSplatWarpPlatePlayTests.cs";
        private const string PlateCase = "public IEnumerator TheSplatEdges_PlatedSettledAtNoon()";
        private const string Hold = "void HoldTheAmbientParticlesOff()";
        private const string Emitters = "static IEnumerable<MonoBehaviour> AmbientEmitters()";
        private const string Capture = "Color32[] ReadMedian()";
        private const string CostCase = "public IEnumerator TheGroundAndTheSea_CostAtThePlatePose()";
        private const string TearDown = "public IEnumerator TearDownRegion()";

        /// <summary>The emitters of <c>AmbientParticleConfig</c> that place or age themselves from Time.deltaTime,
        /// Time.time or a spawn counter (the t19 typed read). The gulls run on the unscaled clock and are not here.</summary>
        private static readonly string[] Held =
            { "SeaMistEmitter", "DustMotes", "ChimneySmoke", "RainEmitter", "SurfSprayEmitter", "SprayEmitter" };

        [Test]
        public void ThePlateCase_HoldsTheAmbientParticlesOff_TheCaptureAndTheCostCaseDoNot()
        {
            string code = Code(PlatePath);

            // the emitters: the six the typed read found, from every loaded object (the hosts are HideAndDontSave)
            string emitters = Squash(Body(code, Emitters));
            foreach (string type in Held)
                StringAssert.Contains("Resources.FindObjectsOfTypeAll<" + type + ">()", emitters, type + " is held off");
            Assert.AreEqual(Held.Length, Count(emitters, "FindObjectsOfTypeAll<"), "the six and nothing else");
            StringAssert.Contains(".gameObject.scene.IsValid()", emitters, "a loaded prefab asset is not a host");

            // the hold: each host off and recorded; a host that carries more than its emitter is left on; no re-seeding
            string hold = Squash(Body(code, Hold));
            StringAssert.Contains("host.SetActive(false);_particlesOff.Add(host);", hold);
            StringAssert.Contains("host.GetComponents<Component>().Length!=2", hold, "only a host that is its emitter alone");
            foreach (string word in new[] { "SetActive(true)", "_spawnCounter", "Random", "Hash01", "enabled=" })
                Assert.IsFalse(hold.Contains(word), "held off, not re-seeded or re-aged: " + word);

            // the plate case holds them off once, after shader time is pinned, before the first pose, at its top level
            string plate = Squash(Body(code, PlateCase));
            const string call = "HoldTheAmbientParticlesOff();";
            Assert.AreEqual(1, Count(plate, Regex.Escape(call)), "the plate case holds them off once");
            int at = plate.IndexOf(call, StringComparison.Ordinal);
            Assert.Greater(at, plate.IndexOf("newShaderTimePin(", StringComparison.Ordinal), "after shader time is pinned");
            Assert.Less(at, plate.IndexOf("foreach(varfinPoses)", StringComparison.Ordinal), "before the first pose");
            string before = plate.Substring(0, at);
            Assert.AreEqual(Count(before, @"\{"), Count(before, @"\}"), "at the case's top level");

            // the teardown turns them back on, ahead of its early return for a case that loaded nothing
            string tear = Squash(Body(code, TearDown));
            int on = tear.IndexOf("foreach(GameObjectgoin_particlesOff)if(go!=null)go.SetActive(true);", StringComparison.Ordinal);
            Assert.Greater(on, -1, "the teardown puts the hosts back on");
            Assert.Less(on, tear.IndexOf("if(!_loadedAny)yieldbreak;", StringComparison.Ordinal), "whatever the case loaded");

            // neither the capture nor the cost case touches them, and the plate case is the only caller
            foreach (string body in new[] { Body(code, Capture), Body(code, CostCase) })
                foreach (string word in new[] { "HoldTheAmbientParticlesOff", "AmbientEmitters", "_particlesOff", "SetActive" })
                    Assert.IsFalse(Squash(body).Contains(word), "the capture and the cost case keep the particles: " + word);
            Assert.AreEqual(1, Count(Squash(code), Regex.Escape(call)), "the plate case is the only caller");
        }
    }
}
