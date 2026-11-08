using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE SPLAT WARP PLATES' SHADER TIME, PINNED</b> (terrain-fidelity 3b, seat ruling t17). The settled plates
    /// froze the game clock and <c>Time.timeScale</c>, but URP feeds the shaders <c>_Time</c> from Unity's own clock,
    /// which stood wherever loading left it (12.47 s in one launch, 55.64 s in the next), so the water, the
    /// shallows' patterns and the swaying plants differed between the before launch and the after launch. The plate
    /// case now pins the six time globals by the shore capture's law, from before its first pose to its end.
    ///
    /// <para>Read from the sources: CI has no GPU, and whether two launches now agree is the next lease's first
    /// plate. The pin's set and restore run for real in
    /// <c>StPetersSplatWarpPlatePlayTests.TheShaderTimePin_SetsTheLawAndRestoresWhatItFound</c>, which renders
    /// nothing and needs no device.</para>
    ///
    /// <para><b>On the base</b> (ce239f36) the harness has no pin: every case here fails at its first assert except
    /// <see cref="TheSettledCapture_AndTheCostCase_CarryNoPin"/>, a guard that holds on any base.</para>
    /// </summary>
    public sealed class SplatWarpPlateTimePinTests
    {
        private const string PlatePath = "Assets/Tests/PlayMode/StPetersSplatWarpPlatePlayTests.cs";
        private const string ShorePath = "Assets/Tests/EditMode/Art/ShoreLineCaptureHarness.cs";
        private const string PinClass = "internal sealed class ShaderTimePin";
        private const string Law = "internal static void Law(float t, Action<string, Vector4> set)";
        private const string PlateCase = "public IEnumerator TheSplatEdges_PlatedSettledAtNoon()";
        private const string RuntimeCase = "public void TheShaderTimePin_SetsTheLawAndRestoresWhatItFound()";
        private const string Capture = "Color32[] ReadMedian()";
        private const string CostCase = "public IEnumerator TheGroundAndTheSea_CostAtThePlatePose()";
        private const string TearDown = "public IEnumerator TearDownRegion()";

        /// <summary>The six globals URP writes from Unity's clock at each camera's setup.</summary>
        private static readonly string[] Names =
            { "_Time", "_SinTime", "_CosTime", "unity_DeltaTime", "_TimeParameters", "_LastTimeParameters" };

        [Test]
        public void TheLaw_IsTheShoreCapturesLaw_StatementForStatement()
        {
            string law = Squash(Body(Code(PlatePath), Law));
            string shore = Squash(Code(ShorePath));
            const string start = "floatt=d.Seconds;", end = "Applied++;";
            int from = shore.IndexOf(start, StringComparison.Ordinal);
            Assert.Greater(from, -1, "the shore capture's frozen clock is gone");
            int to = shore.IndexOf(end, from, StringComparison.Ordinal);
            Assert.Greater(to, from);
            string shoreLaw = shore.Substring(from + start.Length, to - from - start.Length)
                                   .Replace("context.cmd.SetGlobalVector(", "set(");
            Assert.AreEqual(shoreLaw, law, "the plates pin shader time by the shore capture's law, statement for statement");
            foreach (string n in Names)
                StringAssert.Contains("set(\"" + n + "\",", law, n + " is one of the six the law sets");
            Assert.AreEqual(Names.Length, Count(law, @"set\("), "the law sets the six and nothing else");
        }

        [Test]
        public void ThePin_RunsWhereTheShoreCapturesDoes_OnThePlateCameraOnly()
        {
            string pin = Squash(Body(Code(PlatePath), PinClass));
            foreach (string statement in new[]
            {
                "renderPassEvent2D=RenderPassEvent2D.BeforeRenderingSprites;",
                "renderPassSortingLayerID=SortingLayer.layers.OrderBy(l=>l.value).First().id;",
                "builder.AllowPassCulling(false);builder.AllowGlobalStateModification(true);",
                "Law(d.Seconds,context.cmd.SetGlobalVector);Applied++;",
                "RenderPipelineManager.beginCameraRendering+=Begin;",
                "if(camera!=null&&_subject(camera))camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);",
            })
                StringAssert.Contains(statement, pin, "the pin is the shore capture's pass, at the shore capture's point");
            // Queued at camera start with the classic event left at its default, the stable sort keeps the pin ahead
            // of the water's pass at the same 2D point (that pass sets BeforeRenderingTransparents).
            Assert.AreEqual(0, Count(pin, @"(?<![A-Za-z0-9_])renderPassEvent="), "the pin leaves the classic event at its default");
        }

        [Test]
        public void ThePin_PutsBackWhatItFound_AndTheTeardownDisposesIt()
        {
            string code = Code(PlatePath);
            string pin = Body(code, PinClass);
            string names = Squash(Regex.Match(pin, @"Names\s*=\s*\{([^}]*)\}").Groups[1].Value);
            Assert.AreEqual(Squash("\"" + string.Join("\",\"", Names) + "\""), names, "the pin keeps the six globals URP writes");

            string ctor = Squash(Body(pin, "public ShaderTimePin(Func<Camera, bool> subject, float seconds)"));
            int found = ctor.IndexOf("_found=Names.Select(Shader.GetGlobalVector).ToArray();", StringComparison.Ordinal);
            int set = ctor.IndexOf("Law(seconds,Shader.SetGlobalVector);", StringComparison.Ordinal);
            Assert.Greater(found, -1, "the pin reads the six values it found");
            Assert.Greater(set, found, "it reads them before it sets the law");

            string dispose = Squash(Body(pin, "public void Dispose()"));
            StringAssert.StartsWith("if(!Pinning)return;Pinning=false;", dispose, "a second dispose does nothing");
            StringAssert.Contains("RenderPipelineManager.beginCameraRendering-=Begin;", dispose);
            StringAssert.Contains("for(inti=0;i<Names.Length;i++)Shader.SetGlobalVector(Names[i],_found[i]);", dispose,
                "dispose puts back all six as found");

            StringAssert.Contains("_shaderTime?.Dispose();_shaderTime=null;", Squash(Body(code, TearDown)),
                "a case stopped mid-pose still puts shader time back");
        }

        [Test]
        public void ThePlateCase_PinsTheNoonHour_FromBeforeItsFirstPose_ToItsEnd()
        {
            string code = Code(PlatePath);
            string plate = Squash(Body(code, PlateCase));
            const string pinAt =
                "usingvarshaderTime=_shaderTime=newShaderTimePin(camera=>camera==_cam,NoonHours*SecondsPerHour);";
            int pin = plate.IndexOf(pinAt, StringComparison.Ordinal);
            Assert.Greater(pin, -1, "the plate case pins shader time to the noon hour, on the plate camera");
            Assert.AreEqual(1, Count(plate, Regex.Escape(pinAt)));
            string before = plate.Substring(0, pin);
            Assert.AreEqual(Count(before, @"\{"), Count(before, @"\}"),
                "a using declaration at the case's top level holds to the end of the case");
            Assert.Greater(pin, plate.IndexOf("yieldreturnPinTheHour(NoonHours);", StringComparison.Ordinal),
                "after the game clock is pinned");
            int poses = plate.IndexOf("foreach(varfinPoses)", StringComparison.Ordinal);
            Assert.Greater(poses, pin, "before the first pose");

            int steps = plate.IndexOf("Color32[]plate=ReadMedian();for(inti=0;i<ControlGapFrames;i++)yieldreturnnull;",
                StringComparison.Ordinal);
            Assert.Greater(steps, poses, "the settle and median, then the control gap, as before, under the pin");
            Assert.Greater(plate.IndexOf("Color32[]control=ReadMedian();", steps, StringComparison.Ordinal), steps);
            Assert.AreEqual(2, Count(plate, @"ReadMedian\(\)"), "the plate and its control, and no third capture");
            Assert.AreEqual(0, Count(plate, @"\.Dispose\(\)"), "nothing lifts the pin before the case ends");

            string squashed = Squash(code);
            StringAssert.Contains("constfloatNoonHours=12f;", squashed);
            StringAssert.Contains("constfloatSecondsPerHour=3600f;", squashed);
            StringAssert.Contains("_pass=newClockPass(hour*3600);", Squash(Code(ShorePath)),
                "the shore capture's shader time is its hour in seconds too");
        }

        [Test]
        public void TheSettledCapture_AndTheCostCase_CarryNoPin()
        {
            string code = Code(PlatePath);
            string capture = Squash(Body(code, Capture));
            foreach (string word in new[] { "ShaderTimePin", "_shaderTime", "SetGlobalVector", "Law(", "_Time" })
                Assert.IsFalse(capture.Contains(word), "the settle and the median of three are the same calls as before: " + word);
            string cost = Squash(Body(code, CostCase));
            Assert.IsFalse(cost.Contains("ShaderTimePin") || cost.Contains("_shaderTime"),
                "the cost case prices the draw as it ships, unpinned");
        }

        [Test]
        public void TheRuntimeProof_RendersNothing_AndNeedsNoDevice()
        {
            string code = Code(PlatePath);
            int at = code.IndexOf(RuntimeCase, StringComparison.Ordinal);
            Assert.Greater(at, -1, "the pin's set and restore are run, not only read");
            StringAssert.EndsWith("[Test]", Squash(code.Substring(Math.Max(0, at - 80), 80)),
                "a plain test: it reads globals and renders nothing");
            string body = Squash(Body(code, RuntimeCase));
            foreach (string word in new[] { "RequireAGraphicsDevice", "Render(", "LoadStPeters" })
                Assert.IsFalse(body.Contains(word), "it runs on CI with no device: " + word);
            StringAssert.Contains("pin.Dispose();", body);
            StringAssert.Contains("ShaderTimePin.Law(seconds,", body);
        }
    }
}
